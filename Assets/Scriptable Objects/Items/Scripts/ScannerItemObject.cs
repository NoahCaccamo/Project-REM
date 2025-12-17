using UnityEngine;

[CreateAssetMenu(fileName = "New Scanner", menuName = "Items/Scanner")]
public class ScannerItemObject : ItemObject
{
    [Header("Scanner Settings")]
    [Tooltip("Duration the waypoint remains visible after pulse")]
    public float waypointDuration = 5f;

    [Tooltip("Cooldown between uses")]
    public float cooldown = 10f;

    public override void OnUse(PlayerContext context, Hand hand)
    {
        // Find the scanner HUD on the instantiated prefab
        ScannerHUD scannerHUD = hand.controller.GetComponentInChildren<ScannerHUD>();
        if (scannerHUD != null)
        {
            // Check cooldown on the HUD instance
            if (scannerHUD.IsOnCooldown())
            {
                Debug.Log("Scanner on cooldown!" + scannerHUD.GetRemainingCooldown());
                return;
            }

            scannerHUD.EmitPulse(waypointDuration);
            scannerHUD.SetCooldown(cooldown);
        }
        else
        {
            Debug.LogWarning("ScannerHUD not found on scanner prefab!");
        }
    }
}