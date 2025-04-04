using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Items/Equipment")]
public class EquipmentObject : ItemObject
{
    public float spdBonus;
    public float defBonus;
    public void Reset()
    {
        type = ItemType.Equipment;
    }
}
