using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class AggressiveMeleeAI : EnemyAIBehaviour
{
    // just a reference, this is actually stored in CharacterObject
    private EnemyData enemyData;

    private NavMeshAgent agent;

    public override void Initialize(CharacterObject enemy)
    {
        enemyData = enemy.enemyData;
        agent = GetComponent<NavMeshAgent>();

        agent.updatePosition = false;
        agent.updateRotation = false;
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
        if (_enemy._playerTrans == null)
            return;

        agent.nextPosition = _enemy.transform.position; // Sync agent with enemy

        // Set destination if needed
        if (agent.destination != _enemy._playerTrans.position)
            agent.SetDestination(_enemy._playerTrans.position);

        // Make sure there's a valid path and enough corners
        if (agent.hasPath && agent.path.corners.Length > 1)
        {
            Vector3 nextCorner = agent.path.corners[1];
            Vector3 dir = nextCorner - _enemy.transform.position;
            dir.y = 0;
            dir.Normalize();

            // Apply velocity directly????
            // MAKE SURE NO CONFLICTS WITH OTHER VELOCITY
            // METHOD MIGHT BE SAFER?
            float chaseSpeed = 0.02f * Time.deltaTime * 60 * _enemy.localTimescale;
            _enemy.velocity += dir * chaseSpeed;

        }
    }

    public void OldChase(CharacterObject _enemy)
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