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
        [Tooltip("Degrees per second the view turns toward parallel-to-wall while looking into it.")]
        public float WallRunCameraAlignSpeed = 45f;
        [Tooltip("Stops aligning once the view is within this many degrees of parallel.")]
        public float WallRunCameraAlignDeadzone = 3f;


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

            Vector3 travelDir = Vector3.ProjectOnPlane(Motor.BaseVelocity, up);
            if (travelDir.sqrMagnitude < 0.01f)
            {
                travelDir = Motor.CharacterForward;
            }
            travelDir.Normalize();

            float maxEntrySin = Mathf.Sin(WallRunMaxEntryAngle * Mathf.Deg2Rad);
            float bestScore = float.MaxValue;
            bool found = false;

            int rays = Mathf.Max(1, WallRunDetectionRays);
            for (int side = -1; side <= 1; side += 2) // -1 = left, +1 = right
            {
                for (int i = 0; i < rays; i++)
                {
                    // t = 0 -> straight sideways, t = 1 -> fully forward-fanned
                    float t = rays == 1 ? 0f : (float)i / (rays - 1);
                    float angleFromTravel = 90f - t * WallRunDetectionFanAngle;
                    Vector3 dir = Quaternion.AngleAxis(side * angleFromTravel, up) * travelDir;

                    // Angled rays need more length to reach the same lateral distance
                    float sin = Mathf.Max(0.2f, Mathf.Sin(angleFromTravel * Mathf.Deg2Rad));
                    float distance = Mathf.Min(WallRunDetectionDistance / sin, WallRunMaxDetectionReach);

                    if (!Physics.Raycast(origin, dir, out RaycastHit hit, distance, wallLayer, QueryTriggerInteraction.Ignore))
                        continue;

                    // Wall must be mostly vertical
                    if (Vector3.Dot(hit.normal, up) >= 0.1f)
                        continue;

                    Vector3 planarNormal = Vector3.ProjectOnPlane(hit.normal, up).normalized;
                    float into = Vector3.Dot(travelDir, planarNormal);

                    // Not moving away from the wall, and not hitting too head-on
                    if (into > 0.05f || Mathf.Abs(into) > maxEntrySin)
                        continue;

                    // Prefer the wall that is laterally closest
                    float lateral = Vector3.Dot(hit.point - origin, -planarNormal);
                    if (lateral < bestScore)
                    {
                        bestScore = lateral;
                        wallNormal = planarNormal;
                        found = true;
                    }
                }
            }

            if (found)
            {
                isLeftWall = Vector3.Dot(-wallNormal, Motor.CharacterRight) < 0f;
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
            if (Mathf.Abs(angle) <= WallRunCameraAlignDeadzone)
                return 0f;

            float step = WallRunCameraAlignSpeed * deltaTime;
            return Mathf.Sign(angle) * Mathf.Min(Mathf.Abs(angle), step);
        }

        /// <summary>
        /// Handles wall running physics - PRESERVES MOMENTUM, INDEPENDENT OF CAMERA
        /// </summary>
        private void HandleWallRunning(ref Vector3 currentVelocity, float deltaTime)
        {
            _wallRunTimer += deltaTime;

            // Check if still against wall
            if (!DetectWallForRunning(out Vector3 currentWallNormal, out bool isLeftWall))
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Check duration limit
            if (_wallRunTimer >= WallRunMaxDuration)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Check if player has released forward input - exit wall run if no forward input
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(playerCamera.transform.forward, Motor.CharacterUp).normalized;
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);
            Vector3 rawMoveInput = _moveInputVector; // This is already set in SetInputs

            // Exit wall run if player is not holding forward
            if (rawMoveInput.sqrMagnitude < 0.1f)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Update wall normal and side (wall may curve)
            _wallRunNormal = currentWallNormal;
            _isWallRunningLeft = isLeftWall;

            // Calculate wall run direction (forward along wall) - INDEPENDENT of camera rotation
            Vector3 wallForward = Vector3.Cross(_wallRunNormal, Motor.CharacterUp).normalized;

            // Keep running in the same direction as when we started (or current velocity direction)
            Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);
            if (currentHorizontalVelocity.magnitude > 0.1f)
            {
                // Use current velocity direction to determine which way along wall to run
                if (Vector3.Dot(wallForward, currentHorizontalVelocity.normalized) < 0f)
                {
                    wallForward = -wallForward;
                }
            }
            else
            {
                // Fallback: use character forward
                if (Vector3.Dot(wallForward, Motor.CharacterForward) < 0f)
                {
                    wallForward = -wallForward;
                }
            }

            // Update stored wall run direction
            _wallRunDirection = wallForward;

            // PRESERVE MOMENTUM: Get current speed along wall
            float currentSpeedAlongWall = Vector3.Dot(currentHorizontalVelocity, wallForward);

            // Use the maximum of current speed or base wall run speed
            float targetSpeed = Mathf.Max(Mathf.Abs(currentSpeedAlongWall), WallRunSpeed);

            // Apply slight decay over time (set WallRunSpeedDecay to 1.0 for no decay)
            targetSpeed *= Mathf.Pow(WallRunSpeedDecay, deltaTime);

            Vector3 targetVelocity = wallForward * targetSpeed;

            // Allow some vertical input control
            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 verticalInput = Vector3.Project(_moveInputVector, Motor.CharacterUp);
                targetVelocity += verticalInput * WallRunSpeed * 0.5f;
            }

            // Smooth transition to target velocity (less aggressive to preserve momentum better)
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-5f * deltaTime));

            // Reduced gravity while wall running
            currentVelocity += playerCharacter.CurrentStats.gravity * WallRunGravityMultiplier * deltaTime;

            // Apply slight pull toward wall to maintain contact
            currentVelocity += -_wallRunNormal * 2f * deltaTime;
        }
    }
}
