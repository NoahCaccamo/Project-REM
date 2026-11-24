
using System;
using UnityEngine;

public interface IMinigame
{
    void Begin(RenderTexture target);
    void End();
    void SetMoveInput(Vector2 move);
}

/// <summary>
/// Put a subclass of this on the root of every minigame prefab.
/// The prefab should contain its own Camera (disabled, no AudioListener,
/// culling mask = Minigame, background alpha 0, no target texture).
/// </summary>
public abstract class MinigameRoot : MonoBehaviour, IMinigame
{
    [Header("Minigame")]
    [SerializeField] protected Camera cam;
    [SerializeField] string layerName = "Minigame";

    /// <summary>Latest WASD value, (-1..1, -1..1). Read this in Update/FixedUpdate.</summary>
    protected Vector2 Move { get; private set; }

    /// <summary>Raise (via Finish()) when the game is won, lost or otherwise over.</summary>
    public event Action<MinigameRoot> Finished;

    /// <summary>Current time multiplier (1 = normal, 0 = paused). Set by the manager.</summary>
    public float TimeScale { get; private set; } = 1f;

    /// <summary>False while the player isn't gripping with both hands. Move is forced to zero.</summary>
    public bool ControlEnabled { get; private set; } = true;

    /// <summary>Use this instead of Time.deltaTime in minigame logic.</summary>
    protected float DeltaTime => Time.deltaTime * TimeScale;

    /// <summary>Use this instead of Time.fixedDeltaTime in minigame logic.</summary>
    protected float FixedDeltaTime => Time.fixedDeltaTime * TimeScale;

    protected virtual void Awake()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>(true);

        // Make sure nothing renders to the screen before a target texture is assigned.
        if (cam != null)
        {
            cam.enabled = false;
            cam.targetTexture = null;
        }

        SetLayerRecursive();
    }

    void SetLayerRecursive()
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            Debug.LogError($"Layer '{layerName}' does not exist. Add it in Project Settings > Tags and Layers.");
            return;
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    public void SetGripState(float timeScale, bool controlEnabled)
    {
        TimeScale = Mathf.Max(0f, timeScale);

        if (ControlEnabled != controlEnabled)
        {
            ControlEnabled = controlEnabled;

            // Clear held input immediately when control is lost
            if (!controlEnabled)
            {
                Move = Vector2.zero;
                OnMoveInput(Vector2.zero);
            }
        }

        OnGripStateChanged();
    }

    public void Begin(RenderTexture target)
    {
        if (cam == null)
        {
            Debug.LogError($"{name}: no Camera assigned.", this);
            return;
        }

        cam.targetTexture = target;
        cam.enabled = true;
        OnBegin();
    }

    public void End()
    {
        OnEnd();

        if (cam != null)
        {
            cam.enabled = false;
            cam.targetTexture = null;
        }
    }

    public void SetMoveInput(Vector2 move)
    {
        if (!ControlEnabled)
        {
            move = Vector2.zero;
        }

        Move = move;
        OnMoveInput(move);
    }

    /// <summary>True if the game ended with Succeed(). Valid once Finished has been raised.</summary>
    public bool Succeeded { get; private set; }

    /// <summary>Call when the player wins (fish captured). The manager closes the game.</summary>
    protected void Succeed()
    {
        Succeeded = true;
        Finished?.Invoke(this);
    }

    /// <summary>Call when the player loses or the game ends without a capture.</summary>
    protected void Fail()
    {
        Succeeded = false;
        Finished?.Invoke(this);
    }

    /// <summary>Raise (via Finish()) when the game is over. Counts as a fail; prefer Succeed()/Fail().</summary>
    protected void Finish() => Fail();

    protected virtual void OnBegin() { }
    protected virtual void OnEnd() { }
    protected virtual void OnMoveInput(Vector2 move) { }
    /// <summary>Called whenever TimeScale / ControlEnabled are set. Override to pause animators, particles, audio, etc.</summary>
    protected virtual void OnGripStateChanged() { }
}
