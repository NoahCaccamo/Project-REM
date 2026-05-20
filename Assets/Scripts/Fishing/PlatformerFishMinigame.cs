using KinematicCharacterController.Examples;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 2D Platformer-style minigame for fish wrangling.
/// Player controls a cube with A/D for movement and W for jumping.
/// Must collect all pickups spawned in the level to catch the fish.
/// </summary>
public class PlatformerFishMinigame : MonoBehaviour
{
    [Header("Player Settings")]
    public float playerMoveSpeed = 150f;
    public float jumpForce = 400f;
    public float gravity = 800f;
    public Vector2 playerSize = new Vector2(30f, 30f);
    public Color playerColor = Color.cyan;

    [Header("Play Area")]
    public Vector2 playAreaSize = new Vector2(600f, 400f);
    public Color playAreaBorderColor = Color.white;
    public float borderThickness = 5f;

    [Header("Platform Settings")]
    public Vector2 platformSize = new Vector2(100f, 20f);
    public Color platformColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public int platformCount = 5;
    public float maxJumpHeight = 100f; // Maximum height player can jump (calculated from jumpForce/gravity)

    [Header("Pickup Settings")]
    public Vector2 pickupSize = new Vector2(20f, 20f);
    public Color pickupColor = Color.yellow;

    [Header("Fall Penalty")]
    public float fallStaminaDrain = 10f;

    [Header("One-Hand Slowdown")]
    public float oneHandTimeScale = 0.3f;

    [Header("UI References")]
    public Canvas minigameCanvas;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI warningText;

    // Internal state
    private bool isActive = false;
    private FishWrangler currentFish;
    private ExampleCharacterController player;
    private HandStaminaSystem staminaSystem;

    private RectTransform playArea;
    private RectTransform playerCube;
    private List<RectTransform> platforms = new List<RectTransform>();
    private List<RectTransform> pickups = new List<RectTransform>();

    private Vector2 playerVelocity = Vector2.zero;
    private bool isGrounded = false;
    private int pickupsCollected = 0;
    private int totalPickups = 0;

    void Awake()
    {
        SetupUI();
        CalculateMaxJumpHeight();
    }

    private void CalculateMaxJumpHeight()
    {
        // Calculate max jump height using physics: h = v^2 / (2 * g)
        maxJumpHeight = (jumpForce * jumpForce) / (2f * gravity);
    }

