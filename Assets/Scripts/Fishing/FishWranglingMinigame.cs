using KinematicCharacterController.Examples;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Guitar Hero-style rhythm minigame for fish wrangling.
/// Notes fall down lanes (W, A, S, D) and player must hit them at the right time.
/// Minigame starts with one hand, but can only be controlled with two hands.
/// Slows down when only one hand is grabbing.
/// </summary>
public class FishWranglingMinigame : MonoBehaviour
{
    [Header("Minigame Settings")]
    public float noteSpeed = 3f;
    public float noteSpawnInterval = 1f;
    public float hitWindow = 0.2f;
    public float missStaminaDrain = 5f;

    [Header("One-Hand Slowdown")]
    public float oneHandTimeScale = 0.3f; // NEW: How much to slow down with one hand (0.5 = half speed)

    [Header("UI References")]
    public Canvas minigameCanvas;
    public RectTransform noteContainer;
    public RectTransform hitLine;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI warningText; // Warning when only one hand grabbing
    public GameObject noteWPrefab;
    public GameObject noteAPrefab;
    public GameObject noteSPrefab;
    public GameObject noteDPrefab;

    [Header("Lane Positions")]
    public float laneSpacing = 100f;

    [Header("Hit Line Settings")]
    public float hitLineHeight = 100f; // Y position from bottom of container
    public Color hitLineColor = Color.yellow;
    public float hitLineThickness = 10f;

    // Internal state
    private bool isActive = false;
    private FishWrangler currentFish;
    private ExampleCharacterController player;
    private HandStaminaSystem staminaSystem;

    private List<Note> activeNotes = new List<Note>();
    private float spawnTimer = 0f;
    private int score = 0;

