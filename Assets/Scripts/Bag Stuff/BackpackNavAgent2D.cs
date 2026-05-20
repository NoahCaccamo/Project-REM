using UnityEngine;

/// <summary>
/// A simple 2D navigation agent that moves toward the edge of the backpack.
/// For testing pathfinding in the 2D inventory physics system.
/// </summary>
public class BackpackNavAgent2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The backpack surface this agent belongs to")]
    public BackpackSurface backpackSurface;

    [Header("Movement Settings")]
    [Tooltip("Movement speed of the agent")]
    public float moveSpeed = 2f;

    [Tooltip("Maximum force applied to reach target speed")]
    public float maxForce = 10f;

    [Tooltip("How close to consider 'reached' the target")]
    public float stoppingDistance = 0.1f;

    [Tooltip("If true, agent seeks the closest edge. If false, seeks a random edge point")]
    public bool seekClosestEdge = true;

    [Header("Physics Settings")]
    [Tooltip("Drag coefficient when moving (higher = more resistance)")]
    public float moveDrag = 1f;

    [Tooltip("Mass of the agent")]
    public float mass = 1f;

    [Header("Bounds Settings")]
    [Tooltip("Manual bounds definition (if no collider found)")]
    public Vector2 boundsSize = new Vector2(4f, 3f);

    [Header("Debug")]
    public bool drawDebugLines = true;

    private Rigidbody2D rb2d;
    private Vector2 targetPosition;
    private bool hasTarget = false;
    private BoxCollider2D backpackBounds2D;
    private bool useManualBounds = false;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
        if (rb2d == null)
        {
            rb2d = gameObject.AddComponent<Rigidbody2D>();
        }

        // Configure Rigidbody2D for proper physics interaction
        rb2d.gravityScale = 0; // No gravity for top-down 2D
        rb2d.linearDamping = moveDrag;
        rb2d.mass = mass;
        rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Find the backpack bounds in physics world
        if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            backpackBounds2D = backpackSurface.physicsWorldRoot.GetComponentInChildren<BoxCollider2D>();

            if (backpackBounds2D == null)
            {
                Debug.LogWarning("No BoxCollider2D found in PhysicsWorld! Using manual bounds.");
                useManualBounds = true;
            }
        }
        else
        {
            Debug.LogWarning("BackpackSurface or PhysicsWorldRoot not assigned! Using manual bounds.");
            useManualBounds = true;
        }

        SetTargetToEdge();
    }

    void Update()
    {
        // Press N to set a new edge target
        if (Input.GetKeyDown(KeyCode.N))
        {
            SetTargetToEdge();
        }
    }

    void FixedUpdate()
    {
        if (hasTarget)
        {
            MoveTowardsTarget();
        }
    }

    /// <summary>
    /// Sets the target position to a point on the edge of the backpack
    /// </summary>
    public void SetTargetToEdge()
    {
        Vector2 center;
        Vector2 extents;

        if (useManualBounds)
        {
            // Use manual bounds centered at physics world origin
            if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
            {
                center = backpackSurface.physicsWorldRoot.position;
            }
            else
            {
                center = Vector2.zero;
            }
            extents = boundsSize * 0.5f;
        }
        else if (backpackBounds2D != null)
        {
            // Use collider bounds
            Bounds bounds = backpackBounds2D.bounds;
            center = bounds.center;
            extents = bounds.extents;
        }
        else
        {
            Debug.LogWarning("No backpack bounds available!");
            return;
        }

        Vector2 currentPos = transform.position;

        if (seekClosestEdge)
        {
            targetPosition = GetClosestEdgePoint(currentPos, center, extents);
        }
        else
        {
            targetPosition = GetRandomEdgePoint(center, extents);
        }

        hasTarget = true;
        Debug.Log($"Agent targeting edge at: {targetPosition}");
    }

    /// <summary>
    /// Gets the closest point on the edge of the backpack bounds
    /// </summary>
    Vector2 GetClosestEdgePoint(Vector2 fromPos, Vector2 center, Vector2 extents)
    {
        // Calculate distances to each edge
        float distToTop = Mathf.Abs((center.y + extents.y) - fromPos.y);
        float distToBottom = Mathf.Abs((center.y - extents.y) - fromPos.y);
        float distToRight = Mathf.Abs((center.x + extents.x) - fromPos.x);
        float distToLeft = Mathf.Abs((center.x - extents.x) - fromPos.x);

        float minDist = Mathf.Min(distToTop, distToBottom, distToRight, distToLeft);

        // Return point on the closest edge
        if (minDist == distToTop)
            return new Vector2(fromPos.x, center.y + extents.y);
        else if (minDist == distToBottom)
            return new Vector2(fromPos.x, center.y - extents.y);
        else if (minDist == distToRight)
            return new Vector2(center.x + extents.x, fromPos.y);
        else
            return new Vector2(center.x - extents.x, fromPos.y);
    }

    /// <summary>
    /// Gets a random point on the edge of the backpack bounds
    /// </summary>
    Vector2 GetRandomEdgePoint(Vector2 center, Vector2 extents)
    {
        int edge = Random.Range(0, 4); // 0=top, 1=bottom, 2=left, 3=right

        switch (edge)
        {
            case 0: // Top
                return new Vector2(Random.Range(center.x - extents.x, center.x + extents.x), center.y + extents.y);
            case 1: // Bottom
                return new Vector2(Random.Range(center.x - extents.x, center.x + extents.x), center.y - extents.y);
            case 2: // Left
                return new Vector2(center.x - extents.x, Random.Range(center.y - extents.y, center.y + extents.y));
            case 3: // Right
                return new Vector2(center.x + extents.x, Random.Range(center.y - extents.y, center.y + extents.y));
            default:
                return center;
        }
    }

    /// <summary>
    /// Moves the agent towards the target position using physics forces.
    /// This allows other rigidbodies to naturally impede movement through collisions.
    /// </summary>
    void MoveTowardsTarget()
    {
        Vector2 currentPos = rb2d.position;
        float distance = Vector2.Distance(currentPos, targetPosition);

        if (distance <= stoppingDistance)
        {
            hasTarget = false;
            rb2d.linearVelocity = Vector2.zero;
            Debug.Log("Agent reached edge!");
            return;
        }

        // Calculate desired velocity
        Vector2 direction = (targetPosition - currentPos).normalized;
        Vector2 desiredVelocity = direction * moveSpeed;

        // Calculate steering force (difference between desired and current velocity)
        Vector2 steeringForce = desiredVelocity - rb2d.linearVelocity;

        // Clamp the steering force to max force
        steeringForce = Vector2.ClampMagnitude(steeringForce, maxForce);

        // Apply force instead of directly setting velocity
        // This allows physics to naturally handle collisions with other objects
        rb2d.AddForce(steeringForce, ForceMode2D.Force);
    }

    void OnDrawGizmos()
    {
        if (!drawDebugLines) return;

        // Draw current position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.1f);

        // Draw target position
        if (hasTarget)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(targetPosition, 0.15f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }

        // Draw backpack bounds
        Vector2 center = Vector2.zero;
        Vector2 size = boundsSize;

        if (backpackBounds2D != null)
        {
            Bounds bounds = backpackBounds2D.bounds;
            center = bounds.center;
            size = bounds.size;
        }
        else if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            center = backpackSurface.physicsWorldRoot.position;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);

        // Draw velocity vector for debugging
        if (rb2d != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, (Vector2)transform.position + rb2d.linearVelocity * 0.5f);
        }
    }
}