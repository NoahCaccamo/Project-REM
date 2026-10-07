using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        private Vector3 _slideDirection;
        private float _slideSpeed;
        private float _slideStrafeInput;

        // Persistent forward-only slide speed. This is intentionally NOT re-derived from
        // currentVelocity's magnitude each frame, because currentVelocity also contains the lateral
        // strafe component baked in from the previous frame - measuring magnitude from that combined
        // vector re-introduces speed inflation every single frame strafe is held (since forward and
        // lateral velocity are orthogonal, their combined magnitude is always > forward speed alone).
        // Instead this value only ever changes via explicit accel/decel below, fully decoupled from
        // whatever lateral strafe velocity is layered on top when actually applying currentVelocity.
        private float _forwardSlideSpeed;

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

            // THE SLIDING CHANGES MAY HAVE AFFECTED HOW BHOP MOMENTUM IS GAINED.

            // Forward speed is persistent state (_forwardSlideSpeed), NOT re-derived from
            // horizontalVelocity.magnitude. currentVelocity always contains last frame's lateral
            // strafe component baked in alongside the forward component; since they're orthogonal,
            // measuring magnitude from the combined vector always yields sqrt(forward^2 + lateral^2),
            // which is strictly greater than forward speed alone. Re-measuring from that inflated
            // value every frame compounded the inflation the entire time strafe was held. Using
            // persistent state sidesteps this entirely - forward speed only ever changes via the
            // explicit accel/decel below.
            float forwardSpeed = _forwardSlideSpeed;

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

            _forwardSlideSpeed = forwardSpeed;

            Vector3 forwardVelocity = _slideDirection * forwardSpeed;

            // Left and right strafing
            Vector3 rightDir = Vector3.Cross(groundNormal, _slideDirection).normalized;
            Vector3 lateralVelocity = rightDir * (_slideStrafeInput * slideStrafeSpeed);

            /* old
            Vector3 newHorizontalVelocity = forwardVelocity + lateralVelocity;
            currentVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);
            */
            // NEW:
            // forwardVelocity and lateralVelocity are orthogonal, so simply adding them always
            // produces a combined magnitude of sqrt(forwardSpeed^2 + lateralSpeed^2), which is
            // strictly greater than forwardSpeed alone whenever strafing. This showed up as a flat
            // speed boost the instant strafe input was held. To keep strafing a pure REDIRECT
            // (same total speed, different direction) rather than an addition, we renormalize the
            // combined vector back down to exactly forwardSpeed.
            
            Vector3 combinedVelocity = forwardVelocity + lateralVelocity;
            Vector3 newHorizontalVelocity = combinedVelocity.sqrMagnitude > 0.0001f
                ? combinedVelocity.normalized * forwardSpeed
                : combinedVelocity;

            currentVelocity = new Vector3(newHorizontalVelocity.x, currentVelocity.y, newHorizontalVelocity.z);

            // Track forward speed only (not the combined/inflated magnitude) so UI/other systems
            // reading _slideSpeed reflect the actual intended forward speed, unaffected by strafing.
            _slideSpeed = forwardSpeed;

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