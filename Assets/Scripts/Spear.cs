using UnityEngine;

public class Spear : MonoBehaviour
{
    public Quaternion lookDirection;
    public Vector3 direction;
    public float speed = 20;

    public CharacterObject character;


    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = lookDirection;

        transform.position += transform.forward * speed * Time.deltaTime;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject != character.transform.root.gameObject) // the root is where our character object script is
        {
            // slowwww
            CharacterObject victim = other.transform.root.GetComponent<CharacterObject>();
            Debug.Log("sHIT!");
            Destroy(this.gameObject);
        }
    }
}
