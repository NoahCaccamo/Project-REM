using UnityEngine;

public class ItemMagnet : MonoBehaviour
{
    public float speed = 1f;

    private Transform target;

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        var step = speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, target.position, step);
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Player")
        {
            target = other.transform;
        }
    }
}
