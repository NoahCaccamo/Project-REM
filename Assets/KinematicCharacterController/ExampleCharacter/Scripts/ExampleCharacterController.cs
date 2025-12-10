using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using UnityEngine.Windows;
using UnityEngine.UIElements;
using static UnityEngine.UI.Image;
using Unity.Burst.CompilerServices;

namespace KinematicCharacterController.Examples
{
    public enum CharacterState
    {
        Default,
        Climbing,
        Sliding
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

        [Header("Stable Movement")]
        public float MaxStableMoveSpeed = 10f;
        public float StableMovementSharpness = 15f;
        public float OrientationSharpness = 10f;
        public OrientationMethod OrientationMethod = OrientationMethod.TowardsCamera;

        [Header("Air Movement")]
        public float MaxAirMoveSpeed = 15f;
        public float AirAccelerationSpeed = 15f;
        public float Drag = 0.1f;

        [Header("Jumping")]
        public bool AllowJumpingWhenSliding = false;
        public float JumpUpSpeed = 10f;
        public float JumpScalableForwardSpeed = 10f;
        public float JumpPreGroundingGraceTime = 0f;
        public float JumpPostGroundingGraceTime = 0f;

        [Header("Hands")]
        public float grabRadius = 0.1f;
        public float grabDistance = 0.2f;

        // Climb Variables
        public float idealRadius = 0.8f; // distance hand-to-body should stay
        public float springStrength = 20f;
        public float springDamping = 5f;
        public float climbMoveSpeed = 1f;
        public bool _hasMovedWhileClimbing = false;


        public MemoryType defaultMemoryType;

        // This should change later to a check?
        public LayerMask climbableLayer;
        public LayerMask wallLayer;
        public LayerMask interactableLayer;

        private RaycastHit leftHandHit;
        private Vector3 leftHandGrabAnchor;

        private RaycastHit rightHandHit;
        private Vector3 rightHandGrabAnchor;

        private bool wantsGrabL = false;
        private bool isGrabbingL = false;
        private bool grabLDown = false;

        private bool wantsGrabR = false;
        private bool isGrabbingR = false;
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

        private Vector3 lastInnerNormal = Vector3.zero;
        private Vector3 lastOuterNormal = Vector3.zero;

        private void Awake()
        {
            // Handle initial state
            TransitionToState(CharacterState.Default);

            // Assign the characterController to the motor
            Motor.CharacterController = this;

            // uneeded if we set in inspector
            playerCamera = Camera.main;

            playerCharacter = GetComponent<PlayerCharacter>();
        }

