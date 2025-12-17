using UnityEngine;

public class DeliveryLocation : MonoBehaviour
{
    [SerializeField] private string locationName;

    public string LocationName => locationName;
    public bool isPickup = false;

    private void Awake()
    {
        LocationRegistry.Register(this);
    }

    private void Start()
    {
        DeliveryJobManager.Instance.RegisterLocation(locationName, transform.position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Optional: enforce that the name is not empty
        if (string.IsNullOrWhiteSpace(locationName))
            locationName = gameObject.name;
    }
#endif
}
