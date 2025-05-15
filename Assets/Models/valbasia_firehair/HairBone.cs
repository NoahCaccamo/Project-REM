using UnityEngine;

public class HairBone : MonoBehaviour
{
    public Transform player;
    public Transform target;           // The previous bone
    public float boneLength = 1f;

    [Header("Physics Settings")]
    public float inertia = 0.95f;
    public float correctionStrength = 10f;

    [Header("Bending Limit")]
    [Range(0f, 180f)] public float maxBendAngle = 45f;

    [Header("Gravity Settings")]
    public bool enableGravity = true;
    public float gravityStrength = 9.81f;

    [Header("Sideways Force")]
    public bool enableSideforce = true;
    public float sideforceStrength = 9.81f;

    [Header("Wind Settings")]
    public bool enableWind = false;
    public Vector3 windDirection = new Vector3(1, 0, 0);
    public float windStrength = 1f;

    private Vector3 prevPosition;

    void Start()
    {
        prevPosition = transform.position;
    }

    void LateUpdate()
    {
        // 1. Simulate inertia
        Vector3 velocity = (transform.position - prevPosition) * inertia;
        prevPosition = transform.position;

        // 2. Apply external forces
        if (enableGravity)
            velocity += Vector3.down * gravityStrength * Time.deltaTime;
        
        if (enableSideforce)
            velocity += -player.transform.forward * sideforceStrength * Time.deltaTime;

        if (enableWind)
            velocity += windDirection.normalized * windStrength * Time.deltaTime;

        Vector3 predictedPos = transform.position + velocity;

        // 3. Enforce distance constraint
        Vector3 toTarget = predictedPos - target.position;
        Vector3 direction = toTarget.normalized;
        Vector3 desiredPos = target.position + direction * boneLength;

        // 4. Limit bending
        Vector3 currentDir = (transform.position - target.position).normalized;
        float angle = Vector3.Angle(currentDir, direction);
        if (angle > maxBendAngle)
            direction = Vector3.Slerp(currentDir, direction, maxBendAngle / angle);

        // 5. Final position correction
        Vector3 finalPos = target.position + direction * boneLength;
        transform.position = Vector3.Lerp(transform.position, finalPos, Time.deltaTime * correctionStrength);

        // 6. Orientation
        transform.LookAt(target.position);
        transform.Rotate(-90f, 0f, 0f); // Adjust if your mesh faces a different axis
    }
}
