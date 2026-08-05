using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using UnityEngine.Windows;
using UnityEngine.UIElements;
using static UnityEngine.UI.Image;
using Unity.Burst.CompilerServices;
using TMPro;

namespace KinematicCharacterController.Examples
{
    public enum CharacterState
    {
        Default,
        Climbing,
        Sliding,
        WallRunning,
        Drifting
    }

    public enum OrientationMethod
    {
        TowardsCamera,
        TowardsMovement,
    }

    public struct PlayerCharacterInputs
    {
        public float MoveAxisForward;
        public float MoveAxisRight;
        public Quaternion CameraRotation;
        public bool JumpDown;
        public bool JumpHeld;
        public bool CrouchDown;
        public bool CrouchUp;
        public bool LeftHand;
        public bool RightHand;
        public bool LeftHandDown;
        public bool RightHandDown;
        public bool SprintDown;
    }

    public struct AICharacterInputs
    {
        public Vector3 MoveVector;
        public Vector3 LookVector;
    }

    public enum BonusOrientationMethod
    {
        None,
        TowardsGravity,
        TowardsGroundSlopeAndGravity,
    }

    public class ExampleCharacterController : MonoBehaviour, ICharacterController
    {
        public KinematicCharacterMotor Motor;
        public Camera playerCamera;
        public PlayerCharacter playerCharacter;

        [Header("Hands")]
        public Hand leftHand;
        public Hand rightHand;
        public HandController LeftHandController;
        public HandController RightHandController;

        [Header("Momentum System")]
        public float currentSpeed;
        public float momentumDecayRate = 0.5f;
        public float maxMomentumSpeed = 25f;
        public float speedRetentionOnLanding = 0.8f;
        [SerializeField] private TextMeshProUGUI speedDisplay;

