using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FishPond : MonoBehaviour
{
    [Header("Pond Settings")]
    public float waterHeight = 0f;
    public float pondDepth = 2f;

    [Header("Fish Population")]
    public List<FishSpawnEntry> fishTypes = new List<FishSpawnEntry>();
    public int minFishCount = 5;
    public int maxFishCount = 15;

    private List<Fish> spawnedFish = new List<Fish>();
    private Collider pondCollider;

    [System.Serializable]
    public class FishSpawnEntry
    {
        public FishData fishData;
        [Range(0f, 1f)]
        public float spawnChance = 0.5f;
    }

    private void Awake()
    {
        pondCollider = GetComponent<Collider>();
        pondCollider.isTrigger = true;

        // Assign to Water layer for easy identification
        gameObject.layer = LayerMask.NameToLayer("Water");
    }

    private void Start()
    {
        SpawnFish();
    }

    private void SpawnFish()
    {
        int fishCount = Random.Range(minFishCount, maxFishCount + 1);

        for (int i = 0; i < fishCount; i++)
        {
            FishData selectedFish = SelectRandomFish();
            if (selectedFish != null)
            {
                Vector3 spawnPos = GetRandomPointInPond();
                GameObject fishObj = new GameObject($"Fish_{selectedFish.fishName}_{i}");
                fishObj.transform.position = spawnPos;
                fishObj.transform.parent = transform;

                Fish fish = fishObj.AddComponent<Fish>();
                fish.Initialize(selectedFish, this);
                spawnedFish.Add(fish);
            }
        }
    }

    private FishData SelectRandomFish()
    {
        float totalWeight = 0f;
        foreach (var entry in fishTypes)
        {
            totalWeight += entry.spawnChance;
        }

        float random = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (var entry in fishTypes)
        {
            cumulative += entry.spawnChance;
            if (random <= cumulative)
            {
                return entry.fishData;
            }
        }

        return fishTypes.Count > 0 ? fishTypes[0].fishData : null;
    }

    public Vector3 GetRandomPointInPond()
    {
        // Get random point within collider bounds
        Bounds bounds = pondCollider.bounds;

        Vector3 randomPoint = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            waterHeight - Random.Range(0f, pondDepth),
            Random.Range(bounds.min.z, bounds.max.z)
        );

        return randomPoint;
    }

    public Fish TryCatchFish(Vector3 bobberPosition)
    {
        // Find closest fish to bobber
        Fish closestFish = null;
        float closestDistance = float.MaxValue;

        foreach (var fish in spawnedFish)
        {
            if (fish == null) continue;

            float distance = Vector3.Distance(fish.transform.position, bobberPosition);
            if (distance < closestDistance && distance < 3f) // 3m catch radius
            {
                closestDistance = distance;
                closestFish = fish;
            }
        }

        if (closestFish != null)
        {
            spawnedFish.Remove(closestFish);
            Destroy(closestFish.gameObject);
        }

        return closestFish;
    }

    private void OnDrawGizmos()
    {
        if (pondCollider == null) pondCollider = GetComponent<Collider>();
        if (pondCollider == null) return;

        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireCube(pondCollider.bounds.center, pondCollider.bounds.size);
    }
}