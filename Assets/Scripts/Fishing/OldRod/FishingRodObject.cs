using UnityEngine;

[CreateAssetMenu(fileName = "New Fishing Rod", menuName = "Inventory System/Items/Fishing Rod")]
public class FishingRodObject : EquipmentObject
{
    [Header("Fishing Rod Settings")]
    public float maxCastDistance = 15f;
    public float castSpeed = 8f; // How fast the reticle moves
    public float bobberCastSpeed = 20f;
    public float reelSpeed = 5f;
    public GameObject reticlePrefab;
    public GameObject bobberPrefab;
    public LineRenderer rodLinePrefab;

    private FishingRodController activeController;

    public void Reset()
    {
        type = ItemType.Equipment;
    }

    public override void OnUse(PlayerContext context, Hand hand)
    {
        if (activeController == null)
        {
            // Create fishing rod controller
            GameObject controllerObj = new GameObject("FishingRodController");
            activeController = controllerObj.AddComponent<FishingRodController>();
            activeController.Initialize(this, context, hand);
        }
        else
        {
            activeController.OnUsePressed();
        }
    }

    public void CleanupController()
    {
        if (activeController != null)
        {
            GameObject.Destroy(activeController.gameObject);
            activeController = null;
        }
    }
}