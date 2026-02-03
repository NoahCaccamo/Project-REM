using KinematicCharacterController.Examples;
using UnityEngine;

/// <summary>
/// Helper script to spawn test items in the backpack for testing visual mirroring and drag functionality.
/// Attach to BackpackSystem GameObject.
/// </summary>
public class BackpackTestSetup : MonoBehaviour
{
    [Header("References")]
    public BackpackSurface backpackSurface;
    public ExampleCharacterController characterController;

    [Header("Test Item Settings")]
    [Tooltip("Prefab for physics object (with Rigidbody2D)")]
    public GameObject testPhysicsPrefab;

    [Tooltip("Prefab for visual representation")]
    public GameObject testVisualPrefab;

    [Tooltip("Number of test items to spawn")]
    public int numberOfTestItems = 3;

    [Tooltip("Spawn radius around center")]
    public float spawnRadius = 1f;

    [Header("World Item Settings")]
    [Tooltip("ItemObject ScriptableObject to use for world items")]
    public ItemObject testItemData;

    [Tooltip("Prefab with ItemPickup component for world spawning")]
    public GameObject worldItemPrefab;

    [Tooltip("Distance from player to spawn world items")]
    public float worldSpawnDistance = 3f;

    [Header("NavAgent Settings")]
    [Tooltip("Prefab for NavAgent (optional)")]
    public GameObject navAgentPrefab;

    [Tooltip("Prefab for NavAgent visual representation")]
    public GameObject navAgentVisualPrefab;

    [Header("Water System")]
    public BackpackWaterSystem waterSystem;

    void Start()
    {
        if (backpackSurface == null)
        {
            backpackSurface = GetComponent<BackpackSurface>();
        }

        if (characterController == null)
        {
            characterController = FindObjectOfType<ExampleCharacterController>();
        }
    }

    void Update()
    {
        // Press T to spawn test items IN inventory
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnTestItems();
        }

        // Press Y to spawn item in WORLD (for pickup testing)
        if (Input.GetKeyDown(KeyCode.Y))
        {
            SpawnWorldItem();
        }