        [Header("Stable Movement")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public OrientationMethod OrientationMethod = OrientationMethod.TowardsCamera;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 15f;
        public float AirAccelerationSpeed = 15f;
        public float AirStrafeMultiplier = 1.5f;
        public float Drag = 0.1f;

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
        public bool OnlyGainMomentumFromBigLoss = true;
        [Tooltip("Time window (seconds) within which a momentum drop is considered a 'big loss'")]
        public float BigMomentumLossWindow = 0.25f;
        [Tooltip("Minimum speed lost within the window to count as a big loss and reset the grace window")]
        public float BigMomentumLossThreshold = 8f;
        private float _windowStartTime = 0f;
        private float _windowStartSpeed = 0f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public float JumpUpSpeed = 10f;
        public float JumpScalableForwardSpeed = 10f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;
        public bool PreserveHorizontalMomentumOnJump = true;

        [Header("Bounce")]
        public bool EnableBounce = true;
        public float BounceSpeed = 15f;
        public float BounceSpeedMultiplier = 1.2f;
        public float MinBounceSpeed = 10f;
        public float MaxBounceSpeed = 30f;
        public float BounceAirAccelerationMultiplier = 0.3f;
        public float BounceVelocityRedirection = 0.5f;

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

        [Header("Hands")]
        public float grabRadius = 0.1f;
        public float grabDistance = 0.2f;

        [Header("Static Climb Variables")]
        public float climbMoveSpeed = 1f;
        public float maxStaticClimbRadius = 2.5f;

        [Header("Momentum Climb Variables")]
        public float maxMomentumSwingRadius = 5f;
        public float momentumSwingDamping = 0.5f;

        public bool _hasMovedWhileClimbing = false;

        public MemoryType defaultMemoryType;

        public LayerMask climbableLayer;
        public LayerMask wallLayer;
        public LayerMask interactableLayer;

        private RaycastHit leftHandHit;
        private Vector3 leftHandGrabAnchor;
        private Handhold leftHandhold;

        private RaycastHit rightHandHit;
        private Vector3 rightHandGrabAnchor;
        private Handhold rightHandhold;

        // Momentum preservation system
        private Vector3 _storedHorizontalMomentum;
        private float _momentumGrabTime;
        private float _momentumGraceWindow;
        private float _momentumRetention;
        private bool _hasMomentumToRestore;

        // Drift system
        private Vector3 _driftPolePosition;
        private float _driftRadius;
        private float _driftSpeed;
        private float _driftMinMomentum; // NEW: Minimum momentum during drift

        private bool wantsGrabL = false;
        public bool isGrabbingL = false;
        private bool grabLDown = false;

        private bool wantsGrabR = false;
        public bool isGrabbingR = false;
        private bool grabRDown = false;

        private bool _interactRequestedL = false;
        private bool _interactRequestedR = false;

        private bool _sprintPressed = false;

        public DeliveryWaypoint deliveryWaypoint;

        [Header("Misc")]
        public List<Collider> IgnoredColliders = new List<Collider>();
        public BonusOrientationMethod BonusOrientationMethod = BonusOrientationMethod.None;
        public float BonusOrientationSharpness = 10f;
        public Vector3 Gravity = new Vector3(0, -30f, 0);
        public Transform MeshRoot;
        public Transform CameraFollowPoint;
        public float CrouchedCapsuleHeight = 1f;

        public CharacterState CurrentCharacterState { get; private set; }

        private Collider[] _probedColliders = new Collider[8];
        private RaycastHit[] _probedHits = new RaycastHit[8];
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private bool _jumpRequested = false;
        private bool _jumpConsumed = false;
        private bool _jumpedThisFrame = false;
        private float _timeSinceJumpRequested = Mathf.Infinity;
        private float _timeSinceLastAbleToJump = 0f;
        private Vector3 _internalVelocityAdd = Vector3.zero;
        private bool _shouldBeCrouching = false;
        private bool _isCrouching = false;

        private bool _jumpHeld = false;
        private bool _shouldBounceOnLanding = false;
        private Vector3 _velocityBeforeLanding = Vector3.zero;
        private bool _isBouncing = false;
        private float _normalAirAcceleration;

        private Vector3 lastInnerNormal = Vector3.zero;
        private Vector3 lastOuterNormal = Vector3.zero;

        private void Awake()
        {
            TransitionToState(CharacterState.Default);
            Motor.CharacterController = this;
            playerCamera = Camera.main;
            playerCharacter = GetComponent<PlayerCharacter>();
            _normalAirAcceleration = AirAccelerationSpeed;

            _airDashesRemaining = MaxAirDashes;
            _momentumTank = 0f;
            _lastGrabTime = Time.time;
            _previousHorizontalSpeed = 0f;
            _windowStartTime = Time.time;
            _windowStartSpeed = 0f;

            SetupSpeedDisplay();
        }

        private void SetupSpeedDisplay()
        {
            if (speedDisplay == null)
            {
                GameObject canvasObj = new GameObject("SpeedDisplayCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                GameObject textObj = new GameObject("SpeedText");
                textObj.transform.SetParent(canvasObj.transform, false);

                speedDisplay = textObj.AddComponent<TextMeshProUGUI>();
                speedDisplay.fontSize = 36;
                speedDisplay.alignment = TextAlignmentOptions.Center;
                speedDisplay.color = Color.white;

                RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 1f);
                rectTransform.anchoredPosition = new Vector2(0f, -100f);
                rectTransform.sizeDelta = new Vector2(400f, 100f);
            }
        }

        private void Update()
        {
            UpdateSpeedDisplay();
            UpdateWallRunCooldown();
            UpdateMomentumGraceWindow();
            UpdateMomentumTank();
        }

        private void LateUpdate()
        {
            UpdateCameraTilt();
        }

        private void UpdateSpeedDisplay()
        {
            if (speedDisplay != null)
            {
                Vector3 horizontalVelocity = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                currentSpeed = horizontalVelocity.magnitude;

                string colorTag = GetSpeedColorTag(currentSpeed);

                string momentumBar = GetMomentumBar();
                speedDisplay.text = $"{colorTag}{currentSpeed:F1}</color> m/s\n{momentumBar}\nDashes: {_airDashesRemaining}/{MaxAirDashes}";

            }
        }

        private string GetMomentumBar()
        {
            float fillPercent = MaxMomentumTank > 0f ? Mathf.Clamp01(_momentumTank / MaxMomentumTank) : 0f;
            int filledSegments = Mathf.RoundToInt(fillPercent * 10f);

            string bar = "Momentum: [";
            for (int i = 0; i < 10; i++)
            {
                bar += i < filledSegments ? "<color=#00FFFF>●</color>" : "<color=#808080>○</color>";
            }
            bar += $"] {_momentumTank:F1}/{MaxMomentumTank:F1}";
            return bar;
        }

        private string GetSpeedColorTag(float speed)
        {
            if (speed < 5f) return "<color=#FFFFFF>";
            if (speed < 10f) return "<color=#00FF00>";
            if (speed < 15f) return "<color=#FFFF00>";
            if (speed < 20f) return "<color=#FFA500>";
            return "<color=#FF0000>";
        }

        private void UpdateMomentumGraceWindow()
        {
            if (_hasMomentumToRestore)
            {
                float timeSinceGrab = Time.time - _momentumGrabTime;
                if (timeSinceGrab > _momentumGraceWindow)
                {
                    _hasMomentumToRestore = false;
                    _storedHorizontalMomentum = Vector3.zero;
                }
            }
        }

        private void UpdateMomentumTank()
        {
            float horizontalSpeed = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z).magnitude;

            // Normal (non-experimental) behavior: fill the tank from any per-frame momentum loss
            if (!OnlyGainMomentumFromBigLoss)
            {
                float speedLoss = _previousHorizontalSpeed - horizontalSpeed;
                if (speedLoss > 0f)
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
                    if (OnlyGainMomentumFromBigLoss)
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
        /// Called whenever a hand successfully grabs onto something.
        /// Resets the momentum tank's decay grace window and replenishes a dash charge.
        /// </summary>
        private void OnPlayerGrabbed()
        {
            _lastGrabTime = Time.time;

            if (_airDashesRemaining < MaxAirDashes)
            {
                _airDashesRemaining++;

                if (ShowPowerFeedback)
                {
                    Debug.Log($"<color=yellow>★ DASH CHARGED! ({_airDashesRemaining}/{MaxAirDashes}) ★</color>");
                }
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

        public void TransitionToState(CharacterState newState)
        {
            if (newState == CurrentCharacterState)
            {
                return;
            }
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        public void OnStateEnter(CharacterState state, CharacterState fromState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        break;
                    }
                case CharacterState.Climbing:
                    {
                        _hasMovedWhileClimbing = false;

                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        _storedHorizontalMomentum = horizontalVel;
                        _momentumGrabTime = Time.time;

                        bool isLeftMomentum = leftHandhold != null && leftHandhold.handholdType == HandholdType.Momentum;
                        bool isRightMomentum = rightHandhold != null && rightHandhold.handholdType == HandholdType.Momentum;

                        if (isLeftMomentum || isRightMomentum)
                        {
                            Handhold activeHandhold = isLeftMomentum ? leftHandhold : rightHandhold;
                            _momentumGraceWindow = activeHandhold.momentumGraceWindow;
                            _momentumRetention = activeHandhold.momentumRetention;
                            _hasMomentumToRestore = true;
                        }
                        else
                        {
                            _hasMomentumToRestore = false;
                        }

                        break;
                    }
                case CharacterState.Drifting:
                    {
                        // Store initial momentum magnitude (not direction)
                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        float initialSpeed = horizontalVel.magnitude;
                        _driftSpeed = initialSpeed;
                        _momentumGrabTime = Time.time;

                        if (isGrabbingL && leftHandhold != null && leftHandhold.handholdType == HandholdType.Drift)
                        {
                            _driftPolePosition = leftHandGrabAnchor;
                            _driftRadius = leftHandhold.driftRadius;
                            _momentumGraceWindow = leftHandhold.driftMomentumWindow;
                            _momentumRetention = leftHandhold.driftMomentumRetention;
                            _driftMinMomentum = leftHandhold.driftMinMomentum; // NEW
                        }
                        else if (isGrabbingR && rightHandhold != null && rightHandhold.handholdType == HandholdType.Drift)
                        {
                            _driftPolePosition = rightHandGrabAnchor;
                            _driftRadius = rightHandhold.driftRadius;
                            _momentumGraceWindow = rightHandhold.driftMomentumWindow;
                            _momentumRetention = rightHandhold.driftMomentumRetention;
                            _driftMinMomentum = rightHandhold.driftMinMomentum; // NEW
                        }

                        // Store SPEED (magnitude) not direction - direction will be player facing on release
                        _storedHorizontalMomentum = Vector3.zero; // Clear directional momentum
                        _hasMomentumToRestore = true;

                        Debug.Log($"Drift started! Stored speed: {_driftSpeed:F2} m/s, Min momentum: {_driftMinMomentum:F2} m/s");
                        break;
                    }
                case CharacterState.WallRunning:
                    {
                        _wallRunTimer = 0f;
                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        _wallRunEntrySpeed = horizontalVel.magnitude;

                        Vector3 wallForward = Vector3.Cross(_wallRunNormal, Motor.CharacterUp).normalized;
                        if (Vector3.Dot(wallForward, Motor.CharacterForward) < 0f)
                        {
                            wallForward = -wallForward;
                        }
                        _wallRunDirection = wallForward;
                        break;
                    }
                case CharacterState.Sliding:
                    {
                        _slideMomentumBoostTimer = 0f;
                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        if (horizontalVel.magnitude > 0.1f)
                        {
                            _slideDirection = horizontalVel.normalized;
                        }
                        else
                        {
                            _slideDirection = Motor.CharacterForward;
                        }
                        break;
                    }
            }
        }

        public void OnStateExit(CharacterState state, CharacterState toState)
        {
            switch (state)
            {
                case CharacterState.Default:
                    {
                        break;
                    }
                case CharacterState.Climbing:
                    {
                        _hasMovedWhileClimbing = false;

                        if (toState == CharacterState.Default && _hasMomentumToRestore)
                        {
                            float timeSinceGrab = Time.time - _momentumGrabTime;
                            if (timeSinceGrab <= _momentumGraceWindow)
                            {
                                Vector3 restoredMomentum = _storedHorizontalMomentum * _momentumRetention;
                                _internalVelocityAdd = restoredMomentum;
                                Debug.Log($"Restored momentum: {restoredMomentum.magnitude:F2} m/s");
                            }
                        }

                        _hasMomentumToRestore = false;
                        _storedHorizontalMomentum = Vector3.zero;

                        leftHandhold = null;
                        rightHandhold = null;
                        break;
                    }
                case CharacterState.Drifting:
                    {
                        // NEW: Redirect momentum in player's facing direction
                        if (toState == CharacterState.Default && _hasMomentumToRestore)
                        {
                            float timeSinceGrab = Time.time - _momentumGrabTime;
                            if (timeSinceGrab <= _momentumGraceWindow)
                            {
                                // Get player's facing direction (horizontal plane)
                                Vector3 facingDirection = Vector3.ProjectOnPlane(Motor.CharacterForward, Motor.CharacterUp).normalized;

                                // Apply stored speed in facing direction with retention multiplier
                                float exitSpeed = Mathf.Max(_driftSpeed, _driftMinMomentum) * _momentumRetention;
                                Vector3 redirectedMomentum = facingDirection * exitSpeed;

                                _internalVelocityAdd = redirectedMomentum;
                                Debug.Log($"Drift exit! Speed: {exitSpeed:F2} m/s in direction: {facingDirection}");
                            }
                        }

                        _hasMomentumToRestore = false;
                        _storedHorizontalMomentum = Vector3.zero;
                        _driftSpeed = 0f;
                        leftHandhold = null;
                        rightHandhold = null;
                        break;
                    }
                case CharacterState.WallRunning:
                    {
                        _isWallRunning = false;
                        _wallRunCooldown = WALL_RUN_COOLDOWN_TIME;
                        _canWallRun = false;
                        break;
                    }
            }
        }

        /// <summary>
        /// This is called every frame by ExamplePlayer in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            // Clamp input
            Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);

            // Calculate camera direction and rotation on the character plane
            Vector3 cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.forward, Motor.CharacterUp).normalized;
            if (cameraPlanarDirection.sqrMagnitude == 0f)
            {
                cameraPlanarDirection = Vector3.ProjectOnPlane(inputs.CameraRotation * Vector3.up, Motor.CharacterUp).normalized;
            }
            Quaternion cameraPlanarRotation = Quaternion.LookRotation(cameraPlanarDirection, Motor.CharacterUp);

            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Move and look inputs
                        _moveInputVector = cameraPlanarRotation * moveInputVector;

                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                _lookInputVector = _moveInputVector.normalized;
                                break;
                        }

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Track if jump is being held for bounce
                        _jumpHeld = inputs.JumpHeld;

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // Grab input
                        wantsGrabL = inputs.LeftHand;
                        wantsGrabR = inputs.RightHand;
                        grabLDown = inputs.LeftHandDown;
                        grabRDown = inputs.RightHandDown;

