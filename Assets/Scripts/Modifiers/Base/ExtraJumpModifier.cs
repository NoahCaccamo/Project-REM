using UnityEngine;

[CreateAssetMenu(menuName = "Modifiers/ExtraJump")]
public class ExtraJumpModifier : ScriptableObject, IModifier
{
    public int extraJumps = 1;

    public void Apply(PlayerCharacter character)
    {
        character.CurrentStats.maxJumps += extraJumps;
    }

    public void Remove(PlayerCharacter character)
    {
        character.CurrentStats.maxJumps -= extraJumps;
    }
}
