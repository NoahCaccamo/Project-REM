using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("State Speed Bonuses")]
        [Tooltip("How fast (m/s^2) a state's speed bonus is added or removed. Higher = snappier.")]
        [SerializeField] private float StateBonusChangeRate = 60f;

        // Bonus currently applied on top of the momentum pool. Kept in the air, removed on the ground
        // unless the next state grants it too.
        private float _stateBonus = 0f;

        /// <summary>Horizontal speed without the state bonus (the "real" momentum).</summary>
        private float PoolSpeed()
        {
            return Mathf.Max(0f, HorizontalSpeed() - _stateBonus);
        }

        private float GetStateBonusTarget()
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Sliding:
                    return slideSpeedBonus;
                case CharacterState.WallRunning:
                    return WallRunSpeedBonus;
                default:
                    // Airborne in Default: keep whatever bonus we carry
                    if (!Motor.GroundingStatus.IsStableOnGround)
                    {
                        return _stateBonus;
                    }

                    // Landed crouched and fast enough to slide: the slide starts next frame, so don't drop the bonus
                    if ((_isCrouching || _shouldBeCrouching) && PoolSpeed() > crawlStateMaxSpeed)
                    {
                        return slideSpeedBonus;
                    }
                    return 0f;
            }
        }

        private void ApplyStateBonus(ref Vector3 currentVelocity, float deltaTime)
        {
            // Crawling and climbing set their own speeds, so just forget the bonus
            if (CurrentCharacterState == CharacterState.Crawling || CurrentCharacterState == CharacterState.Climbing)
            {
                _stateBonus = 0f;
                return;
            }

            float target = GetStateBonusTarget();
            float newBonus = Mathf.MoveTowards(_stateBonus, target, StateBonusChangeRate * deltaTime);
            float delta = newBonus - _stateBonus;
            if (Mathf.Abs(delta) < 0.0001f)
            {
                return;
            }

            Vector3 up = Motor.CharacterUp;
            Vector3 flat = Vector3.ProjectOnPlane(currentVelocity, up);
            float speed = flat.magnitude;

            // Never remove more speed than there is
            if (delta < 0f && -delta > speed)
            {
                delta = -speed;
                newBonus = _stateBonus + delta;
            }

            Vector3 dir = speed > 0.01f ? flat / speed : Vector3.ProjectOnPlane(Motor.CharacterForward, up).normalized;
            currentVelocity += dir * delta;
            _stateBonus = newBonus;

            // The slide keeps its own persistent speed, so it has to receive the same change
            if (CurrentCharacterState == CharacterState.Sliding)
            {
                _forwardSlideSpeed = Mathf.Max(0f, _forwardSlideSpeed + delta);
            }
        }
    }
}