    private KeyCode[] noteKeys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };
    private GameObject[] notePrefabs;
    private float[] lanePositions = new float[4];
    private GameObject[] laneIndicators = new GameObject[4];

    private class Note
    {
        public GameObject gameObject;
        public KeyCode key;
        public RectTransform rectTransform;
        public float spawnTime;
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
            GameObject canvasObj = new GameObject("FishMinigameCanvas");
            minigameCanvas = canvasObj.AddComponent<Canvas>();
            minigameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create note container
        if (noteContainer == null)
        {
            GameObject containerObj = new GameObject("NoteContainer");
            containerObj.transform.SetParent(minigameCanvas.transform, false);
            noteContainer = containerObj.AddComponent<RectTransform>();
            noteContainer.anchorMin = new Vector2(0.5f, 0f);
            noteContainer.anchorMax = new Vector2(0.5f, 1f);
            noteContainer.sizeDelta = new Vector2(400f, 600f);
        }

        // Create hit line (IMPROVED - more visible)
        if (hitLine == null)
        {
            GameObject lineObj = new GameObject("HitLine");
            lineObj.transform.SetParent(noteContainer, false);
            hitLine = lineObj.AddComponent<RectTransform>();
            hitLine.anchorMin = new Vector2(0f, 0f);
            hitLine.anchorMax = new Vector2(1f, 0f);
            hitLine.anchoredPosition = new Vector2(0f, hitLineHeight);
            hitLine.sizeDelta = new Vector2(0f, hitLineThickness);

            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = hitLineColor;
        }

        // Create score text
        if (scoreText == null)
        {
            GameObject textObj = new GameObject("ScoreText");
            textObj.transform.SetParent(minigameCanvas.transform, false);
            scoreText = textObj.AddComponent<TextMeshProUGUI>();
            scoreText.fontSize = 24;
            scoreText.alignment = TextAlignmentOptions.Center;
            scoreText.color = Color.white;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.8f);
            textRect.anchorMax = new Vector2(0.5f, 0.8f);
            textRect.sizeDelta = new Vector2(300f, 50f);
        }

        // Create warning text for one-hand grabbing
        if (warningText == null)
        {
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
        }

        // Setup note prefabs (simplified - create colored squares)
        notePrefabs = new GameObject[4];
        Color[] noteColors = { Color.green, Color.blue, Color.red, Color.yellow };

        for (int i = 0; i < 4; i++)
        {
            // Calculate lane position
            lanePositions[i] = -150f + (i * laneSpacing);

            // Create note prefab
            GameObject prefab = new GameObject($"Note{noteKeys[i]}");
            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80f, 80f);

            Image img = prefab.AddComponent<Image>();
            img.color = noteColors[i];

            TextMeshProUGUI keyText = new GameObject("KeyText").AddComponent<TextMeshProUGUI>();
            keyText.transform.SetParent(prefab.transform, false);
            keyText.text = noteKeys[i].ToString();
            keyText.fontSize = 36;
            keyText.alignment = TextAlignmentOptions.Center;
            keyText.color = Color.black;

            RectTransform keyRect = keyText.GetComponent<RectTransform>();
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.sizeDelta = Vector2.zero;

            notePrefabs[i] = prefab;
            prefab.SetActive(false);

            // Create lane indicator at hit line position
            CreateLaneIndicator(i, noteColors[i]);
        }

        minigameCanvas.gameObject.SetActive(false);
    }

    private void CreateLaneIndicator(int laneIndex, Color laneColor)
    {
        GameObject indicator = new GameObject($"LaneIndicator_{noteKeys[laneIndex]}");
        indicator.transform.SetParent(noteContainer, false);

        RectTransform rect = indicator.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(90f, 90f);
        rect.anchoredPosition = new Vector2(lanePositions[laneIndex], hitLineHeight);

        // Outline box
        Image boxImage = indicator.AddComponent<Image>();
        boxImage.color = new Color(laneColor.r, laneColor.g, laneColor.b, 0.3f); // Semi-transparent

        // Key label
        GameObject labelObj = new GameObject("KeyLabel");
        labelObj.transform.SetParent(indicator.transform, false);

        TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
        labelText.text = noteKeys[laneIndex].ToString();
        labelText.fontSize = 48;
        labelText.fontStyle = FontStyles.Bold;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.outlineWidth = 0.2f;
        labelText.outlineColor = Color.black;

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        laneIndicators[laneIndex] = indicator;
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
        score = 0;
        activeNotes.Clear();
        spawnTimer = 0f;

        minigameCanvas.gameObject.SetActive(true);
        UpdateScore();

        Debug.Log("Fish wrangling minigame started!");
    }

    public void EndMinigame(bool success)
    {
        isActive = false;

        // Clear all notes
        foreach (var note in activeNotes)
        {
            if (note.gameObject != null)
            {
                Destroy(note.gameObject);
            }
        }
        activeNotes.Clear();

        minigameCanvas.gameObject.SetActive(false);

        if (success)
        {
            Debug.Log($"Minigame complete! Final score: {score}");
        }
    }

    void Update()
    {
        if (!isActive) return;

        // NEW: Check if player can control minigame (both hands grabbing)
        bool canControlMinigame = player.isGrabbingL && player.isGrabbingR;

        // NEW: Calculate time scale based on hand count
        float currentTimeScale = canControlMinigame ? 1f : oneHandTimeScale;

        // Show/hide warning text
        if (warningText != null)
        {
            warningText.gameObject.SetActive(!canControlMinigame);
        }

        // Spawn notes (affected by time scale)
        spawnTimer -= Time.deltaTime * currentTimeScale;
        if (spawnTimer <= 0f)
        {
            SpawnRandomNote();
            spawnTimer = noteSpawnInterval;
        }

        // Update notes (affected by time scale)
        UpdateNotes(currentTimeScale);

        // Only check input if player has both hands grabbing
        if (canControlMinigame)
        {
            CheckInput();
        }

        // Update stamina drain during bucking
        if (currentFish != null && currentFish.IsBucking())
        {
            // Drain stamina faster when holding with two hands during buck
            if (player.isGrabbingL && player.isGrabbingR && staminaSystem != null)
            {
                staminaSystem.DrainStamina(true, 20f * Time.deltaTime);
                staminaSystem.DrainStamina(false, 20f * Time.deltaTime);
            }
        }
    }

    private void SpawnRandomNote()
    {
        int laneIndex = Random.Range(0, 4);

        GameObject noteObj = Instantiate(notePrefabs[laneIndex], noteContainer);
        noteObj.SetActive(true);

        RectTransform noteRect = noteObj.GetComponent<RectTransform>();
        noteRect.anchoredPosition = new Vector2(lanePositions[laneIndex], 500f);

        Note note = new Note
        {
            gameObject = noteObj,
            key = noteKeys[laneIndex],
            rectTransform = noteRect,
            spawnTime = Time.time
        };

        activeNotes.Add(note);
    }

    // NEW: Accept time scale parameter
    private void UpdateNotes(float timeScale)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            Note note = activeNotes[i];

            // Move note down (affected by time scale)
            note.rectTransform.anchoredPosition += Vector2.down * noteSpeed * 100f * Time.deltaTime * timeScale;

            // Remove if off screen (missed) - use hit line position as reference
            if (note.rectTransform.anchoredPosition.y < hitLineHeight - 150f)
            {
                OnNoteMissed(note);
                Destroy(note.gameObject);
                activeNotes.RemoveAt(i);
            }
        }
    }

    private void CheckInput()
    {
        foreach (KeyCode key in noteKeys)
        {
            if (Input.GetKeyDown(key))
            {
                TryHitNote(key);
            }
        }
    }

    private void TryHitNote(KeyCode key)
    {
        // Find closest note with matching key near hit line
        Note closestNote = null;
        float closestDistance = float.MaxValue;

        foreach (Note note in activeNotes)
        {
            if (note.key == key)
            {
                float distance = Mathf.Abs(note.rectTransform.anchoredPosition.y - hitLineHeight);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestNote = note;
                }
            }
        }

        // Check if hit was within window
        if (closestNote != null && closestDistance < hitWindow * 100f)
        {
            OnNoteHit(closestNote);
            Destroy(closestNote.gameObject);
            activeNotes.Remove(closestNote);
        }
    }

    private void OnNoteHit(Note note)
    {
        score++;
        UpdateScore();

        if (currentFish != null)
        {
            currentFish.OnNoteHit();
        }

        Debug.Log($"HIT! Score: {score}");
    }

    private void OnNoteMissed(Note note)
    {
        // Drain stamina on miss
        if (staminaSystem != null)
        {
            staminaSystem.DrainStamina(true, missStaminaDrain);
            staminaSystem.DrainStamina(false, missStaminaDrain);
        }

        if (currentFish != null)
        {
            currentFish.OnNoteMissed();
        }

        Debug.Log("MISS! Stamina drained!");
    }

    private void UpdateScore()
    {
        if (scoreText != null && currentFish != null)
        {
            scoreText.text = $"Score: {score} / {currentFish.notesRequiredToCapture}";
        }
    }
}