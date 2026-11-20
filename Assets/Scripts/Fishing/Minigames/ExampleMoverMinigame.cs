using UnityEngine;

/// <summary>
/// Minimal test minigame: a sprite/object you move with WASD inside a box.
/// Prefab setup: root with this script + a child Camera (orthographic, size 5, z = -10)
/// + a child "Player" object (any sprite or mesh) assigned below.
/// </summary>
public class ExampleMoverMinigame : MinigameRoot
{
    [Header("Example")]
    [SerializeField] Transform player;
    [SerializeField] float speed = 5f;
    [SerializeField] Vector2 bounds = new(8f, 4.5f);   // half extents
    [SerializeField] float lifetime = 0f;              // >0 auto-finishes after N seconds

    float timer;

    protected override void OnBegin()
    {
        timer = 0f;
        if (player != null) player.localPosition = Vector3.zero;
    }

    void Update()
    {
        if (player == null) return;

        Vector3 p = player.localPosition;
        p += (Vector3)(Move * (speed * Time.deltaTime));
        p.x = Mathf.Clamp(p.x, -bounds.x, bounds.x);
        p.y = Mathf.Clamp(p.y, -bounds.y, bounds.y);
        player.localPosition = p;

        if (lifetime > 0f)
        {
            timer += Time.deltaTime;
            if (timer >= lifetime) Finish();
        }
    }
}