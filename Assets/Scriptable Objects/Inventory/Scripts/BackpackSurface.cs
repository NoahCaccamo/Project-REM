using UnityEngine;
using KinematicCharacterController.Examples;
using System.Collections.Generic;

public class BackpackSurface : MonoBehaviour
{
    public InventoryObject inventoryData;

    [Header("World References")]
    [Tooltip("Root of the 2D physics world (stays at fixed position/rotation)")]
    public Transform physicsWorldRoot;

    [Tooltip("Visual plane that displays to the player")]
    public Transform visualPlane;

    [Tooltip("Collider on the visual plane for raycasting")]
    public Collider visualPlaneCollider;

    public ExampleCharacterController characterController;

    private Camera playerCamera;

    [Header("Physics World Settings")]
    [Tooltip("Fixed position for the physics world")]
    public Vector3 physicsWorldPosition = new Vector3(0, -1000, 0);

    [Header("Visual Plane Settings")]
    [Tooltip("Distance from player when viewing inventory")]
    public float visualPlaneDistance = 2f;

    [Tooltip("Fixed rotation for the visual plane (independent of camera)")]
    public Quaternion fixedRotation = Quaternion.identity;

    [Tooltip("Offset from player position")]
    public Vector3 offsetFromPlayer = new Vector3(0, 1.5f, 0);

    private bool isInventoryOpen = false;
    private Vector3 initialForwardDirection;

    [Header("Bump Force Settings")]
    public float bumpForce = 500f;

    [Header("Click & Drag Settings")]
    public float clickHoldThreshold = 0.3f;
    public float dragForceMultiplier = 50f;
    public float maxDragForce = 100f;
    public float dragDamping = 0.95f;
    public float maxRaycastDistance = 10f;
    public LayerMask visualPlaneLayerMask = -1;

    // Drag state tracking
    private bool isMouseDown = false;
    private float mouseDownTime = 0f;
    private bool isDragging = false;
    private Rigidbody2D draggedPhysicsObject = null;
    private BackpackItemVisual draggedVisual = null;
    private Vector2 dragAnchorPoint;
    private Vector2 dragTargetPhysicsPos;
    private bool hasValidDragTarget = false;

    // Item tracking
    private Dictionary<Rigidbody2D, BackpackItemVisual> physicsToVisualMap = new Dictionary<Rigidbody2D, BackpackItemVisual>();

    void Start()
    {
        playerCamera = Camera.main;
        characterController = FindObjectOfType<ExampleCharacterController>();

        // Initialize physics world at fixed position
        if (physicsWorldRoot != null)
        {
            physicsWorldRoot.position = physicsWorldPosition;
            physicsWorldRoot.rotation = Quaternion.identity;
        }

        // Visual plane starts hidden
        if (visualPlane != null)
        {
            visualPlane.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        HandleClickAndDrag();
        HandleBumpForceInput();

        // Update visual plane to follow player position (but not camera rotation)
        if (isInventoryOpen)
        {
            UpdateVisualPlanePosition();
        }
    }

    /// <summary>
    /// Updates visual plane to follow player position while maintaining fixed rotation
    /// </summary>
    void UpdateVisualPlanePosition()
    {
        if (visualPlane == null || characterController == null) return;

        // Get player position
        Vector3 playerPosition = characterController.transform.position;

        // Calculate position in front of player using the initial forward direction (locked)
        Vector3 targetPosition = playerPosition + (initialForwardDirection * visualPlaneDistance) + offsetFromPlayer;

        // Update position (follows player movement)
        visualPlane.position = targetPosition;

        // Maintain fixed rotation (doesn't rotate with camera)
        visualPlane.rotation = fixedRotation;
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

            if (holdDuration >= clickHoldThreshold && !isDragging && draggedPhysicsObject != null)
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
            else if (draggedPhysicsObject != null)
            {
                TryPickupDraggedObject();
            }

            isMouseDown = false;
            draggedPhysicsObject = null;
            draggedVisual = null;
        }
    }

