using System.Collections.Generic;
using UnityEngine;

public class TimeSlowZone : MonoBehaviour
{
    public float slowMultiplier = 0.25f;
    public float duration = 5f;
    private List<CharacterObject> affectedEnemies = new();

    private void OnTriggerEnter(Collider other)
    {
        CharacterObject character = other.GetComponent<CharacterObject>();
        if (character != null && character.controlType == CharacterObject.ControlType.AI)
        {
            character.localTimescale = slowMultiplier;
            if (!affectedEnemies.Contains(character))
                affectedEnemies.Add(character);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        CharacterObject character = other.GetComponent<CharacterObject>();
        if (character != null && character.controlType == CharacterObject.ControlType.AI)
        {
            character.localTimescale = 1f;
            affectedEnemies.Remove(character);
        }
    }

    private void Start()
    {
        Destroy(gameObject, duration);
    }

    private void OnDestroy()
    {
        // Reset time scale for all remaining affected enemies (in case they're still in the field)
        foreach (var character in affectedEnemies)
        {
            if (character != null)
                character.localTimescale = 1f;
        }
    }
}
