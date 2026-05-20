using KinematicCharacterController.Examples;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bullet hell-style minigame for fish wrangling (Undertale-inspired).
/// Player controls a cube with WASD and must dodge bullets while collecting pickups.
/// Getting hit drains stamina. Collecting X pickups catches the fish.
/// </summary>
public class BulletHellFishMinigame : MonoBehaviour
{
    [Header("Minigame Settings")]
    public float bulletHitStaminaDrain = 15f;

    [Header("Player Settings")]
    public float playerMoveSpeed = 200f;
    public Vector2 playerSize = new Vector2(30f, 30f);
    public Color playerColor = Color.white;

    [Header("Play Area")]
    public Vector2 playAreaSize = new Vector2(400f, 400f);
    public Color playAreaBorderColor = Color.white;
    public float borderThickness = 5f;

    [Header("Bullet Settings")]
    public float bulletSpawnInterval = 0.5f;
    public float bulletSpeed = 150f;
    public Vector2 bulletSize = new Vector2(20f, 20f);
    public Color bulletColor = Color.red;
    public int bulletsPerSpawn = 1;

    [Header("Pickup Settings")]
    public Vector2 pickupSize = new Vector2(25f, 25f);
    public Color pickupColor = Color.green;

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
    private RectTransform currentPickup;

    private List<Bullet> activeBullets = new List<Bullet>();
    private float bulletSpawnTimer = 0f;
    private int pickupsCollected = 0;

    private Vector2 playerVelocity = Vector2.zero;

