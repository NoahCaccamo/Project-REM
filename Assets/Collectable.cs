using UnityEngine;

public class Collectable : MonoBehaviour
{

    CharacterObject player;
    public float amount = 5f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name == "Player")
        {
            player = other.GetComponent<CharacterObject>();
            player.Collect(amount);
            Destroy(transform.parent.gameObject);
        }
    }
}
