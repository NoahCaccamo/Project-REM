using UnityEngine;
using KinematicCharacterController.Examples;
using UnityEngine.XR;

public class BackpackSurface : MonoBehaviour
{
    public InventoryObject inventoryData;
    public Transform backpackRoot;
    public ExampleCharacterController characterController;

    private Camera playerCamera;

    [Header("Bump Force Settings")]
    public float bumpForce = 500f;

    [Header("Click & Drag Settings")]
    public float clickHoldThreshold = 0.3f;
    public float dragForceMultiplier = 50f;
    public float maxDragForce = 100f;
    public float dragDamping = 0.95f;
    public float maxRaycastDistance = 10f;
    public LayerMask backpackLayerMask = -1;

    // Drag state tracking
    private bool isMouseDown = false;
    private float mouseDownTime = 0f;
    private bool isDragging = false;
    private Rigidbody2D draggedObject = null;
    private Vector2 dragAnchorPoint;
    private Vector2 dragTargetWorldPos;
    private bool hasValidDragTarget = false;

    void Start()
    {
        playerCamera = Camera.main;
        characterController = FindObjectOfType<ExampleCharacterController>();
    }

    void Update()
    {
        HandleClickAndDrag();
        HandleBumpForceInput();
    }

    void HandleClickAndDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isMouseDown = true;
            mouseDownTime = Time.time;
            TryStartDragOrPickup();
        }

        if (Input.GetMouseButton(0) && isMouseDown)
        {
            float holdDuration = Time.time - mouseDownTime;

            if (holdDuration >= clickHoldThreshold && !isDragging && draggedObject != null)
            {
                StartDragging();
            }

            if (isDragging)
            {
                UpdateDragTarget();
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                StopDragging();
            }
            else if (draggedObject != null)
            {
                TryPickupDraggedObject();
            }

            isMouseDown = false;
            draggedObject = null;
        }
    }

    void TryStartDragOrPickup()
    {
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(playerCamera.transform.position, playerCamera.transform.forward, maxRaycastDistance);

        if (hit.collider != null)
        {
            if (hit.collider.transform.IsChildOf(backpackRoot))
            {
                Rigidbody2D rb = hit.collider.GetComponent<Rigidbody2D>();
                if (rb != null && !rb.isKinematic)
                {
                    draggedObject = rb;
                    dragAnchorPoint = rb.transform.InverseTransformPoint(hit.point);
                    Debug.Log($"Detected potential drag target: {hit.collider.name}");
                }
            }
        }
    }

    void StartDragging()
    {
        isDragging = true;
        Debug.Log($"Started dragging: {draggedObject.name}");
        characterController.leftHand.controller.StartGrab(draggedObject.transform.position);
    }

    void UpdateDragTarget()
    {
        if (draggedObject == null) return;

        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, maxRaycastDistance, backpackLayerMask))
        {
            if (hit.collider.transform == backpackRoot || hit.collider.transform.IsChildOf(backpackRoot))
            {
                Vector3 hitPoint = hit.point;
                Vector2 anchorWorldOffset = draggedObject.transform.TransformDirection(dragAnchorPoint);
                dragTargetWorldPos = new Vector2(hitPoint.x, hitPoint.y);
                hasValidDragTarget = true;
                characterController.leftHand.controller.StartGrab(draggedObject.transform.position);
            }
            else
            {
                hasValidDragTarget = false;
            }
        }
        else
        {
            hasValidDragTarget = false;
            Debug.LogWarning("Drag target lost - no backpack surface hit");
        }
    }

    void FixedUpdate()
    {
        if (isDragging && draggedObject != null && hasValidDragTarget)
        {
            ApplyDragForce();
        }
    }

    void ApplyDragForce()
    {
        if (draggedObject == null) return;

        Vector2 currentPos = draggedObject.position;
        Vector2 toTarget = dragTargetWorldPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance > 0.01f)
        {
            Vector2 direction = toTarget.normalized;
            float forceMagnitude = distance * dragForceMultiplier;
            forceMagnitude = Mathf.Min(forceMagnitude, maxDragForce);

            Vector2 force = direction * forceMagnitude;
            draggedObject.AddForce(force, ForceMode2D.Force);
            draggedObject.linearVelocity *= dragDamping;
        }
    }

    void StopDragging()
    {
        if (draggedObject != null)
        {
            Debug.Log($"Stopped dragging: {draggedObject.name}");
            draggedObject.linearVelocity *= 0.5f;
        }

        isDragging = false;
        draggedObject = null;
        hasValidDragTarget = false;
        characterController.leftHand.controller.StopGrab();
    }

    void TryPickupDraggedObject()
    {
        if (draggedObject == null) return;

        Hand targetHand = null;
        if (characterController.leftHand.IsEmpty)
        {
            targetHand = characterController.leftHand;
        }
        else if (characterController.rightHand.IsEmpty)
        {
            targetHand = characterController.rightHand;
        }
        else
        {
            Debug.Log("Both hands are full!");
            return;
        }

        ItemPickup pickup = draggedObject.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            targetHand.PickUp(pickup);
            Debug.Log($"Picked up {pickup.ItemData.name} with quick click");
        }
        else
        {
            Debug.LogWarning($"Object {draggedObject.name} doesn't have ItemPickup component");
        }
    }

    void HandleBumpForceInput()
    {
        Vector2 bumpDirection = Vector2.zero;

        if (Input.GetKeyDown(KeyCode.UpArrow))
            bumpDirection = Vector2.up;
        else if (Input.GetKeyDown(KeyCode.DownArrow))
            bumpDirection = Vector2.down;
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
            bumpDirection = Vector2.left;
        else if (Input.GetKeyDown(KeyCode.RightArrow))
            bumpDirection = Vector2.right;

        if (bumpDirection != Vector2.zero)
        {
            ApplyBumpForceToAllItems(bumpDirection);
        }
    }

    void ApplyBumpForceToAllItems(Vector2 direction)
    {
        if (backpackRoot == null) return;

        Rigidbody2D[] items = backpackRoot.GetComponentsInChildren<Rigidbody2D>();

        foreach (Rigidbody2D rb in items)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(direction * bumpForce, ForceMode2D.Impulse);
            }
        }

        Debug.Log($"Applied bump force to {items.Length} items in direction {direction}");
    }

    void TryPlaceItemFromHand(bool isLeftHand)
    {
        Hand activeHand = isLeftHand ? characterController.leftHand : characterController.rightHand;

        if (activeHand.IsEmpty) return;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, 3.0f))
        {
            if (hit.collider.gameObject == gameObject)
            {
                PlaceItemAt(hit, activeHand);
            }
        }
    }

    void PlaceItemAt(RaycastHit hit, Hand hand)
    {
        var itemData = hand.heldItem;

        var slot = new InventorySlot(itemData, 1);
        slot.localPosition = backpackRoot.InverseTransformPoint(hit.point);
        slot.localRotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(90, 0, 0);
        inventoryData.Container.Add(slot);

        var visual = Instantiate(itemData.prefab, backpackRoot);
        visual.transform.localPosition = slot.localPosition;
        visual.transform.localRotation = slot.localRotation;

        var rb = visual.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;

            // Add rotation sync component
            if (!visual.GetComponent<Sync2DRotationWithParent>())
            {
                visual.AddComponent<Sync2DRotationWithParent>();
            }
        }

        hand.Drop();
    }

    float distanceFromCamera = 2f;

    public void OpenBackpack()
    {
        backpackRoot.gameObject.SetActive(true);

        Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward * distanceFromCamera;
        Quaternion lookRot = Quaternion.LookRotation(spawnPos - playerCamera.transform.position);

        backpackRoot.transform.position = spawnPos;
        backpackRoot.transform.rotation = lookRot;

        // Ensure all existing items have the sync component
        EnsureAllItemsHaveRotationSync();
    }

    public void CloseBackpack()
    {
        if (isDragging)
        {
            StopDragging();
        }

        isMouseDown = false;
        draggedObject = null;
        // backpackRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Ensures all 2D physics items have rotation sync component
    /// </summary>
    void EnsureAllItemsHaveRotationSync()
    {
        Rigidbody2D[] items = backpackRoot.GetComponentsInChildren<Rigidbody2D>();

        foreach (Rigidbody2D rb in items)
        {
            if (!rb.GetComponent<Sync2DRotationWithParent>())
            {
                rb.gameObject.AddComponent<Sync2DRotationWithParent>();
            }
        }

        Debug.Log($"Ensured rotation sync for {items.Length} items");
    }

    void OnDrawGizmos()
    {
        if (isDragging && draggedObject != null && hasValidDragTarget)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(draggedObject.position, dragTargetWorldPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(dragTargetWorldPos, 0.1f);

            Gizmos.color = Color.red;
            Vector2 anchorWorld = draggedObject.transform.TransformPoint(dragAnchorPoint);
            Gizmos.DrawWireSphere(anchorWorld, 0.05f);
        }
    }
}