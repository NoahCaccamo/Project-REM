using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Wall Running")]
        public bool EnableWallRunning = true;
        public float WallRunSpeed = 12f;
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
        /// Detects walls suitable for wall running
        /// </summary>
        private bool DetectWallForRunning(out Vector3 wallNormal, out bool isLeftWall)
        {
            wallNormal = Vector3.zero;
            isLeftWall = false;

            Vector3 rightDir = Motor.CharacterRight;
            Vector3 leftDir = -Motor.CharacterRight;

            // Check right side
            if (Physics.Raycast(Motor.TransientPosition, rightDir, out RaycastHit rightHit, WallRunDetectionDistance, wallLayer, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(rightHit.normal, Vector3.up) < 0.1f) // Wall is mostly vertical
                {
                    wallNormal = rightHit.normal;
                    isLeftWall = false;
                    return true;
                }
            }

            // Check left side
            if (Physics.Raycast(Motor.TransientPosition, leftDir, out RaycastHit leftHit, WallRunDetectionDistance, wallLayer, QueryTriggerInteraction.Ignore))
            {
                if (Vector3.Dot(leftHit.normal, Vector3.up) < 0.1f)
                {
                    wallNormal = leftHit.normal;
                    isLeftWall = true;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Initiates wall running state
        /// </summary>
        private void StartWallRun(Vector3 wallNormal, bool isLeftWall)
        {
            TransitionToState(CharacterState.WallRunning);
            _wallRunNormal = wallNormal;
            _isWallRunning = true;
            _isWallRunningLeft = isLeftWall;
            _jumpConsumed = false;
            Motor.ForceUnground();
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
