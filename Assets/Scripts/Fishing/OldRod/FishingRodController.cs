using System.Collections;
using UnityEngine;

public class FishingRodController : MonoBehaviour
{
    private enum FishingState
    {
        Idle,
        Aiming,
        Casting,
        Bobbing,
        Nibbling,
        Biting,
        Reeling,
        Success
    }

    private FishingState currentState = FishingState.Idle;
    private FishingRodObject rodData;
    private PlayerContext playerContext;
    private Hand hand;

    private GameObject reticleInstance;
    private GameObject bobberInstance;
    private LineRenderer lineRenderer;

    private Vector3 currentReticlePosition;
    private Vector3 targetLandingPosition;
    private float currentCastDistance = 0f;
    private bool mouseHeld = false;
    private bool bobberLanded = false;

    private FishPond currentPond;
    private Fish hookedFish;

    [Header("Bite Settings")]
    private float biteCheckTimer = 0f;
    private float biteCheckInterval = 3f;
    private float nibbleChance = 0.3f;
    private float biteChance = 0.15f;
    private float biteWindowDuration = 2f;
    private float biteWindowTimer = 0f;

    private Vector3 bobberRestPosition;
    private bool isNibbling = false;
    private float nibbleTimer = 0f;

    public void Initialize(FishingRodObject rod, PlayerContext context, Hand handRef)
    {
        rodData = rod;
        playerContext = context;
        hand = handRef;

        if (rodData.reticlePrefab != null)
        {
            reticleInstance = Instantiate(rodData.reticlePrefab);
            reticleInstance.SetActive(false);
        }

        if (rodData.rodLinePrefab != null)
        {
            lineRenderer = Instantiate(rodData.rodLinePrefab);
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        currentState = FishingState.Idle;
    }

    public void Initialize(FishingRodObject rod, PlayerContext context, Hand handRef, LineRenderer existingLine)
    {
        rodData = rod;
        playerContext = context;
        hand = handRef;

        if (rodData.reticlePrefab != null)
        {
            reticleInstance = Instantiate(rodData.reticlePrefab);
            reticleInstance.SetActive(false);
        }

        lineRenderer = existingLine;
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;
        }

        currentState = FishingState.Idle;
    }

    public void OnUsePressed()
    {
        mouseHeld = true;

        if (currentState == FishingState.Idle)
        {
            StartAiming();
        }
        else if (currentState == FishingState.Bobbing || currentState == FishingState.Nibbling)
        {
            StartReeling();
        }
        else if (currentState == FishingState.Biting)
        {
            CatchFishSuccess();
        }
    }

    private void Update()
    {
        if (!Input.GetMouseButton(0))
        {
            if (mouseHeld && currentState == FishingState.Aiming)
            {
                CastLine();
            }
            mouseHeld = false;
        }

        if (currentState == FishingState.Reeling)
        {
            if (!Input.GetMouseButton(0))
            {
                currentState = FishingState.Bobbing;
                biteCheckTimer = 0f;
            }
        }

        switch (currentState)
        {
            case FishingState.Aiming:
                UpdateAiming();
                break;
            case FishingState.Casting:
                UpdateCasting();
                break;
            case FishingState.Bobbing:
                UpdateBobbing();
                break;
            case FishingState.Nibbling:
                UpdateNibbling();
                break;
            case FishingState.Biting:
                UpdateBiting();
                break;
            case FishingState.Reeling:
                UpdateReeling();
                break;
            case FishingState.Success:
                UpdateSuccess();
                break;
        }

        UpdateLine();
    }

    private void StartAiming()
    {
        currentState = FishingState.Aiming;
        currentCastDistance = 0f;

        if (reticleInstance != null)
        {
            reticleInstance.SetActive(true);
        }
    }

    private void UpdateAiming()
    {
        currentCastDistance += rodData.castSpeed * Time.deltaTime;
        currentCastDistance = Mathf.Min(currentCastDistance, rodData.maxCastDistance);

        CalculateTrajectoryLanding();

        if (reticleInstance != null)
        {
            reticleInstance.transform.position = currentReticlePosition;
        }
    }

