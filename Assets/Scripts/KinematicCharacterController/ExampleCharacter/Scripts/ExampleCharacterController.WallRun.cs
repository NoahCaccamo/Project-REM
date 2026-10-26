using System.Collections.Generic;
using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Wall Running")]
        public bool EnableWallRunning = true;
        public float WallRunSpeed = 16.5f;
        public float WallRunMaxDuration = 2f;
        public float WallRunGravityMultiplier = 0.2f;
        public float WallRunJumpHeight = 12f;
        public float WallRunJumpAwayForce = 8f;
        public float WallRunJumpMomentumBoost = 1.15f;
        public float WallRunDetectionDistance = 0.6f;
        public float MinSpeedForWallRun = 5f;
        public float WallRunCameraTilt = 15f;
        public float CameraTiltSpeed = 8f;
        public float WallRunSpeedDecay = 0.95f;
        private bool _isWallRunning = false;
        private bool _isWallRunningLeft = false;
        private Vector3 _wallRunNormal;
        private Vector3 _wallRunDirection;
        private float _wallRunTimer = 0f;
        private bool _canWallRun = true;
        private float _wallRunCooldown = 0f;
        private const float WALL_RUN_COOLDOWN_TIME = 0.3f;
        private float _currentCameraTilt = 0f;
        private float _wallRunEntrySpeed = 0f;

        [Header("Wall Run Entry")]
        [Tooltip("How far forward of straight-sideways (degrees) wall rays fan out. 0 = old behaviour (pure side rays). Higher = can catch walls from shallower approach.")]
        [Range(0f, 80f)] public float WallRunDetectionFanAngle = 55f;
        [Tooltip("Number of rays per side in the fan.")]
        [Range(1, 12)] public int WallRunDetectionRays = 5;
        [Tooltip("Max angle (degrees) between velocity and the wall surface for a wall run to start. Steeper (head-on) hits are rejected.")]
        [Range(5f, 90f)] public float WallRunMaxEntryAngle = 65f;
        [Tooltip("Cap on how far forward angled rays can reach (meters).")]
        public float WallRunMaxDetectionReach = 2.5f;

        [Header("Wall Run Camera Alignment")]
        public bool WallRunCameraAlign = true;
        [Tooltip("Pull speed (deg/s) when looking straight into the wall (90 degrees off parallel).")]
        public float WallRunCameraAlignMaxSpeed = 90f;
        [Tooltip("Pull speed (deg/s) as the view nears parallel. Keeps the final approach gentle but never stalls.")]
        public float WallRunCameraAlignMinSpeed = 8f;
        [Tooltip("Shapes the ramp. 1 = linear in angle, higher = stays gentle longer and ramps up late.")]
        [Range(0.5f, 4f)] public float WallRunCameraAlignCurve = 1.5f;
        [Tooltip("Stops aligning once the view is within this many degrees of parallel.")]
        public float WallRunCameraAlignDeadzone = 3f;

        /// <summary>True when the camera should be lowered (crouched/crouching and on the ground).</summary>
        public bool WantsLowCamera => (_isCrouching || _shouldBeCrouching) && Motor.GroundingStatus.IsStableOnGround;


        [Tooltip("Fan the detection rays out from the camera's horizontal facing instead of the velocity direction.")]
        public bool WallRunRaysUseCamera = true;

        [Header("Wall Run Input Rules")]
        [Tooltip("Forward input must exceed this to enter / stay in a wall run.")]
        public float WallRunForwardInputThreshold = 0.1f;
        [Tooltip("Sideways input (away from the wall) above this ends the wall run.")]
        public float WallRunAwayInputThreshold = 0.5f;
        [Tooltip("After leaving a wall run with crouch/slide, wall running is blocked for this long (s). It is always blocked while crouch is held.")]
        public float WallRunCrouchBlockTime = 0.25f;
        [Tooltip("Looking more than this (dot product with the wall axis) toward the opposite direction reverses the wall run.")]
        [Range(0.05f, 0.9f)] public float WallRunReverseLookThreshold = 0.3f;
        [Tooltip("Speed change (m/s^2) while reversing. Decelerates through zero and accelerates to WallRunSpeed the other way.")]
        public float WallRunReverseAcceleration = 90f;
        [Tooltip("Window (s) after leaving a wall run without jumping in which a mid-air jump is allowed.")]
        public float WallRunCoyoteTime = 0.15f;

        private Vector3 _rawMoveInput;          // x = strafe, z = forward (NOT camera-relative)
        private bool _crouchButtonHeld;
        private float _wallRunBlockTimer;
        private float _wallRunCoyoteTimer;
        private bool _wallRunReversing;

        /// <summary>Input-based requirements for starting a wall run.</summary>
        private bool CanInitiateWallRun()
        {
            return _rawMoveInput.z > WallRunForwardInputThreshold
                && !_crouchButtonHeld
                && _wallRunBlockTimer <= 0f;
        }

        /// <summary>
        /// Horizontal reference direction the detection fan is built around.
        /// Not wall running: camera facing (unaffected by capsule rotation).
        /// Wall running: velocity direction.
        /// Falls back to velocity / camera / capsule forward if the preferred source is unavailable.
        /// </summary>
        private Vector3 GetWallRunReferenceDirection(Vector3 up)
        {
            Vector3 vel = Vector3.ProjectOnPlane(Motor.BaseVelocity, up);
            Vector3 cam = Vector3.ProjectOnPlane(_lookInputVector, up);

            bool preferCamera = WallRunRaysUseCamera && CurrentCharacterState != CharacterState.WallRunning;

            if (preferCamera)
            {
                if (cam.sqrMagnitude > 0.0001f) return cam.normalized;
                if (vel.sqrMagnitude > 0.01f) return vel.normalized;
            }
            else
            {
                if (vel.sqrMagnitude > 0.01f) return vel.normalized;
                if (cam.sqrMagnitude > 0.0001f) return cam.normalized;
            }

            return Vector3.ProjectOnPlane(Motor.CharacterForward, up).normalized;
        }

        // Separate into own class later on when tilting for generic movement
        // Get movement tilt, update camera tilt, etc (functions
        private void UpdateCameraTilt()
        {
            if (playerCamera == null) return;

            float targetTilt = 0f;

            if (CurrentCharacterState == CharacterState.WallRunning)
            {
                targetTilt = _isWallRunningLeft ? -WallRunCameraTilt : WallRunCameraTilt;
            }

            _currentCameraTilt = Mathf.Lerp(_currentCameraTilt, targetTilt, Time.deltaTime * CameraTiltSpeed);

            Vector3 currentEuler = playerCamera.transform.eulerAngles;
            currentEuler.z = _currentCameraTilt;
            playerCamera.transform.rotation = Quaternion.Euler(currentEuler);
        }

        private void UpdateWallRunCooldown()
        {
            if (_wallRunCooldown > 0f)
            {
                _wallRunCooldown -= Time.deltaTime;
                if (_wallRunCooldown <= 0f)
                {
                    _canWallRun = true;
                }
            }
        }

        /// <summary>
        /// Detects walls suitable for wall running using a fan of horizontal rays on each side,
        /// relative to the travel direction so shallow approaches are caught before impact.
        /// </summary>
        private bool DetectWallForRunning(out Vector3 wallNormal, out bool isLeftWall)
        {
            wallNormal = Vector3.zero;
            isLeftWall = false;

            Vector3 up = Motor.CharacterUp;
            Vector3 origin = Motor.TransientPosition;

            // Rays are fanned around this direction (camera-based), NOT the capsule
            Vector3 refDir = GetWallRunReferenceDirection(up);

            // Actual travel direction is still used to judge whether entering this wall makes sense
            Vector3 travelDir = Vector3.ProjectOnPlane(Motor.BaseVelocity, up);
            if (travelDir.sqrMagnitude < 0.01f)
            {
                travelDir = refDir;
            }
            travelDir.Normalize();

            bool debug = DrawWallRunGizmos;
            if (debug)
            {
                _wallRayDebug.Clear();
                _wallRayDebugTime = Time.time;
            }

            float maxEntrySin = Mathf.Sin(WallRunMaxEntryAngle * Mathf.Deg2Rad);
            float bestScore = float.MaxValue;
            int bestDebugIndex = -1;
            bool found = false;

            int rays = Mathf.Max(1, WallRunDetectionRays);
            for (int side = -1; side <= 1; side += 2) // -1 = left, +1 = right
            {
                for (int i = 0; i < rays; i++)
                {
                    GetWallRunRay(side, i, rays, refDir, up, out Vector3 dir, out float distance);

                    WallRayDebug dbg = new WallRayDebug
                    {
                        Origin = origin,
                        End = origin + dir * distance,
                        Result = WallRayResult.Miss
                    };

                    if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, wallLayer, QueryTriggerInteraction.Ignore))
                    {
                        dbg.HitPoint = hit.point;
                        dbg.HitNormal = hit.normal;
                        dbg.End = hit.point;
                        dbg.Result = WallRayResult.Rejected;

                        // Wall must be mostly vertical
                        if (Vector3.Dot(hit.normal, up) < 0.1f)
                        {
                            Vector3 planarNormal = Vector3.ProjectOnPlane(hit.normal, up).normalized;
                            float into = Vector3.Dot(travelDir, planarNormal);

                            // Not moving away from the wall, and not hitting too head-on
                            if (!(into > 0.05f || Mathf.Abs(into) > maxEntrySin))
                            {
                                // Prefer the wall that is laterally closest
                                float lateral = Vector3.Dot(hit.point - origin, -planarNormal);
                                if (lateral < bestScore)
                                {
                                    bestScore = lateral;
                                    wallNormal = planarNormal;
                                    found = true;
                                    bestDebugIndex = debug ? _wallRayDebug.Count : -1;
                                }
                            }
                        }
                    }

                    if (debug)
                    {
                        _wallRayDebug.Add(dbg);
                    }
                }
            }

            if (debug && bestDebugIndex >= 0)
            {
                WallRayDebug chosen = _wallRayDebug[bestDebugIndex];
                chosen.Result = WallRayResult.Chosen;
                _wallRayDebug[bestDebugIndex] = chosen;
            }

            if (found)
            {
                // Wall is on the left if it lies on the negative side of the travel direction
                Vector3 right = Vector3.Cross(up, travelDir);
                isLeftWall = Vector3.Dot(-wallNormal, right) < 0f;
            }
            return found;
        }

        /// <summary>
        /// Initiates wall running state
        /// </summary>
        private void StartWallRun(Vector3 wallNormal, bool isLeftWall)
        {
            // Redirect horizontal velocity along the wall, keeping its speed (no momentum lost to the wall)
            Vector3 up = Motor.CharacterUp;
            Vector3 vel = Motor.BaseVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(vel, up);
            Vector3 vertical = vel - horizontal;
            float speed = horizontal.magnitude;
            Vector3 along = Vector3.ProjectOnPlane(horizontal, wallNormal);
            if (along.sqrMagnitude > 0.0001f)
            {
                Motor.BaseVelocity = along.normalized * speed + vertical;
            }

            TransitionToState(CharacterState.WallRunning);
            _wallRunNormal = wallNormal;
            _isWallRunning = true;
            _isWallRunningLeft = isLeftWall;
            _jumpConsumed = false;
            Motor.ForceUnground();
        }

        /// <summary>
        /// Yaw (degrees) to add to the camera this frame so the view turns parallel to the wall
        /// when the player is looking into it. Returns 0 otherwise.
        /// </summary>
        public float GetWallRunCameraYaw(Vector3 cameraPlanarDirection, float deltaTime)
        {
            if (!WallRunCameraAlign || CurrentCharacterState != CharacterState.WallRunning)
                return 0f;

            Vector3 up = Motor.CharacterUp;
            Vector3 look = Vector3.ProjectOnPlane(cameraPlanarDirection, up).normalized;
            Vector3 normal = Vector3.ProjectOnPlane(_wallRunNormal, up).normalized;
            if (look.sqrMagnitude < 0.001f || normal.sqrMagnitude < 0.001f)
                return 0f;

            // Only when looking into the wall
            if (Vector3.Dot(look, normal) >= 0f)
                return 0f;

            // Nearest wall-parallel direction
            Vector3 parallel = Vector3.Cross(normal, up).normalized;
            if (Vector3.Dot(parallel, look) < 0f)
                parallel = -parallel;

            float angle = Vector3.SignedAngle(look, parallel, up);
            float absAngle = Mathf.Abs(angle);
            if (absAngle <= WallRunCameraAlignDeadzone)
                return 0f;

            // 0 = parallel, 1 = looking straight into the wall.
            // Speed scales up with depth, then SmoothStep eases it off over the last stretch.
            float depth = Mathf.Clamp01(absAngle / 90f);
            float shaped = Mathf.SmoothStep(0f, 1f, Mathf.Pow(depth, WallRunCameraAlignCurve));
            float speed = Mathf.Lerp(WallRunCameraAlignMinSpeed, WallRunCameraAlignMaxSpeed, shaped);

            float step = speed * deltaTime;
            return Mathf.Sign(angle) * Mathf.Min(absAngle, step);
        }

        /// <summary>
        /// Handles wall running physics - PRESERVES MOMENTUM, direction can be reversed by looking the other way
        /// </summary>
        private void HandleWallRunning(ref Vector3 currentVelocity, float deltaTime)
        {
            _wallRunTimer += deltaTime;
            Vector3 up = Motor.CharacterUp;

            // Exit: crouch / slide pressed or held (also blocks re-entry briefly)
            if (_crouchButtonHeld)
            {
                _wallRunBlockTimer = WallRunCrouchBlockTime;
                TransitionToState(CharacterState.Default);
                return;
            }

            // Exit: forward input not held
            if (_rawMoveInput.z <= WallRunForwardInputThreshold)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Exit: wall lost (also covers running off the end of a wall)
            if (!DetectWallForRunning(out Vector3 currentWallNormal, out bool isLeftWall))
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Exit: duration limit
            if (_wallRunTimer >= WallRunMaxDuration)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Camera-relative frame (input is relative to the camera)
            Vector3 camDir = Vector3.ProjectOnPlane(playerCamera.transform.forward, up).normalized;
            if (camDir.sqrMagnitude < 0.001f)
            {
                camDir = Motor.CharacterForward;
            }
            Vector3 camRight = Vector3.Cross(up, camDir);

            // Update wall normal and side (wall may curve). Side is camera-relative so it matches the input.
            _wallRunNormal = currentWallNormal;
            _isWallRunningLeft = Vector3.Dot(-_wallRunNormal, camRight) < 0f;

            // Exit: pressing away from the wall (e.g. wall on the left, press right)
            float awayDot = Vector3.Dot(_wallRunNormal, camRight);
            if (Mathf.Abs(awayDot) > 0.3f && _rawMoveInput.x * Mathf.Sign(awayDot) > WallRunAwayInputThreshold)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Direction axis along the wall
            Vector3 wallAxis = Vector3.Cross(_wallRunNormal, up).normalized;
            Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, up);

            // Choose direction: look direction wins when clearly pointing along one way, otherwise keep velocity direction
            Vector3 wallForward = wallAxis;
            float lookAlong = Vector3.Dot(wallAxis, camDir);
            if (Mathf.Abs(lookAlong) > WallRunReverseLookThreshold)
            {
                wallForward = wallAxis * Mathf.Sign(lookAlong);
            }
            else if (currentHorizontalVelocity.magnitude > 0.1f)
            {
                if (Vector3.Dot(wallAxis, currentHorizontalVelocity) < 0f) wallForward = -wallAxis;
            }
            else if (Vector3.Dot(wallAxis, Motor.CharacterForward) < 0f)
            {
                wallForward = -wallAxis;
            }

            // Start reversing when moving against the chosen direction
            float signedSpeed = Vector3.Dot(currentHorizontalVelocity, wallForward);
            if (signedSpeed < 0f)
            {
                _wallRunReversing = true;
            }

            _wallRunDirection = wallForward;

            if (_wallRunReversing)
            {
                // Rapidly decelerate through zero and accelerate up to WallRunSpeed the other way
                float newSpeed = Mathf.MoveTowards(signedSpeed, WallRunSpeed, WallRunReverseAcceleration * deltaTime);
                if (newSpeed >= WallRunSpeed - 0.01f)
                {
                    _wallRunReversing = false;
                }

                Vector3 vertical = currentVelocity - currentHorizontalVelocity;
                currentVelocity = wallForward * newSpeed + vertical;
            }
            else
            {
                // PRESERVE MOMENTUM: use the larger of current speed or base wall run speed
                float targetSpeed = Mathf.Max(signedSpeed, WallRunSpeed);
                targetSpeed *= Mathf.Pow(WallRunSpeedDecay, deltaTime);

                Vector3 targetVelocity = wallForward * targetSpeed;

                // Smooth transition to target velocity (less aggressive to preserve momentum better)
                currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-5f * deltaTime));
            }

            // Reduced gravity while wall running
            currentVelocity += playerCharacter.CurrentStats.gravity * WallRunGravityMultiplier * deltaTime;

            // Apply slight pull toward wall to maintain contact
            currentVelocity += -_wallRunNormal * 2f * deltaTime;
        }

        [Header("Wall Run Debug")]
        [Tooltip("Draw the wall run detection rays as gizmos (Gizmos must be enabled in the Scene/Game view).")]
        public bool DrawWallRunGizmos = true;

        private enum WallRayResult { Miss, Rejected, Chosen }

        private struct WallRayDebug
        {
            public Vector3 Origin;
            public Vector3 End;
            public Vector3 HitPoint;
            public Vector3 HitNormal;
            public WallRayResult Result;
        }

        private readonly List<WallRayDebug> _wallRayDebug = new List<WallRayDebug>();
        private float _wallRayDebugTime = -1f;

        private void DrawGizmosForWallrun()
        {
            if (!DrawWallRunGizmos || Motor == null)
                return;

            // Show real detection results if they're fresh, otherwise a gray preview of the fan
            bool fresh = Application.isPlaying && (Time.time - _wallRayDebugTime) < 0.1f;
            if (fresh)
            {
                for (int i = 0; i < _wallRayDebug.Count; i++)
                {
                    WallRayDebug r = _wallRayDebug[i];
                    switch (r.Result)
                    {
                        case WallRayResult.Chosen: Gizmos.color = Color.green; break;
                        case WallRayResult.Rejected: Gizmos.color = Color.yellow; break;
                        default: Gizmos.color = Color.red; break;
                    }

                    Gizmos.DrawLine(r.Origin, r.End);
                    if (r.Result != WallRayResult.Miss)
                    {
                        Gizmos.DrawSphere(r.HitPoint, 0.06f);
                        Gizmos.DrawLine(r.HitPoint, r.HitPoint + r.HitNormal * 0.5f);
                    }
                }
                return;
            }

            Vector3 up = Motor.CharacterUp;
            Vector3 origin = Motor.TransientPosition;
            Vector3 refDir = GetWallRunReferenceDirection(up);

            Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
            int rays = Mathf.Max(1, WallRunDetectionRays);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < rays; i++)
                {
                    GetWallRunRay(side, i, rays, refDir, up, out Vector3 dir, out float distance);
                    Gizmos.DrawLine(origin, origin + dir * distance);
                }
            }
        }

        /// <summary>Builds the direction and length of one detection ray. Shared by detection and gizmos.</summary>
        private void GetWallRunRay(int side, int index, int rayCount, Vector3 travelDir, Vector3 up, out Vector3 dir, out float distance)
        {
            // t = 0 -> straight sideways, t = 1 -> fully forward-fanned
            float t = rayCount == 1 ? 0f : (float)index / (rayCount - 1);
            float angleFromTravel = 90f - t * WallRunDetectionFanAngle;
            dir = Quaternion.AngleAxis(side * angleFromTravel, up) * travelDir;

            // Angled rays need more length to reach the same lateral distance
            float sin = Mathf.Max(0.2f, Mathf.Sin(angleFromTravel * Mathf.Deg2Rad));
            distance = Mathf.Min(WallRunDetectionDistance / sin, WallRunMaxDetectionReach);
        }

    }
}
