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
    public abstract void Initialize(CharacterObject enemy);
    public abstract void UpdateAI(CharacterObject enemy);

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
