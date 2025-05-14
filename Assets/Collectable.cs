using UnityEngine;

public class Collectable : MonoBehaviour
{

    CharacterObject player;
    public float amount = 5f;
    bool isCollected = false;
    public ParticleSystem particleSystem;
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
        if (other.name == "Player" && !isCollected)
        {
            isCollected = true;
            player = other.GetComponent<CharacterObject>();
            player.Collect(amount);
            particleSystem.enableEmission = false;
            Destroy(transform.parent.gameObject, 0.5f);
        }
    }
}
