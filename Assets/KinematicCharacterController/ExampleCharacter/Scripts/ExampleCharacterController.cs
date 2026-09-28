using System.Collections.Generic;
using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public enum CharacterState
    {
        Default,
        Climbing,
        Sliding,
        WallRunning
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

    public partial class ExampleCharacterController : MonoBehaviour, ICharacterController
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

        public bool _hasMovedWhileClimbing = false;

        public MemoryType defaultMemoryType;

        public LayerMask climbableLayer;
        public LayerMask wallLayer;
        public LayerMask interactableLayer;

        // Momentum preservation system
        private Vector3 _storedHorizontalMomentum;
        private float _momentumGrabTime;
        private float _momentumGraceWindow;
        private float _momentumRetention;
        private bool _hasMomentumToRestore;

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


       // Should this be moved to the momentum tank system/Dash class?
       // keep the hook but make it call a seperate function
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

                    // this used to have drifting and climbing in the same line - recheck if things are funky
                case CharacterState.Climbing:
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