        // Press Tab to toggle backpack
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleBackpack();
        }

        // Press C to clear all items
        if (Input.GetKeyDown(KeyCode.C))
        {
            ClearAllItems();
        }

        // Press N to spawn NavAgent
        if (Input.GetKeyDown(KeyCode.N))
        {
            SpawnNavAgent();
        }

        // Press Q to toggle water
        if (Input.GetKeyDown(KeyCode.Q))
        {
            if (waterSystem != null)
            {
                waterSystem.ToggleWaterContact();
            }
        }

        // Press H to transfer item from hand to inventory
        if (Input.GetKeyDown(KeyCode.H))
        {
            TransferHandItemToInventory();
        }
    }

    void SpawnTestItems()
    {
        if (backpackSurface == null || backpackSurface.physicsWorldRoot == null)
        {
            Debug.LogError("BackpackSurface or PhysicsWorldRoot not assigned!");
            return;
        }

        Debug.Log($"Spawning {numberOfTestItems} test items IN INVENTORY...");

        for (int i = 0; i < numberOfTestItems; i++)
        {
            // Random position in circle
            Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = new Vector3(randomPos.x, randomPos.y, 0);

            GameObject physicsObj;

            // Create physics object
            if (testPhysicsPrefab != null)
            {
                physicsObj = Instantiate(testPhysicsPrefab, backpackSurface.physicsWorldRoot);
            }
            else
            {
                // Create default test object
                physicsObj = CreateDefaultTestObject();
            }

            physicsObj.transform.localPosition = spawnPos;
            physicsObj.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            physicsObj.name = $"TestItem_{i}";

            // Get or add Rigidbody2D
            Rigidbody2D rb = physicsObj.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = physicsObj.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0;
                rb.linearDamping = 1f;
            }

            // Add ItemPickup component with test data
            ItemPickup pickup = physicsObj.GetComponent<ItemPickup>();
            if (pickup == null && testItemData != null)
            {
                pickup = physicsObj.AddComponent<ItemPickup>();
                // Note: You'll need to make ItemPickup.itemData settable or add a SetItemData method
            }

            // Create visual representation
            if (testVisualPrefab != null)
            {
                backpackSurface.CreateVisualForPhysicsObject(rb, testVisualPrefab);
            }
            else
            {
                // Create default visual
                GameObject defaultVisual = CreateDefaultVisualObject();
                backpackSurface.CreateVisualForPhysicsObject(rb, defaultVisual);
                DestroyImmediate(defaultVisual); // Remove the template
            }
        }

        Debug.Log($"Spawned {numberOfTestItems} test items successfully!");
    }

    /// <summary>
    /// Spawns a pickupable item in the world in front of the player
    /// </summary>
    void SpawnWorldItem()
    {
        if (characterController == null)
        {
            Debug.LogError("Character controller not found!");
            return;
        }

        Camera playerCamera = Camera.main;
        if (playerCamera == null)
        {
            Debug.LogError("Main camera not found!");
            return;
        }

        // Calculate spawn position in front of player
        Vector3 spawnPosition = characterController.transform.position +
                               playerCamera.transform.forward * worldSpawnDistance +
                               Vector3.up * 1.5f; // At chest height

        GameObject worldItem;

        if (worldItemPrefab != null)
        {
            // Use provided prefab
            worldItem = Instantiate(worldItemPrefab, spawnPosition, Quaternion.identity);
        }
        else
        {
            // Create default world item
            worldItem = CreateDefaultWorldItem(spawnPosition);
        }

        Debug.Log($"Spawned world item at {spawnPosition} for pickup testing");
    }

    /// <summary>
    /// Creates a default pickupable world item with ItemPickup component
    /// </summary>
    GameObject CreateDefaultWorldItem(Vector3 position)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.position = position;
        obj.transform.localScale = Vector3.one * 0.5f;
        obj.name = "WorldTestItem";

        // Add Rigidbody for physics
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.mass = 1f;

        // Make it a nice color
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(1f, 0.8f, 0.2f); // Gold color
            renderer.material = mat;
        }

        // Add ItemPickup component
        ItemPickup pickup = obj.AddComponent<ItemPickup>();
        if (testItemData != null)
        {
            // If you have SetItemData method:
            // pickup.SetItemData(testItemData);

            // Otherwise, you'll need to set it via reflection or make itemData public
            Debug.Log("Added ItemPickup component - assign ItemData in inspector or via SetItemData method");
        }

        // Set to interactable layer
        obj.layer = LayerMask.NameToLayer("Interactable");

        return obj;
    }

    /// <summary>
    /// Transfers item from player's hand to the inventory
    /// </summary>
    void TransferHandItemToInventory()
    {
        if (characterController == null || backpackSurface == null)
        {
            Debug.LogError("Missing references for hand-to-inventory transfer!");
            return;
        }

        // Check left hand first
        Hand sourceHand = null;
        if (!characterController.leftHand.IsEmpty)
        {
            sourceHand = characterController.leftHand;
        }
        else if (!characterController.rightHand.IsEmpty)
        {
            sourceHand = characterController.rightHand;
        }
        else
        {
            Debug.Log("Both hands are empty - nothing to transfer!");
            return;
        }

        // Get the item data
        ItemObject itemData = sourceHand.heldItem;

        if (itemData == null || itemData.bagPrefabPhysics == null)
        {
            Debug.LogError("Hand item has no data or prefab!");
            return;
        }

        // Spawn in physics world at center
        Vector3 spawnPos = Vector3.zero; // Center of backpack
        GameObject physicsObj = Instantiate(itemData.bagPrefabPhysics, backpackSurface.physicsWorldRoot);
        physicsObj.transform.localPosition = spawnPos;
        physicsObj.transform.localRotation = Quaternion.identity;
        physicsObj.name = $"TransferredItem_{itemData.name}";

        // Setup physics
        Rigidbody2D rb = physicsObj.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = physicsObj.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 1;
        rb.linearDamping = 1f;
        rb.isKinematic = false;

        // Add ItemPickup if missing
        ItemPickup pickup = physicsObj.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            pickup = physicsObj.AddComponent<ItemPickup>();
        }

        // Create visual if altPrefab exists
        if (itemData.altPrefab != null)
        {
            backpackSurface.CreateVisualForPhysicsObject(rb, itemData.bagPrefabVisual);
        }
        else
        {
            Debug.LogWarning($"No visual prefab (altPrefab) for {itemData.name}");
        }

        // Add to inventory data
        var slot = new InventorySlot(itemData, 1);
        slot.localPosition = spawnPos;
        slot.localRotation = Quaternion.identity;
        backpackSurface.inventoryData.Container.Add(slot);

        // Drop from hand
        sourceHand.Drop();

        Debug.Log($"Transferred {itemData.name} from hand to inventory");
    }

    void SpawnNavAgent()
    {
        if (backpackSurface == null || backpackSurface.physicsWorldRoot == null)
        {
            Debug.LogError("BackpackSurface or PhysicsWorldRoot not assigned!");
            return;
        }

        Debug.Log("Spawning NavAgent...");

        GameObject agentPhysicsObj;

        // Create physics agent object
        if (navAgentPrefab != null)
        {
            agentPhysicsObj = Instantiate(navAgentPrefab, backpackSurface.physicsWorldRoot);
        }
        else
        {
            // Create default agent
            agentPhysicsObj = CreateDefaultNavAgent();
        }

        // Position at center
        agentPhysicsObj.transform.localPosition = Vector3.zero;
        agentPhysicsObj.name = "NavAgent";

        // Ensure it has required components
        Rigidbody2D rb = agentPhysicsObj.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = agentPhysicsObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1;
            rb.linearDamping = 1f;
        }

        // Add NavAgent component if not already present
        BackpackNavAgent2D navAgent = agentPhysicsObj.GetComponent<BackpackNavAgent2D>();
        if (navAgent == null)
        {
            navAgent = agentPhysicsObj.AddComponent<BackpackNavAgent2D>();
        }

        // Configure NavAgent
        navAgent.backpackSurface = backpackSurface;
        navAgent.moveSpeed = 2f;
        navAgent.maxForce = 10f;

        // Create visual representation
        if (navAgentVisualPrefab != null)
        {
            backpackSurface.CreateVisualForPhysicsObject(rb, navAgentVisualPrefab);
        }
        else
        {
            // Create default visual (different color to distinguish from items)
            GameObject defaultVisual = CreateDefaultNavAgentVisual();
            backpackSurface.CreateVisualForPhysicsObject(rb, defaultVisual);
            DestroyImmediate(defaultVisual);
        }

        // Automatically start seeking edge
        navAgent.SetTargetToEdge();

        Debug.Log("NavAgent spawned successfully! Press SPACE to set new target.");
    }

    GameObject CreateDefaultNavAgent()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.transform.localScale = Vector3.one * 0.4f;

        // Remove 3D collider, add 2D
        Destroy(obj.GetComponent<SphereCollider>());
        CircleCollider2D collider2D = obj.AddComponent<CircleCollider2D>();
        collider2D.radius = 0.2f;

        // Add Rigidbody2D
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.linearDamping = 1f;

        // Distinctive color (cyan for agent)
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.cyan;
        }

        obj.layer = LayerMask.NameToLayer("BackpackPhysics");

        return obj;
    }

    GameObject CreateDefaultNavAgentVisual()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.transform.localScale = Vector3.one * 0.4f;

        // Keep 3D collider for raycasting
        SphereCollider collider = obj.GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.radius = 0.2f;
        }

        // Bright cyan color
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0f, 1f, 1f, 1f); // Bright cyan
        }

        obj.layer = LayerMask.NameToLayer("BackpackVisuals");

        return obj;
    }

    GameObject CreateDefaultTestObject()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.localScale = Vector3.one * 0.3f;

        // Remove 3D collider, add 2D
        Destroy(obj.GetComponent<BoxCollider>());
        BoxCollider2D collider2D = obj.AddComponent<BoxCollider2D>();
        collider2D.size = new Vector2(0.3f, 0.3f);

        // Add Rigidbody2D
        Rigidbody2D rb = obj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.linearDamping = 1f;

        // Random color
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = Random.ColorHSV();
        }

        obj.layer = LayerMask.NameToLayer("BackpackPhysics");

        return obj;
    }

    GameObject CreateDefaultVisualObject()
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.transform.localScale = Vector3.one * 0.3f;

        // Keep 3D collider for raycasting
        BoxCollider collider = obj.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.size = new Vector3(0.3f, 0.3f, 0.05f);
        }

        // Random color (slightly brighter than physics version)
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material.color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
        }

        obj.layer = LayerMask.NameToLayer("BackpackVisuals");

        return obj;
    }

    void ToggleBackpack()
    {
        if (backpackSurface == null) return;

        if (backpackSurface.visualPlane != null && backpackSurface.visualPlane.gameObject.activeSelf)
        {
            backpackSurface.CloseBackpack();
            Debug.Log("Backpack closed");
        }
        else
        {
            backpackSurface.OpenBackpack();
            Debug.Log("Backpack opened");
        }
    }

    void ClearAllItems()
    {
        if (backpackSurface == null || backpackSurface.physicsWorldRoot == null)
        {
            Debug.LogError("BackpackSurface or PhysicsWorldRoot not assigned!");
            return;
        }

        // Clear physics objects (including NavAgents)
        Rigidbody2D[] physicsObjects = backpackSurface.physicsWorldRoot.GetComponentsInChildren<Rigidbody2D>();
        foreach (Rigidbody2D rb in physicsObjects)
        {
            Destroy(rb.gameObject);
        }

        // Clear visual objects
        if (backpackSurface.visualPlane != null)
        {
            BackpackItemVisual[] visuals = backpackSurface.visualPlane.GetComponentsInChildren<BackpackItemVisual>();
            foreach (BackpackItemVisual visual in visuals)
            {
                Destroy(visual.gameObject);
            }
        }

        // Clear inventory data
        if (backpackSurface.inventoryData != null)
        {
            backpackSurface.inventoryData.Container.Clear();
        }

        Debug.Log("Cleared all test items and NavAgents");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 320));
        GUILayout.Label("=== Backpack Test Controls ===");
        GUILayout.Label("T - Spawn Test Items IN Inventory");
        GUILayout.Label("Y - Spawn World Item (for pickup)");
        GUILayout.Label("H - Transfer Hand Item to Inventory");
        GUILayout.Label("N - Spawn NavAgent");
        GUILayout.Label("Tab - Toggle Backpack Open/Close");
        GUILayout.Label("C - Clear All Items");
        GUILayout.Label("Q - Toggle Water Contact");
        GUILayout.Label("Arrow Keys - Apply Bump Force");
        GUILayout.Label("Space - NavAgent Seek New Edge");
        GUILayout.Label("Click & Hold - Drag Items");
        GUILayout.Label("Quick Click - Pickup Item from Inventory");
        GUILayout.Label("Left Click (on world item) - Pickup to Hand");

        GUILayout.Label(""); // Spacer

        if (backpackSurface != null && backpackSurface.visualPlane != null)
        {
            bool isOpen = backpackSurface.visualPlane.gameObject.activeSelf;
            GUILayout.Label($"Backpack Status: {(isOpen ? "OPEN" : "CLOSED")}");
        }

        // Show hand status
        if (characterController != null)
        {
            string leftHandStatus = characterController.leftHand.IsEmpty ? "Empty" : characterController.leftHand.heldItem.name;
            string rightHandStatus = characterController.rightHand.IsEmpty ? "Empty" : characterController.rightHand.heldItem.name;
            GUILayout.Label($"Left Hand: {leftHandStatus}");
            GUILayout.Label($"Right Hand: {rightHandStatus}");
        }

        // Show NavAgent count
        if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            BackpackNavAgent2D[] agents = backpackSurface.physicsWorldRoot.GetComponentsInChildren<BackpackNavAgent2D>();
            GUILayout.Label($"Active NavAgents: {agents.Length}");
        }

        if (waterSystem != null)
        {
            GUILayout.Label($"Water Contact: {(waterSystem.waterContactEnabled ? "ON" : "OFF")}");
            GUILayout.Label($"Water Level: {(waterSystem.waterLevel * 100f):F1}%");
        }

        GUILayout.EndArea();
    }
}