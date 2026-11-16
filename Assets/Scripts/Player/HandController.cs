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

    [Header("Grab Cooldown")]
    [Tooltip("How long (seconds) this hand can't grab after the player jumps off a handhold. This is also how long the hand takes to return to its default position.")]
    public float jumpGrabCooldown = 1f;
    [Tooltip("Hand return progress over time. X = normalized time (0-1), Y = progress from the release position (0) to the default position (1). Values above 1 overshoot.")]
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool isReturning = false;
    private float returnElapsed = 0f;
    private float returnDuration = 0f;
    private Vector3 returnStartPosition;

    /// <summary>
    /// True while the hand is returning to its default position after a jump. Grabbing is blocked during this time.
    /// </summary>
    public bool IsOnCooldown => isReturning;

    [Header("Hand Visual")]
    [SerializeField] private Transform handModelParent; // REDUNDANT, USE ANCHOR
    [SerializeField] public GameObject currentHandModel;

    [Header("References")]
    [SerializeField] private DeliveryWaypoint deliveryWaypoint; // Reference to scene waypoint

    private ItemObject currentItem;
    private ScannerHUD activeScannerHUD; // Reference to HUD on instantiated scanner

    void Update()
    {
        if (isReturning)
        {
            returnElapsed += Time.deltaTime;
            float t = returnDuration > 0f ? Mathf.Clamp01(returnElapsed / returnDuration) : 1f;
            float progress = returnCurve.Evaluate(t);

            // The end point follows the camera anchor so the hand stays correct while the player moves
            transform.position = Vector3.LerpUnclamped(returnStartPosition, cameraAnchor.position, progress);

            if (t >= 1f)
            {
                isReturning = false;
                grabBlend = 0f;
            }
            return;
        }

        grabBlend = Mathf.MoveTowards(grabBlend, isGrabbing ? 1f : 0f, Time.deltaTime * transitionSpeed);

        Vector3 targetPos = Vector3.Lerp(cameraAnchor.position, worldTarget, grabBlend);
        if (isGrabbing)
        {
            targetPos = worldTarget;
        }
        // Quaternion targetRot = Quaternion.Slerp(cameraAnchor.rotation, worldTarget.rotation, grabBlend);

        transform.position = targetPos;
       // transform.rotation = targetRot;
    }

    public void StartGrab(Vector3 _worldTarget)
    {
        isReturning = false;
        worldTarget = _worldTarget;
        isGrabbing = true;
        spriteRenderer.sprite = grabbingSprite;
    }

    public void StopGrab()
    {
        isGrabbing = false;
        spriteRenderer.sprite = idleSprite;
    }

    /// <summary>
    /// Releases the hand and starts the grab cooldown. The hand travels from where it currently is
    /// back to its default position over <see cref="jumpGrabCooldown"/> seconds, following <see cref="returnCurve"/>.
    /// </summary>
    public void StartReleaseCooldown()
    {
        isGrabbing = false;
        spriteRenderer.sprite = idleSprite;

        if (jumpGrabCooldown <= 0f)
        {
            return;
        }

        returnStartPosition = transform.position;
        returnDuration = jumpGrabCooldown;
        returnElapsed = 0f;
        isReturning = true;
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
