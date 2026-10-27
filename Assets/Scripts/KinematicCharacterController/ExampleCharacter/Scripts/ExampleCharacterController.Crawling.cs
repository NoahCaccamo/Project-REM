using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Crawling")]
        [Tooltip("Max horizontal speed (m/s) while crawling. Crouching on the ground at or below this speed enters Crawling, and a slide that drops to this speed hands over to Crawling.")]
        [SerializeField] private float crawlStateMaxSpeed = 5f;
        [Tooltip("Crawling goes back to sliding only if speed exceeds the max by this much (m/s). Prevents flicker at the threshold.")]
        [SerializeField] private float crawlExitSpeedMargin = 0.5f;

        private void StartCrawl()
        {
            TransitionToState(CharacterState.Crawling);
        }

        private float HorizontalSpeed()
        {
            return Vector3.ProjectOnPlane(Motor.BaseVelocity, Motor.CharacterUp).magnitude;
        }

        /// <summary>
        /// Crawling ground movement: same friction/acceleration feel as walking, but capped at crawlStateMaxSpeed.
        /// </summary>
        private void HandleCrawling(ref Vector3 currentVelocity, float deltaTime)
        {
            if (!Motor.GroundingStatus.IsStableOnGround)
            {
                // Left the ground this frame; state is switched back to Default after the jump check
                currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;
                return;
            }

            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;

            // Reorient velocity on slope
            float magnitude = currentVelocity.magnitude;
            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, groundNormal) * magnitude;

            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
            Vector3 reorientedInput = Vector3.Cross(groundNormal, inputRight).normalized * _moveInputVector.magnitude;

            // Friction first (skipped on a frame that is about to jump), then accelerate towards the wish velocity
            bool willJump = _jumpRequested && !_jumpConsumed;
            if (!willJump)
            {
                currentVelocity = ApplyGroundFriction(currentVelocity, WalkFriction, WalkStopSpeed, deltaTime);
            }

            if (_moveInputVector.sqrMagnitude > 0f)
            {
                Vector3 wishDir = reorientedInput.normalized;
                float wishSpeed = crawlStateMaxSpeed * reorientedInput.magnitude;
                float addSpeed = wishSpeed - Vector3.Dot(currentVelocity, wishDir);
                if (addSpeed > 0f)
                {
                    float accelSpeed = Mathf.Min(WalkAcceleration * wishSpeed * deltaTime, addSpeed);
                    currentVelocity += wishDir * accelSpeed;
                }
            }

            // Hard cap
            if (currentVelocity.magnitude > crawlStateMaxSpeed)
            {
                currentVelocity = currentVelocity.normalized * crawlStateMaxSpeed;
            }
        }
    }
}