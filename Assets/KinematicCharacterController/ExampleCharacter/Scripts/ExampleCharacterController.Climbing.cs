using UnityEngine;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [Header("Hands")]
        public float grabRadius = 0.1f;
        public float grabDistance = 0.2f;

        [Header("Static Climb Variables")]
        public float climbMoveSpeed = 1f;
        public float maxStaticClimbRadius = 2.5f;

        [Header("Momentum Climb Variables")]
        public float maxMomentumSwingRadius = 5f;
        public float momentumSwingDamping = 0.5f;

        public float handSurfaceOffset = 0.05f;
        Vector3 climbNormal;

        [Header("Fish Wrangling")]
        public LayerMask fishLayer;
        private FishWrangler currentFish = null;
        private Vector3 leftHandFishLocalOffset;  // NEW: Local space offset on fish
        private Vector3 rightHandFishLocalOffset;

        [Header("Ledge Detection Settings")]
        public float forwardCheckDistance = 1.2f;
        public float ledgeHeight = 1.5f;
        public float ledgeDropDistance = 2.0f;

        private RaycastHit leftHandHit;
        private Vector3 leftHandGrabAnchor;
        private Handhold leftHandhold;

        private RaycastHit rightHandHit;
        private Vector3 rightHandGrabAnchor;
        private Handhold rightHandhold;

        private bool wantsGrabL = false;
        public bool isGrabbingL = false;
        private bool grabLDown = false;

        private bool wantsGrabR = false;
        public bool isGrabbingR = false;
        private bool grabRDown = false;


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


        private bool DetectLedge(out Vector3 grabPoint, out Vector3 grabNormal)
        {
            grabPoint = Vector3.zero;
            grabNormal = Vector3.zero;

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            // Use Motors own capsule cast utilities to stay consistent with KCC physics
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
                    // 1 Clamp position at the boundary
                    Vector3 targetPos = leftHandGrabAnchor + dir * outerRadius;
                    currentVelocity += (targetPos - Motor.TransientPosition);

                    // 2 Kill any velocity along that direction (heavy damping)
                    Vector3 alongDir = Vector3.Project(currentVelocity, dir);
                    currentVelocity -= alongDir;
                    */

                    // 3 Optional: add extra force opposite to overshoot if still moving out
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

    }
}
