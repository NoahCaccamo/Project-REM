using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns the RenderTexture, the on-screen RawImage, the spawn anchor and the input routing.
/// Requires the new Input System package.
/// </summary>
public class MinigameManager : MonoBehaviour
{
    public static MinigameManager Instance { get; private set; }

    [Header("Display")]
    [SerializeField] RawImage view;                       // centered RawImage, Raycast Target OFF
    [SerializeField] Vector2Int textureSize = new(512, 512);
    [SerializeField] FilterMode filterMode = FilterMode.Bilinear; // Point for pixel look
    [SerializeField, Range(0, 8)] int antiAliasing = 1;   // 1, 2, 4 or 8
    [SerializeField] int depthBits = 24;                  // 0 for 2D-only games

    [Header("Spawning")]
    [SerializeField] Transform anchor;                    // far from the player, e.g. (10000, 0, 0)

    [Header("Debug")]
    [SerializeField] MinigameRoot debugPrefab;
    [SerializeField] KeyCode debugStartKey = KeyCode.F1;

    [Header("Grip")]
    [Tooltip("Minigame speed while only one hand is holding the fish. 0 = fully paused.")]
    [SerializeField, Range(0f, 1f)] float oneHandTimeScale = 0.25f;

    bool bothHandsGripping;

    RenderTexture rt;
    MinigameRoot active;

    public bool IsPlaying => active != null;

    void Awake()
    {
        Instance = this;

        rt = new RenderTexture(textureSize.x, textureSize.y, depthBits,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            name = "MinigameRT",
            filterMode = filterMode,
            antiAliasing = Mathf.Max(1, antiAliasing),
            useMipMap = false
        };
        rt.Create();

        view.texture = rt;
        view.raycastTarget = false;          // never eat mouse clicks
        view.gameObject.SetActive(false);
    }

    void OnEnable()
    {

    }

    void OnDisable()
    {

    }

    void OnDestroy()
    {
        Stop();
        if (rt != null) rt.Release();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (debugPrefab != null && Input.GetKeyDown(debugStartKey))
        {
            if (IsPlaying) Stop();
            else StartMinigame(debugPrefab);
        }

        if (active != null)
        {
            float horizontalInput = 0f;
            if (Input.GetKey(KeyCode.A)) horizontalInput -= 1f;
            if (Input.GetKey(KeyCode.D)) horizontalInput += 1f;

            float verticalInput = 0f;
            if (Input.GetKey(KeyCode.W)) verticalInput += 1f;
            if (Input.GetKey(KeyCode.S)) verticalInput -= 1f;

            Vector2 movement = new Vector2(horizontalInput, verticalInput).normalized;

            active.SetMoveInput(movement);
        }
    }

    public MinigameRoot StartMinigame(MinigameRoot prefab)
    {
        Stop();

        active = Instantiate(prefab, anchor.position, anchor.rotation, anchor);
        active.Finished += OnGameFinished;
        active.Begin(rt);
        ApplyGripState();

        view.gameObject.SetActive(true);
        return active;
    }

    // TEMP DELETE THIS
    public MinigameRoot StartMinigameNOW()
    {
        Stop();

        active = Instantiate(debugPrefab, anchor.position, anchor.rotation, anchor);
        active.Finished += OnGameFinished;
        active.Begin(rt);
        ApplyGripState();

        view.gameObject.SetActive(true);
        return active;
    }

    public void Stop()
    {
        if (active == null) return;

        active.Finished -= OnGameFinished;
        active.End();
        Destroy(active.gameObject);
        active = null;
        bothHandsGripping = false;

        view.gameObject.SetActive(false);
    }

    void OnGameFinished(MinigameRoot game)
    {
        if (game == active) Stop();
    }

    /// <summary>Called by the character each frame while wrangling. Two hands = full speed + control.</summary>
    public void SetBothHandsGripping(bool both)
    {
        if (bothHandsGripping == both) return;

        bothHandsGripping = both;
        ApplyGripState();
    }

    void ApplyGripState()
    {
        if (active == null) return;

        active.SetGripState(bothHandsGripping ? 1f : oneHandTimeScale, bothHandsGripping);
    }

}