    void TryStartDragOrPickup()
    {
        // Raycast against the visual plane
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, visualPlaneLayerMask))
        {
            // Check if we hit a visual representation
            BackpackItemVisual visual = hit.collider.GetComponent<BackpackItemVisual>();
            if (visual != null && visual.physicsObject != null)
            {
                draggedPhysicsObject = visual.physicsObject;
                draggedVisual = visual;

                // Map the hit point to physics space
                Vector2 physicsHitPoint = visual.MapVisualToPhysics(hit.point);

                // Store anchor in physics object's local space
                dragAnchorPoint = draggedPhysicsObject.transform.InverseTransformPoint(physicsHitPoint);

                Debug.Log($"Detected potential drag target: {visual.name}");
            }
        }
    }

    void StartDragging()
    {
        isDragging = true;
        Debug.Log($"Started dragging: {draggedPhysicsObject.name}");

        if (characterController != null)
        {
            characterController.leftHand.controller.StartGrab(draggedVisual.transform.position);
        }
    }

    void UpdateDragTarget()
    {
        if (draggedPhysicsObject == null || draggedVisual == null) return;

        // Raycast against the visual plane
        Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, visualPlaneLayerMask))
        {
            // Map the visual hit point to physics space
            Vector2 physicsHitPoint = draggedVisual.MapVisualToPhysics(hit.point);

            // Account for anchor offset
            Vector2 anchorWorldOffset = draggedPhysicsObject.transform.TransformDirection(dragAnchorPoint);
            dragTargetPhysicsPos = physicsHitPoint - anchorWorldOffset;

            hasValidDragTarget = true;

            if (characterController != null)
            {
                characterController.leftHand.controller.StartGrab(draggedVisual.transform.position);
            }
        }
        else
        {
            hasValidDragTarget = false;
            Debug.LogWarning("Drag target lost - no visual plane hit");
        }
    }

    void FixedUpdate()
    {
        if (isDragging && draggedPhysicsObject != null && hasValidDragTarget)
        {
            ApplyDragForce();
        }
    }

    void ApplyDragForce()
    {
        if (draggedPhysicsObject == null) return;

        Vector2 currentPos = draggedPhysicsObject.position;
        Vector2 toTarget = dragTargetPhysicsPos - currentPos;
        float distance = toTarget.magnitude;

        if (distance > 0.01f)
        {
            Vector2 direction = toTarget.normalized;
            float forceMagnitude = distance * dragForceMultiplier;
            forceMagnitude = Mathf.Min(forceMagnitude, maxDragForce);

            Vector2 force = direction * forceMagnitude;
            draggedPhysicsObject.AddForce(force, ForceMode2D.Force);
            draggedPhysicsObject.linearVelocity *= dragDamping;
        }
    }

    void StopDragging()
    {
        if (draggedPhysicsObject != null)
        {
            Debug.Log($"Stopped dragging: {draggedPhysicsObject.name}");
            draggedPhysicsObject.linearVelocity *= 0.5f;
        }

        isDragging = false;
        draggedPhysicsObject = null;
        draggedVisual = null;
        hasValidDragTarget = false;

        if (characterController != null)
        {
            characterController.leftHand.controller.StopGrab();
        }
    }

    void TryPickupDraggedObject()
    {
        if (draggedPhysicsObject == null) return;

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

        ItemPickup pickup = draggedPhysicsObject.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            targetHand.PickUp(pickup);

            // Remove visual representation
            if (draggedVisual != null)
            {
                physicsToVisualMap.Remove(draggedPhysicsObject);
                Destroy(draggedVisual.gameObject);
            }

            Debug.Log($"Picked up {pickup.ItemData.name} with quick click");
        }
        else
        {
            Debug.LogWarning($"Object {draggedPhysicsObject.name} doesn't have ItemPickup component");
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
        if (physicsWorldRoot == null) return;

        Rigidbody2D[] items = physicsWorldRoot.GetComponentsInChildren<Rigidbody2D>();

        foreach (Rigidbody2D rb in items)
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForce(direction * bumpForce, ForceMode2D.Impulse);
            }
        }

        Debug.Log($"Applied bump force to {items.Length} items in direction {direction}");
    }

    public BackpackItemVisual CreateVisualForPhysicsObject(Rigidbody2D physicsObject, GameObject visualPrefab)
    {
        if (physicsObject == null || visualPrefab == null || visualPlane == null)
        {
            Debug.LogError("Cannot create visual - missing references");
            return null;
        }

        // Instantiate visual
        GameObject visualObj = Instantiate(visualPrefab, visualPlane);

        // Add visual component
        BackpackItemVisual visual = visualObj.AddComponent<BackpackItemVisual>();
        visual.physicsObject = physicsObject;
        visual.visualPlane = visualPlane;
        visual.physicsWorldRoot = physicsWorldRoot;

        // Add collider for raycasting if it doesn't have one
        if (visualObj.GetComponent<Collider>() == null)
        {
            BoxCollider collider = visualObj.AddComponent<BoxCollider>();
        }

        // Track the mapping
        physicsToVisualMap[physicsObject] = visual;

        return visual;
    }

    void PlaceItemAt(RaycastHit hit, Hand hand)
    {
        var itemData = hand.heldItem;

        // Map visual hit point to physics space
        Vector3 visualHitPoint = hit.point;
        Vector3 localPoint = visualPlane.InverseTransformPoint(visualHitPoint);
        Vector2 physicsLocalPos = new Vector2(localPoint.x, localPoint.y);

        var slot = new InventorySlot(itemData, 1);
        slot.localPosition = physicsLocalPos;
        slot.localRotation = Quaternion.identity;
        inventoryData.Container.Add(slot);

        // Create physics object in physics world
        GameObject physicsObj = Instantiate(itemData.prefab, physicsWorldRoot);
        physicsObj.transform.localPosition = new Vector3(physicsLocalPos.x, physicsLocalPos.y, 0);
        physicsObj.transform.localRotation = Quaternion.identity;

        Rigidbody2D rb = physicsObj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
        }

        // Create visual representation
        if (itemData.altPrefab != null)
        {
            CreateVisualForPhysicsObject(rb, itemData.altPrefab);
        }
        else
        {
            Debug.LogWarning($"No visual prefab (altPrefab) set for {itemData.name}");
        }

        hand.Drop();
    }

    public void OpenBackpack()
    {
        if (visualPlane == null) return;

        visualPlane.gameObject.SetActive(true);
        isInventoryOpen = true;

        // Store the camera's forward direction at the moment of opening (this locks the direction)
        initialForwardDirection = playerCamera.transform.forward;
        initialForwardDirection.y = 0; // Keep it horizontal
        initialForwardDirection.Normalize();

        // Set fixed rotation to face the camera's initial direction
        fixedRotation = Quaternion.LookRotation(initialForwardDirection, Vector3.up);

        // Initial position setup
        Vector3 playerPosition = characterController.transform.position;
        Vector3 spawnPos = playerPosition + (initialForwardDirection * visualPlaneDistance) + offsetFromPlayer;

        visualPlane.position = spawnPos;
        visualPlane.rotation = fixedRotation;

        // Ensure all physics objects have visual representations
        SyncAllVisualsWithPhysics();

        Debug.Log("Backpack opened - following player position, rotation locked");
    }

    public void CloseBackpack()
    {
        if (isDragging)
        {
            StopDragging();
        }

        isMouseDown = false;
        draggedPhysicsObject = null;
        draggedVisual = null;
        isInventoryOpen = false;

        if (visualPlane != null)
        {
            visualPlane.gameObject.SetActive(false);
        }

        Debug.Log("Backpack closed - physics world still running");
    }

    void SyncAllVisualsWithPhysics()
    {
        if (physicsWorldRoot == null || visualPlane == null) return;

        Rigidbody2D[] physicsObjects = physicsWorldRoot.GetComponentsInChildren<Rigidbody2D>();

        foreach (Rigidbody2D rb in physicsObjects)
        {
            // Skip if already has visual
            if (physicsToVisualMap.ContainsKey(rb)) continue;

            // Try to get item data to create visual
            ItemPickup pickup = rb.GetComponent<ItemPickup>();
            if (pickup != null && pickup.ItemData != null && pickup.ItemData.altPrefab != null)
            {
                CreateVisualForPhysicsObject(rb, pickup.ItemData.altPrefab);
            }
        }

        Debug.Log($"Synced {physicsToVisualMap.Count} visual representations");
    }

    void OnDrawGizmos()
    {
        if (isDragging && draggedPhysicsObject != null && hasValidDragTarget)
        {
            // Draw in physics space
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(draggedPhysicsObject.position, dragTargetPhysicsPos);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(dragTargetPhysicsPos, 0.1f);

            Gizmos.color = Color.red;
            Vector2 anchorWorld = draggedPhysicsObject.transform.TransformPoint(dragAnchorPoint);
            Gizmos.DrawWireSphere(anchorWorld, 0.05f);
        }

        // Draw physics world bounds
        if (physicsWorldRoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(physicsWorldRoot.position, Vector3.one * 2f);
        }

        // Draw visual plane
        if (visualPlane != null && visualPlane.gameObject.activeSelf)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(visualPlane.position, new Vector3(2f, 2f, 0.1f));
        }
    }
}