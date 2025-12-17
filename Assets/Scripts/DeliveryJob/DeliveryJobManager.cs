using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DeliveryJobManager : MonoBehaviour
{
    public static DeliveryJobManager Instance { get; private set; }

    [Header("Available Memory Types")]
    [SerializeField] private List<MemoryType> availableMemoryTypes = new();

    [Header("Location References")]
    [SerializeField] private Transform playerTransform;

    private List<DeliveryJob> availableJobs = new();
    private DeliveryJob activeJob;

    private Dictionary<string, Vector3> locationPositions = new();

    public DeliveryWaypoint deliveryWaypoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeLocationPositions();
        GenerateAvailableJobs();
    }


    // NEEDS HUGE FIXING
    private void InitializeLocationPositions()
    {
        // Find all transport locations and waypoints in the scene
        var locations = FindObjectsByType<DeliveryLocation>(FindObjectsSortMode.None);
    }

    public void RegisterLocation(string locationName, Vector3 position)
    {
        if (!locationPositions.ContainsKey(locationName))
        {
            locationPositions[locationName] = position;
        }
    }

    private void GenerateAvailableJobs()
    {
        availableJobs.Clear();

        foreach (var memoryType in availableMemoryTypes)
        {
            if (memoryType == null) continue;

            DeliveryJob job = new DeliveryJob(memoryType);

            // Calculate distance between pickup and dropoff
            if (locationPositions.ContainsKey(job.pickupLocation) &&
                locationPositions.ContainsKey(job.dropoffLocation))
            {
                Vector3 pickup = locationPositions[job.pickupLocation];
                Vector3 dropoff = locationPositions[job.dropoffLocation];
                float distance = Vector3.Distance(pickup, dropoff);
                job.CalculateReward(distance);
            }
            else
            {
                // Default distance if locations not found
                job.CalculateReward(50f);
            }

            availableJobs.Add(job);
        }
    }

    public List<DeliveryJob> GetAvailableJobs()
    {
        return new List<DeliveryJob>(availableJobs);
    }

    public bool SetActiveJob(DeliveryJob job)
    {
        if (job == null || !availableJobs.Contains(job))
            return false;

        // Deactivate previous job
        if (activeJob != null)
        {
            activeJob.isActive = false;
        }

        activeJob = job;
        activeJob.isActive = true;

        Vector3 pickup = locationPositions[job.pickupLocation];


        deliveryWaypoint.target = pickup;

        return true;
    }

    public DeliveryJob GetActiveJob()
    {
        return activeJob;
    }

    public void CompleteActiveJob()
    {
        if (activeJob != null)
        {
            // Add reward to player currency (implement your currency system)
            Debug.Log($"Delivery completed! Earned ${activeJob.rewardAmount:F2}");

            availableJobs.Remove(activeJob);
            activeJob = null;

            // Optionally generate new jobs
            GenerateAvailableJobs();
        }
    }
}