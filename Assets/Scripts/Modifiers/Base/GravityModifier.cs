using UnityEngine;

[CreateAssetMenu(menuName = "Modifiers/Gravity")]
public class GravityModifier : ScriptableObject, IModifier
{
    public float multiplier = 0.5f;

    public void Apply(PlayerCharacter character)
    {
        character.CurrentStats.gravity *= multiplier;
    }

    public void Remove(PlayerCharacter character)
    {
        character.CurrentStats.gravity /= multiplier;
    }
}
