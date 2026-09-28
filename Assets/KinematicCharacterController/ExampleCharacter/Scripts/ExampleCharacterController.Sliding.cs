using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        private Vector3 _slideDirection;
        private float _slideSpeed;

        [Header("Sliding")]
        [SerializeField] private float slideInitialBoost = 0f;
        [SerializeField] private float slideGravityMultiplier = 1.3f;
        [SerializeField] private float slideFriction = 2f;
        [SerializeField] private float maxSlideSpeed = 20f;
        [SerializeField] private float minSlideAngle = 10f;
        [SerializeField] private float flatFrictionMultiplier = 5f;
        [SerializeField] private float slideSteeringSpeed = 5f;
        [SerializeField] private float slideSteeringSharpness = 8f;

        [Header("Slide Momentum Boost")]
        [SerializeField] private float slideMinMomentumThreshold = 8f;
        [SerializeField] private float slideMomentumBoostDuration = 0.5f;
        private float _slideMomentumBoostTimer = 0f;
        private bool _hasSlideBoost = false;

        private void HandleSliding(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float currentHorizontalSpeed = horizontalVelocity.magnitude;

            Vector3 desiredDirection = _moveInputVector;

            if (desiredDirection.sqrMagnitude < 0.01f)
            {
                if (horizontalVelocity.magnitude > 0.1f)
                {
                    desiredDirection = horizontalVelocity.normalized;
                }
                else
                {
                    desiredDirection = _slideDirection;
                }
            }
            else
            {
                desiredDirection = desiredDirection.normalized;
            }

            desiredDirection = Vector3.ProjectOnPlane(desiredDirection, groundNormal).normalized;

            _slideDirection = Vector3.Slerp(_slideDirection, desiredDirection, 1f - Mathf.Exp(-slideSteeringSharpness * deltaTime));

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
            float downhillDot = Vector3.Dot(_slideDirection, downhillDirection);

            // Update boost timer
            if (_hasSlideBoost)
            {
                _slideMomentumBoostTimer += deltaTime;

                // Maintain minimum threshold speed during boost period
                if (_slideMomentumBoostTimer < slideMomentumBoostDuration)
                {
                    if (currentHorizontalSpeed < slideMinMomentumThreshold)
                    {
                        // Maintain threshold speed during boost period
                        currentVelocity = new Vector3(_slideDirection.x * slideMinMomentumThreshold, currentVelocity.y, _slideDirection.z * slideMinMomentumThreshold);
                    }
                }
                else
                {
                    _hasSlideBoost = false; // Boost period ended
                }
            }

            // Apply slope acceleration only downhill
            if (downhillDot > 0f)
            {
                float slideAcceleration = 9.81f * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slideGravityMultiplier;
                currentVelocity += _slideDirection * slideAcceleration * deltaTime * downhillDot;
            }

            if (_moveInputVector.sqrMagnitude > 0.01f)
            {
                Vector3 steeringForce = desiredDirection * slideSteeringSpeed * deltaTime;
                currentVelocity += steeringForce;
            }

            // Apply friction (reduced during boost period)
            float frictionMultiplier = 1f;
            if (slopeAngle <= minSlideAngle) frictionMultiplier = flatFrictionMultiplier;
            if (downhillDot < 0f) frictionMultiplier *= 2f;

            // Reduce friction during boost period to help maintain speed
            if (_hasSlideBoost && _slideMomentumBoostTimer < slideMomentumBoostDuration)
            {
                frictionMultiplier *= 0.3f;
            }

            currentVelocity *= 1f - (slideFriction * frictionMultiplier * deltaTime);

            // Clamp max speed
            horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            if (horizontalVelocity.magnitude > maxSlideSpeed)
            {
                horizontalVelocity = horizontalVelocity.normalized * maxSlideSpeed;
                currentVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
            }

            // Update stored direction for next frame (so we gradually re-align)
            _slideSpeed = horizontalVelocity.magnitude;
        }


        private void StartSlide()
        {
            CurrentCharacterState = CharacterState.Sliding;

            Vector3 currentVelocity = Motor.BaseVelocity;
            float currentHorizontalSpeed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;

            if (currentHorizontalSpeed > 0.1f)
                _slideDirection = currentVelocity.normalized;
            else
                _slideDirection = Motor.CharacterForward;

            // MOMENTUM BOOST: If speed is below threshold, boost to threshold
            if (currentHorizontalSpeed < slideMinMomentumThreshold)
            {
                _hasSlideBoost = true;
                _slideMomentumBoostTimer = 0f;

                // Apply immediate velocity boost to horizontal components only
                Vector3 horizontalDir = new Vector3(_slideDirection.x, 0f, _slideDirection.z).normalized;
                float boostAmount = slideMinMomentumThreshold - currentHorizontalSpeed;
                _internalVelocityAdd = horizontalDir * boostAmount;
            }
            else
            {
                // No boost needed
                _hasSlideBoost = false;
            }

            _slideSpeed = currentHorizontalSpeed;
        }

        private void StopSlide()
        {
            CurrentCharacterState = CharacterState.Default;

            // Don't add extra momentum when exiting slide - just keep what we have
            _hasSlideBoost = false;
        }

    }
}
