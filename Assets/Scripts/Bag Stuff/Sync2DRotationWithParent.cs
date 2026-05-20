using UnityEngine;

/// <summary>
/// Synchronizes a 2D rigidbody's rotation with its parent's 2D rotation (Z-axis only).
/// Attach this to each 2D physics object in the backpack.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Sync2DRotationWithParent : MonoBehaviour
{
    private Rigidbody2D rb2d;
    private Transform parentTransform;
    private float lastParentZRotation;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        parentTransform = transform.parent;

        if (parentTransform != null)
        {
            lastParentZRotation = parentTransform.eulerAngles.z;
        }
    }

    void FixedUpdate()
    {
        if (parentTransform == null) return;

        // Get current parent Z rotation
        float currentParentZRotation = parentTransform.eulerAngles.z;

        // Calculate rotation delta
        float rotationDelta = Mathf.DeltaAngle(lastParentZRotation, currentParentZRotation);

        // Apply rotation delta to the Rigidbody2D
        if (Mathf.Abs(rotationDelta) > 0.001f)
        {
            rb2d.rotation += rotationDelta;
        }

        // Update last known rotation
        lastParentZRotation = currentParentZRotation;
    }
}