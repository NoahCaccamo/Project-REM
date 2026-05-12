using System;
using UnityEngine;

public class BobberCollisionDetector : MonoBehaviour
{
    public Action onLanded;

    [Tooltip("Minimum impact velocity to count as landing")]
    public float minImpactVelocity = 2f;

    private bool hasLanded = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (hasLanded) return;

        // Check if impact was significant enough
        if (collision.relativeVelocity.magnitude >= minImpactVelocity)
        {
            hasLanded = true;
            Debug.Log($"Bobber landed on {collision.gameObject.name}");
            onLanded?.Invoke();
        }
    }
}