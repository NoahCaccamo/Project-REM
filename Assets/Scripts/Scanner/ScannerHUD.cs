using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space UI attached to the scanner device prefab
/// </summary>
public class ScannerHUD : MonoBehaviour
{
    [Header("World-Space Canvas UI Elements")]
    [SerializeField] private RectTransform pickupDot;
    [SerializeField] private RectTransform dropoffDot;
    [SerializeField] private TextMeshProUGUI pickupDistanceText;
    [SerializeField] private TextMeshProUGUI dropoffDistanceText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Pulse Effect")]
    [SerializeField] private Image pulseEffect;

    [Header("Colors")]
    [SerializeField] private Color pickupColor = Color.green;
    [SerializeField] private Color dropoffColor = Color.red;

    [Header("Canvas Settings")]
    [SerializeField] private Canvas scannerCanvas;
    [SerializeField] private Vector2 screenBounds = new Vector2(400f, 300f);

    [Header("Distance Text Positioning")]
    [SerializeField] private Vector2 textOffset = new Vector2(20f, 0f);
    [SerializeField] private bool hideWhenOffScreen = false;
    [SerializeField] private bool hideWhenBehind = true;

    private Camera playerCamera;
    private Transform playerTransform;
    private Vector3 pickupPosition;
    private Vector3 dropoffPosition;
    private bool hasActiveJob = false;
    private DeliveryWaypoint deliveryWaypoint;

    // Cooldown tracking
    private float cooldownEndTime = -999f;
    private float cooldownTimer = 0f;

    private void Start()
    {
        playerCamera = Camera.main;
        if (playerCamera != null)
            playerTransform = playerCamera.transform;

        if (scannerCanvas != null && scannerCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning("ScannerHUD Canvas should be set to World Space!");
        }

        if (pulseEffect != null)
            pulseEffect.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (hasActiveJob)
        {
            UpdateDotPositions();
        }

        if (IsOnCooldown())
        {
            cooldownTimer += Time.deltaTime;
        }
    }

    // Cooldown management
    public bool IsOnCooldown()
    {
        return cooldownTimer < cooldownEndTime;
    }

    public void SetCooldown(float duration)
    {
        cooldownEndTime = duration;
        cooldownTimer = 0f;
    }

    public float GetRemainingCooldown()
    {
        return cooldownEndTime - cooldownTimer;
    }

    public void SetWaypoint(DeliveryWaypoint waypoint)
    {
        deliveryWaypoint = waypoint;
    }

    public void UpdateActiveJob(DeliveryJob job)
    {
        if (job == null)
        {
            hasActiveJob = false;
            if (statusText != null)
                statusText.text = "no active job";

            if (pickupDot != null) pickupDot.gameObject.SetActive(false);
            if (dropoffDot != null) dropoffDot.gameObject.SetActive(false);
            if (pickupDistanceText != null) pickupDistanceText.gameObject.SetActive(false);
            if (dropoffDistanceText != null) dropoffDistanceText.gameObject.SetActive(false);
            return;
        }

        hasActiveJob = true;

        Transform pickup = LocationRegistry.ResolvePickup(job.pickupLocation);
        Transform dropoff = LocationRegistry.ResolveDropoff(job.dropoffLocation);

        if (pickup != null && dropoff != null)
        {
            pickupPosition = pickup.position;
            dropoffPosition = dropoff.position;

            if (deliveryWaypoint != null)
                deliveryWaypoint.target = dropoffPosition;

            if (pickupDot != null) pickupDot.gameObject.SetActive(true);
            if (dropoffDot != null) dropoffDot.gameObject.SetActive(true);
            if (pickupDistanceText != null) pickupDistanceText.gameObject.SetActive(true);
            if (dropoffDistanceText != null) dropoffDistanceText.gameObject.SetActive(true);

            if (statusText != null)
                statusText.text = "scanning...";
        }
    }

    private void UpdateDotPositions()
    {
        if (playerCamera == null) return;

        UpdateDot(pickupDot, pickupDistanceText, pickupPosition, "pickup");
        UpdateDot(dropoffDot, dropoffDistanceText, dropoffPosition, "dest");
    }

    private void UpdateDot(RectTransform dot, TextMeshProUGUI distanceText, Vector3 worldPosition, string label)
    {
        if (dot == null || worldPosition == Vector3.zero) return;

        Vector3 directionToTarget = (worldPosition - playerTransform.position).normalized;
        Vector3 localDir = playerTransform.InverseTransformDirection(directionToTarget);

        bool isBehind = localDir.z < 0f;

        if (hideWhenBehind && isBehind)
        {
            dot.gameObject.SetActive(false);
            if (distanceText != null)
                distanceText.gameObject.SetActive(false);
            return;
        }

        dot.gameObject.SetActive(true);
        if (distanceText != null)
            distanceText.gameObject.SetActive(true);

        float screenX = localDir.x * screenBounds.x;
        float screenY = localDir.y * screenBounds.y;

        if (hideWhenOffScreen)
        {
            bool isVisible = Mathf.Abs(screenX) <= screenBounds.x / 2 &&
                           Mathf.Abs(screenY) <= screenBounds.y / 2;
            dot.gameObject.SetActive(isVisible);
            if (distanceText != null)
                distanceText.gameObject.SetActive(isVisible);
        }

        dot.anchoredPosition = new Vector2(screenX, screenY);

        if (distanceText != null)
        {
            float distance = Vector3.Distance(playerTransform.position, worldPosition);
            distanceText.text = $"{distance:F1}m";

            RectTransform textRect = distanceText.GetComponent<RectTransform>();
            if (textRect != null)
            {
                textRect.anchoredPosition = new Vector2(screenX, screenY) + textOffset;
            }
        }
    }

    public void EmitPulse(float duration)
    {
        if (!hasActiveJob)
        {
            if (statusText != null)
                statusText.text = "no job!";
            return;
        }

        StartCoroutine(PulseCoroutine(duration));
    }

    private IEnumerator PulseCoroutine(float duration)
    {
        if (pulseEffect != null)
        {
            pulseEffect.gameObject.SetActive(true);

            float elapsed = 0f;
            float pulseDuration = 0.5f;
            Color startColor = pulseEffect.color;
            Vector3 startScale = pulseEffect.transform.localScale;

            while (elapsed < pulseDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / pulseDuration;

                float scale = Mathf.Lerp(1f, 2.5f, t);
                pulseEffect.transform.localScale = startScale * scale;

                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                pulseEffect.color = color;

                yield return null;
            }

            pulseEffect.gameObject.SetActive(false);
            pulseEffect.transform.localScale = startScale;
            pulseEffect.color = startColor;
        }

        if (deliveryWaypoint != null)
        {
            deliveryWaypoint.Enable();
            yield return new WaitForSeconds(duration);
            deliveryWaypoint.Disable();
        }

        if (statusText != null)
            statusText.text = "scanning...";
    }
}