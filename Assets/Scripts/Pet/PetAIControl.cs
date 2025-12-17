using UnityEngine;

public class PetAIControl : IControlStrategy
{
    private PetAIBehaviour aiBehaviour;

    public PetAIControl(PetAIBehaviour behaviour)
    {
        aiBehaviour = behaviour;
    }

    public void Tick(CharacterObject character)
    {
        aiBehaviour?.UpdateAI(character);
        aiBehaviour?.TickCooldowns();
    }
}
