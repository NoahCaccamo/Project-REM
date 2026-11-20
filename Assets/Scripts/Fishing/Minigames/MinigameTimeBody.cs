using UnityEngine;

/// <summary>
/// Makes a Rigidbody follow MinigameRoot.TimeScale (slow motion and full pause).
/// Gravity is applied manually so it can be scaled. Put this on every physics object in a minigame.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MinigameTimeBody : MonoBehaviour
{
    [Tooltip("Leave empty to find the MinigameRoot in a parent.")]
    [SerializeField] MinigameRoot root;

    Rigidbody rb;
    float baseDrag;
    float baseAngularDrag;
    bool baseUsedGravity;

    // Velocities at TimeScale = 1. Held while paused so they can be restored.
    Vector3 baseVelocity;
    Vector3 baseAngularVelocity;

    float appliedScale = 1f;
    bool paused;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (root == null) root = GetComponentInParent<MinigameRoot>();

        baseDrag = rb.linearDamping;
        baseAngularDrag = rb.angularDamping;
        baseUsedGravity = rb.useGravity;

        // Gravity is applied manually in FixedUpdate so it can follow the time scale
        rb.useGravity = false;
    }

    void FixedUpdate()
    {
        if (root == null) return;

        float scale = root.TimeScale;

        if (!Mathf.Approximately(scale, appliedScale))
        {
            ChangeScale(scale);
        }

        if (!paused && baseUsedGravity)
        {
            // Acceleration scales with the square of time
            rb.AddForce(Physics.gravity * (scale * scale), ForceMode.Acceleration);
        }
    }

    void ChangeScale(float newScale)
    {
        // Capture velocity at normal speed before leaving the current scale
        if (!paused)
        {
            baseVelocity = rb.linearVelocity / appliedScale;
            baseAngularVelocity = rb.angularVelocity / appliedScale;
        }

        if (newScale <= 0f)
        {
            paused = true;
            rb.isKinematic = true;
        }
        else
        {
            if (paused)
            {
                paused = false;
                rb.isKinematic = false;
            }

            rb.linearVelocity = baseVelocity * newScale;
            rb.angularVelocity = baseAngularVelocity * newScale;
            rb.linearDamping = baseDrag * newScale;
            rb.angularDamping = baseAngularDrag * newScale;
        }

        appliedScale = newScale;
    }

    void OnDestroy()
    {
        // Nothing to clean up, but kept so subclasses can extend
    }
}