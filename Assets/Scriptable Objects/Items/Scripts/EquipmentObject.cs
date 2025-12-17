using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Equipment Object", menuName = "Inventory System/Items/Equipment")]
public class EquipmentObject : ItemObject
{
    public float spdBonus;
    public float defBonus;
    // temp spear test
    public float throwForce = 25f;
    public float recoilForce = 8f;
    public void Reset()
    {
        type = ItemType.Equipment;
    }

    // also temp for spear test
    public override void OnUse(PlayerContext context, Hand hand)
    {
        if (prefab == null || context.playerCamera == null)
        {
            Debug.LogWarning("Equipment has no prefab or missing context camera.");
            return;
        }

        // Spawn a physical spear projectile
        GameObject spear = GameObject.Instantiate(altPrefab, context.playerCamera.transform.position + context.playerCamera.transform.forward, Quaternion.identity);
        if (spear.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.linearVelocity = context.playerCamera.transform.forward * throwForce;
        }

        // Rocket jump kickback
        if (context.motor != null)
        {
            Vector3 backwardsForce = -context.playerCamera.transform.forward * recoilForce;
            context.motor.ForceUnground();
            context.motor.BaseVelocity += backwardsForce;
        }

        // Hand now drops the item after throwing
        // maybe remove and drop should be seperate?
        hand.Drop();
    }
}
