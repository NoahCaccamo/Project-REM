using System;
using UnityEngine;

public class FriedEgg : MonoBehaviour
{
    [Header("Cooking Settings")]
    public float minSlideSpeed = 0.6f;
    public float timeToCookSide = 5f;


    [Header("Cook Progress (0 to 1)")]
    public float bottomCooked = 0f;
    public float topCooked = 0f;

    public ParticleSystem cookingSizzle;

    public event Action<FriedEgg> OnEggOutOfBound;
    public event Action<FriedEgg, int> OnEggCooked; // Event to notify when the egg is cooked, passing the egg instance and the cooking level


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Pan")) return;

        if (collision.relativeVelocity.magnitude > minSlideSpeed)
        {
            // Play cooking particle effect
            cookingSizzle.Play();


            Vector3 panNormal = collision.contacts[0].normal;

            // check orientation relative to pan surface
            // Egg rotated 90 on X so we have to use -transform.foward instead of transform.up
            float dot = Vector3.Dot(-transform.forward, panNormal);

            if (dot > 0.5f) // Bottom is cooking
            {
                bottomCooked = Mathf.Clamp01(bottomCooked + (Time.deltaTime / timeToCookSide));
                if (bottomCooked >= 1f && topCooked >= 1f)
                {
                    OnEggCooked?.Invoke(this, 2); // Notify that the egg is fully cooked
                }
            }
            else if (dot < -0.5f) // Top is cooking
            {
                topCooked = Mathf.Clamp01(topCooked + (Time.deltaTime / timeToCookSide));
                if (bottomCooked >= 1f && topCooked >= 1f)
                {
                    OnEggCooked?.Invoke(this, 2); // Notify that the egg is fully cooked
                }
            }
            else
            {
                cookingSizzle.Stop();
            }
        }
        else
        {
            cookingSizzle.Stop();
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Pan"))
        {
            cookingSizzle.Stop();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bounds"))
        {
            OnEggOutOfBound?.Invoke(this);

            Destroy(gameObject);
        }
    }

}