    private void CalculateTrajectoryLanding()
    {
        Vector3 rodTip = playerContext.playerCamera.transform.position +
                        playerContext.playerCamera.transform.forward * 0.5f;

        Vector3 cameraForward = playerContext.playerCamera.transform.forward;

        float cameraPitch = Vector3.Angle(Vector3.ProjectOnPlane(cameraForward, Vector3.up), cameraForward);
        if (cameraForward.y < 0) cameraPitch = -cameraPitch;

        float launchAngle = Mathf.Clamp(45f - cameraPitch, 15f, 75f);

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(cameraForward, Vector3.up).normalized;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angleRad = launchAngle * Mathf.Deg2Rad;

        float sin2Angle = Mathf.Sin(2f * angleRad);
        if (sin2Angle < 0.1f) sin2Angle = 0.1f;

        float initialSpeed = Mathf.Sqrt((currentCastDistance * gravity) / sin2Angle);

        Vector3 velocity = horizontalDirection * initialSpeed * Mathf.Cos(angleRad);
        velocity.y = initialSpeed * Mathf.Sin(angleRad);

        Vector3 position = rodTip;
        Vector3 vel = velocity;
        float timeStep = 0.05f;
        float maxTime = 5f;
        float elapsed = 0f;

        while (elapsed < maxTime)
        {
            vel.y -= gravity * timeStep;
            position += vel * timeStep;
            elapsed += timeStep;

            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 1f, LayerMask.GetMask("Default", "Water")))
            {
                currentReticlePosition = hit.point;
                targetLandingPosition = hit.point;
                return;
            }

