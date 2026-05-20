using UnityEngine;

/// <summary>
/// Visual 3D representation of a 2D physics item in the backpack.
/// Syncs position and rotation from the 2D physics object.
/// </summary>
public class BackpackItemVisual : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The 2D physics object this visual represents")]
    public Rigidbody2D physicsObject;

    [Tooltip("The visual plane this item belongs to")]
    public Transform visualPlane;

    [Tooltip("The physics world root (for local space calculations)")]
    public Transform physicsWorldRoot;

    [Header("Sync Settings")]
    [Tooltip("Smoothing factor for position updates (0 = instant, 1 = very smooth)")]
    [Range(0f, 1f)]
    public float positionSmoothing = 0.1f;

    [Tooltip("Smoothing factor for rotation updates")]
    [Range(0f, 1f)]
    public float rotationSmoothing = 0.1f;

    private Transform physicsTransform;

    void Start()
    {
        if (physicsObject != null)
        {
            physicsTransform = physicsObject.transform;
        }

        // Auto-find physics world root if not assigned
        if (physicsWorldRoot == null && physicsObject != null)
        {
            // Walk up the hierarchy to find the root
            Transform current = physicsObject.transform.parent;
            while (current != null && current.parent != null)
            {
                current = current.parent;
            }
            physicsWorldRoot = current;
        }
    }

    void LateUpdate()
    {
        if (physicsObject == null || visualPlane == null) return;

        // Get the physics object's position in LOCAL space relative to physics world root
        Vector3 physicsLocalPos;
        if (physicsWorldRoot != null)
        {
            physicsLocalPos = physicsWorldRoot.InverseTransformPoint(physicsTransform.position);
        }
        else
        {
            // Fallback: use world position
            physicsLocalPos = physicsTransform.position;
        }

        // Map the local physics position (X, Y, Z) to visual plane local space (X, Y, 0)
        // Physics local space and visual plane local space share the same coordinate system
        Vector3 visualLocalPos = new Vector3(physicsLocalPos.x, physicsLocalPos.y, 0f);

        // Convert to world space using visual plane's transform
        Vector3 targetWorldPos = visualPlane.TransformPoint(visualLocalPos);

        // Smooth position
        if (positionSmoothing > 0f)
        {
            transform.position = Vector3.Lerp(transform.position, targetWorldPos, 1f - positionSmoothing);
        }
        else
        {
            transform.position = targetWorldPos;
        }

        // Sync rotation - map 2D Z-axis rotation to 3D
        float physicsRotationZ = physicsObject.rotation;
        Quaternion targetRotation = visualPlane.rotation * Quaternion.Euler(0, 0, physicsRotationZ);

        if (rotationSmoothing > 0f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - rotationSmoothing);
        }
        else
        {
            transform.rotation = targetRotation;
        }
    }

    /// <summary>
    /// Maps a point on the visual plane to the 2D physics world space
    /// </summary>
    public Vector2 MapVisualToPhysics(Vector3 visualWorldPoint)
    {
        // Convert visual world point to local space of visual plane
        Vector3 visualLocalPoint = visualPlane.InverseTransformPoint(visualWorldPoint);

        // The local coordinates (X, Y) map directly to physics world local space
        // Convert from visual plane local space to physics world local space
        Vector3 physicsLocalPoint = new Vector3(visualLocalPoint.x, visualLocalPoint.y, 0f);

        // Convert to physics world space
        if (physicsWorldRoot != null)
        {
            Vector3 physicsWorldPoint = physicsWorldRoot.TransformPoint(physicsLocalPoint);
            return new Vector2(physicsWorldPoint.x, physicsWorldPoint.y);
        }

        // Fallback
        return new Vector2(physicsLocalPoint.x, physicsLocalPoint.y);
    }

    /// <summary>
    /// Maps a 2D physics point to the visual plane world space
    /// </summary>
    public Vector3 MapPhysicsToVisual(Vector2 physicsPoint)
    {
        // Get physics world position
        Vector3 physicsWorldPoint = new Vector3(physicsPoint.x, physicsPoint.y, 0f);

        // Convert to physics local space if we have a root
        Vector3 physicsLocalPoint;
        if (physicsWorldRoot != null)
        {
            physicsLocalPoint = physicsWorldRoot.InverseTransformPoint(physicsWorldPoint);
        }
        else
        {
            physicsLocalPoint = physicsWorldPoint;
        }

        // Map to visual plane local space (X, Y stay the same, Z = 0)
        Vector3 visualLocalPoint = new Vector3(physicsLocalPoint.x, physicsLocalPoint.y, 0f);

        // Convert to visual plane world space
        return visualPlane.TransformPoint(visualLocalPoint);
    }
}