using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrownSpear : MonoBehaviour
{
    public float stickDepth = 0.2f;        // How much it embeds in the wall
    public LayerMask climbableLayer;

    private Rigidbody rb;
    private bool hasStuck = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!hasStuck && rb.linearVelocity.sqrMagnitude > 0.01f)
        {
            // Rotate spear so its forward matches velocity
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity.normalized, transform.up);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasStuck) return;

        StickIntoSurface(collision);
        /*
        // Stick into static objects only
        if (collision.gameObject.isStatic)
        {
            StickIntoSurface(collision);
        }
        else if (collision.gameObject.CompareTag("Enemy"))
        {
            // Optional: deal damage, play effects
            StickIntoSurface(collision);
        }
        else
        {
            // Bounce slightly for other dynamic objects
        }
        */
    }

    private void StickIntoSurface(Collision collision)
    {
        hasStuck = true;

        // Stop physics
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Move slightly into the surface along the collision normal
        ContactPoint contact = collision.contacts[0];
        transform.position = contact.point + contact.normal * -stickDepth;

        // Align spear to surface normal
       //  transform.rotation = Quaternion.LookRotation(-contact.normal, transform.up);

        // Optionally parent to the object so it moves with it (e.g., moving platforms)
        //transform.SetParent(collision.transform, true);

        gameObject.layer = Mathf.RoundToInt(Mathf.Log(climbableLayer.value, 2));

    }
}