    private void SetupUI()
    {
        // Create canvas if it doesn't exist
        if (minigameCanvas == null)
        {
            GameObject canvasObj = new GameObject("PlatformerMinigameCanvas");
            minigameCanvas = canvasObj.AddComponent<Canvas>();
            minigameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create play area container
        GameObject playAreaObj = new GameObject("PlayArea");
        playAreaObj.transform.SetParent(minigameCanvas.transform, false);
        playArea = playAreaObj.AddComponent<RectTransform>();
        playArea.anchorMin = new Vector2(0.5f, 0.5f);
        playArea.anchorMax = new Vector2(0.5f, 0.5f);
        playArea.sizeDelta = playAreaSize;

        // Add border to play area
        Image borderImage = playAreaObj.AddComponent<Image>();
        borderImage.color = new Color(0, 0, 0, 0.8f); // Semi-transparent background

        // Create border outline
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(playArea, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.sizeDelta = Vector2.zero;

        Outline outline = borderObj.AddComponent<Outline>();
        outline.effectColor = playAreaBorderColor;
        outline.effectDistance = new Vector2(borderThickness, borderThickness);

        // Create player cube
        GameObject playerObj = new GameObject("PlayerCube");
        playerObj.transform.SetParent(playArea, false);
        playerCube = playerObj.AddComponent<RectTransform>();
        playerCube.sizeDelta = playerSize;
        playerCube.anchoredPosition = Vector2.zero;

        Image playerImage = playerObj.AddComponent<Image>();
        playerImage.color = playerColor;

        // Create score text
        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(minigameCanvas.transform, false);
        scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.fontSize = 24;
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.color = Color.white;

        RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0.5f, 0.85f);
        scoreRect.anchorMax = new Vector2(0.5f, 0.85f);
        scoreRect.sizeDelta = new Vector2(300f, 50f);

        // Create warning text
        GameObject warningObj = new GameObject("WarningText");
        warningObj.transform.SetParent(minigameCanvas.transform, false);
        warningText = warningObj.AddComponent<TextMeshProUGUI>();
        warningText.fontSize = 32;
        warningText.alignment = TextAlignmentOptions.Center;
        warningText.color = Color.red;
        warningText.fontStyle = FontStyles.Bold;
        warningText.text = "USE BOTH HANDS!";

        RectTransform warningRect = warningObj.GetComponent<RectTransform>();
        warningRect.anchorMin = new Vector2(0.5f, 0.5f);
        warningRect.anchorMax = new Vector2(0.5f, 0.5f);
        warningRect.sizeDelta = new Vector2(400f, 100f);

        warningText.gameObject.SetActive(false);

        minigameCanvas.gameObject.SetActive(false);
    }

    public void StartMinigame(FishWrangler fish, ExampleCharacterController playerController)
    {
        currentFish = fish;
        player = playerController;
        staminaSystem = player.GetComponent<HandStaminaSystem>();

        if (staminaSystem == null)
        {
            staminaSystem = player.gameObject.AddComponent<HandStaminaSystem>();
        }

        isActive = true;
        pickupsCollected = 0;
        totalPickups = currentFish.GetNotesRequiredToCapture();

        // Clear existing level elements
        ClearLevel();

        // Generate level
        GenerateLevel();

        // Reset player position - spawn ABOVE the ground platform
        Vector2 halfPlayArea = playAreaSize * 0.5f;
        float groundPlatformTop = -halfPlayArea.y + 10f + 10f; // Ground Y + half ground height
        playerCube.anchoredPosition = new Vector2(0f, groundPlatformTop + playerSize.y * 0.5f + 5f);
        playerVelocity = Vector2.zero;
        isGrounded = false;

        minigameCanvas.gameObject.SetActive(true);
        UpdateScore();

        Debug.Log("Platformer fish wrangling minigame started!");
    }

    public void EndMinigame(bool success)
    {
        isActive = false;

        // Clear level
        ClearLevel();

        minigameCanvas.gameObject.SetActive(false);

        if (success)
        {
            Debug.Log($"Minigame complete! Pickups collected: {pickupsCollected}");
        }
    }

    private void ClearLevel()
    {
        // Clear platforms
        foreach (var platform in platforms)
        {
            if (platform != null)
            {
                Destroy(platform.gameObject);
            }
        }
        platforms.Clear();

        // Clear pickups
        foreach (var pickup in pickups)
        {
            if (pickup != null)
            {
                Destroy(pickup.gameObject);
            }
        }
        pickups.Clear();
    }

    private void GenerateLevel()
    {
        Vector2 halfPlayArea = playAreaSize * 0.5f;

        // Create ground platform at the bottom
        float groundY = -halfPlayArea.y + 10f;
        CreatePlatform(new Vector2(0f, groundY), new Vector2(playAreaSize.x, 20f));

        // Track the highest accessible platform for sequential generation
        float currentHighestY = groundY + 10f; // Top of ground platform
        float currentX = 0f;

        // Create platforms sequentially, ensuring each is reachable
        for (int i = 0; i < platformCount; i++)
        {
            // Calculate safe jump height (use 80% of max to ensure reachability)
            float safeJumpHeight = maxJumpHeight * 0.8f;

            // Randomly decide height increase (but keep it within jump range)
            float heightIncrease = Random.Range(safeJumpHeight * 0.3f, safeJumpHeight);

            // Calculate new platform Y position
            float newY = currentHighestY + heightIncrease;

            // Ensure we don't exceed play area bounds
            if (newY > halfPlayArea.y - 40f)
            {
                newY = halfPlayArea.y - 40f;
            }

            // Alternate between left and right, or place near center
            float xOffset = Random.Range(-halfPlayArea.x + platformSize.x, halfPlayArea.x - platformSize.x);

            // Ensure horizontal distance is reasonable (not too far from previous platform)
            float maxHorizontalDistance = platformSize.x * 2f;
            if (Mathf.Abs(xOffset - currentX) > maxHorizontalDistance)
            {
                // Clamp to reasonable distance
                xOffset = currentX + Mathf.Sign(xOffset - currentX) * maxHorizontalDistance;
            }

            CreatePlatform(new Vector2(xOffset, newY), platformSize);

            // Update tracking variables
            currentHighestY = newY + platformSize.y * 0.5f; // Top of new platform
            currentX = xOffset;
        }

        // Spawn pickups on platforms
        SpawnPickups();
    }

    private void CreatePlatform(Vector2 position, Vector2 size)
    {
        GameObject platformObj = new GameObject("Platform");
        platformObj.transform.SetParent(playArea, false);

        RectTransform platformRect = platformObj.AddComponent<RectTransform>();
        platformRect.sizeDelta = size;
        platformRect.anchoredPosition = position;

        Image platformImage = platformObj.AddComponent<Image>();
        platformImage.color = platformColor;

        platforms.Add(platformRect);
    }

    private void SpawnPickups()
    {
        int pickupsToSpawn = totalPickups;
        int pickupsSpawned = 0;

        // Ensure we have enough platforms
        if (platforms.Count == 0)
        {
            Debug.LogWarning("No platforms to spawn pickups on!");
            return;
        }

        // Distribute pickups across platforms (skip ground platform at index 0)
        int startPlatformIndex = Mathf.Min(1, platforms.Count - 1);

        while (pickupsSpawned < pickupsToSpawn)
        {
            // Select platform (prefer higher platforms, skip ground)
            int platformIndex = Random.Range(startPlatformIndex, platforms.Count);
            RectTransform platform = platforms[platformIndex];

            // Position pickup above platform
            Vector2 pickupPos = platform.anchoredPosition;
            pickupPos.y += (platform.sizeDelta.y * 0.5f) + (pickupSize.y * 0.5f) + 5f;

            // Add some random horizontal offset (keep within platform bounds)
            float horizontalOffset = Random.Range(-platform.sizeDelta.x * 0.3f, platform.sizeDelta.x * 0.3f);
            pickupPos.x += horizontalOffset;

            CreatePickup(pickupPos);
            pickupsSpawned++;
        }
    }

    private void CreatePickup(Vector2 position)
    {
        GameObject pickupObj = new GameObject("Pickup");
        pickupObj.transform.SetParent(playArea, false);

        RectTransform pickupRect = pickupObj.AddComponent<RectTransform>();
        pickupRect.sizeDelta = pickupSize;
        pickupRect.anchoredPosition = position;

        Image pickupImage = pickupObj.AddComponent<Image>();
        pickupImage.color = pickupColor;

        pickups.Add(pickupRect);
    }

    void Update()
    {
        if (!isActive) return;

        // Check if player can control minigame (both hands grabbing)
        bool canControlMinigame = player.isGrabbingL && player.isGrabbingR;

        // Calculate time scale based on hand count
        float currentTimeScale = canControlMinigame ? 1f : oneHandTimeScale;

        // Show/hide warning text
        if (warningText != null)
        {
            warningText.gameObject.SetActive(!canControlMinigame);
        }

        // Only allow movement if both hands are grabbing
        if (canControlMinigame)
        {
            HandlePlayerMovement(currentTimeScale);
        }
        else
        {
            // Still apply gravity even when not controlling
            ApplyGravity(currentTimeScale);
        }

        // Update physics
        UpdatePhysics(currentTimeScale);

        // Check collisions
        CheckCollisions();

        // Check if minigame is complete
        if (pickupsCollected >= totalPickups)
        {
            if (currentFish != null)
            {
                currentFish.OnNoteHit(); // Trigger final hit to complete capture
            }
        }
    }

    private void HandlePlayerMovement(float timeScale)
    {
        // Horizontal movement
        float horizontalInput = 0f;
        if (Input.GetKey(KeyCode.A)) horizontalInput -= 1f;
        if (Input.GetKey(KeyCode.D)) horizontalInput += 1f;

        playerVelocity.x = horizontalInput * playerMoveSpeed * timeScale;

        // Jumping
        if (Input.GetKeyDown(KeyCode.W) && isGrounded)
        {
            playerVelocity.y = jumpForce;
            isGrounded = false;
        }

        // Apply gravity
        ApplyGravity(timeScale);
    }

    private void ApplyGravity(float timeScale)
    {
        if (!isGrounded)
        {
            playerVelocity.y -= gravity * Time.deltaTime * timeScale;
        }
    }

    private void UpdatePhysics(float timeScale)
    {
        // Store old position
        Vector2 oldPos = playerCube.anchoredPosition;

        // Apply velocity
        playerCube.anchoredPosition += playerVelocity * Time.deltaTime;

        // Check ground collision with platforms
        isGrounded = false;
        foreach (var platform in platforms)
        {
            if (CheckPlatformCollision(platform, oldPos))
            {
                isGrounded = true;
                playerVelocity.y = 0f;
                break;
            }
        }

        // Clamp to play area bounds (horizontal)
        Vector2 halfPlayArea = playAreaSize * 0.5f;
        Vector2 halfPlayer = playerSize * 0.5f;

        Vector2 clampedPos = playerCube.anchoredPosition;
        clampedPos.x = Mathf.Clamp(clampedPos.x, -halfPlayArea.x + halfPlayer.x, halfPlayArea.x - halfPlayer.x);

        // Check if fell off bottom
        if (clampedPos.y < -halfPlayArea.y)
        {
            OnPlayerFell();
            // Respawn on ground platform
            float groundPlatformTop = -halfPlayArea.y + 10f + 10f;
            clampedPos.y = groundPlatformTop + halfPlayer.y + 5f;
            playerVelocity.y = 0f;
            isGrounded = false;
        }

        playerCube.anchoredPosition = clampedPos;
    }

    private bool CheckPlatformCollision(RectTransform platform, Vector2 oldPos)
    {
        Vector2 playerPos = playerCube.anchoredPosition;
        Vector2 platformPos = platform.anchoredPosition;
        Vector2 playerHalf = playerSize * 0.5f;
        Vector2 platformHalf = platform.sizeDelta * 0.5f;

        // Check if horizontally aligned
        bool horizontalOverlap = Mathf.Abs(playerPos.x - platformPos.x) < (playerHalf.x + platformHalf.x);

        if (horizontalOverlap)
        {
            // Check if landing on top of platform
            float playerBottom = playerPos.y - playerHalf.y;
            float platformTop = platformPos.y + platformHalf.y;
            float oldPlayerBottom = oldPos.y - playerHalf.y;

            // Only collide if falling down and crossed the platform top
            if (playerVelocity.y <= 0 && oldPlayerBottom >= platformTop && playerBottom <= platformTop)
            {
                // Snap to platform top
                playerCube.anchoredPosition = new Vector2(playerPos.x, platformTop + playerHalf.y);
                return true;
            }
        }

        return false;
    }

    private void CheckCollisions()
    {
        // Check pickup collisions
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            RectTransform pickup = pickups[i];

            if (RectOverlap(playerCube, pickup))
            {
                OnPickupCollected();
                Destroy(pickup.gameObject);
                pickups.RemoveAt(i);
            }
        }
    }

    private bool RectOverlap(RectTransform a, RectTransform b)
    {
        Vector2 aPos = a.anchoredPosition;
        Vector2 bPos = b.anchoredPosition;
        Vector2 aHalf = a.sizeDelta * 0.5f;
        Vector2 bHalf = b.sizeDelta * 0.5f;

        return Mathf.Abs(aPos.x - bPos.x) < (aHalf.x + bHalf.x) &&
               Mathf.Abs(aPos.y - bPos.y) < (aHalf.y + bHalf.y);
    }

    private void OnPlayerFell()
    {
        // Drain stamina when falling off
        if (staminaSystem != null)
        {
            staminaSystem.DrainStamina(true, fallStaminaDrain);
            staminaSystem.DrainStamina(false, fallStaminaDrain);
        }

        Debug.Log("Fell off! Stamina drained!");
    }

    private void OnPickupCollected()
    {
        pickupsCollected++;
        UpdateScore();

        // Notify fish of progress
        if (currentFish != null)
        {
            currentFish.OnNoteHit();
        }

        Debug.Log($"Pickup collected! {pickupsCollected}/{totalPickups}");
    }

    private void UpdateScore()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Pickups: {pickupsCollected} / {totalPickups}";
        }
    }
}