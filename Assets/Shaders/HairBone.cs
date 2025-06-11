using UnityEngine;

public class HairBone : MonoBehaviour
{
    // The player's transform for physics calculations relative to player motion
    public Transform player;
    // The Transform of the PARENT bone in the armature hierarchy
    public Transform target;

    // Calculated automatically: the length of this bone
    private float boneLength;

    [Header("Physics Settings")]
    [Tooltip("How much previous velocity is retained (0-1, 1 is no dampening)")]
    public float inertia = 0.95f;
    [Tooltip("Strength of correction towards desired position. Higher is stiffer.")]
    public float correctionStrength = 10f;

    [Header("Bending Limit")]
    [Range(0f, 180f)]
    [Tooltip("Maximum angle this bone can bend relative to its parent bone.")]
    public float maxBendAngle = 45f;

    [Header("Gravity Settings")]
    public bool enableGravity = true;
    public float gravityStrength = 9.81f;

    [Header("Sideways Force")]
    public bool enableSideforce = true;
    public float sideforceStrength = 9.81f; // Force pushing against player's forward direction

    [Header("Wind Settings")]
    public bool enableWind = false;
    public Vector3 windDirection = new Vector3(1, 0, 0);
    public float windStrength = 1f;

    private Vector3 prevPosition; // Tracks the world position of this bone's *tip* from the previous frame
    private Vector3 currentVelocity; // Current velocity of this bone's tip

    void Start()
    {
        // Initialize prevPosition to current position
        prevPosition = transform.position;
        currentVelocity = Vector3.zero;

        // Calculate boneLength: distance from this bone's pivot to its first child's pivot
        // Assumes that the bone's visual length corresponds to the distance to its child.
        if (transform.childCount > 0)
        {
            // Bone's local Z-axis usually points down the bone to its child.
            // If the bone's pivot is at its base, the length is the distance to its first child.
            boneLength = Vector3.Distance(transform.position, transform.GetChild(0).position);
        }
        else if (target != null)
        {
            // If this is the last bone (no children), infer length from its parent.
            // This is a fallback and assumes uniform bone length or a known end-point.
            boneLength = Vector3.Distance(transform.position, target.position);
        }
        else
        {
            Debug.LogWarning("HairBone: No children or target bone found to calculate boneLength. Defaulting to 1 unit. Please ensure proper setup.", this);
            boneLength = 1f; // Default if no children or target
        }

        // Ensure boneLength is not zero to prevent division by zero
        if (boneLength < 0.001f) boneLength = 0.001f;
    }

    void LateUpdate()
    {
        // If no target (e.g., root bone without a parent, or misconfigured), do nothing.
        if (target == null)
        {
            // Root bone might be handled by player's actual movement or special logic.
            // For a chain, every bone needs a parent target.
            return;
        }

        // 1. Simulate inertia: Apply velocity from previous frame, dampened by inertia
        // We're tracking the predicted position of *this bone's tip*
        currentVelocity = (transform.position - prevPosition) * inertia;
        prevPosition = transform.position; // Update prevPosition for next frame

        // 2. Apply external forces
        Vector3 appliedForces = Vector3.zero;
        if (enableGravity)
            appliedForces += Vector3.down * gravityStrength;

        if (enableSideforce)
            // Assumes player.transform.forward is the player's facing direction
            // We want a force pushing perpendicular to this, e.g., to the side.
            // Using player.transform.right for left/right push relative to player
            appliedForces += -player.transform.forward * sideforceStrength;

        if (enableWind)
            appliedForces += windDirection.normalized * windStrength;

        currentVelocity += appliedForces * Time.deltaTime; // Integrate forces into velocity

        Vector3 predictedTipPos = transform.position + currentVelocity;

        // 3. Enforce distance constraint (bone length)
        // The bone's pivot is at transform.position, its tip is at predictedTipPos
        Vector3 targetBoneBasePos = target.position; // Parent bone's pivot (base of current bone)
        Vector3 toPredictedTip = predictedTipPos - targetBoneBasePos;
        Vector3 constrainedDirection = toPredictedTip.normalized;
        // The desired position for the *tip* of this bone based on its length
        Vector3 desiredConstrainedTipPos = targetBoneBasePos + constrainedDirection * boneLength;

        // 4. Limit bending (relative to parent's orientation)
        // currentBoneDirection: current direction from this bone's base to its tip (this bone's local Z-axis usually)
        Vector3 currentBoneDirection = (transform.position - targetBoneBasePos).normalized;
        // desiredBoneDirection: the direction we want the bone to point after physics
        Vector3 desiredBoneDirection = (desiredConstrainedTipPos - targetBoneBasePos).normalized;

        float angle = Vector3.Angle(currentBoneDirection, desiredBoneDirection);

        if (angle > maxBendAngle && maxBendAngle > 0)
        {
            // Slerp towards the desired direction, but limit by maxBendAngle
            // This pulls the bone back if it tries to bend too far
            desiredBoneDirection = Vector3.Slerp(currentBoneDirection, desiredBoneDirection, maxBendAngle / angle);
        }

        // 5. Final position correction (for the *tip* of the bone, NOT the bone's pivot)
        // The bone's pivot stays at transform.position (unless modified by parent bone)
        // We calculate what rotation this bone needs to achieve the desiredBoneDirection
        Vector3 finalTipPos = targetBoneBasePos + desiredBoneDirection * boneLength;

        // 6. Calculate bone's local rotation
        // We need to rotate this bone's Transform so its local Z-axis (common bone forward)
        // points along 'desiredBoneDirection' relative to its parent.

        // Get the parent's world rotation inverse for local space calculations
        Quaternion parentWorldRotationInverse = Quaternion.Inverse(target.rotation);

        // Convert desiredBoneDirection to local space of the parent
        Vector3 localDesiredBoneDirection = parentWorldRotationInverse * desiredBoneDirection;

        // Calculate the local rotation needed for this bone to point along localDesiredBoneDirection
        // Assuming bone's default forward is its local Z-axis (0,0,1)
        Quaternion targetLocalRotation = Quaternion.FromToRotation(Vector3.forward, localDesiredBoneDirection);

        // Smoothly apply the rotation to this bone's local rotation
        // Lerp factor is based on correctionStrength for smooth movement
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, Time.deltaTime * correctionStrength);

        // For the next frame, prevPosition should be this bone's actual new tip position.
        // We calculate this based on its *new* world rotation and boneLength.
        prevPosition = transform.position + transform.forward * boneLength; // Assuming bone's forward is its length axis
    }
}
