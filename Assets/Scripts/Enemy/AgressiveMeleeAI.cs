using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class AggressiveMeleeAI : EnemyAIBehaviour
{
    private NavMeshAgent agent;
    private EnemyAttackOption currentAttack;
    private float attackLockedUntil = 0f;

    public override void Initialize(CharacterObject enemy)
    {
        base.Initialize(enemy);
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

        if (Time.time < attackLockedUntil)
            return;

        bool inSight = PlayerInRange(enemy, enemyData.sightRange);
        bool inAttackRange = PlayerInRange(enemy, enemyData.attackRange);

        if (!inSight && !inAttackRange) { enemy.target = null; currentSubState = SubState.Idle; return; }
        //  if (inSight && !inAttackRange) currentSubState = SubState.Chase;//enemy.ChangeState(enemyData.chaseState);
        //if (inSight && inAttackRange) currentSubState = SubState.Attacking;

        Vector3 targetPos = enemy._playerTrans?.position ?? Vector3.zero;

        EnemyAttackOption chosenAtk = ChooseAttack(enemy.transform.position, enemy._playerTrans.position);

        if (chosenAtk != null)
        {
            currentSubState = SubState.Attacking;
            currentAttack = chosenAtk;
        }
        else
        {
            EnemyAttackOption fallback = GetClosestReadyAttack(enemy.transform.position, targetPos);

            if (fallback != null)
            {
                currentSubState = SubState.Chase;
                currentAttack = fallback; // for range reference
            }
            else
            {
                currentSubState = SubState.Idle;
                currentAttack = null;
            }
        }

        switch (currentSubState)
        {
            case SubState.Chase:
                if (currentAttack != null)
                    MoveTowardRange(enemy, currentAttack.maxRange, currentAttack.maxRange);
                else
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

        // Make sure there's a valid path with more than the starting point
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

    public void Attacking(CharacterObject enemy)
    {
        if (currentAttack == null) return;

        enemy.atkCooldown--;
        if (enemy.atkCooldown <= 0)
        {
            enemy.FaceTarget(enemy._playerTrans.position);
            enemy.ChangeState(currentAttack.stateIndex);
            currentAttack.cooldownTimer = currentAttack.cooldown;

            // Reset for next round
            currentAttack = null;
            enemy.atkCooldown = enemyData.attackCooldownDuration;

            attackLockedUntil = Time.time + 3;
        }
    }

    private void UpdateAttackStuff(CharacterObject enemy)
    {

    }


    // BUGGY WHEN FLEEING!!! Maybe sometimes just cant find the path
    private void MoveTowardRange(CharacterObject enemy, float minRange, float maxRange)
    {
        if (enemy._playerTrans == null)
            return;

        float distance = Vector3.Distance(enemy.transform.position, enemy._playerTrans.position);

        // Sync agent with enemy's position
        agent.nextPosition = enemy.transform.position;

        if (distance > maxRange)
        {
            // Move toward player
            agent.SetDestination(enemy._playerTrans.position);
            MoveAlongNavPath(enemy);
        }
        else if (distance < minRange)
        {
            // Move away (flee)
            Vector3 awayDir = (enemy.transform.position - enemy._playerTrans.position).normalized;
            float fleeDistance = minRange + 1.5f;

            Vector3 fleeTarget = enemy.transform.position + awayDir * fleeDistance;

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(fleeTarget, out navHit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
                MoveAlongNavPath(enemy);
            }
            else
            {
                // Couldn�t find valid position, stop movement
                enemy.velocity = Vector3.zero;
            }
        }
        else
        {
            // Already in ideal range
            agent.ResetPath();
            enemy.velocity = Vector3.zero;
        }
    }


    private void MoveAlongNavPath(CharacterObject enemy)
    {
        if (agent.hasPath && agent.path.corners.Length > 1)
        {
            Vector3 nextCorner = agent.path.corners[1];
            Vector3 dir = nextCorner - enemy.transform.position;
            dir.y = 0;
            dir.Normalize();

            float moveSpeed = 0.02f * Time.deltaTime * 60 * enemy.localTimescale;
            enemy.velocity += dir * moveSpeed;
        }
    }


    private void MoveTowardRangeOld(CharacterObject enemy, float minRange, float maxRange)
    {
        float dist = Vector3.Distance(enemy.transform.position, enemy._playerTrans.position);

        if (dist > maxRange)
        {
            // Chase logic
            Vector3 dir = enemy._playerTrans.position - enemy.transform.position;
            dir.y = 0;
            dir.Normalize();
            enemy.velocity += dir * Time.deltaTime * 60 * enemy.localTimescale * 0.02f;
        }
        else if (dist < minRange)
        {
            // Optional: Back away logic
            // UNTESTED
            Vector3 dir = enemy._playerTrans.position - enemy.transform.position;
            dir.y = 0;
            dir.Normalize();
            enemy.velocity -= dir * Time.deltaTime * 60 * enemy.localTimescale * 0.02f;
        }
        else
        {
            // In range, idle or strafe
            enemy.velocity = Vector3.zero;
        }
    }
}