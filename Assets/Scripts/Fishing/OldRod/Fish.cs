using UnityEngine;

public class Fish : MonoBehaviour
{
    public FishData fishData;
    public float size; // Actual size in cm
    public FishSizeCategory sizeCategory;

    private FishPond parentPond;
    private Vector3 targetPosition;
    private float idleTimer = 0f;
    private bool isIdling = false;

    public void Initialize(FishData data, FishPond pond)
    {
        fishData = data;
        parentPond = pond;
        size = data.GetRandomSize();
        sizeCategory = data.GetSizeCategory(size);

        // Scale the model based on size
        if (fishData.fishModel != null)
        {
            GameObject model = Instantiate(fishData.fishModel, transform);
            float scale = size / fishData.maxSize;
            model.transform.localScale = Vector3.one * scale;
        }

        PickNewTarget();
    }

    private void Update()
    {
        if (parentPond == null) return;

        if (isIdling)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                isIdling = false;
                PickNewTarget();
            }
            return;
        }

        // Move toward target
        Vector3 direction = (targetPosition - transform.position).normalized;
        transform.position += direction * fishData.swimSpeed * Time.deltaTime;

        // Rotate toward direction
        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                fishData.turnSpeed * Time.deltaTime
            );
        }

        // Check if reached target
        if (Vector3.Distance(transform.position, targetPosition) < 0.5f)
        {
            // Chance to idle
            if (Random.value < fishData.idleChance)
            {
                isIdling = true;
                idleTimer = Random.Range(1f, 3f);
            }
            else
            {
                PickNewTarget();
            }
        }
    }

    private void PickNewTarget()
    {
        if (parentPond != null)
        {
            targetPosition = parentPond.GetRandomPointInPond();
            // Keep at same height
            targetPosition.y = transform.position.y;
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, targetPosition);
            Gizmos.DrawWireSphere(targetPosition, 0.3f);
        }
    }
}