                        if (grabLDown)
                        {
                            _interactRequestedL = true;
                        }

                        if (grabRDown)
                        {
                            _interactRequestedR = true;
                        }

                        // _sprintPressed = inputs.SprintDown;

                        // Request dash when shift is pressed and cooldown is ready
                        if (inputs.SprintDown && Time.time >= _lastDashTime + DashCooldown)
                        {
                            if (_airDashesRemaining <= 0)
                            {
                                if (ShowPowerFeedback)
                                {
                                    Debug.Log("<color=red>No dash charges remaining! Grab something to recharge.</color>");
                                }
                            }
                            else if (_momentumTank <= 0f)
                            {
                                if (ShowPowerFeedback)
                                {
                                    Debug.Log("<color=red>Momentum tank is empty! Lose some speed first.</color>");
                                }
                            }
                            else
                            {
                                _dashRequested = true;
                            }
                        }

                        break;
                    }

                case CharacterState.WallRunning:
                    {
                        // Store raw input for wall running - don't transform by camera
                        _moveInputVector = cameraPlanarRotation * moveInputVector;

                        // Look direction still follows camera, but doesn't affect wall run direction
                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                // During wall run, look towards wall run direction instead of input
                                _lookInputVector = _wallRunDirection;
                                break;
                        }

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Track if jump is being held for bounce
                        _jumpHeld = inputs.JumpHeld;

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // Grab input
                        wantsGrabL = inputs.LeftHand;
                        wantsGrabR = inputs.RightHand;
                        grabLDown = inputs.LeftHandDown;
                        grabRDown = inputs.RightHandDown;

                        if (grabLDown)
                        {
                            _interactRequestedL = true;
                        }

                        if (grabRDown)
                        {
                            _interactRequestedR = true;
                        }

                        // sprint
                        _sprintPressed = inputs.SprintDown;

