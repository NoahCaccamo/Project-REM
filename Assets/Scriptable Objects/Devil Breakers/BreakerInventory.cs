using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Inventory", menuName = "Inventory System/Breaker Inventory")]
public class BreakerInventoryObject : ScriptableObject
{
    [SerializeField]
    public int maxSize = 4;

    public List<BreakerObject> breakerSlots = new List<BreakerObject>();
    public bool AddBreaker(BreakerObject breaker)
    {
        if (breakerSlots.Count >= 4) return false;

        breakerSlots.Add(breaker);
        return true;
    }

    public BreakerObject RemoveActiveBreaker()
    {
        if (breakerSlots.Count == 0) return null;

        BreakerObject removed = breakerSlots[0];
        breakerSlots.RemoveAt(0); // shifts up automatically

        return removed;
    }

    public BreakerObject GetActiveBreaker()
    {
        if (breakerSlots.Count == 0) return null;
        return breakerSlots[0];
    }

    public void ClearInventory()
    {
        breakerSlots.Clear();
    }
}