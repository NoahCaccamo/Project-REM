using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class AggressiveMeleeAI : EnemyAIBehaviour
{
    // just a reference, this is actually stored in CharacterObject
    private EnemyData enemyData;

    public override void Initialize(CharacterObject enemy)
    {
        enemyData = enemy.enemyData;
    }
    public override void UpdateAI(CharacterObject enemy)
    {
        if (enemy.hitStun > 0)
        {
            enemy.atkCooldown = enemyData.attackCooldownDuration;
            return;
        }

        bool inSight = PlayerInRange(enemy, enemyData.sightRange);
        bool inAttackRange = PlayerInRange(enemy, enemyData.attackRange);

        if (!inSight && !inAttackRange) { enemy.target = null; currentSubState = SubState.Idle; return; }
        if (inSight && !inAttackRange) currentSubState = SubState.Chase;//enemy.ChangeState(enemyData.chaseState);
        if (inSight && inAttackRange) currentSubState = SubState.Attacking;


        switch (currentSubState)
        {
            case SubState.Chase:
                Chase(enemy);
                break;
            case SubState.Attacking:
                Attacking(enemy);
                break;
        }
    }
    public void Chase(CharacterObject _enemy)
    {
        Vector3 velDir = new Vector3(0, 0, 0);

        velDir = GetFlatDirectionToPlayer(_enemy);

        velDir *= 0.02f;

        _enemy.velocity += velDir * Time.deltaTime * 60 * _enemy.localTimescale;
    }

    public void Attacking(CharacterObject _enemy)
    {
        _enemy.atkCooldown--;
        if (_enemy.atkCooldown == 0)
        {
            _enemy.FaceTarget(_enemy._playerTrans.position);
            _enemy.ChangeState(enemyData.attackState); // Enemy punch
        }
        if (_enemy.atkCooldown < -enemyData.attackCooldownDuration * 2)
        {
            _enemy.atkCooldown = enemyData.attackCooldownDuration;
        }
    }
}