using UnityEngine;

[CreateAssetMenu(fileName = "New Breaker Object", menuName = "Inventory System/Breaker")]
public class BreakerObject : ScriptableObject
{
    // public GameObject prefab;
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int tapState;
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int holdState;

    [TextArea(15, 20)]
    public string description;
}