    private class Bullet
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Vector2 velocity;
        public Image image;
    }

    void Awake()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        // Create canvas if it doesn't exist
        if (minigameCanvas == null)
        {
            GameObject canvasObj = new GameObject("BulletHellMinigameCanvas");
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
        activeBullets.Clear();
        bulletSpawnTimer = 0f;

        // Reset player position
        playerCube.anchoredPosition = Vector2.zero;
        playerVelocity = Vector2.zero;

        minigameCanvas.gameObject.SetActive(true);
        SpawnPickup();
        UpdateScore();

        Debug.Log("Bullet hell fish wrangling minigame started!");
    }

    public void EndMinigame(bool success)
    {
        isActive = false;

        // Clear all bullets
        foreach (var bullet in activeBullets)
        {
            if (bullet.gameObject != null)
            {
                Destroy(bullet.gameObject);
            }
        }
        activeBullets.Clear();

        // Clear pickup
        if (currentPickup != null)
        {
            Destroy(currentPickup.gameObject);
            currentPickup = null;
        }

        minigameCanvas.gameObject.SetActive(false);

        if (success)
        {
            Debug.Log($"Minigame complete! Pickups collected: {pickupsCollected}");
        }
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

        // Spawn bullets (affected by time scale)
        bulletSpawnTimer -= Time.deltaTime * currentTimeScale;
        if (bulletSpawnTimer <= 0f)
        {
            SpawnBullets();
            bulletSpawnTimer = bulletSpawnInterval;
        }

        // Update bullets (affected by time scale)
        UpdateBullets(currentTimeScale);

        // Check collisions
        CheckCollisions();

        // Check if minigame is complete
        if (currentFish != null && pickupsCollected >= currentFish.GetNotesRequiredToCapture())
        {
            currentFish.OnNoteHit(); // Trigger final hit to complete capture
        }
    }

    private void HandlePlayerMovement(float timeScale)
    {
        // Get input
        Vector2 input = Vector2.zero;
        if (Input.GetKey(KeyCode.W)) input.y += 1f;
        if (Input.GetKey(KeyCode.S)) input.y -= 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;

        // Normalize diagonal movement
        if (input.magnitude > 1f)
        {
            input.Normalize();
        }

        // Move player
        playerVelocity = input * playerMoveSpeed * timeScale;
        playerCube.anchoredPosition += playerVelocity * Time.deltaTime;

        // Clamp to play area bounds
        Vector2 halfPlayArea = playAreaSize * 0.5f;
        Vector2 halfPlayer = playerSize * 0.5f;

        Vector2 clampedPos = playerCube.anchoredPosition;
        clampedPos.x = Mathf.Clamp(clampedPos.x, -halfPlayArea.x + halfPlayer.x, halfPlayArea.x - halfPlayer.x);
        clampedPos.y = Mathf.Clamp(clampedPos.y, -halfPlayArea.y + halfPlayer.y, halfPlayArea.y - halfPlayer.y);
        playerCube.anchoredPosition = clampedPos;
    }

    private void SpawnBullets()
    {
        for (int i = 0; i < bulletsPerSpawn; i++)
        {
            // Spawn from random edge
            Vector2 spawnPos = GetRandomEdgePosition();
            Vector2 targetPos = GetRandomPlayAreaPosition();
            Vector2 direction = (targetPos - spawnPos).normalized;

            GameObject bulletObj = new GameObject("Bullet");
            bulletObj.transform.SetParent(playArea, false);

            RectTransform bulletRect = bulletObj.AddComponent<RectTransform>();
            bulletRect.sizeDelta = bulletSize;
            bulletRect.anchoredPosition = spawnPos;

            Image bulletImage = bulletObj.AddComponent<Image>();
            bulletImage.color = bulletColor;

            Bullet bullet = new Bullet
            {
                gameObject = bulletObj,
                rectTransform = bulletRect,
                velocity = direction * bulletSpeed,
                image = bulletImage
            };

            activeBullets.Add(bullet);
        }
    }

    private Vector2 GetRandomEdgePosition()
    {
        Vector2 halfArea = playAreaSize * 0.5f;
        int edge = Random.Range(0, 4); // 0: top, 1: right, 2: bottom, 3: left

        switch (edge)
        {
            case 0: // Top
                return new Vector2(Random.Range(-halfArea.x, halfArea.x), halfArea.y);
            case 1: // Right
                return new Vector2(halfArea.x, Random.Range(-halfArea.y, halfArea.y));
            case 2: // Bottom
                return new Vector2(Random.Range(-halfArea.x, halfArea.x), -halfArea.y);
            case 3: // Left
                return new Vector2(-halfArea.x, Random.Range(-halfArea.y, halfArea.y));
            default:
                return Vector2.zero;
        }
    }

    private Vector2 GetRandomPlayAreaPosition()
    {
        Vector2 halfArea = playAreaSize * 0.5f;
        return new Vector2(
            Random.Range(-halfArea.x, halfArea.x),
            Random.Range(-halfArea.y, halfArea.y)
        );
    }

    private void UpdateBullets(float timeScale)
    {
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = activeBullets[i];

            // Move bullet
            bullet.rectTransform.anchoredPosition += bullet.velocity * Time.deltaTime * timeScale;

            // Remove if out of bounds
            Vector2 halfArea = playAreaSize * 0.5f + bulletSize;
            if (Mathf.Abs(bullet.rectTransform.anchoredPosition.x) > halfArea.x ||
                Mathf.Abs(bullet.rectTransform.anchoredPosition.y) > halfArea.y)
            {
                Destroy(bullet.gameObject);
                activeBullets.RemoveAt(i);
            }
        }
    }

    private void SpawnPickup()
    {
        // Destroy old pickup if it exists
        if (currentPickup != null)
        {
            Destroy(currentPickup.gameObject);
        }

        // Create new pickup at random position
        GameObject pickupObj = new GameObject("Pickup");
        pickupObj.transform.SetParent(playArea, false);

        currentPickup = pickupObj.AddComponent<RectTransform>();
        currentPickup.sizeDelta = pickupSize;
        currentPickup.anchoredPosition = GetRandomPlayAreaPosition();

        Image pickupImage = pickupObj.AddComponent<Image>();
        pickupImage.color = pickupColor;
    }

    private void CheckCollisions()
    {
        // Check bullet collisions with player
        for (int i = activeBullets.Count - 1; i >= 0; i--)
        {
            Bullet bullet = activeBullets[i];

            if (RectOverlap(playerCube, bullet.rectTransform))
            {
                OnBulletHit();
                Destroy(bullet.gameObject);
                activeBullets.RemoveAt(i);
            }
        }

        // Check pickup collision with player
        if (currentPickup != null && RectOverlap(playerCube, currentPickup))
        {
            OnPickupCollected();
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

    private void OnBulletHit()
    {
        // Drain stamina on bullet hit
        if (staminaSystem != null)
        {
            staminaSystem.DrainStamina(true, bulletHitStaminaDrain);
            staminaSystem.DrainStamina(false, bulletHitStaminaDrain);
        }

        Debug.Log("Hit by bullet! Stamina drained!");
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

        // Spawn next pickup if not complete
        if (currentFish != null && pickupsCollected < currentFish.GetNotesRequiredToCapture())
        {
            SpawnPickup();
        }

        Debug.Log($"Pickup collected! {pickupsCollected}/{currentFish.GetNotesRequiredToCapture()}");
    }

    private void UpdateScore()
    {
        if (scoreText != null && currentFish != null)
        {
            scoreText.text = $"Pickups: {pickupsCollected} / {currentFish.GetNotesRequiredToCapture()}";
        }
    }
}