using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Momentum Tank")]
        [Tooltip("Maximum momentum the tank can store")]
        public float MaxMomentumTank = 30f;
        [Tooltip("How fast the tank decays per second once the grace window has expired")]
        public float MomentumTankDecayRate = 20f;
        [Tooltip("How long after grabbing something (or a big momentum loss) before the tank starts rapidly decaying")]
        public float MomentumTankGraceWindow = 1.5f;
        [Tooltip("Current momentum stored in the tank")]
        [SerializeField] private float _momentumTank = 0f;
        [Tooltip("Percentage of the tank transferred into dash speed when dashing while sliding")]
        [Range(0f, 1f)] public float DashSlideMomentumTransfer = 1f;
        [Tooltip("Percentage of the tank transferred into dash speed when dashing while airborne/grounded (non-sliding)")]
        [Range(0f, 1f)] public float DashAirMomentumTransfer = 0.8f;
        [Tooltip("Minimum bounce speed considered for momentum feedback logging")]
        public float MinBounceSpeedForPower = 12f;
        [Tooltip("Maximum bounce speed considered for momentum feedback logging")]
        public float MaxBounceSpeedForPower = 25f;
        [Tooltip("Multiplier applied to bounce speed when converting it into stored momentum")]
        public float BounceMomentumMultiplier = 1f;
        [Tooltip("Visual feedback for momentum/dash charge gain")]
        public bool ShowPowerFeedback = true;
        private float _lastGrabTime = -999f;
        private float _previousHorizontalSpeed = 0f;

        [Header("Momentum Tank - Big Loss Detection")]
        [Tooltip("EXPERIMENTAL: If true, the tank ONLY gains momentum from big losses detected within the short window, ignoring gradual/small frame-to-frame speed loss.")]
        public bool OnlyGainMomentumFromBigLoss = false;
        [Tooltip("Time window (seconds) within which a momentum drop is considered a 'big loss'")]
        public float BigMomentumLossWindow = 0.25f;
        [Tooltip("Minimum speed lost within the window to count as a big loss and reset the grace window")]
        public float BigMomentumLossThreshold = 8f;
        [Tooltip("Momentum lost while the player's current speed is at or below this threshold will NOT be added to the tank")]
        public float MinSpeedThresholdForMomentumLoss = 3f;
        private float _windowStartTime = 0f;
        private float _windowStartSpeed = 0f;

        private void UpdateMomentumTank()
        {
            float horizontalSpeed = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z).magnitude;

            // Normal (non-experimental) behavior: fill the tank from any per-frame momentum loss
            if (!OnlyGainMomentumFromBigLoss)
            {
                float speedLoss = _previousHorizontalSpeed - horizontalSpeed;
                if (speedLoss > 0f && horizontalSpeed > MinSpeedThresholdForMomentumLoss)
                {
                    _momentumTank = Mathf.Min(_momentumTank + speedLoss, MaxMomentumTank);
                }
            }

            // Detect a big chunk of momentum lost within a short window (e.g. slamming into a wall)
            if (Time.time - _windowStartTime > BigMomentumLossWindow)
            {
                // Window expired, start a new one from the current speed
                _windowStartTime = Time.time;
                _windowStartSpeed = horizontalSpeed;
            }
            else
            {
                float windowSpeedLoss = _windowStartSpeed - horizontalSpeed;
                if (windowSpeedLoss >= BigMomentumLossThreshold)
                {
                    // Reset the decay grace window, just like a grab would
                    _lastGrabTime = Time.time;

                    // EXPERIMENTAL: Only gain momentum here, exclusively from big losses
                    if (OnlyGainMomentumFromBigLoss && horizontalSpeed > MinSpeedThresholdForMomentumLoss)
                    {
                        _momentumTank = Mathf.Min(_momentumTank + windowSpeedLoss, MaxMomentumTank);
                    }

                    if (ShowPowerFeedback)
                    {
                        Debug.Log($"<color=orange>Big momentum loss detected ({windowSpeedLoss:F1} m/s)! Grace window reset.</color>");
                    }

                    // Restart the window from the new (lower) speed so we don't immediately re-trigger
                    _windowStartTime = Time.time;
                    _windowStartSpeed = horizontalSpeed;
                }
            }

            _previousHorizontalSpeed = horizontalSpeed;

            // Rapidly decay the tank once the post-grab/post-impact grace window has expired
            if (Time.time - _lastGrabTime > MomentumTankGraceWindow)
            {
                _momentumTank = Mathf.Max(0f, _momentumTank - (MomentumTankDecayRate * Time.deltaTime));
            }
        }

        /// <summary>
        /// Adds momentum to the tank based on bounce speed. Bigger bounces build more momentum.
        /// </summary>
        /// NOTE: This may be jank since you are losing momentum a lot when bouncing which will double add momentum.
        /// find a way to disable momentum from loss when bouncing? or just use it instead of adding momentum from bounce speed
        private void AddMomentumFromBounce(float bounceSpeed)
        {
            float gain = bounceSpeed * BounceMomentumMultiplier;
            _momentumTank = Mathf.Min(_momentumTank + gain, MaxMomentumTank);

            if (ShowPowerFeedback)
            {
                Debug.Log($"<color=cyan>Momentum gained from bounce! ({_momentumTank:F1}/{MaxMomentumTank:F1})</color>");
            }

            // EXPERIMENTAL RESET: Reset the grace window on bounce, just like a grab would
            _lastGrabTime = Time.time;
        }
    }
}

