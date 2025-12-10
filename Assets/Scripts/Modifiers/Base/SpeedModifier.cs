using KinematicCharacterController.Examples;
using UnityEngine;

[CreateAssetMenu(menuName = "Modifiers/Speed Modifier")]
public class SpeedModifier : ScriptableObject, IModifier
{
    public float speedMultiplier = 1.2f;

    public void Apply(PlayerCharacter character)
    {
        character.CurrentStats.moveSpeed *= speedMultiplier;
    }

    public void Remove(PlayerCharacter character)
    {
        character.CurrentStats.moveSpeed /= speedMultiplier;
    }
}
