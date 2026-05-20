using System.Collections.Generic;
using UnityEngine;


// This is supposed to be a dumb visual object controlled by the controller class.
// its bloated and sucks for proto
// refactor later
[System.Serializable]
public class Hand
{
    public string name;
    public ItemObject heldItem;
    public HandController controller; // Reference to the visual component
    public bool IsEmpty => heldItem == null;

    public void PickUp(ItemPickup pickup)
    {
        Debug.Log("pickup is being called");
        Debug.Log("pickup item data: " + pickup);

        if (pickup == null) return;

        heldItem = pickup.ItemData;

        // Show the item model in the hand
        if (controller != null && heldItem != null)
        {
            Debug.Log("Showing item in hand: " + heldItem.name);
            controller.ShowItem(heldItem);
        }

        if (heldItem.prefab != null)
        {
            // change this to be a sprite in the object which is the hand holding said object
            // so if we have a sprite, change the hand's sprite to that in the controller
        }
        ItemObject item = pickup.ItemData;

        // is this necessary for anything?
        switch (item.type)
        {
            case ItemType.Equipment:
                EquipmentObject equipment = item as EquipmentObject;
                if (equipment != null)
                {
                    // add item to inventory here

                    Debug.Log($"Picked up Equipment: {item.name}");
                }
                break;
            default:
                break;
        }

        pickup.OnPickedUp();

        Debug.Log($"{name} picked up {heldItem.name}");
    }

    public void Drop()
    {
        if (IsEmpty) return;

        if (heldItem != null)
        {
           //  GameObject dropped = GameObject.Instantiate(heldItem.prefab, attachPoint.position, attachPoint.rotation);
            // dropped.AddComponent<ItemPickup>().SetItemData(heldItem);
        }

        // Hide the item model
        if (controller != null)
        {
            controller.HideItem();
        }

        heldItem = null;
    }

    public void Use(PlayerContext context)
    {
        if (IsEmpty)
        {
            Debug.Log($"{name} hand is empty.");
            return;
        }

        heldItem.OnUse(context, this);
    }
}
