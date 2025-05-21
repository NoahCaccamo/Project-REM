using UnityEngine;

public enum SubState
{
    Idle,
    Chase,
    Attacking
}

public abstract class EnemyAIBehaviour : MonoBehaviour
{
    public SubState currentSubState = SubState.Idle;
    protected EnemyData enemyData;
    public virtual void Initialize(CharacterObject enemy)
    {
        enemyData = enemy.enemyData;
    }
    public abstract void UpdateAI(CharacterObject enemy);

    public virtual void TickCooldowns()
    {
        if (enemyData == null || enemyData.attackOptions == null)
            return;

        foreach (var option in enemyData.attackOptions)
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
}