                        break;
                    }

                case CharacterState.Sliding:
                    {
                        _moveInputVector = cameraPlanarRotation * moveInputVector;

                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                                if (horizontalVel.magnitude > 0.1f)
                                {
                                    _lookInputVector = horizontalVel.normalized;
                                }
                                else
                                {
                                    _lookInputVector = cameraPlanarDirection;
                                }
                                break;
                        }

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // Grab input
                        wantsGrabL = inputs.LeftHand;
                        wantsGrabR = inputs.RightHand;
                        grabLDown = inputs.LeftHandDown;
                        grabRDown = inputs.RightHandDown;

                        // Request dash while sliding (100% momentum transfer)
                        if (inputs.SprintDown && Time.time >= _lastDashTime + DashCooldown)
                        {
                            if (_airDashesRemaining <= 0)
                            {
                                if (ShowPowerFeedback)
                                {
                                    Debug.Log("<color=red>No dash charges remaining! Grab something to recharge.</color>");
                                }
                            }
                            else if (_momentumTank <= 0f)
                            {
                                if (ShowPowerFeedback)
                                {
                                    Debug.Log("<color=red>Momentum tank is empty! Lose some speed first.</color>");
                                }
                            }
                            else
                            {
                                _dashRequested = true;
                            }
                        }

                        break;
                    }

                case CharacterState.Climbing:
                case CharacterState.Drifting:
                    {
                        // Change move input to be where youre looking
                        _moveInputVector = inputs.CameraRotation * moveInputVector;

                        switch (OrientationMethod)
                        {
                            case OrientationMethod.TowardsCamera:
                                _lookInputVector = cameraPlanarDirection;
                                break;
                            case OrientationMethod.TowardsMovement:
                                _lookInputVector = _moveInputVector.normalized;
                                break;
                        }

                        // Jumping input
                        if (inputs.JumpDown)
                        {
                            _timeSinceJumpRequested = 0f;
                            _jumpRequested = true;
                        }

                        // Crouching input
                        if (inputs.CrouchDown)
                        {
                            _shouldBeCrouching = true;

                            if (!_isCrouching)
                            {
                                _isCrouching = true;
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                                MeshRoot.localScale = new Vector3(1f, 0.5f, 1f);
                            }
                        }
                        else if (inputs.CrouchUp)
                        {
                            _shouldBeCrouching = false;
                        }

                        // Grab input
                        wantsGrabL = inputs.LeftHand;
                        wantsGrabR = inputs.RightHand;
                        grabLDown = inputs.LeftHandDown;
                        grabRDown = inputs.RightHandDown;

                        break;
                    }
            }
        }

        private void HandleItemPickup(ItemPickup pickup)
        {
            ItemObject item = pickup.ItemData;

            switch (item.type)
            {
                case ItemType.Equipment:
                    EquipmentObject equipment = item as EquipmentObject;
                    if (equipment != null)
                    {
                        // add item to inventory here

                        Debug.Log($"Picked up Equipment: {item.name}");
                    }
                    break;
                default:
                    break;
            }

            pickup.OnPickedUp();
        }

        void Interact(bool isRight = false)
        {
            Hand activeHand = isRight ? rightHand : leftHand;

            PlayerContext context = new PlayerContext(transform, playerCamera, Motor);

            if (!activeHand.IsEmpty)
            {
                activeHand.Use(context);

                // Reset the interact request right after use
                if (!isRight)
                {
                    _interactRequestedL = false;
                    return;
                }
                else
                {
                    _interactRequestedR = false;
                    return;
                }
            }

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            RaycastHit hit = isRight ? rightHandHit : leftHandHit;

            if (Physics.SphereCast(origin, grabRadius, direction, out hit, grabDistance, interactableLayer))
            {

                // Update the appropriate hand hit variable
                if (isRight)
                {
                    rightHandHit = hit;
                }
                else
                {
                    leftHandHit = hit;
                }

                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null && interactable.CanInteract())
                {
                    interactable.OnInteract(context);
                    ResetInteractRequest(isRight);
                    return;
                }

                ItemPickup pickup = hit.collider.GetComponent<ItemPickup>();
                if (pickup != null)
                {
                    Debug.Log("picking up item" + pickup.ItemData.name);
                    activeHand.PickUp(pickup);
                    // HandleItemPickup(pickup);
                    ResetInteractRequest(isRight);
                    return;
                }

                // Next, check if it's a package
                PackagePickup packagePickup = hit.collider.GetComponent<PackagePickup>();
                if (packagePickup != null)
                {
                    HandlePackagePickup(packagePickup);
                    ResetInteractRequest(isRight);
                    return;
                }

                // THIS SUCKS - change later
                if (hit.collider.name.Contains("PackageDropoff"))
                {
                    DropPackage();
                    ResetInteractRequest(isRight);
                    return;
                }
            }



            // We interacted so stop the request
            ResetInteractRequest(isRight);

        }

        void ResetInteractRequest(bool isRight)
        {
            if (isRight)
            {
                _interactRequestedR = false;
            }
            else
            {
                _interactRequestedL = false;
            }
        }

        void HandlePackagePickup(PackagePickup packagePickup)
        {
            if (playerCharacter.currentPackage != null)
            {
                Debug.Log("Already carrying a package!");
                return;
            }
            // Apply package modifiers
            playerCharacter.AcceptPackage(packagePickup.PackageData);

            //Transform pickup
            Transform dropoff = LocationRegistry.ResolveDropoff(packagePickup.PackageData.dropoffLocation);
            deliveryWaypoint.target = dropoff.position;

            // Update world sections if needed
            WorldSectionManager.Instance.RefreshWorld(packagePickup.PackageData);

            Debug.Log($"Picked up package: {packagePickup.PackageData.themeName}");
        }

        public void DropPackage()
        {
            if (playerCharacter.currentPackage == null) return;

            // Remove all modifiers applied by the package
            playerCharacter.modifierHandler.RemoveAll();

            // Optionally reset world sections
            WorldSectionManager.Instance.RefreshWorld(defaultMemoryType);

            Transform pickup = LocationRegistry.FindNearestPickup(playerCamera.transform);
            deliveryWaypoint.target = pickup.position;

            playerCharacter.currentPackage = null;
        }

        public float handSurfaceOffset = 0.05f;

        Vector3 climbNormal;

        [Header("Fish Wrangling")]
        public LayerMask fishLayer;
        private FishWrangler currentFish = null;
        private Vector3 leftHandFishLocalOffset;  // NEW: Local space offset on fish
        private Vector3 rightHandFishLocalOffset;

        void TryGrabL()
        {
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            // Check for fish first (use climbableLayer, not fishLayer!)
            if (Physics.SphereCast(origin, grabRadius, direction, out RaycastHit fishHit, grabDistance * 2, climbableLayer))
            {
                FishWrangler fish = fishHit.collider.GetComponent<FishWrangler>();
                if (fish != null)
                {
                    isGrabbingL = true;
                    OnPlayerGrabbed();
                    // NEW: Only set currentFish if not already set (left hand might have grabbed first)
                    if (currentFish == null)
                    {
                        currentFish = fish;
                    }

                    // NEW: Store the local-space offset from fish center to grab point
                    leftHandFishLocalOffset = fish.transform.InverseTransformPoint(fishHit.point);


                    // Set grab anchor to fish's current position
                    leftHandGrabAnchor = fish.transform.position;
                    leftHandhold = null; // Fish has no handhold component

                    Vector3 handVisualPosition = fishHit.point + fishHit.normal * handSurfaceOffset;
                    LeftHandController.StartGrab(handVisualPosition);

                    // Transition to climbing state so player follows fish
                    TransitionToState(CharacterState.Climbing);
                    _jumpConsumed = false;
                    Motor.ForceUnground();

                    // NEW: Start minigame on FIRST hand grab (even if only one hand)
                    if (!fish.IsBeingWrangled())
                    {
                        fish.OnGrabbedByPlayer(this, isGrabbingL, isGrabbingR);
                    }
                }
            }

            if (Physics.SphereCast(origin, grabRadius, direction, out leftHandHit, grabDistance, climbableLayer))
            {
                // Skip if this is a fish (already handled above)
                if (leftHandHit.collider.GetComponent<FishWrangler>() != null)
                {
                    return;
                }
                isGrabbingL = true;

                leftHandhold = leftHandHit.collider.GetComponent<Handhold>();

                Vector3 handVisualPosition = leftHandHit.point + leftHandHit.normal * handSurfaceOffset;
                LeftHandController.StartGrab(handVisualPosition);
                leftHandGrabAnchor = leftHandHit.point;
                climbNormal = leftHandHit.normal;

                if (leftHandhold != null && leftHandhold.handholdType == HandholdType.Drift)
                {
                    if (CurrentCharacterState == CharacterState.Sliding)
                    {
                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        if (horizontalVel.magnitude >= leftHandhold.minDriftSpeed)
                        {
                            OnPlayerGrabbed();
                            TransitionToState(CharacterState.Drifting);
                            _jumpConsumed = false;
                            Motor.ForceUnground();
                            return;
                        }
                        else
                        {
                            isGrabbingL = false;
                            LeftHandController.OpenHand();
                            Debug.Log("Not fast enough to drift! Need to be sliding faster.");
                            return;
                        }
                    }
                    else
                    {
                        isGrabbingL = false;
                        LeftHandController.OpenHand();
                        Debug.Log("Must be sliding to grab drift pole!");
                        return;
                    }
                }

                OnPlayerGrabbed();
                TransitionToState(CharacterState.Climbing);
                _jumpConsumed = false;
                Motor.ForceUnground();
            }
            else
            {
                if (DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal))
                {
                    isGrabbingL = true;

                    leftHandhold = null;

                    Vector3 handVisualPosition = grabPoint + grabNormal * handSurfaceOffset;
                    LeftHandController.StartGrab(handVisualPosition);
                    leftHandGrabAnchor = grabPoint;
                    climbNormal = grabNormal;

                    OnPlayerGrabbed();
                    TransitionToState(CharacterState.Climbing);
                    _jumpConsumed = false;
                    Motor.ForceUnground();
                }
                else
                {
                    // we hit nothing
                    LeftHandController.OpenHand();
                }
            }
        }

        void TryGrabR()
        {
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            // Check for fish first (use climbableLayer, not fishLayer!)
            if (Physics.SphereCast(origin, grabRadius, direction, out RaycastHit fishHit, grabDistance * 2, climbableLayer))
            {
                FishWrangler fish = fishHit.collider.GetComponent<FishWrangler>();
                if (fish != null)
                {
                    isGrabbingR = true;
                    OnPlayerGrabbed();
                    // NEW: Only set currentFish if not already set (left hand might have grabbed first)
                    if (currentFish == null)
                    {
                        currentFish = fish;
                    }

                    // NEW: Store the local-space offset from fish center to grab point
                    rightHandFishLocalOffset = fish.transform.InverseTransformPoint(fishHit.point);

                    // Set grab anchor to fish's current position
                    rightHandGrabAnchor = fish.transform.position;
                    rightHandhold = null; // Fish has no handhold component

                    Vector3 handVisualPosition = fishHit.point + fishHit.normal * handSurfaceOffset;
                    RightHandController.StartGrab(handVisualPosition);

                    // Transition to climbing state so player follows fish
                    TransitionToState(CharacterState.Climbing);
                    _jumpConsumed = false;
                    Motor.ForceUnground();

                    // NEW: Start minigame on FIRST hand grab (even if only one hand)
                    if (!fish.IsBeingWrangled())
                    {
                        fish.OnGrabbedByPlayer(this, isGrabbingL, isGrabbingR);
                    }

                    return;
                }
            }

            if (Physics.SphereCast(origin, grabRadius, direction, out rightHandHit, grabDistance, climbableLayer))
            {
                // Skip if this is a fish (already handled above)
                if (rightHandHit.collider.GetComponent<FishWrangler>() != null)
                {
                    return;
                }
                isGrabbingR = true;

                rightHandhold = rightHandHit.collider.GetComponent<Handhold>();

                Vector3 handVisualPosition = rightHandHit.point + rightHandHit.normal * handSurfaceOffset;
                RightHandController.StartGrab(rightHandHit.point);
                rightHandGrabAnchor = rightHandHit.point;

                if (rightHandhold != null && rightHandhold.handholdType == HandholdType.Drift)
                {
                    if (CurrentCharacterState == CharacterState.Sliding)
                    {
                        Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                        if (horizontalVel.magnitude >= rightHandhold.minDriftSpeed)
                        {
                            OnPlayerGrabbed();
                            TransitionToState(CharacterState.Drifting);
                            _jumpConsumed = false;
                            Motor.ForceUnground();
                            return;
                        }
                        else
                        {
                            isGrabbingR = false;
                            RightHandController.OpenHand();
                            Debug.Log("Not fast enough to drift! Need to be sliding faster.");
                            return;
                        }
                    }
                    else
                    {
                        isGrabbingR = false;
                        RightHandController.OpenHand();
                        Debug.Log("Must be sliding to grab drift pole!");
                        return;
                    }
                }

                OnPlayerGrabbed();
                TransitionToState(CharacterState.Climbing);
                _jumpConsumed = false;
                Motor.ForceUnground();
            }
            else
            {
                if (DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal))
                {
                    isGrabbingR = true;

                    rightHandhold = null;

                    Vector3 handVisualPosition = grabPoint + grabNormal * handSurfaceOffset;
                    RightHandController.StartGrab(handVisualPosition);
                    // do i need leftHandHit = hit??
                    rightHandGrabAnchor = grabPoint;
                    climbNormal = grabNormal;

                    OnPlayerGrabbed();
                    TransitionToState(CharacterState.Climbing);
                    _jumpConsumed = false;
                    Motor.ForceUnground();
                }
                else
                {
                    // we hit nothing
                    RightHandController.OpenHand();
                }
            }
        }

        [Header("Ledge Detection Settings")]
        public float forwardCheckDistance = 1.2f;
        public float ledgeHeight = 1.5f;
        public float ledgeDropDistance = 2.0f;

        private bool DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal)
        {
            grabPoint = Vector3.zero;
            grabNormal = Vector3.zero;

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            // Use Motor’s own capsule cast utilities to stay consistent with KCC physics
            if (Physics.SphereCast(origin, grabRadius, direction, out RaycastHit wallHit, forwardCheckDistance, wallLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 ledgeCheckStart = wallHit.point + (Vector3.up * ledgeHeight) - (wallHit.normal * 0.05f);

                if (Physics.Raycast(ledgeCheckStart, Vector3.down, out RaycastHit ledgeHit, ledgeDropDistance, wallLayer, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) > 0.7f)
                    {
                        grabPoint = ledgeHit.point;
                        grabNormal = wallHit.normal;
                        return true;
                    }
                }
            }

            return false;
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

        void OnDrawGizmos()
        {
            if (Motor == null) return;

            Vector3 origin = playerCamera.transform.position;
            Vector3 dir = playerCamera.transform.forward;

            // End point
            Vector3 end = origin + dir * grabDistance;

            Gizmos.color = Color.red;

            // Draw line
            Gizmos.DrawLine(origin, end);

            // Draw start and end spheres
            Gizmos.DrawWireSphere(origin, grabRadius);
            Gizmos.DrawWireSphere(end, grabRadius);

            // Draw wall run detection rays
            if (EnableWallRunning && Motor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(Motor.TransientPosition, Motor.CharacterRight * WallRunDetectionDistance);
                Gizmos.DrawRay(Motor.TransientPosition, -Motor.CharacterRight * WallRunDetectionDistance);
            }

            // Draw wall run direction when active
            if (CurrentCharacterState == CharacterState.WallRunning)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(Motor.TransientPosition, _wallRunDirection * 2f);
            }

            if (_hasMomentumToRestore && CurrentCharacterState == CharacterState.Climbing)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(Motor.TransientPosition, _storedHorizontalMomentum);
            }

            if (CurrentCharacterState == CharacterState.Drifting)
            {
                Gizmos.color = Color.yellow;
                DrawCircleGizmo(_driftPolePosition, _driftRadius);
                Gizmos.DrawLine(_driftPolePosition, Motor.TransientPosition);

                // NEW: Draw exit direction (where momentum will be redirected)
                Gizmos.color = Color.green;
                Vector3 exitDirection = Vector3.ProjectOnPlane(Motor.CharacterForward, Motor.CharacterUp).normalized;
                Gizmos.DrawRay(Motor.TransientPosition, exitDirection * _driftSpeed * _momentumRetention);
            }

            if (CurrentCharacterState == CharacterState.Sliding)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(Motor.TransientPosition, _slideDirection * 2f);
            }
        }

        private void DrawCircleGizmo(Vector3 center, float radius, int segments = 32)
        {
            float angleStep = 360f / segments;
            Vector3 prevPoint = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 newPoint = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(prevPoint, newPoint);
                prevPoint = newPoint;
            }
        }

        /// <summary>
        /// This is called every frame by the AI script in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref AICharacterInputs inputs)
        {
            _moveInputVector = inputs.MoveVector;
            _lookInputVector = inputs.LookVector;
        }

        private Quaternion _tmpTransientRot;

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            if (_interactRequestedL)
            {
                Interact(false);
            }
            if (_interactRequestedR)
            {
                Interact(true);
            }

            if (wantsGrabL && !isGrabbingL)
            {
                TryGrabL();
            }

            if (wantsGrabR && !isGrabbingR)
            {
                TryGrabR();
            }

            if (!wantsGrabL)
            {
                isGrabbingL = false;
                LeftHandController.StopGrab();
            }

            if (!wantsGrabR)
            {
                isGrabbingR = false;
                RightHandController.StopGrab();
            }

            if (!wantsGrabL || !wantsGrabR)
            {
                if (currentFish != null && currentFish.IsBeingWrangled())
                {
                    // Check if BOTH hands released (complete release)
                    if (!isGrabbingL && !isGrabbingR)
                    {
                        currentFish.OnReleased();
                        currentFish = null;
                    }
                }
            }

            if (!isGrabbingL && !isGrabbingR && CurrentCharacterState == CharacterState.Climbing)
            {
                TransitionToState(CharacterState.Default);
            }

            if (!isGrabbingL && !isGrabbingR && CurrentCharacterState == CharacterState.Drifting)
            {
                TransitionToState(CharacterState.Default);
            }

            float slideMinSpeed = 1f;
            // Only slide when grounded and moving
            if (_isCrouching && Motor.GroundingStatus.IsStableOnGround)
            {
                float speed = Motor.BaseVelocity.magnitude;

                if (speed > slideMinSpeed && CurrentCharacterState != CharacterState.Sliding)
                {
                    StartSlide();
                }
            }

            // Wall run detection
            if (EnableWallRunning && CurrentCharacterState == CharacterState.Default && !Motor.GroundingStatus.IsStableOnGround && _canWallRun)
            {
                Vector3 horizontalVel = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                if (horizontalVel.magnitude >= MinSpeedForWallRun)
                {
                    if (DetectWallForRunning(out Vector3 wallNormal, out bool isLeftWall))
                    {
                        StartWallRun(wallNormal, isLeftWall);
                    }
                }
            }

        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            // Smoothly interpolate from current to target look direction
                            // Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                            // Set the current rotation (which will be used by the KinematicCharacterMotor)
                            // currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                            currentRotation = Quaternion.LookRotation(_lookInputVector, Motor.CharacterUp);
                        }

                        Vector3 currentUp = (currentRotation * Vector3.up);
                        if (BonusOrientationMethod == BonusOrientationMethod.TowardsGravity)
                        {
                            // Rotate from current up to invert gravity
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -playerCharacter.CurrentStats.gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        else if (BonusOrientationMethod == BonusOrientationMethod.TowardsGroundSlopeAndGravity)
                        {
                            if (Motor.GroundingStatus.IsStableOnGround)
                            {
                                Vector3 initialCharacterBottomHemiCenter = Motor.TransientPosition + (currentUp * Motor.Capsule.radius);

                                Vector3 smoothedGroundNormal = Vector3.Slerp(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGroundNormal) * currentRotation;

                                // Move the position to create a rotation around the bottom hemi center instead of around the pivot
                                Motor.SetTransientPosition(initialCharacterBottomHemiCenter + (currentRotation * Vector3.down * Motor.Capsule.radius));
                            }
                            else
                            {
                                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -playerCharacter.CurrentStats.gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                                currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                            }
                        }
                        else
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        break;
                    }

                case CharacterState.WallRunning:
                    {
                        // During wall running, orient towards wall run direction, not camera
                        if (_wallRunDirection.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            currentRotation = Quaternion.LookRotation(_wallRunDirection, Motor.CharacterUp);
                        }

                        Vector3 currentUp = (currentRotation * Vector3.up);
                        if (BonusOrientationMethod == BonusOrientationMethod.TowardsGravity)
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -playerCharacter.CurrentStats.gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        else
                        {
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, Vector3.up, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
                            currentRotation = Quaternion.FromToRotation(currentUp, smoothedGravityDir) * currentRotation;
                        }
                        break;
                    }


        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
                case CharacterState.Sliding:
                    {
                        if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            currentRotation = Quaternion.Slerp(currentRotation, Quaternion.LookRotation(_lookInputVector, Motor.CharacterUp), 1f - Mathf.Exp(-OrientationSharpness * deltaTime));
                        }
                        break;
                    }

                case CharacterState.Drifting:
                    {
                        // NEW: Allow player to rotate freely during drift (camera controls facing)
                        if (_lookInputVector.sqrMagnitude > 0f && OrientationSharpness > 0f)
                        {
                            currentRotation = Quaternion.Slerp(currentRotation, Quaternion.LookRotation(_lookInputVector, Motor.CharacterUp), 1f - Mathf.Exp(-OrientationSharpness * deltaTime));
                        }
                        break;
                    }
            }
        }

        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;

        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Ground movement
                        // Handle dash request (works in air and on ground)
                        if (_dashRequested)
                        {
                            PerformDash(ref currentVelocity, false);
                        }

                        // Check if dash duration ended
                        if (_isDashing && Time.time >= _dashEndTime)
                        {
                            _isDashing = false;
                            Debug.Log("Dash ended");
                        }
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            float currentVelocityMagnitude = currentVelocity.magnitude;

                            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                            // Reorient velocity on slope
                            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                            // Calculate target velocity
                            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;

                            float speed = _sprintPressed ? walkSpeed : sprintSpeed;

                            // Dynamic FOV based on actual speed for visual feedback
                            float targetFOV = 75f + Mathf.Clamp((currentSpeed - 5f) * 2f, 0f, 20f);
                            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * 5f);

                            Vector3 targetMovementVelocity = reorientedInput * speed;

                            // pre sprint
                            // this will depricate maxstablemovespeed
                            //Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

                            // Smooth movement Velocity
                            currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity, 1f - Mathf.Exp(-StableMovementSharpness * deltaTime));
                        }
                        // Air movement
                        else
                        {
                            // Use modified air acceleration if bouncing
                            float currentAirAcceleration = _isBouncing
                                ? _normalAirAcceleration * BounceAirAccelerationMultiplier
                                : AirAccelerationSpeed;

                            // Enhanced air strafing
                            if (_moveInputVector.sqrMagnitude > 0f)
                            {
                                Vector3 addedVelocity = _moveInputVector * currentAirAcceleration * AirStrafeMultiplier * deltaTime;

                                Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // Limit air velocity from inputs
                                if (currentVelocityOnInputsPlane.magnitude < MaxAirMoveSpeed)
                                {
                                    // clamp addedVel to make total vel not exceed max vel on inputs plane
                                    Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, MaxAirMoveSpeed);
                                    addedVelocity = newTotal - currentVelocityOnInputsPlane;
                                }
                                else
                                {
                                    // Make sure added vel doesn't go in the direction of the already-exceeding velocity
                                    if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                                    {
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                                    }
                                }

                                // Prevent air-climbing sloped walls
                                if (Motor.GroundingStatus.FoundAnyGround)
                                {
                                    if (Vector3.Dot(currentVelocity + addedVelocity, addedVelocity) > 0f)
                                    {
                                        Vector3 perpenticularObstructionNormal = Vector3.Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal), Motor.CharacterUp).normalized;
                                        addedVelocity = Vector3.ProjectOnPlane(addedVelocity, perpenticularObstructionNormal);
                                    }
                                }

                                // Apply added velocity
                                currentVelocity += addedVelocity;
                            }

                            // Gravity
                            currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;

                            // Drag
                            currentVelocity *= (1f / (1f + (Drag * deltaTime)));
                        }

                        // Handle jumping with CONSISTENT HEIGHT
                        _jumpedThisFrame = false;
                        _timeSinceJumpRequested += deltaTime;
                        if (_jumpRequested)
                        {
                            // See if we actually are allowed to jump
                            if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                            {
                                // Calculate jump direction before ungrounding
                                Vector3 jumpDirection = Motor.CharacterUp;
                                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                {
                                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                                }

                                // Makes the character skip ground probing/snapping on its next update. 
                                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                                Motor.ForceUnground();

                                // CONSISTENT JUMP HEIGHT: Zero out vertical component, preserve horizontal
                                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // Set new velocity: preserved horizontal + FIXED vertical jump speed
                                currentVelocity = horizontalVelocity + (jumpDirection * JumpUpSpeed);

                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;
                            }
                        }

                        // Take into account additive velocity
                        if (_internalVelocityAdd.sqrMagnitude > 0f)
                        {
                            currentVelocity += _internalVelocityAdd;
                            _internalVelocityAdd = Vector3.zero;
                        }
                        break;
                    }

                case CharacterState.Sliding:
                    {
                        if (_dashRequested)
                        {
                            PerformDash(ref currentVelocity, true);
                        }

                        HandleSliding(ref currentVelocity, deltaTime);
                        HandleSliding(ref currentVelocity, deltaTime);

                        _jumpedThisFrame = false;
                        _timeSinceJumpRequested += deltaTime;

                        if (_jumpRequested)
                        {
                            // See if we actually are allowed to jump
                            if (!_jumpConsumed && ((AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround) || _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime))
                            {
                                // Calculate jump direction before ungrounding
                                Vector3 jumpDirection = Motor.CharacterUp;
                                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                {
                                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                                }

                                // Makes the character skip ground probing/snapping on its next update. 
                                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                                Motor.ForceUnground();

                                // SLIDE JUMP: Preserve horizontal momentum, CONSISTENT vertical jump (NO BOOST)
                                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // Set velocity: preserved horizontal + FIXED vertical (consistent height)
                                currentVelocity = horizontalVelocity + (jumpDirection * JumpUpSpeed);

                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;
                            }
                        }

                        // Apply gravity after jump logic (since we may now be airborne)
                        if (!Motor.GroundingStatus.IsStableOnGround)
                        {
                            StopSlide();
                        }
                        break;
                    }

                case CharacterState.Climbing:
                    {
                        HandleClimbVelocity(ref currentVelocity, deltaTime);
                        if (_jumpRequested)
                        {
                            // See if we actually are allowed to jump
                            if (!_jumpConsumed)
                            {
                                // Calculate jump direction before ungrounding
                                Vector3 jumpDirection = Motor.CharacterUp;
                                if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                                {
                                    jumpDirection = Motor.GroundingStatus.GroundNormal;
                                }

                                // Makes the character skip ground probing/snapping on its next update. 
                                // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                                Motor.ForceUnground();

                                // CLIMB JUMP: Consistent upward velocity
                                currentVelocity = (jumpDirection * JumpUpSpeed) + (_moveInputVector * JumpScalableForwardSpeed);

                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;

                                TransitionToState(CharacterState.Default);
                                isGrabbingL = false;
                                isGrabbingR = false;
                                LeftHandController.StopGrab();
                                RightHandController.StopGrab();
                                // MAKE A TIMER WHERE YOU CANT GRAB FOR A LITTLE WHEN JUMPING
                            }
                        }
                        break;
                    }

                case CharacterState.Drifting:
                    {
                        HandleDriftVelocity(ref currentVelocity, deltaTime);

                        if (_jumpRequested)
                        {
                            if (!_jumpConsumed)
                            {
                                Motor.ForceUnground();

                                // NEW: Redirect momentum in facing direction on jump
                                Vector3 facingDirection = Vector3.ProjectOnPlane(Motor.CharacterForward, Motor.CharacterUp).normalized;
                                float exitSpeed = Mathf.Max(_driftSpeed, _driftMinMomentum) * _momentumRetention;

                                currentVelocity = (facingDirection * exitSpeed) + (Motor.CharacterUp * JumpUpSpeed);

                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;

                                Debug.Log($"Drift jump! Speed: {exitSpeed:F2} m/s, Direction: {facingDirection}");

                                TransitionToState(CharacterState.Default);
                                isGrabbingL = false;
                                isGrabbingR = false;
                                LeftHandController.StopGrab();
                                RightHandController.StopGrab();
                            }
                        }
                        break;
                    }

                case CharacterState.WallRunning:
                    {
                        HandleWallRunning(ref currentVelocity, deltaTime);

                        // Wall run jump
                        if (_jumpRequested)
                        {
                            if (!_jumpConsumed)
                            {
                                Motor.ForceUnground();

                                // WALL JUMP: Preserve ALL momentum and add boost
                                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(currentVelocity, Motor.CharacterUp);

                                // Apply momentum boost to horizontal velocity
                                horizontalVelocity *= WallRunJumpMomentumBoost;

                                // Jump away from wall with fixed upward component
                                Vector3 wallJumpVelocity = (_wallRunNormal * WallRunJumpAwayForce) + (Motor.CharacterUp * WallRunJumpHeight);

                                currentVelocity = horizontalVelocity + wallJumpVelocity;

                                _jumpRequested = false;
                                _jumpConsumed = true;
                                _jumpedThisFrame = true;

                                TransitionToState(CharacterState.Default);
                            }
                        }
                        break;
                    }
            }
        }

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

        void HandleClimbVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            bool hasLeftMomentum = leftHandhold != null && leftHandhold.handholdType == HandholdType.Momentum;
            bool hasRightMomentum = rightHandhold != null && rightHandhold.handholdType == HandholdType.Momentum;

            if (hasLeftMomentum || hasRightMomentum)
            {
                HandleMomentumSwingVelocity(ref currentVelocity, deltaTime);
            }
            else
            {
                HandleStaticClimbVelocity(ref currentVelocity, deltaTime);
            }
        }

        /// <summary>
        /// REWORKED: Drift pole physics - maintains minimum momentum, redirects on exit
        /// </summary>
        private void HandleDriftVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Handhold driftHandhold = (leftHandhold != null && leftHandhold.handholdType == HandholdType.Drift)
                ? leftHandhold
                : rightHandhold;

            if (driftHandhold == null)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

            // Calculate position relative to pole (horizontal plane only)
            Vector3 horizontalPosition = new Vector3(Motor.TransientPosition.x, _driftPolePosition.y, Motor.TransientPosition.z);
            Vector3 poleCenter = new Vector3(_driftPolePosition.x, _driftPolePosition.y, _driftPolePosition.z);
            Vector3 toPlayer = horizontalPosition - poleCenter;
            float currentDistance = toPlayer.magnitude;

            if (currentDistance < 0.01f)
            {
                TransitionToState(CharacterState.Default);
                return;
            }

        /// SORT THIS OUT FUCKIN MESS SECTION
        /// 

            Vector3 radialDirection = toPlayer / currentDistance;
            Vector3 tangentDirection = Vector3.Cross(Vector3.up, radialDirection).normalized;

            // Decompose velocity into radial and tangential components (horizontal plane only)
            Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            float radialVelocity = Vector3.Dot(horizontalVelocity, radialDirection);
            float tangentialVelocity = Vector3.Dot(horizontalVelocity, tangentDirection);

            // CONSTRAIN TO DRIFT RADIUS using spring force
            float radiusError = currentDistance - _driftRadius;
            float radialForce = -radiusError * driftHandhold.driftRadiusSpring;

            // Damp radial velocity to prevent oscillation
            radialForce -= radialVelocity * (driftHandhold.driftRadiusSpring * 0.5f);

            // Apply radial force
            radialVelocity += radialForce * deltaTime;

            // HARD CLAMP: Kill outward velocity if exceeding radius significantly
            if (currentDistance > _driftRadius * 1.1f && radialVelocity > 0f)
            {
                radialVelocity = 0f;
            }

            // NEW: MAINTAIN MINIMUM MOMENTUM during drift
            float currentTangentialSpeed = Mathf.Abs(tangentialVelocity);
            if (currentTangentialSpeed < _driftMinMomentum)
            {
                // Boost tangential velocity to maintain minimum speed
                float sign = Mathf.Sign(tangentialVelocity);
                if (sign == 0f) sign = 1f; // Default to positive if no velocity
                tangentialVelocity = sign * _driftMinMomentum;
            }
            else
            {
                // Apply minimal damping only if above minimum
                tangentialVelocity *= Mathf.Pow(1f - driftHandhold.driftDamping, deltaTime);
            }

            // Update stored drift speed for exit
            _driftSpeed = Mathf.Abs(tangentialVelocity);

            // Reconstruct horizontal velocity
            horizontalVelocity = (radialDirection * radialVelocity) + (tangentDirection * tangentialVelocity);

            // Update velocity (preserve vertical component)
            currentVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);

            // Apply gravity (full gravity for natural pendulum feel)
            currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;

            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        private void HandleMomentumSwingVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 input = _moveInputVector;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input * climbMoveSpeed;
            if (isGrabbingL && isGrabbingR)
            {
                inputMove *= 1.25f;
            }
            currentVelocity += inputMove;

            Handhold activeHandhold = leftHandhold != null && leftHandhold.handholdType == HandholdType.Momentum
                ? leftHandhold
                : rightHandhold;

            if (activeHandhold != null)
            {
                float swingDamping = activeHandhold.swingDamping;

                if (isGrabbingL && leftHandhold != null && leftHandhold.handholdType == HandholdType.Momentum)
                {
                    Vector3 toAnchor = Motor.TransientPosition - leftHandGrabAnchor;
                    float dist = toAnchor.magnitude;

                    if (dist > leftHandhold.maxSwingRadius)
                    {
                        Vector3 dir = toAnchor.normalized;

                        float radialVelocity = Vector3.Dot(currentVelocity, dir);
                        if (radialVelocity > 0f)
                        {
                            currentVelocity -= dir * radialVelocity;

                            currentVelocity *= (1f / (1f + (5f * deltaTime)));
                        }

                        float overshoot = dist - leftHandhold.maxSwingRadius;
                        currentVelocity -= dir * overshoot * 50f * deltaTime;
                    }
                }

                if (isGrabbingR && rightHandhold != null && rightHandhold.handholdType == HandholdType.Momentum)
                {
                    Vector3 toAnchor = Motor.TransientPosition - rightHandGrabAnchor;
                    float dist = toAnchor.magnitude;

                    if (dist > rightHandhold.maxSwingRadius)
                    {
                        Vector3 dir = toAnchor.normalized;

                        float radialVelocity = Vector3.Dot(currentVelocity, dir);
                        if (radialVelocity > 0f)
                        {
                            currentVelocity -= dir * radialVelocity;

                            currentVelocity *= (1f / (1f + (5f * deltaTime)));
                        }

                        float overshoot = dist - rightHandhold.maxSwingRadius;
                        currentVelocity -= dir * overshoot * 50f * deltaTime;
                    }
                }

                currentVelocity *= (1f / (1f + (swingDamping * deltaTime)));
            }

            currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;

            if (input.sqrMagnitude > 0f)
            {
                _hasMovedWhileClimbing = true;
            }

            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        private void HandleStaticClimbVelocity(ref Vector3 currentVelocity, float deltaTime)
        {

            // ----------------------
            // 2. Free WSAD movement (camera-relative)
            // ----------------------
            Vector3 input = _moveInputVector;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input;
            inputMove *= climbMoveSpeed * ((isGrabbingL && isGrabbingR) ? 1.25f : 1f);

            currentVelocity += inputMove;

            float outerRadius = maxStaticClimbRadius;

            // NEW: If grabbing a fish, update anchors using stored local offsets
            if (currentFish != null && currentFish.IsBeingWrangled())
            {
                // Convert local offsets back to world space (accounts for fish rotation & position)
                if (isGrabbingL)
                {
                    leftHandGrabAnchor = currentFish.transform.TransformPoint(leftHandFishLocalOffset);
                    LeftHandController.StartGrab(leftHandGrabAnchor);
                }
                if (isGrabbingR)
                {
                    rightHandGrabAnchor = currentFish.transform.TransformPoint(rightHandFishLocalOffset);
                    RightHandController.StartGrab(rightHandGrabAnchor);
                }

                // Position player behind fish
                Vector3 targetPosition = currentFish.transform.position - currentFish.transform.forward * 0.5f;

                // Smoothly move player to that position
                Vector3 toTarget = targetPosition - Motor.TransientPosition;
                currentVelocity = toTarget / deltaTime;

                // Don't apply gravity or normal climbing physics when on fish
                return;
            }

            if (isGrabbingL)
            {
                Vector3 toAnchorLeft = Motor.TransientPosition - leftHandGrabAnchor;
                float distLeft = toAnchorLeft.magnitude;

                if (distLeft > outerRadius)
                {
                    Vector3 dir = toAnchorLeft.normalized;

                    /*
                    // 1️⃣ Clamp position at the boundary
                    Vector3 targetPos = leftHandGrabAnchor + dir * outerRadius;
                    currentVelocity += (targetPos - Motor.TransientPosition);

                    // 2️⃣ Kill any velocity along that direction (heavy damping)
                    Vector3 alongDir = Vector3.Project(currentVelocity, dir);
                    currentVelocity -= alongDir;
                    */

                    // 3️⃣ Optional: add extra force opposite to overshoot if still moving out
                    float overshootSpeed = Vector3.Dot(currentVelocity, dir);
                    if (overshootSpeed > 0f)
                    {
                        currentVelocity -= dir * overshootSpeed * 1.3f; // multiplier can be tuned
                        // add aditional drag
                        currentVelocity *= (1f / (1f + (2f * 2f * deltaTime)));
                    }
                }
            }

            if (isGrabbingR)
            {
                Vector3 toAnchorRight = Motor.TransientPosition - rightHandGrabAnchor;
                float distRight = toAnchorRight.magnitude;
                if (distRight > outerRadius)
                {
                    Vector3 dir = toAnchorRight.normalized;

                    float overshootSpeed = Vector3.Dot(currentVelocity, dir);
                    if (overshootSpeed > 0f)
                    {
                        currentVelocity -= dir * overshootSpeed * 1.3f; // multiplier can be tuned

                        // add aditional drag
                        currentVelocity *= (1f / (1f + (2f * 2f * deltaTime)));
                    }
                }
            }

            if (input.sqrMagnitude > 0f)
            {
                _hasMovedWhileClimbing = true;

                /*
                float horizontalSpeed = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
                if (horizontalSpeed > maxClimbSpeed)
                {
                    Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z).normalized * maxClimbSpeed;
                    currentVelocity = new Vector3(horizontalVelocity.x, currentVelocity.y, horizontalVelocity.z);
                }
                */

                // redundant
                /*
                float verticalSpeed = new Vector3(0f, currentVelocity.y, 0f).magnitude;
                if (verticalSpeed > maxClimbSpeed)
                {
                    Vector3 verticalVelocity = new Vector3(0f, currentVelocity.y, 0f).normalized * maxClimbSpeed;
                    currentVelocity = new Vector3(currentVelocity.x, verticalVelocity.y, currentVelocity.z);
                }
                */
            }
            else
            {
                currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;
            }

            if (_hasMovedWhileClimbing && input.sqrMagnitude == 0f)
            {
                // ADD IF INPUT HAS BEEN PRESSED AND RESET THE FLAG
                // Limit fall speed after input movement
                float maxFallSpeed = -0.5f; // Negative value for downward velocity
                if (currentVelocity.y < maxFallSpeed)
                {
                    currentVelocity.y = maxFallSpeed;
                }
            }

            // Drag
            float dragMultiplier = input.sqrMagnitude > 0f ? 2f : 20f;
            currentVelocity *= (1f / (1f + (Drag * dragMultiplier * deltaTime)));


            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }





        /// END OF SORT THIS OUT FUCKIN MESS SECTION





        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Handle jump-related values
                        {
                            // Handle jumping pre-ground grace period
                            if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                            {
                                _jumpRequested = false;
                            }

                            if (AllowJumpingWhenSliding ? Motor.GroundingStatus.FoundAnyGround : Motor.GroundingStatus.IsStableOnGround)
                            {
                                // If we're on a ground surface, reset jumping values
                                if (!_jumpedThisFrame)
                                {
                                    _jumpConsumed = false;
                                }
                                _timeSinceLastAbleToJump = 0f;
                            }
                            else
                            {
                                // Keep track of time since we were last able to jump (for grace period)
                                _timeSinceLastAbleToJump += deltaTime;
                            }
                        }

                        // Handle uncrouching
                        if (_isCrouching && !_shouldBeCrouching)
                        {
                            // Do an overlap test with the character's standing height to see if there are any obstructions
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                Motor.TransientPosition,
                                Motor.TransientRotation,
                                _probedColliders,
                                Motor.CollidableLayers,
                                QueryTriggerInteraction.Ignore) > 0)
                            {
                                // If obstructions, just stick to crouching dimensions
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                            }
                            else
                            {
                                // If no obstructions, uncrouch
                                MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                                _isCrouching = false;
                            }
                        }
                        break;
                    }
                case CharacterState.Sliding:
                    {
                        // Handle uncrouching
                        if (_isCrouching && !_shouldBeCrouching)
                        {
                            // Do an overlap test with the character's standing height to see if there are any obstructions
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                Motor.TransientPosition,
                                Motor.TransientRotation,
                                _probedColliders,
                                Motor.CollidableLayers,
                                QueryTriggerInteraction.Ignore) > 0)
                            {
                                // If obstructions, just stick to crouching dimensions
                                Motor.SetCapsuleDimensions(0.5f, CrouchedCapsuleHeight, CrouchedCapsuleHeight * 0.5f);
                            }
                            else
                            {
                                // If no obstructions, uncrouch
                                MeshRoot.localScale = new Vector3(1f, 1f, 1f);
                                _isCrouching = false;
                            }
                        }

                        if (!_isCrouching)
                        {
                            StopSlide();
                        }
                        break;
                    }
            }
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // Handle landing and leaving ground
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLanded();
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
            }

            // Store velocity while airborne for bounce calculation
            if (!Motor.GroundingStatus.IsStableOnGround)
            {
                _velocityBeforeLanding = Motor.BaseVelocity;
            }
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            if (IgnoredColliders.Count == 0)
            {
                return true;
            }

            if (IgnoredColliders.Contains(coll))
            {
                return false;
            }

            return true;
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void AddVelocity(Vector3 velocity)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        _internalVelocityAdd += velocity;
                        break;
                    }
            }
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        protected void OnLanded()
        {
            // MOMENTUM PRESERVATION ON LANDING
            _isDashing = false;

            Vector3 horizontalVelocity = new Vector3(_velocityBeforeLanding.x, 0f, _velocityBeforeLanding.z);
            float landingSpeed = horizontalVelocity.magnitude;

            // Check if player should bounce on landing
            if (EnableBounce && _jumpHeld && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                // Get the surface normal we landed on
                Vector3 bounceNormal = Motor.GroundingStatus.GroundNormal;

                // Calculate the speed at which player was moving toward the surface
                // Project velocity onto the inverse of the surface normal (downward component relative to surface)
                float impactSpeed = Mathf.Abs(Vector3.Dot(_velocityBeforeLanding, -bounceNormal));

                // Scale bounce speed based on impact speed
                float scaledBounceSpeed = impactSpeed * BounceSpeedMultiplier;

                // Clamp to min/max values
                scaledBounceSpeed = Mathf.Clamp(scaledBounceSpeed, MinBounceSpeed, MaxBounceSpeed);

                // Build momentum based on the size of the bounce
                // AddMomentumFromBounce(scaledBounceSpeed);

                Vector3 bounceVelocity = bounceNormal * scaledBounceSpeed;

                // Get the tangential (parallel to surface) component of velocity to preserve
                Vector3 tangentialVelocity = Vector3.ProjectOnPlane(_velocityBeforeLanding, bounceNormal);

                // Combine bounce with preserved tangential velocity
                Vector3 finalBounceVelocity = bounceVelocity + (tangentialVelocity * BounceVelocityRedirection);

                // CRITICAL: Set velocity directly by canceling current velocity and applying new one
                // Motor.BaseVelocity is the current velocity at landing (should be ~0 after ground snap)
                _internalVelocityAdd = finalBounceVelocity - Motor.BaseVelocity;

                // Set bouncing state to reduce air control
                _isBouncing = true;

                // Force unground to allow the bounce to take effect
                Motor.ForceUnground();
            }
            else
            {
                // MOMENTUM LANDING: Preserve horizontal speed for flow
                if (landingSpeed > sprintSpeed)
                {
                    _internalVelocityAdd = horizontalVelocity * speedRetentionOnLanding;
                }

                _isBouncing = false;
            }
        }

        // Add these new methods after OnLanded (around line 1775):

        protected void OnLeaveStableGround()
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }
}