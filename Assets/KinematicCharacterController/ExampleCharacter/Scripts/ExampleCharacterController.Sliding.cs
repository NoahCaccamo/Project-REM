using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        private Vector3 _slideDirection;
        private float _slideSpeed;
        private float _slideStrafeInput;

        [Header("Sliding")]
        [SerializeField] private float slideBaseSpeed = 16f;
        [SerializeField] private float slideDecelRate = 4f;
        [SerializeField] private float minSlideAngle = 10f;
        [SerializeField] private float slideSteeringSharpness = 8f;
        [SerializeField] private float slideStrafeSpeed = 2f;

        [Header("Slide Timer")]
        [SerializeField] private float slideMaxTimer = 3f;
        [SerializeField] private float slideMinTimer = 1f;
        [SerializeField] private float slideEndDecelRate = 20f;
        [SerializeField] private float slideEndStopSpeed = 0.5f;
        private float _slideTimer = 0f;

        private void HandleSliding(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

            Vector3 desiredDirection = _lookInputVector;

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
            bool isGoingDownhill = slopeAngle > minSlideAngle && downhillDot > 0f;

            // Slide timer: counts down while sliding on the ground, pauses while going downhill,
            // and never drops below the specified minimum.
            bool timerHasEnded = _slideTimer <= slideMinTimer;

            if (!isGoingDownhill)
            {
                _slideTimer -= deltaTime;
            }
            if (_slideTimer < slideMinTimer)
            {
                _slideTimer = slideMinTimer;
            }

            float forwardSpeed = Vector3.Dot(horizontalVelocity, _slideDirection);

            if (timerHasEnded)
            {
                // Timer ran out: rapidly decelerate to a stop instead of holding at base speed.
                forwardSpeed = Mathf.Max(0f, forwardSpeed - (slideEndDecelRate * deltaTime));
            }
            else if (forwardSpeed > slideBaseSpeed)
            {
                // Forward speed: decelerate towards base speed, never going below it.
                // Sliding downhill is treated the same as flat/horizontal sliding (no extra speed from slope).
                forwardSpeed = Mathf.Max(slideBaseSpeed, forwardSpeed - (slideDecelRate * deltaTime));
            }
            else
            {
                forwardSpeed = slideBaseSpeed;
            }

            Vector3 forwardVelocity = _slideDirection * forwardSpeed;

            // Left and right strafing
            Vector3 rightDir = Vector3.Cross(groundNormal, _slideDirection).normalized;
            Vector3 lateralVelocity = rightDir * (_slideStrafeInput * slideStrafeSpeed);

            Vector3 newHorizontalVelocity = forwardVelocity + lateralVelocity;
            currentVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);

            _slideSpeed = newHorizontalVelocity.magnitude;

            // Once the timer has ended and speed has bled off, end the slide.
            if (timerHasEnded && forwardSpeed <= slideEndStopSpeed)
            {
                StopSlide();
            }
        }

        private void StartSlide()
        {
            TransitionToState(CharacterState.Sliding);
        }

        private void StopSlide()
        {
            TransitionToState(CharacterState.Default);
        }

    }
}