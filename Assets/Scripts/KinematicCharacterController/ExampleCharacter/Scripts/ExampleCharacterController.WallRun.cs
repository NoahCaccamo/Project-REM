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

        [Header("Wall Run Detection Distances")]
        [Tooltip("Entering a wall run: max gap (m) between the capsule SURFACE and the wall. Keep it above the distance you cover in one physics step toward the wall (speed x 0.02 s), or you will hit the wall before entering.")]
        public float WallRunEntryDistance = 0.45f;
        [Tooltip("Staying in a wall run (and wall jumping from it): max gap (m) between the capsule surface and the wall. Smaller than the entry distance so you can't jump off a wall that is far away.")]
        public float WallRunStayDistance = 0.3f;
        [Tooltip("How fast (m/s^2) speed ramps up to WallRunSpeed when entering slower than it. Speed above WallRunSpeed is never reduced by this.")]
        public float WallRunEntryAcceleration = 40f;
        [Tooltip("A wall jump only fires if the jump was pressed within this many seconds (separate from the ground jump buffer, which is much longer).")]
        public float WallJumpBufferTime = 0.06f;

        [Header("Wall Run Entry")]
        [Tooltip("How far forward of straight-sideways (degrees) wall rays fan out. 0 = old behaviour (pure side rays). Higher = can catch walls from shallower approach.")]
        [Range(0f, 80f)] public float WallRunDetectionFanAngle = 55f;
        [Tooltip("Number of rays per side in the fan.")]
        [Range(1, 12)] public int WallRunDetectionRays = 5;
        [Tooltip("Max angle (degrees) between your LOOK direction and the wall surface for a wall run to start. 0 = looking along the wall, 90 = looking straight at it. Steeper than this is rejected.")]
        [Range(5f, 90f)] public float WallRunMaxLookAngle = 65f;
        [Tooltip("Look angles up to this (degrees) keep ALL horizontal speed on entry. Between this and the max look angle, speed is reduced. Set equal to the max look angle for no slowdown, or to 0 to slow every entry.")]
        [Range(0f, 90f)] public float WallRunFullSpeedLookAngle = 30f;
        [Tooltip("Fraction of horizontal speed kept when entering at exactly the max look angle (blends linearly from 1 at the full-speed angle).")]
        [Range(0.1f, 1f)] public float WallRunSlowedRetention = 0.6f;

        /// <summary>Angle (degrees, 0-90) between a horizontal look direction and the wall surface.</summary>
        private float GetLookAngleToWall(Vector3 look, Vector3 wallNormal, Vector3 up)
        {
            Vector3 planarNormal = Vector3.ProjectOnPlane(wallNormal, up).normalized;
            return Mathf.Asin(Mathf.Clamp01(Mathf.Abs(Vector3.Dot(look.normalized, planarNormal)))) * Mathf.Rad2Deg;
        }

        /// <summary>Fraction of horizontal speed kept when entering a wall run at the given look angle.</summary>
        private float GetWallRunEntrySpeedRetention(float lookAngle)
        {
            if (lookAngle <= WallRunFullSpeedLookAngle) return 1f;
            if (WallRunMaxLookAngle <= WallRunFullSpeedLookAngle) return WallRunSlowedRetention;

            float t = Mathf.Clamp01((lookAngle - WallRunFullSpeedLookAngle) / (WallRunMaxLookAngle - WallRunFullSpeedLookAngle));
            return Mathf.Lerp(1f, WallRunSlowedRetention, t);
        }
        [Tooltip("Cap on how far forward angled rays can reach (meters).")]
        public float WallRunMaxDetectionReach = 2.5f;

        [Header("Wall Run Sliding Down")]
        [Tooltip("Seconds into a single wall run before the player starts sliding down.")]
        public float WallRunSlideDelay = 1f;
        [Tooltip("Max downward speed (m/s) while wall running.")]
        public float WallRunMaxFallSpeed = 4f;
        [Tooltip("How quickly (m/s^2) the downward speed ramps up once sliding starts.")]
        public float WallRunFallAcceleration = 8f;
        [Tooltip("How quickly vertical velocity blends to its target (entry and slide).")]
        public float WallRunVerticalSharpness = 12f;

        private float _wallRunFallSpeed;

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

        [Tooltip("After releasing a grab (either hand), wall running cannot be initiated for this long (s). 0 = no timeout.")]
        public float WallRunGrabReleaseTimeout = 0.5f;

        private float _lastGrabReleaseTime = -999f;

        /// <summary>Input-based requirements for starting a wall run.</summary>
        private bool CanInitiateWallRun()
        {
            return _rawMoveInput.z > WallRunForwardInputThreshold
                && !_crouchButtonHeld
                && _wallRunBlockTimer <= 0f
                && Time.time - _lastGrabReleaseTime >= WallRunGrabReleaseTimeout;
        }

        /// <summary>
        /// Horizontal reference direction the detection fan is built around.
        /// Not wall running: ALWAYS the direction the camera is looking (horizontal), nothing else.
        /// Wall running: the velocity direction.
        /// </summary>
        private Vector3 GetWallRunReferenceDirection(Vector3 up)
        {
            Vector3 vel = Vector3.ProjectOnPlane(Motor.BaseVelocity, up);

            if (CurrentCharacterState == CharacterState.WallRunning)
            {
                if (vel.sqrMagnitude > 0.01f) return vel.normalized;
                return _wallRunDirection.sqrMagnitude > 0.0001f
                    ? _wallRunDirection.normalized
                    : Vector3.ProjectOnPlane(Motor.CharacterForward, up).normalized;
            }

            // Camera forward, read directly from the camera transform. Flattening only removes pitch; if the
            // camera is looking almost straight up or down, fall back to its up vector so there is always a heading.
            if (playerCamera != null)
            {
                Vector3 cam = Vector3.ProjectOnPlane(playerCamera.transform.forward, up);
                if (cam.sqrMagnitude < 0.0001f)
                {
                    cam = Vector3.ProjectOnPlane(playerCamera.transform.up, up);
                }
                if (cam.sqrMagnitude > 0.0001f)
                {
                    return cam.normalized;
                }
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
        /// Detects walls for wall running using a fan of horizontal rays from the capsule center.
        /// A hit only counts if the wall PLANE is within the gap (entry or stay distance) of the capsule surface,
        /// so the ray length no longer decides how far away a wall can be.
        /// </summary>
        private bool DetectWallForRunning(out Vector3 wallNormal, out bool isLeftWall, bool staying = false)
        {
            wallNormal = Vector3.zero;
            isLeftWall = false;

            Vector3 up = Motor.CharacterUp;
            Vector3 origin = GetWallRunOrigin();
            float radius = Motor.Capsule.radius;
            float gap = staying ? WallRunStayDistance : WallRunEntryDistance;

            Vector3 refDir = GetWallRunReferenceDirection(up);

            // Actual travel direction judges whether entering this wall makes sense
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

            float bestGap = float.MaxValue;
            int bestDebugIndex = -1;
            bool found = false;

            int rays = Mathf.Max(1, WallRunDetectionRays);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < rays; i++)
                {
                    GetWallRunRay(side, i, rays, refDir, up, gap, out Vector3 dir, out float distance);

                    WallRayDebug dbg = new WallRayDebug
                    {
                        Origin = origin,
                        RayEnd = origin + dir * distance,
                        Result = WallRayResult.Miss
                    };

                    if (Physics.Raycast(origin, dir, out RaycastHit hit, distance, wallLayer, QueryTriggerInteraction.Ignore))
                    {
                        dbg.HitPoint = hit.point;
                        dbg.HitNormal = hit.normal;
                        dbg.Result = WallRayResult.Rejected;

                        if (Vector3.Dot(hit.normal, up) < 0.1f)
                        {
                            Vector3 planarNormal = Vector3.ProjectOnPlane(hit.normal, up).normalized;
                            float into = Vector3.Dot(travelDir, planarNormal);

                            // Real distance from the capsule surface to the wall plane
                            float planeGap = Vector3.Dot(hit.point - origin, -planarNormal) - radius;

                            // Entry: judged by LOOK direction vs the wall (refDir is the camera heading when not wall running).
                            // Staying: only drop the run if the velocity is clearly moving away from the wall.
                            bool angleOk = staying
                                ? into <= 0.3f
                                : GetLookAngleToWall(refDir, planarNormal, up) <= WallRunMaxLookAngle;

                            if (angleOk && planeGap <= gap && planeGap < bestGap)
                            {
                                bestGap = planeGap;
                                wallNormal = planarNormal;
                                found = true;
                                bestDebugIndex = debug ? _wallRayDebug.Count : -1;
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
                Vector3 right = Vector3.Cross(up, staying ? travelDir : refDir);
                isLeftWall = Vector3.Dot(-wallNormal, right) < 0f;
            }
            return found;
        }

        private void StartWallRun(Vector3 wallNormal, bool isLeftWall)
        {
            Vector3 up = Motor.CharacterUp;
            Vector3 vel = Motor.BaseVelocity;
            Vector3 horizontal = Vector3.ProjectOnPlane(vel, up);
            Vector3 vertical = vel - horizontal;
            float speed = horizontal.magnitude;

            // Still in the Default state here, so this returns the camera's horizontal facing
            Vector3 look = GetWallRunReferenceDirection(up);

            // Run direction along the wall follows where the player is LOOKING.
            // Only if the look is almost perpendicular to the wall (ambiguous) do we fall back to velocity.
            Vector3 alongDir = Vector3.Cross(wallNormal, up).normalized;
            float lookAlong = Vector3.Dot(alongDir, look);
            if (Mathf.Abs(lookAlong) > 0.05f)
            {
                if (lookAlong < 0f) alongDir = -alongDir;
            }
            else if (Vector3.Dot(alongDir, horizontal) < 0f)
            {
                alongDir = -alongDir;
            }

            // Shallow-look entries keep all speed; steeper ones (up to the max look angle) lose some
            float retention = GetWallRunEntrySpeedRetention(GetLookAngleToWall(look, wallNormal, up));

            // Move ALL horizontal speed onto the wall axis (scaled by retention)
            Motor.BaseVelocity = alongDir * (speed * retention) + vertical;

            // Set wall data BEFORE entering the state so OnStateEnter sees the new wall
            _wallRunNormal = wallNormal;
            _wallRunDirection = alongDir;
            _isWallRunning = true;
            _isWallRunningLeft = isLeftWall;
            _wallRunFallSpeed = 0f;
            _jumpConsumed = false;

            TransitionToState(CharacterState.WallRunning);
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
            if (!DetectWallForRunning(out Vector3 currentWallNormal, out bool isLeftWall, true))
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

            _wallRunDirection = wallForward;

            // Start reversing when moving against the chosen direction
            float signedSpeed = Vector3.Dot(currentHorizontalVelocity, wallForward);
            if (signedSpeed < 0f)
            {
                _wallRunReversing = true;
            }

            // ---- Horizontal ----
            Vector3 newHorizontal;
            if (_wallRunReversing)
            {
                // Rapidly decelerate through zero and accelerate up to WallRunSpeed the other way
                float newSpeed = Mathf.MoveTowards(signedSpeed, WallRunSpeed, WallRunReverseAcceleration * deltaTime);
                if (newSpeed >= WallRunSpeed - 0.01f)
                {
                    _wallRunReversing = false;
                }

                newHorizontal = wallForward * newSpeed;
            }
            else
            {
                // All current horizontal speed goes straight onto the wall axis. No Lerp between directions,
                // which shrinks the vector when they differ (that was the shallow-angle speed loss).
                float carried = currentHorizontalVelocity.magnitude;
                float targetSpeed = carried < WallRunSpeed
                    ? Mathf.MoveTowards(carried, WallRunSpeed, WallRunEntryAcceleration * deltaTime)
                    : carried * Mathf.Pow(WallRunSpeedDecay, deltaTime);

                newHorizontal = wallForward * targetSpeed;
            }

            // Slight pull toward wall to maintain contact
            newHorizontal += -_wallRunNormal * 2f * deltaTime;

            // ---- Vertical (independent of direction / reversing) ----
            // Hold height for WallRunSlideDelay seconds of THIS wall run, then slide down up to a max speed
            if (_wallRunTimer > WallRunSlideDelay)
            {
                _wallRunFallSpeed = Mathf.MoveTowards(_wallRunFallSpeed, WallRunMaxFallSpeed, WallRunFallAcceleration * deltaTime);
            }

            float currentVertical = Vector3.Dot(currentVelocity, up);
            float newVertical = Mathf.Lerp(currentVertical, -_wallRunFallSpeed, 1f - Mathf.Exp(-WallRunVerticalSharpness * deltaTime));

            currentVelocity = newHorizontal + up * newVertical;
        }

        [Header("Wall Run Debug")]
        [Tooltip("Draw the wall run detection rays as gizmos (Gizmos must be enabled in the Scene/Game view).")]
        public bool DrawWallRunGizmos = true;

        private enum WallRayResult { Miss, Rejected, Chosen }

        private struct WallRayDebug
        {
            public Vector3 Origin;
            public Vector3 RayEnd;      // full cast length, exactly what Physics.Raycast was given
            public Vector3 HitPoint;
            public Vector3 HitNormal;
            public WallRayResult Result;
        }

        private readonly List<WallRayDebug> _wallRayDebug = new List<WallRayDebug>();
        private float _wallRayDebugTime = -1f;

        private void DrawGizmosForWallrun()
        {
            if (!DrawWallRunGizmos || Motor == null || !Application.isPlaying)
                return;

            // Only draw rays that were actually cast, using the exact origin / end / result recorded at cast time.
            // Nothing is recomputed here, so the gizmos can never differ from the real raycasts.
            if (Time.time - _wallRayDebugTime > 0.1f)
                return;

            for (int i = 0; i < _wallRayDebug.Count; i++)
            {
                WallRayDebug r = _wallRayDebug[i];
                switch (r.Result)
                {
                    case WallRayResult.Chosen: Gizmos.color = Color.green; break;
                    case WallRayResult.Rejected: Gizmos.color = Color.yellow; break;
                    default: Gizmos.color = Color.red; break;
                }

                Gizmos.DrawLine(r.Origin, r.RayEnd);
                if (r.Result != WallRayResult.Miss)
                {
                    Gizmos.DrawSphere(r.HitPoint, 0.06f);
                    Gizmos.DrawLine(r.HitPoint, r.HitPoint + r.HitNormal * 0.5f);
                }
            }
        }

        /// <summary>Rays start at the capsule center (chest height) instead of the feet.</summary>
        private Vector3 GetWallRunOrigin()
        {
            return Motor.TransientPosition + (Motor.TransientRotation * Motor.CharacterTransformToCapsuleCenter);
        }

        /// <summary>Builds the direction and length of one detection ray. Shared by detection and gizmos.</summary>
        private void GetWallRunRay(int side, int index, int rayCount, Vector3 refDir, Vector3 up, float gap, out Vector3 dir, out float distance)
        {
            // t = 0 -> straight sideways, t = 1 -> fully forward-fanned
            float t = rayCount == 1 ? 0f : (float)index / (rayCount - 1);
            float angleFromTravel = 90f - t * WallRunDetectionFanAngle;
            dir = Quaternion.AngleAxis(side * angleFromTravel, up) * refDir;

            // Long enough that its perpendicular reach equals (capsule radius + gap)
            float sin = Mathf.Max(0.2f, Mathf.Sin(angleFromTravel * Mathf.Deg2Rad));
            distance = Mathf.Min((Motor.Capsule.radius + gap) / sin, WallRunMaxDetectionReach);
        }

    }
}
