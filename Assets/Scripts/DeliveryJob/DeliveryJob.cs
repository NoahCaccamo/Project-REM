using UnityEngine;

[System.Serializable]
public class DeliveryJob
{
    public MemoryType memoryType;
    public string pickupLocation;
    public string dropoffLocation;
    public float distance;
    public float rewardAmount;
    public bool isActive;

    public DeliveryJob(MemoryType memory)
    {
        memoryType = memory;
        pickupLocation = memory.pickupLocation;
        dropoffLocation = memory.dropoffLocation;
        distance = 0f;
        rewardAmount = 0f;
        isActive = false;
    }

    public void CalculateReward(float distanceInMeters)
    {
        distance = distanceInMeters;
        // Base rate: $5 per 10 meters, minimum $10
        rewardAmount = Mathf.Max(10f, (distance / 10f) * 5f);
    }
}