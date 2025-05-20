using UnityEngine;

public class EnemyAIControl : IControlStrategy
{
    private EnemyAIBehaviour aiBehaviour;

    public EnemyAIControl(EnemyAIBehaviour behaviour)
    {
        aiBehaviour = behaviour;
    }

    public void Tick(CharacterObject character)
    {
        aiBehaviour.UpdateAI(character);
    }
}