            if (position.y < rodTip.y - 50f)
            {
                break;
            }
        }

        if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit groundHit, 50f))
        {
            currentReticlePosition = groundHit.point;
            targetLandingPosition = groundHit.point;
        }
        else
        {
            currentReticlePosition = rodTip + horizontalDirection * currentCastDistance;
            targetLandingPosition = currentReticlePosition;
        }
    }

    private void CastLine()
    {
        currentState = FishingState.Casting;
        bobberLanded = false;

        if (rodData.bobberPrefab != null && bobberInstance == null)
        {
            Vector3 rodTip = playerContext.playerCamera.transform.position +
                           playerContext.playerCamera.transform.forward * 0.5f;
            bobberInstance = Instantiate(rodData.bobberPrefab, rodTip, Quaternion.identity);

            Rigidbody rb = bobberInstance.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bobberInstance.AddComponent<Rigidbody>();
            }
            rb.useGravity = true;
            rb.isKinematic = false;

            BobberCollisionDetector detector = bobberInstance.GetComponent<BobberCollisionDetector>();
            if (detector == null)
            {
                detector = bobberInstance.AddComponent<BobberCollisionDetector>();
            }
            detector.onLanded = OnBobberLanded;

            // NEW: Add trigger detector for pond
            BobberPondDetector pondDetector = bobberInstance.GetComponent<BobberPondDetector>();
            if (pondDetector == null)
            {
                pondDetector = bobberInstance.AddComponent<BobberPondDetector>();
            }
            pondDetector.onPondEntered = OnBobberEnteredPond;

            Vector3 launchVelocity = CalculateLaunchVelocity(rodTip, targetLandingPosition);
            rb.linearVelocity = launchVelocity;
        }

        if (reticleInstance != null)
        {
            reticleInstance.SetActive(false);
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
        }
    }

    private Vector3 CalculateLaunchVelocity(Vector3 origin, Vector3 target)
    {
        Vector3 direction = target - origin;
        Vector3 horizontalDir = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        float horizontalDist = Vector3.ProjectOnPlane(direction, Vector3.up).magnitude;
        float verticalDist = target.y - origin.y;

        float gravity = Mathf.Abs(Physics.gravity.y);
        float angle = 45f * Mathf.Deg2Rad;

        float speed = Mathf.Sqrt((horizontalDist * gravity) / Mathf.Sin(2f * angle));

        if (Mathf.Abs(verticalDist) > 0.1f)
        {
            float tanAngle = Mathf.Tan(angle);
            speed = Mathf.Sqrt(gravity * horizontalDist * horizontalDist /
                              (2f * (horizontalDist * tanAngle - verticalDist)));
        }

        Vector3 velocity = horizontalDir * speed * Mathf.Cos(angle);
        velocity.y = speed * Mathf.Sin(angle);

        return velocity;
    }

    private void UpdateCasting()
    {
        if (bobberInstance == null)
        {
            currentState = FishingState.Idle;
            return;
        }
    }

    private void OnBobberLanded()
    {
        if (bobberLanded) return;
        bobberLanded = true;

        Debug.Log("Bobber landed at: " + bobberInstance.transform.position);

        currentState = FishingState.Bobbing;
        bobberRestPosition = bobberInstance.transform.position;

        Rigidbody rb = bobberInstance.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        biteCheckTimer = 0f;
    }

    // NEW: Simple callback when bobber enters pond trigger
    private void OnBobberEnteredPond(FishPond pond)
    {
        currentPond = pond;
        Debug.Log($"Bobber entered pond: {pond.name}");
    }

    private void UpdateBobbing()
    {
        if (currentPond == null) return;

        biteCheckTimer += Time.deltaTime;

        if (biteCheckTimer >= biteCheckInterval)
        {
            biteCheckTimer = 0f;
            CheckForFishActivity();
        }
    }

    private void CheckForFishActivity()
    {
        float roll = Random.value;

        if (roll < biteChance)
        {
            StartBite();
        }
        else if (roll < biteChance + nibbleChance)
        {
            StartNibble();
        }
    }

    private void StartNibble()
    {
        currentState = FishingState.Nibbling;
        isNibbling = true;
        nibbleTimer = 0f;

        Debug.Log("Nibble!");

        if (bobberInstance != null)
        {
            bobberInstance.transform.position = bobberRestPosition + Vector3.down * 0.1f;
        }
    }

    private void UpdateNibbling()
    {
        nibbleTimer += Time.deltaTime;

        if (nibbleTimer >= 0.5f)
        {
            if (bobberInstance != null)
            {
                bobberInstance.transform.position = bobberRestPosition;
            }
            isNibbling = false;
            currentState = FishingState.Bobbing;
            biteCheckTimer = 0f;
        }
    }

    private void StartBite()
    {
        currentState = FishingState.Biting;
        biteWindowTimer = 0f;

        Debug.Log("BITE! Reel in now!");

        if (bobberInstance != null)
        {
            StartCoroutine(BobberPullAnimation());
        }
    }

    private IEnumerator BobberPullAnimation()
    {
        Vector3 startPos = bobberRestPosition;
        Vector3 targetPos = bobberRestPosition + Vector3.down * 0.5f;

        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (bobberInstance == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            bobberInstance.transform.position = Vector3.Lerp(startPos, targetPos, easeT);
            yield return null;
        }

        if (bobberInstance != null)
        {
            bobberInstance.transform.position = targetPos;
        }
    }

    private void UpdateBiting()
    {
        biteWindowTimer += Time.deltaTime;

        if (biteWindowTimer >= biteWindowDuration)
        {
            Debug.Log("Missed the bite window!");
            if (bobberInstance != null)
            {
                bobberInstance.transform.position = bobberRestPosition;
            }
            currentState = FishingState.Bobbing;
            biteCheckTimer = 0f;
        }
    }

    private void CatchFishSuccess()
    {
        Debug.Log("Successfully caught during bite window!");

        currentState = FishingState.Success;

        if (currentPond != null)
        {
            hookedFish = currentPond.TryCatchFish(bobberInstance.transform.position);
        }

        StartCoroutine(InstantReelIn());
    }

    private IEnumerator InstantReelIn()
    {
        if (bobberInstance == null) yield break;

        Vector3 startPos = bobberInstance.transform.position;
        Vector3 rodTip = playerContext.playerCamera.transform.position +
                        playerContext.playerCamera.transform.forward * 0.5f;

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (bobberInstance == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rodTip = playerContext.playerCamera.transform.position +
                    playerContext.playerCamera.transform.forward * 0.5f;

            bobberInstance.transform.position = Vector3.Lerp(startPos, rodTip, t);
            yield return null;
        }

        if (hookedFish != null)
        {
            SpawnFishPickup();
        }

        CompleteCatch();
    }

    private void SpawnFishPickup()
    {
        if (hookedFish == null || hookedFish.fishData == null) return;

        Vector3 spawnPos = playerContext.playerCamera.transform.position +
                          playerContext.playerCamera.transform.forward * 2f;

        GameObject fishPickupObj = new GameObject($"FishPickup_{hookedFish.fishData.fishName}");
        fishPickupObj.transform.position = spawnPos;

        ItemPickup pickup = fishPickupObj.AddComponent<ItemPickup>();

        if (hookedFish.fishData.fishModel != null)
        {
            GameObject model = Instantiate(hookedFish.fishData.fishModel, fishPickupObj.transform);
            float scale = hookedFish.size / hookedFish.fishData.maxSize;
            model.transform.localScale = Vector3.one * scale * 0.5f;
        }

        Rigidbody rb = fishPickupObj.AddComponent<Rigidbody>();
        rb.mass = 0.2f;

        SphereCollider col = fishPickupObj.AddComponent<SphereCollider>();
        col.radius = 0.2f;

        Vector3 toPlayer = (playerContext.playerTransform.position - spawnPos).normalized;
        Vector3 flingVelocity = toPlayer * 3f + Vector3.up * 2f;

        rb.linearVelocity = flingVelocity;
        rb.angularVelocity = Random.insideUnitSphere * 5f;

        Debug.Log($"Spawned fish pickup: {hookedFish.fishData.fishName} ({hookedFish.size}cm)");
    }

    private void StartReeling()
    {
        currentState = FishingState.Reeling;

        if (currentPond != null && hookedFish == null)
        {
            hookedFish = currentPond.TryCatchFish(bobberInstance.transform.position);
            if (hookedFish != null)
            {
                Debug.Log($"Hooked a {hookedFish.fishData.fishName}!");
            }
        }
    }

    private void UpdateReeling()
    {
        if (bobberInstance == null)
        {
            currentState = FishingState.Idle;
            return;
        }

        Vector3 rodTip = playerContext.playerCamera.transform.position +
                       playerContext.playerCamera.transform.forward * 0.5f;

        bobberInstance.transform.position = Vector3.MoveTowards(
            bobberInstance.transform.position,
            rodTip,
            rodData.reelSpeed * Time.deltaTime
        );

        if (Vector3.Distance(bobberInstance.transform.position, rodTip) < 0.5f)
        {
            CompleteCatch();
        }
    }

    private void UpdateSuccess()
    {
    }

    private void CompleteCatch()
    {
        if (bobberInstance != null)
        {
            Destroy(bobberInstance);
            bobberInstance = null;
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        currentPond = null;
        hookedFish = null;
        currentState = FishingState.Idle;
        bobberLanded = false;
        biteCheckTimer = 0f;
        biteWindowTimer = 0f;
        isNibbling = false;
    }

    private void UpdateLine()
    {
        if (lineRenderer != null && bobberInstance != null)
        {
            Vector3 rodTip = playerContext.playerCamera.transform.position +
                           playerContext.playerCamera.transform.forward * 0.5f;

            lineRenderer.SetPosition(0, rodTip);
            lineRenderer.SetPosition(1, bobberInstance.transform.position);
        }
    }

    private void OnDestroy()
    {
        if (reticleInstance != null)
        {
            Destroy(reticleInstance);
        }
        if (bobberInstance != null)
        {
            Destroy(bobberInstance);
        }
        if (lineRenderer != null)
        {
            Destroy(lineRenderer.gameObject);
        }
    }
}