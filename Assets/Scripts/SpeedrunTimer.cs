using UnityEngine;
using TMPro;
using System;

public class SpeedrunTimer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Canvas timerCanvas;

    [Header("Timer Settings")]
    [SerializeField] private bool startTimerOnLoad = false;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color stoppedColor = Color.yellow;

    private float currentTime = 0f;
    private bool isRunning = false;
    private bool hasFinished = false;

    private void Awake()
    {
        SetupUI();
    }

    private void SetupUI()
    {
        // Create canvas if not assigned
        if (timerCanvas == null)
        {
            GameObject canvasObj = new GameObject("SpeedrunTimerCanvas");
            timerCanvas = canvasObj.AddComponent<Canvas>();
            timerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            timerCanvas.sortingOrder = 100;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // Create text if not assigned
        if (timerText == null)
        {
            GameObject textObj = new GameObject("TimerText");
            textObj.transform.SetParent(timerCanvas.transform, false);

            timerText = textObj.AddComponent<TextMeshProUGUI>();
            timerText.fontSize = 48;
            timerText.alignment = TextAlignmentOptions.TopRight;
            timerText.color = normalColor;

            // Position in top right
            RectTransform rectTransform = textObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-20f, -20f);
            rectTransform.sizeDelta = new Vector2(300f, 100f);
        }
    }

    private void Start()
    {
        if (startTimerOnLoad)
        {
            StartTimer();
        }

        UpdateTimerDisplay();
    }

    private void Update()
    {
        // Reset with R key
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetTimer();
        }

        // Update timer if running
        if (isRunning)
        {
            currentTime += Time.deltaTime;
            UpdateTimerDisplay();
        }
    }

    private void UpdateTimerDisplay()
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(currentTime);

        // Format: MM:SS.mmm
        string formattedTime = string.Format("{0:00}:{1:00}.{2:000}",
            (int)timeSpan.TotalMinutes,
            timeSpan.Seconds,
            timeSpan.Milliseconds);

        timerText.text = formattedTime;
        timerText.color = hasFinished ? stoppedColor : normalColor;
    }

    public void StartTimer()
    {
        isRunning = true;
        hasFinished = false;
        currentTime = 0f;
        timerText.color = normalColor;
        UpdateTimerDisplay();
    }

    public void StopTimer()
    {
        isRunning = false;
        hasFinished = true;
        timerText.color = stoppedColor;
        Debug.Log($"Timer stopped at: {timerText.text}");
    }

    public void ResetTimer()
    {
        currentTime = 0f;
        hasFinished = false;
        UpdateTimerDisplay();
        StartTimer();
        Debug.Log("Timer reset and restarted");
    }

    // Call this from trigger
    public void OnTimerFinish()
    {
        if (isRunning)
        {
            StopTimer();
        }
    }

    public float GetCurrentTime()
    {
        return currentTime;
    }
}