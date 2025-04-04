using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Food Object", menuName = "Inventory System/Items/Food")]
public class FoodObject : ItemObject
{
    public int restoreHealthValue;
    public void Reset() // used to be Awake
    {
        type = ItemType.Food;
    }
}
