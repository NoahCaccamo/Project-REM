using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;

namespace KinematicCharacterController.Examples
{
    public enum CharacterState
    {
        Default,
        Climbing
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

        // This should change later to a check?
        public LayerMask climbableLayer;

        private RaycastHit leftHandHit;
        private Vector3 leftHandGrabAnchor;

        private RaycastHit rightHandHit;
        private Vector3 rightHandGrabAnchor;

        private bool wantsGrabL = false;
        private bool isGrabbingL = false;

        private bool wantsGrabR = false;
        private bool isGrabbingR = false;

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
        }

        /// <summary>
        /// Handles movement state transitions and enter/exit callbacks
        /// </summary>
        public void TransitionToState(CharacterState newState)
        {
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

                        break;
                    }

                case CharacterState.Climbing:
                    {
                        // Move and look inputs
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

                        break;
                    }
            }
        }

        Vector3 climbNormal;
        void TryGrabL()
        {
            // unsure where character position should be from
            // Is it initial or after sim or other???
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            
            if (Physics.SphereCast(origin, grabRadius, direction, out leftHandHit, grabDistance, climbableLayer))
            {
                isGrabbingL = true;
                // do i need leftHandHit = hit??
                leftHandGrabAnchor = leftHandHit.point;
                climbNormal = leftHandHit.normal;

                CurrentCharacterState = CharacterState.Climbing;
                Motor.ForceUnground();
            }
        }

        void TryGrabR()
        {
            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;


            if (Physics.SphereCast(origin, grabRadius, direction, out rightHandHit, grabDistance, climbableLayer))
            {
                isGrabbingR = true;
                rightHandGrabAnchor = rightHandHit.point;

                CurrentCharacterState = CharacterState.Climbing;
                Motor.ForceUnground();
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
            }

            if (!wantsGrabR)
            {
                isGrabbingR = false;
            }

            if (!wantsGrabL && !wantsGrabR)
            {
                CurrentCharacterState = CharacterState.Default;
            }
            if (!isGrabbingL && !isGrabbingR)
            {
                CurrentCharacterState = CharacterState.Default;
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
                            Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector, 1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                            // Set the current rotation (which will be used by the KinematicCharacterMotor)
                            currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
                        }

                        Vector3 currentUp = (currentRotation * Vector3.up);
                        if (BonusOrientationMethod == BonusOrientationMethod.TowardsGravity)
                        {
                            // Rotate from current up to invert gravity
                            Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
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
                                Vector3 smoothedGravityDir = Vector3.Slerp(currentUp, -Gravity.normalized, 1 - Mathf.Exp(-BonusOrientationSharpness * deltaTime));
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
                            Vector3 targetMovementVelocity = reorientedInput * MaxStableMoveSpeed;

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
                            currentVelocity += Gravity * deltaTime;

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

                case CharacterState.Climbing:
                    {
                        HandleClimbVelocity(ref currentVelocity, deltaTime);
                        break;
                    }
            }
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

        void HandleClimbVelocity(ref Vector3 currentVelocity, float deltaTime)
        {

            // ----------------------
            // 2. Free WSAD movement (camera-relative)
            // ----------------------
            Vector3 input = _moveInputVector;
           // if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 inputMove = input;
            inputMove *= climbMoveSpeed;

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
                        currentVelocity -= dir * overshootSpeed * 2f; // multiplier can be tuned
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
                        currentVelocity -= dir * overshootSpeed * 2f; // multiplier can be tuned
                    }
                }
            }

            currentVelocity += Gravity * deltaTime;

            // Drag
            currentVelocity *= (1f / (1f + (Drag * 2f * deltaTime)));


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
            currentVelocity += Gravity * deltaTime;

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