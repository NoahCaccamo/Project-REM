using UnityEngine;
using UnityEngine.TextCore.Text;

public class Bullet : MonoBehaviour
{
    public Quaternion lookDirection;
    public Vector3 direction;

    public CharacterObject character;


    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = lookDirection;

        transform.position += transform.forward * 40 * Time.deltaTime;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject != character.transform.root.gameObject) // the root is where our character object script is
        {
            // slowwww
            CharacterObject victim = other.transform.root.GetComponent<CharacterObject>();
            victim.GetShot(character);
            Debug.Log("sHIT!");
            Destroy(this.gameObject);
        }
    }
}
