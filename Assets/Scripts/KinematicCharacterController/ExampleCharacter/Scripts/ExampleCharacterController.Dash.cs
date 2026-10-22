using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Dash")]
        public float DashForce = 15f;
        public float DashCooldown = 0.3f;
        public int MaxAirDashes = 1;
        public float DashDuration = 0.15f;
        public bool PreserveMomentumAfterDash = false;
        private bool _dashRequested = false;
        private float _lastDashTime = -999f;
        private int _airDashesRemaining = 1;
        private bool _isDashing = false;
        private float _dashEndTime = 0f;
        private Vector3 _dashDirection;

        /// <summary>
        /// Consumes the momentum tank (and a dash charge) to perform a dash.
        /// Sliding dashes transfer 100% of the tank into dash speed; airborne/grounded dashes
        /// transfer a reduced percentage. The tank is always fully emptied afterwards.
        /// </summary>
        private void PerformDash(ref Vector3 currentVelocity, bool isSliding)
        {
            // Use FULL camera direction (including vertical component)
            Vector3 cameraForward = playerCamera.transform.forward;
            Vector3 dashDir = cameraForward.normalized;

            float transferPercent = isSliding ? DashSlideMomentumTransfer : DashAirMomentumTransfer;
            float dashSpeed = _momentumTank * transferPercent;

            // Apply dash velocity in full 3D direction
            if (!PreserveMomentumAfterDash)
            {
                // Replace velocity with dash (pure dash)
                currentVelocity = dashDir * dashSpeed;
            }
            else
            {
                // Add to existing velocity (momentum dash)
                currentVelocity += dashDir * dashSpeed;
            }

            // Track dash state
            _isDashing = true;
            _dashEndTime = Time.time + DashDuration;
            _dashDirection = dashDir;
            _lastDashTime = Time.time;
            _dashRequested = false;

            // The tank is always fully emptied on dash, regardless of transfer percentage
            _momentumTank = 0f;

            // Consume a dash charge
            _airDashesRemaining--;

            // Force unground to allow air control
            Motor.ForceUnground();

            Debug.Log($"Dash! Direction: {dashDir}, Speed: {dashSpeed:F1}, Transfer: {transferPercent:P0}, Remaining Charges: {_airDashesRemaining}/{MaxAirDashes}");
        }
    }
}

