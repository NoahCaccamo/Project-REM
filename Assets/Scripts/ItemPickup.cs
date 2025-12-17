using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    [SerializeField] private ItemObject itemData;
    public ItemObject ItemData => itemData; // read only version

    public void OnPickedUp()
    {
        Destroy(gameObject);
    }
}
