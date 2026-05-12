using UnityEngine;

[CreateAssetMenu(fileName = "New Fish", menuName = "Fishing System/Fish Data")]
public class FishData : ScriptableObject
{
    [Header("Basic Info")]
    public string fishName;
    [TextArea(3, 6)]
    public string description;
    public Sprite icon;
    public GameObject fishModel;

    [Header("Size Ranges (cm)")]
    public float minSize = 10f;
    public float maxSize = 50f;

    [Header("Spawn Settings")]
    [Range(0f, 1f)]
    public float spawnWeight = 0.5f; // Rarity (0 = rare, 1 = common)

    [Header("Behavior")]
    public float swimSpeed = 1f;
    public float turnSpeed = 2f;
    public float idleChance = 0.3f; // Chance to stop and idle

    public float GetRandomSize()
    {
        return Random.Range(minSize, maxSize);
    }

    public FishSizeCategory GetSizeCategory(float size)
    {
        float normalizedSize = Mathf.InverseLerp(minSize, maxSize, size);

        if (normalizedSize < 0.33f) return FishSizeCategory.Small;
        if (normalizedSize < 0.66f) return FishSizeCategory.Medium;
        return FishSizeCategory.Large;
    }
}

public enum FishSizeCategory
{
    Small,
    Medium,
    Large
}