using KinematicCharacterController.Examples;
using UnityEngine;

/// <summary>
/// AI controller for fish that can be wrangled. Fish idles, wanders, and when grabbed
/// enters a looping path movement pattern with bucking behavior.
/// Works with the climbing system so player doesn't fall while wrangling.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FishWrangler : MonoBehaviour
{
    [Header("Minigame Selection")]
    public MinigameType minigameType = MinigameType.GuitarHero;

    [Header("Fish Behavior")]
    public FishState currentState = FishState.Idle;
    public float idleSpeed = 1f;
    public float wrangleSpeed = 8f;
    public float wrangleLoopRadius = 10f;
    public Transform loopCenter;

    [Header("Bucking Behavior")]
    public float buckDuration = 2f;
    public float buckCooldownMin = 3f;
    public float buckCooldownMax = 6f;
    public Color normalColor = Color.blue;
    public Color buckingColor = Color.red;
    public MeshRenderer fishRenderer;

    [Header("Wrangling Requirements")]
    public int notesRequiredToCapture = 10;
    public ItemObject capturedFishItem; // The item rewarded on successful capture

    // Internal state
    private bool isBeingWrangled = false;
    private float wrangleAngle = 0f;
    private float buckTimer = 0f;
    private float buckCooldownTimer = 0f;
    private bool isBucking = false;
    private int successfulNotesHit = 0;

    // Track initial grab position to prevent teleporting
    private bool isFirstWrangleFrame = false;

    // Wrangling players
    private ExampleCharacterController wranglingPlayer;
    private FishWranglingMinigame guitarHeroMinigame;
    private BulletHellFishMinigame bulletHellMinigame;
    private PlatformerFishMinigame platformerMinigame;

    public enum FishState
    {
        Idle,
        Wrangling
    }

    public enum MinigameType
    {
        GuitarHero,
        BulletHell,
        Platformer
    }

    void Start()
    {
        if (loopCenter == null)
        {
            loopCenter = transform;
        }

        buckCooldownTimer = Random.Range(buckCooldownMin, buckCooldownMax);

        // Initialize minigames based on selection
        InitializeMinigames();

        // Make sure we're on a layer the climbing system can detect
        if (gameObject.layer == 0) // If on default layer
        {
            Debug.LogWarning("FishWrangler should be on 'Climbable' layer for climbing to work!");
        }
    }

    private void InitializeMinigames()
    {
        // Create Guitar Hero minigame
        guitarHeroMinigame = GetComponent<FishWranglingMinigame>();
        if (guitarHeroMinigame == null)
        {
            guitarHeroMinigame = gameObject.AddComponent<FishWranglingMinigame>();
        }

        // Create Bullet Hell minigame
        bulletHellMinigame = GetComponent<BulletHellFishMinigame>();
        if (bulletHellMinigame == null)
        {
            bulletHellMinigame = gameObject.AddComponent<BulletHellFishMinigame>();
        }

        // Create Platformer minigame
        platformerMinigame = GetComponent<PlatformerFishMinigame>();
        if (platformerMinigame == null)
        {
            platformerMinigame = gameObject.AddComponent<PlatformerFishMinigame>();
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case FishState.Idle:
                UpdateIdle();
                break;
            case FishState.Wrangling:
                UpdateWrangling();
                break;
        }
    }

    private void UpdateIdle()
    {
        // Simple slow circular movement
        transform.position += transform.forward * idleSpeed * Time.deltaTime;
        transform.Rotate(0f, 10f * Time.deltaTime, 0f);
    }

    private void UpdateWrangling()
    {
        if (!isBeingWrangled) return;

        // On first frame of wrangling, calculate the starting angle based on current position
        if (isFirstWrangleFrame)
        {
            // Calculate where we are relative to loop center
            Vector3 offsetFromCenter = transform.position - loopCenter.position;
            wrangleAngle = Mathf.Atan2(offsetFromCenter.z, offsetFromCenter.x);
            isFirstWrangleFrame = false;
        }

        // Move in circular loop (smoothly continue from current angle)
        wrangleAngle += (wrangleSpeed / wrangleLoopRadius) * Time.deltaTime;

        Vector3 offset = new Vector3(
            Mathf.Cos(wrangleAngle) * wrangleLoopRadius,
            0f,
            Mathf.Sin(wrangleAngle) * wrangleLoopRadius
        );

        transform.position = loopCenter.position + offset;
        transform.forward = new Vector3(-Mathf.Sin(wrangleAngle), 0f, Mathf.Cos(wrangleAngle));

        // Handle bucking behavior
        UpdateBucking();
    }

    private void UpdateBucking()
    {
        if (isBucking)
        {
            buckTimer -= Time.deltaTime;
            if (buckTimer <= 0f)
            {
                EndBuck();
            }
        }
        else
        {
            buckCooldownTimer -= Time.deltaTime;
            if (buckCooldownTimer <= 0f)
            {
                StartBuck();
            }
        }
    }

    private void StartBuck()
    {
        isBucking = true;
        buckTimer = buckDuration;

        if (fishRenderer != null)
        {
            fishRenderer.material.color = buckingColor;
        }

        Debug.Log("Fish is BUCKING! Use one hand only!");
    }

    private void EndBuck()
    {
        isBucking = false;
        buckCooldownTimer = Random.Range(buckCooldownMin, buckCooldownMax);

        if (fishRenderer != null)
        {
            fishRenderer.material.color = normalColor;
        }
    }

    // Called whenever player grabs (one or two hands)
    public void OnGrabbedByPlayer(ExampleCharacterController player, bool leftHand, bool rightHand)
    {
        // Only initialize once
        if (isBeingWrangled) return;

        isBeingWrangled = true;
        wranglingPlayer = player;
        currentState = FishState.Wrangling;
        loopCenter.position = transform.position; // Center the loop at current position
        isFirstWrangleFrame = true;

        // Start the appropriate minigame based on selection
        if (minigameType == MinigameType.GuitarHero)
        {
            if (guitarHeroMinigame != null)
            {
                guitarHeroMinigame.StartMinigame(this, player);
            }
        }
        else if (minigameType == MinigameType.BulletHell)
        {
            if (bulletHellMinigame != null)
            {
                bulletHellMinigame.StartMinigame(this, player);
            }
        }
        else if (minigameType == MinigameType.Platformer)
        {
            if (platformerMinigame != null)
            {
                platformerMinigame.StartMinigame(this, player);
            }
        }

        Debug.Log($"Fish wrangling started! Collect {notesRequiredToCapture} to capture!");
    }

    public void OnReleased()
    {
        isBeingWrangled = false;
        wranglingPlayer = null;
        currentState = FishState.Idle;
        successfulNotesHit = 0;
        isFirstWrangleFrame = false;

        // End the active minigame
        if (minigameType == MinigameType.GuitarHero && guitarHeroMinigame != null)
        {
            guitarHeroMinigame.EndMinigame(false);
        }
        else if (minigameType == MinigameType.BulletHell && bulletHellMinigame != null)
        {
            bulletHellMinigame.EndMinigame(false);
        }
        else if (minigameType == MinigameType.Platformer && platformerMinigame != null)
        {
            platformerMinigame.EndMinigame(false);
        }

        EndBuck();

        Debug.Log("Fish escaped!");
    }

    public void OnNoteHit()
    {
        successfulNotesHit++;

        if (successfulNotesHit >= notesRequiredToCapture)
        {
            OnCaptureSuccess();
        }
    }

    public void OnNoteMissed()
    {
        // Drain stamina on miss handled by minigame
        Debug.Log("Missed note! Stamina drained!");
    }

    private void OnCaptureSuccess()
    {
        Debug.Log("FISH CAPTURED!");

        // Award item to player
        if (wranglingPlayer != null && capturedFishItem != null)
        {
            // Give item to player - you'll need to integrate with your inventory system
            Debug.Log($"Player received: {capturedFishItem.name}");
        }

        // End the active minigame
        if (minigameType == MinigameType.GuitarHero && guitarHeroMinigame != null)
        {
            guitarHeroMinigame.EndMinigame(true);
        }
        else if (minigameType == MinigameType.BulletHell && bulletHellMinigame != null)
        {
            bulletHellMinigame.EndMinigame(true);
        }
        else if (minigameType == MinigameType.Platformer && platformerMinigame != null)
        {
            platformerMinigame.EndMinigame(true);
        }

        // Destroy or deactivate fish
        Destroy(gameObject);
    }

    public bool IsBucking() => isBucking;
    public bool IsBeingWrangled() => isBeingWrangled;
    public ExampleCharacterController GetWranglingPlayer() => wranglingPlayer;
    public int GetNotesRequiredToCapture() => notesRequiredToCapture;
}