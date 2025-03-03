using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public CharacterObject character;

    // this could live in InspectorTools instead?
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int stateIndex;
    void Start()
    {
        character = transform.root.GetComponent<CharacterObject>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    // if we run into problems look into ontriggerenter too
    // this is really slow and ?bad?
    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject != transform.root.gameObject) // the root is where our character object script is
        {
            if (character.hitActive > 0)
            {
                // slowwww
                CharacterObject victim = other.transform.root.GetComponent<CharacterObject>();
                victim.GetHit(character);
                //Debug.Log("HIT!");
            }
        }
    }
}
