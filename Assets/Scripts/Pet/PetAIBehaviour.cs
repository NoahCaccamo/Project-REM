using System.Linq;
using UnityEngine;

public enum PetSubState
{
    Idle,
    Chase,
    Wander,
    Eat,
    Interact
}

public enum PetHungerState
{
    Starving,
    Hungry,
    Neutral,
    Satiated
}

public abstract class PetAIBehaviour : MonoBehaviour
{
    public PetSubState currentSubState = PetSubState.Idle;
    protected PetData petData;
    public virtual void Initialize(CharacterObject pet)
    {
        petData = pet.petData;
    }
    public abstract void UpdateAI(CharacterObject pet);

    public virtual void TickCooldowns()
    {
        if (petData == null || petData.actionOptions == null)
            return;

        foreach (var option in petData.actionOptions)
        {
            if (option.cooldownTimer > 0)
                option.cooldownTimer -= Time.deltaTime * 60;
        }
    }


    protected bool PlayerInRange(CharacterObject enemy, float range)
    {
        return Physics.CheckSphere(enemy.transform.position, range, enemy.whatIsPlayer);
    }

    protected Vector3 GetFlatDirectionToPlayer(CharacterObject enemy)
    {
        Vector3 dir = enemy._playerTrans.position - enemy.gameObject.transform.position;
        dir.y = 0;
        dir.Normalize();
        return dir;
    }

    /*
    protected EnemyAttackOption ChooseAttack(Vector3 enemyPos, Vector3 playerPos)
    {
        float dist = Vector3.Distance(enemyPos, playerPos);

        var validAttacks = enemyData.attackOptions
            .Where(a => a.cooldownTimer <= 0f && dist >= a.minRange && dist <= a.maxRange)
            .ToList();

        if (validAttacks.Count == 0)
            return null;

        // TODO: Choose by weight, randomness, or other thing to pick best attack
        return validAttacks[0];
    }
    protected EnemyAttackOption GetClosestReadyAttack(Vector3 enemyPos, Vector3 playerPos)
    {
        var attacks = enemyData.attackOptions
            .Where(a => a.cooldownTimer <= 0f)
            .OrderBy(a =>
            {
                float dist = Vector3.Distance(enemyPos, playerPos);
                float targetDist = Mathf.Clamp(dist, a.minRange, a.maxRange);
                return Mathf.Abs(dist - targetDist);
            });

        return attacks.FirstOrDefault();
    }
    */
}
