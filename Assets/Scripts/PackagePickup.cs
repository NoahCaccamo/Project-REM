using UnityEngine;

public class PackagePickup : MonoBehaviour
{
    [SerializeField] private MemoryType packageData;
    public MemoryType PackageData => packageData; // read only version

    public void OnPickedUp()
    {
        Destroy(gameObject);
    }
}
