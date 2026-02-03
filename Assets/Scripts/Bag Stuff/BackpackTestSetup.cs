using UnityEngine;

/// <summary>
/// Helper script to spawn test items in the backpack for testing visual mirroring and drag functionality.
/// Attach to BackpackSystem GameObject.
/// </summary>
public class BackpackTestSetup : MonoBehaviour
{
    [Header("References")]
    public BackpackSurface backpackSurface;

    [Header("Test Item Settings")]
    [Tooltip("Prefab for physics object (with Rigidbody2D)")]
    public GameObject testPhysicsPrefab;

    [Tooltip("Prefab for visual representation")]
    public GameObject testVisualPrefab;

    [Tooltip("Number of test items to spawn")]
    public int numberOfTestItems = 3;

    [Tooltip("Spawn radius around center")]
    public float spawnRadius = 1f;

    [Header("NavAgent Settings")]
    [Tooltip("Prefab for NavAgent (optional)")]
    public GameObject navAgentPrefab;

    [Tooltip("Prefab for NavAgent visual representation")]
    public GameObject navAgentVisualPrefab;

    void Start()
    {
        if (backpackSurface == null)
        {
            backpackSurface = GetComponent<BackpackSurface>();
        }
    }

    void Update()
    {
        // Press T to spawn test items
        if (Input.GetKeyDown(KeyCode.T))
        {
            SpawnTestItems();
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
    }

    void SpawnTestItems()
    {
        if (backpackSurface == null || backpackSurface.physicsWorldRoot == null)
        {
            Debug.LogError("BackpackSurface or PhysicsWorldRoot not assigned!");
            return;
        }

        Debug.Log($"Spawning {numberOfTestItems} test items...");

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
            rb.gravityScale = 0;
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

        Debug.Log("Cleared all test items and NavAgents");
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 350, 250));
        GUILayout.Label("=== Backpack Test Controls ===");
        GUILayout.Label("T - Spawn Test Items");
        GUILayout.Label("N - Spawn NavAgent");
        GUILayout.Label("Tab - Toggle Backpack Open/Close");
        GUILayout.Label("C - Clear All Items");
        GUILayout.Label("Arrow Keys - Apply Bump Force");
        GUILayout.Label("Space - NavAgent Seek New Edge");
        GUILayout.Label("Click & Hold - Drag Items");
        GUILayout.Label("Quick Click - Pickup Item");

        if (backpackSurface != null && backpackSurface.visualPlane != null)
        {
            bool isOpen = backpackSurface.visualPlane.gameObject.activeSelf;
            GUILayout.Label($"Backpack Status: {(isOpen ? "OPEN" : "CLOSED")}");
        }

        // Show NavAgent count
        if (backpackSurface != null && backpackSurface.physicsWorldRoot != null)
        {
            BackpackNavAgent2D[] agents = backpackSurface.physicsWorldRoot.GetComponentsInChildren<BackpackNavAgent2D>();
            GUILayout.Label($"Active NavAgents: {agents.Length}");
        }

        GUILayout.EndArea();
    }
}