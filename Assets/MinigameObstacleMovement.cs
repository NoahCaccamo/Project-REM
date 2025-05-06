using UnityEngine;

public class MinigameObstacleMovement : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public float speed = 8f;
    public float lifetime = 6;
    public float timer;

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        transform.Translate(Vector3.left * speed * Time.unscaledDeltaTime);
    }
}
