using UnityEngine;

public class HandController : MonoBehaviour
{
    public Transform cameraAnchor;
    // public Transform worldTarget;
    public Vector3 worldTarget;
    public float transitionSpeed = 5f;
    private float grabBlend = 0f;
    private bool isGrabbing = false;

    public SpriteRenderer spriteRenderer;

    [Header("Hand Sprites")]
    public Sprite idleSprite;
    public Sprite openSprite;
    public Sprite grabbingSprite;

    [Header("Hand Visual")]
    [SerializeField] private Transform handModelParent; // REDUNDANT, USE ANCHOR
    [SerializeField] private GameObject currentHandModel;

    [Header("References")]
    [SerializeField] private DeliveryWaypoint deliveryWaypoint; // Reference to scene waypoint

    private ItemObject currentItem;
    private ScannerHUD activeScannerHUD; // Reference to HUD on instantiated scanner

    void Update()
    {
        grabBlend = Mathf.MoveTowards(grabBlend, isGrabbing ? 1f : 0f, Time.deltaTime * transitionSpeed);

        Vector3 targetPos = Vector3.Lerp(cameraAnchor.position, worldTarget, grabBlend);
        // Quaternion targetRot = Quaternion.Slerp(cameraAnchor.rotation, worldTarget.rotation, grabBlend);

        transform.position = targetPos;
       // transform.rotation = targetRot;
    }

    public void StartGrab(Vector3 _worldTarget)
    {
        worldTarget = _worldTarget;
        isGrabbing = true;
        spriteRenderer.sprite = grabbingSprite;
    }

    public void StopGrab()
    {
        isGrabbing = false;
        spriteRenderer.sprite = idleSprite;
    }

    public void OpenHand()
    {
        spriteRenderer.sprite = openSprite;
    }

    public void ShowItem(ItemObject item)
    {
        // HideItem();

        if (item == null) return;

        currentItem = item;

        // Instantiate the item's 3D model (which includes world-space UI for scanner)
        if (item.heldModel != null && handModelParent != null)
        {
            currentHandModel = Instantiate(item.heldModel, handModelParent);
        }

        // If it's a scanner, initialize its HUD
        if (item is ScannerItemObject && currentHandModel != null)
        {
            activeScannerHUD = currentHandModel.GetComponentInChildren<ScannerHUD>();
            if (activeScannerHUD != null)
            {
                // Link the scene's waypoint to the scanner
                activeScannerHUD.SetWaypoint(deliveryWaypoint);

                // Update with current active job
                if (DeliveryJobManager.Instance != null)
                {
                    var activeJob = DeliveryJobManager.Instance.GetActiveJob();
                    activeScannerHUD.UpdateActiveJob(activeJob);
                }
            }
        }
    }

    public void HideItem()
    {
        if (currentHandModel != null)
        {
            Destroy(currentHandModel);
            currentHandModel = null;
        }

        activeScannerHUD = null;
        currentItem = null;
    }
}