        /// <summary>
        /// Handles movement state transitions and enter/exit callbacks
        /// </summary>
        public void TransitionToState(CharacterState newState)
        {
            // i think this fixes the grab register thing
            if (newState == CurrentCharacterState)
            {
                return;
            }
            CharacterState tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        /// <summary>
        /// Event when entering a state
        /// </summary>
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
                        // is this breaking when tapping one hand but holding the other?
                        _hasMovedWhileClimbing = false;
                        break;
                    }
            }
        }

        /// <summary>
        /// Event when exiting a state
        /// </summary>
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
                        // Move and look inputs
                      //  _moveInputVector = cameraPlanarRotation * moveInputVector;

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

            // D
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
                } else
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



        // READ THROUGH THIS BEFORE IMPLEMENTING
        void TryGrab(bool isLeftHand)
        {
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            // Select the appropriate hand data based on parameter
            HandController handController = isLeftHand ? LeftHandController : RightHandController;
            RaycastHit hit;

            if (Physics.SphereCast(origin, grabRadius, direction, out hit, grabDistance, climbableLayer))
            {
                // Set the grabbing state
                if (isLeftHand)
                {
                    isGrabbingL = true;
                    leftHandHit = hit;
                    leftHandGrabAnchor = hit.point;
                }
                else
                {
                    isGrabbingR = true;
                    rightHandHit = hit;
                    rightHandGrabAnchor = hit.point;
                }

                // Visual hand position with offset
                Vector3 handVisualPosition = hit.point + hit.normal * handSurfaceOffset;
                handController.StartGrab(handVisualPosition);

                climbNormal = hit.normal;

                TransitionToState(CharacterState.Climbing);
                _jumpConsumed = false;
                Motor.ForceUnground();
            }
            else
            {
                // Try ledge detection as fallback
                if (DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal))
                {
                    // Set the grabbing state
                    if (isLeftHand)
                    {
                        isGrabbingL = true;
                        leftHandGrabAnchor = grabPoint;
                    }
                    else
                    {
                        isGrabbingR = true;
                        rightHandGrabAnchor = grabPoint;
                    }

                    Vector3 handVisualPosition = grabPoint + grabNormal * handSurfaceOffset;
                    handController.StartGrab(handVisualPosition);

                    climbNormal = grabNormal;

                    TransitionToState(CharacterState.Climbing);
                    _jumpConsumed = false;
                    Motor.ForceUnground();
                }
                else
                {
                    // No valid grab point found
                    handController.OpenHand();
                }
            }
        }
        void TryGrabL()
        {
            // unsure where character position should be from
            // Is it initial or after sim or other???
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

           // DetectLedge(out origin, out direction);
            
            if (Physics.SphereCast(origin, grabRadius, direction, out leftHandHit, grabDistance, climbableLayer))
            {
                isGrabbingL = true;

                // offset visual position slightly away from surface along the normal
                Vector3 handVisualPosition = leftHandHit.point + leftHandHit.normal * handSurfaceOffset;
                LeftHandController.StartGrab(handVisualPosition);
                // do i need leftHandHit = hit??
                leftHandGrabAnchor = leftHandHit.point;
                climbNormal = leftHandHit.normal;

                TransitionToState(CharacterState.Climbing);
                _jumpConsumed = false;
                Motor.ForceUnground();
            } else
            {
                if (DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal))
                {
                    isGrabbingL = true;

                    Vector3 handVisualPosition = grabPoint + grabNormal * handSurfaceOffset;
                    LeftHandController.StartGrab(handVisualPosition);
                    // do i need leftHandHit = hit??
                    leftHandGrabAnchor = grabPoint;
                    climbNormal = grabNormal;

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


            if (Physics.SphereCast(origin, grabRadius, direction, out rightHandHit, grabDistance, climbableLayer))
            {
                isGrabbingR = true;

                Vector3 handVisualPosition = rightHandHit.point + rightHandHit.normal * handSurfaceOffset;

                RightHandController.StartGrab(rightHandHit.point);

                rightHandGrabAnchor = rightHandHit.point;

                TransitionToState(CharacterState.Climbing);
                _jumpConsumed = false;
                Motor.ForceUnground();
            } else
            {
                if (DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal))
                {
                    isGrabbingR = true;

                    Vector3 handVisualPosition = grabPoint + grabNormal * handSurfaceOffset;
                    RightHandController.StartGrab(handVisualPosition);
                    // do i need leftHandHit = hit??
                    rightHandGrabAnchor = grabPoint;
                    climbNormal = grabNormal;

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

            Vector3 origin = playerCamera.transform.position;//Motor.TransientPosition + Vector3.up * 1.2f; // chest height
            Vector3 direction = playerCamera.transform.forward;

            // Use Motor’s own capsule cast utilities to stay consistent with KCC physics
            if (Physics.SphereCast(origin, grabRadius, direction, out RaycastHit wallHit, forwardCheckDistance, wallLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 ledgeCheckStart = wallHit.point + (Vector3.up * ledgeHeight) - (wallHit.normal * 0.05f);

                if (Physics.Raycast(ledgeCheckStart, Vector3.down, out RaycastHit ledgeHit, ledgeDropDistance, wallLayer, QueryTriggerInteraction.Ignore))
                {
                    if (Vector3.Dot(ledgeHit.normal, Vector3.up) > 0.7f)
                    {
                        Vector3 clearanceCenter = ledgeHit.point - wallHit.normal * 0.3f;
                        Debug.DrawLine(origin, wallHit.point, Color.red);
                        Debug.DrawLine(ledgeCheckStart, ledgeHit.point, Color.green);
                        Debug.DrawRay(grabPoint, grabNormal, Color.cyan);

                        // optional: check if capsule space is clear
                        //if (!Physics.CheckCapsule(clearanceCenter, clearanceCenter + Vector3.up * 1.2f, grabRadius * 0.8f))
                        {
                            grabPoint = ledgeHit.point;
                            grabNormal = wallHit.normal;

                           // if (true)// if (debugDraw)
                            {
                                Debug.DrawLine(origin, wallHit.point, Color.red);
                                Debug.DrawLine(ledgeCheckStart, ledgeHit.point, Color.green);
                                Debug.DrawRay(grabPoint, grabNormal, Color.cyan);
                            }

                            return true;
                        }
                    }
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


            /*
            // If we had a hit, draw that too
            if (lastHit.collider != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(lastHit.point, grabRadius * 0.5f);
                Gizmos.DrawRay(lastHit.point, lastHit.normal);
            }
            */
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

            if (!wantsGrabL && !wantsGrabR)
            {
                //CurrentCharacterState = CharacterState.Default;
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
            }
        }

        [SerializeField] private float walkSpeed = 5f;
        [SerializeField] private float sprintSpeed = 8f;

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            switch (CurrentCharacterState)
            {
                case CharacterState.Default:
                    {
                        // Ground movement
                        if (Motor.GroundingStatus.IsStableOnGround)
                        {
                            float currentVelocityMagnitude = currentVelocity.magnitude;

                            Vector3 effectiveGroundNormal = Motor.GroundingStatus.GroundNormal;

                            // Reorient velocity on slope
                            currentVelocity = Motor.GetDirectionTangentToSurface(currentVelocity, effectiveGroundNormal) * currentVelocityMagnitude;

                            // Calculate target velocity
                            Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                            Vector3 reorientedInput = Vector3.Cross(effectiveGroundNormal, inputRight).normalized * _moveInputVector.magnitude;

                            float speed = _sprintPressed ? sprintSpeed : walkSpeed;

                            // temp fov proto
                            playerCamera.fieldOfView = _sprintPressed ? 85 : 75;

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
                            // Add move input
                            if (_moveInputVector.sqrMagnitude > 0f)
                            {
                                Vector3 addedVelocity = _moveInputVector * AirAccelerationSpeed * deltaTime;

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

                        // Handle jumping
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

                                // Add to the return velocity and reset jump state
                                currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                                currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
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

                                // Add to the return velocity and reset jump state
                                currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                                currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
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

                                // Add to the return velocity and reset jump state
                                currentVelocity += (jumpDirection * JumpUpSpeed) - Vector3.Project(currentVelocity, Motor.CharacterUp);
                                currentVelocity += (_moveInputVector * JumpScalableForwardSpeed);
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
            }
        }

        private Vector3 _slideDirection;  // direction we started sliding in
        private float _slideSpeed;        // speed at slide start


        [SerializeField] private float slideInitialBoost = 2f;
        [SerializeField] private float slideGravityMultiplier = 1.3f;
        [SerializeField] private float slideFriction = 2f;
        [SerializeField] private float maxSlideSpeed = 12f;
        [SerializeField] private float minSlideAngle = 10f;

        // flat ground friction
        [SerializeField] private float flatFrictionMultiplier = 5f; // multiply slideFriction on flat


        private void HandleSliding(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

            // Blend stored direction and downhill
            Vector3 slideDir = Vector3.Lerp(_slideDirection, downhillDirection, 0.3f).normalized;

            // Check if moving downhill or uphill
            float slopeDot = Vector3.Dot(slideDir, downhillDirection);

            // Apply slope acceleration only downhill
            float slideAcceleration = 9.81f * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slideGravityMultiplier;
            currentVelocity += slideDir * slideAcceleration * deltaTime * Mathf.Max(slopeDot, 0f);

            // Apply friction
            float frictionMultiplier = 1f;
            if (slopeAngle <= minSlideAngle) frictionMultiplier = flatFrictionMultiplier;
            if (slopeDot < 0f) frictionMultiplier *= 2f; // optional: uphill friction boost
            currentVelocity *= 1f - (slideFriction * frictionMultiplier * deltaTime);

            // Clamp max speed
            if (currentVelocity.magnitude > maxSlideSpeed)
                currentVelocity = currentVelocity.normalized * maxSlideSpeed;

            // Store direction for next frame
            _slideDirection = currentVelocity.normalized;
        }

        private void HandleSlidingOld(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 groundNormal = Motor.GroundingStatus.GroundNormal;
            float slopeAngle = Vector3.Angle(groundNormal, Vector3.up);

            // If too flat, slow down and exit
            if (slopeAngle < minSlideAngle)
            {
                //  old
                // currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, slideFriction * deltaTime);
                // return;
            }

            // Calculate slope’s downhill direction
            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

            // Blend between stored direction and actual downhill
            // The 0.3f factor controls how much gravity influences direction over time
            Vector3 slideDir = Vector3.Lerp(_slideDirection, downhillDirection, 0.3f).normalized;

            // Accelerate along slope
            float slideAcceleration = 9.81f * Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * slideGravityMultiplier;
            currentVelocity += slideDir * slideAcceleration * deltaTime;

            Debug.LogError(slopeAngle);
            // Apply friction
            float frictionMultiplier = 1f;
            if (slopeAngle <= minSlideAngle)
            {
                Debug.LogError("flat");
                frictionMultiplier = flatFrictionMultiplier; // boost friction on flat
            }

            currentVelocity *= 1f - (slideFriction * frictionMultiplier * deltaTime);

            // Clamp max speed
            if (currentVelocity.magnitude > maxSlideSpeed)
                currentVelocity = currentVelocity.normalized * maxSlideSpeed;

            // Update stored direction for next frame (so we gradually re-align)
            _slideDirection = currentVelocity.normalized;
        }


        // make the boost happen when starting slide with _sprintPressed?
        private void StartSlide()
        {
            CurrentCharacterState = CharacterState.Sliding;

            // Store the current velocity direction & speed
            Vector3 currentVelocity = Motor.BaseVelocity;
            _slideSpeed = currentVelocity.magnitude;

            if (_slideSpeed > 0.1f)
                _slideDirection = currentVelocity.normalized;
            else
                _slideDirection = Motor.CharacterForward; // fallback if starting from rest

            // Optional: apply small forward boost to make it feel snappy
            _slideSpeed += slideInitialBoost;

            // Adjust collider for crouch/slide
            //Motor.SetCapsuleDimensions(slideCapsuleRadius, slideCapsuleHeight, slideCapsuleYOffset);
        }

        private void StopSlide()
        {
            CurrentCharacterState = CharacterState.Default;

            // retain some momentup
           //  Motor.BaseVelocity = _slideDirection * Mathf.Max(_slideSpeed * 0.5f, 0f);
        }


        // v1 
        /*
        void HandleClimbVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            // Always apply gravity, so you still "hang"
            currentVelocity += Gravity * deltaTime;

            // Compute spring pull to anchor
            Vector3 toAnchor = leftHandGrabAnchor - Motor.TransientPosition;
            float dist = toAnchor.magnitude;
            if (dist > idealRadius)
            {
                Vector3 dir = toAnchor.normalized;
                float stretch = dist - idealRadius;

                // Hooke's law spring force
                Vector3 springForce = dir * (stretch * springStrength);

                // Damping along spring axis
                Vector3 velAlongDir = Vector3.Dot(currentVelocity, dir) * dir;
                Vector3 dampingForce = -velAlongDir * springDamping;

                // Apply
                currentVelocity += (springForce + dampingForce) * deltaTime;
            }

            if (_moveInputVector.sqrMagnitude > 0f)
            {
                // Map input to world axes instead of wall tangent
                Vector3 inputMove = new Vector3(_moveInputVector.x, 0f, _moveInputVector.y) * climbMoveSpeed;

                // Optionally, rotate input by camera orientation so WASD matches view direction
                inputMove = playerCamera.transform.TransformDirection(inputMove);

                currentVelocity += inputMove * deltaTime;
            }

        }
        */

        [Header("Climbing Physics")]
        public float maxRopeLength = 2.5f; // Maximum distance from anchor
        public float restLength = 1.5f; // Natural/rest length of the elastic joint
        public float elasticStiffness = 50f; // How stiff the elastic is (spring constant)
        public float radialDamping = 10f; // Damping along radial direction (toward/away from anchor)
        public float tangentialDamping = 15f; // Damping perpendicular to radial direction (prevents swinging)
        public float maxElasticForce = 100f; // Maximum force the elastic can apply

        void HandleClimbVelocityElastic(ref Vector3 currentVelocity, float deltaTime)
        {
            // ----------------------
            // Camera-relative input movement
            // ----------------------
            Vector3 input = _moveInputVector;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input;
            inputMove *= climbMoveSpeed * ((isGrabbingL && isGrabbingR) ? 1.25f : 1f);

            currentVelocity += inputMove; // DO NOT multiply by deltaTime

            // ----------------------
            // Elastic joint physics for each hand
            // ----------------------

            // Handle left hand elastic joint
            if (isGrabbingL)
            {
                ApplyElasticJointForce(
                    ref currentVelocity,
                    leftHandGrabAnchor,
                    deltaTime
                );
            }

            // Handle right hand elastic joint
            if (isGrabbingR)
            {
                ApplyElasticJointForce(
                    ref currentVelocity,
                    rightHandGrabAnchor,
                    deltaTime
                );
            }

            // ----------------------
            // Gravity and drag
            // ----------------------

            // Apply gravity differently based on input
            if (input.sqrMagnitude > 0f)
            {
                _hasMovedWhileClimbing = true;
            }
            else
            {
                currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;
            }

            // Limit fall speed after movement
            if (_hasMovedWhileClimbing && input.sqrMagnitude == 0f)
            {
                float maxFallSpeed = -0.5f;
                if (currentVelocity.y < maxFallSpeed)
                {
                    currentVelocity.y = maxFallSpeed;
                }
            }

            // Apply drag based on input state
            float dragMultiplier = input.sqrMagnitude > 0f ? 2f : 20f;
            currentVelocity *= (1f / (1f + (Drag * dragMultiplier * deltaTime)));

            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }

        /// <summary>
        /// Applies elastic joint physics to simulate a rope/elastic connection to an anchor point.
        /// Includes radial and tangential damping to prevent oscillation and swinging.
        /// </summary>
        private void ApplyElasticJointForce(ref Vector3 currentVelocity, Vector3 anchorPoint, float deltaTime)
        {
            Vector3 toAnchor = anchorPoint - Motor.TransientPosition;
            float currentDistance = toAnchor.magnitude;

            // Early exit if at anchor point
            if (currentDistance < 0.001f) return;

            Vector3 radialDirection = toAnchor / currentDistance; // Normalized direction toward anchor

            // ----------------------
            // SAFEGUARD: Pull player back if outside maximum length
            // ----------------------
            /*
            if (currentDistance > maxRopeLength)
            {
                // Calculate how far beyond the limit we are
                float overshoot = currentDistance - maxRopeLength;

                // Hard constraint: instantly move back to max length
                Vector3 correctionPosition = anchorPoint - radialDirection * maxRopeLength;
                Vector3 positionCorrection = correctionPosition - Motor.TransientPosition;

                // Apply correction as velocity (will be integrated by motor)
                currentVelocity += positionCorrection / deltaTime;

                // Kill ALL outward velocity to prevent bouncing
                float radialVelocity = Vector3.Dot(currentVelocity, radialDirection);
                if (radialVelocity < 0f) // Moving away from anchor
                {
                    currentVelocity -= radialDirection * radialVelocity;
                }

                // Kill most tangential velocity too (prevent swinging at limit)
                Vector3 radialVelocityVector = radialDirection * Vector3.Dot(currentVelocity, radialDirection);
                Vector3 tangentialVelocity = currentVelocity - radialVelocityVector;
                currentVelocity -= tangentialVelocity * 0.8f; // Remove 80% of tangential velocity

                return; // Don't apply spring forces when at hard limit
            }
            */

            // ----------------------
            // ELASTIC SPRING: Only applies when stretched beyond rest length
            // ----------------------
            if (currentDistance > restLength)
            {
                float extension = currentDistance - restLength;

                // ----------------------
                // 1. Decompose velocity into radial and tangential components
                // ----------------------
                float radialVelocity = Vector3.Dot(currentVelocity, radialDirection);
                Vector3 radialVelocityVector = radialDirection * radialVelocity;
                Vector3 tangentialVelocity = currentVelocity - radialVelocityVector;

                // ----------------------
                // 2. Spring force (radial only - pulls toward anchor)
                // ----------------------
                float springForceMagnitude = elasticStiffness * extension;
                springForceMagnitude = Mathf.Min(springForceMagnitude, maxElasticForce); // Clamp max force
                Vector3 springForce = radialDirection * springForceMagnitude;

                // ----------------------
                // 3. Radial damping (opposes movement toward/away from anchor)
                // ----------------------
                Vector3 radialDampingForce = -radialVelocityVector * radialDamping;

                // ----------------------
                // 4. Tangential damping (opposes swinging/circular motion)
                // This is KEY to preventing the pendulum effect!
                // ----------------------
                Vector3 tangentialDampingForce = -tangentialVelocity * tangentialDamping;

                // ----------------------
                // 5. Apply all forces
                // ----------------------
                Vector3 totalForce = springForce + radialDampingForce + tangentialDampingForce;
                Vector3 acceleration = totalForce; // Assuming unit mass (F = ma, m = 1)

                currentVelocity += acceleration * deltaTime;
            }
        }

        void HandleClimbVelocityWorkinonit(ref Vector3 currentVelocity, float deltaTime)
        {

            // ----------------------
            // 2. Free WSAD movement (camera-relative)
            // ----------------------
            Vector3 input = _moveInputVector;
           if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input;
            inputMove *= climbMoveSpeed * ((isGrabbingL && isGrabbingR) ? 1.25f : 1f);

            Debug.Log(inputMove);

            currentVelocity += inputMove; // DO NOT multiply by deltaTime

            float outerRadius = 2.5f; // maximum allowed distance

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



            // MAKE THIS WORK VERTICALLY TOO
            // Cap climbing velocity when moving with input
            float maxClimbSpeed = climbMoveSpeed * 4f; // Adjust multiplier as needed
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
            } else
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


            float dragMultiplier = 2f;

            if (input.sqrMagnitude > 0f)
            {
                dragMultiplier = 2f;
            } else
            {
                dragMultiplier = 20f;
            }

            // Drag
            currentVelocity *= (1f / (1f + (Drag * dragMultiplier * deltaTime)));


            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }


        void HandleClimbVelocity(ref Vector3 currentVelocity, float deltaTime)
        {

            // ----------------------
            // 2. Free WSAD movement (camera-relative)
            // ----------------------
            Vector3 input = _moveInputVector;
            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input;
            inputMove *= climbMoveSpeed * ((isGrabbingL && isGrabbingR) ? 1.25f : 1f);

            Debug.Log(inputMove);

            currentVelocity += inputMove; // DO NOT multiply by deltaTime

            float outerRadius = 2.5f; // maximum allowed distance

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



            // MAKE THIS WORK VERTICALLY TOO
            // Cap climbing velocity when moving with input
            float maxClimbSpeed = climbMoveSpeed * 4f; // Adjust multiplier as needed
            if (input.sqrMagnitude > 0f)
            {
                _hasMovedWhileClimbing = true;
            }
            currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;


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


            float dragMultiplier = 2f;

            // Drag was zero in the previous version
            currentVelocity *= (1f / (1f + (Drag * dragMultiplier * deltaTime)));


            // Take into account additive velocity
            if (_internalVelocityAdd.sqrMagnitude > 0f)
            {
                currentVelocity += _internalVelocityAdd;
                _internalVelocityAdd = Vector3.zero;
            }
        }


        void HandleClimbVelocity1(ref Vector3 currentVelocity, float deltaTime)
        {
            // Apply gravity
            currentVelocity += playerCharacter.CurrentStats.gravity * deltaTime;

            // Camera-relative input
            // if (_moveInputVector.sqrMagnitude > 0f)
            //{
            /*
                Debug.Log(_moveInputVector);
                Motor.ForceUnground();
            Vector3 inputMove = _moveInputVector * climbMoveSpeed;

            inputMove = playerCamera.transform.TransformDirection(inputMove);

            currentVelocity += inputMove; // * deltaTime; // DO NOT multiply by deltaTime
            */

            // Normalize input to prevent diagonal boost
            Motor.ForceUnground();
            Vector3 input = _moveInputVector;
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Map x/z input to camera right/forward (use forward vector fully, including Y)
            Vector3 inputMove = playerCamera.transform.forward * input.z + playerCamera.transform.right * input.x;

            // Scale by speed
            inputMove *= climbMoveSpeed;

            // Add directly to KCC velocity
            currentVelocity += inputMove; // NO deltaTime

            // }

            // Apply spring if outside ideal radius
            Vector3 toAnchor = leftHandGrabAnchor - Motor.TransientPosition;
            float dist = toAnchor.magnitude;

            // og
            /*
            if (dist > idealRadius)
            {
                Vector3 dir = toAnchor.normalized;
                float stretch = dist - idealRadius;
                Vector3 springForce = dir * stretch * springStrength;
                Vector3 dampingForce = -Vector3.Project(currentVelocity, dir) * springDamping;
                currentVelocity += (springForce + dampingForce) * deltaTime;
            }
            */

            // Only apply spring if outside dead zone
            float deadZone = 1.5f;
            if (dist > idealRadius + deadZone)
            {
                Vector3 dir = toAnchor.normalized;
                float stretch = dist - (idealRadius + deadZone);

                // Gentle spring
                //float springStrength = 10f;
                Vector3 springForce = dir * stretch * springStrength;

                // Strong damping to stop swing
                //float springDamping = 20f;
                Vector3 dampingForce = -Vector3.Project(currentVelocity, dir) * springDamping;

                currentVelocity += (springForce + dampingForce) * deltaTime;
            }




            /*
             float overDistance = dist - idealRadius;

            float maxOverStretch = 2.5f;
            if (overDistance > 0f)
            {
                float t = Mathf.Clamp01(overDistance / maxOverStretch); // maxOverStretch defines how far before full force
                Vector3 dir = toAnchor.normalized;
                Vector3 springForce = dir * t * overDistance * springStrength;
                Vector3 dampingForce = -Vector3.Project(currentVelocity, dir) * springDamping * t;

                currentVelocity += (springForce + dampingForce) * deltaTime;
            }
            */

        }




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

                        if (!_isCrouching) //|| !Motor.GroundingStatus.IsStableOnGround)
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
        }

        protected void OnLeaveStableGround()
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }
    }
}