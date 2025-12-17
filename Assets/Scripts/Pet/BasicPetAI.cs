using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;

public class BasicPetAI : PetAIBehaviour
{
    private NavMeshAgent agent;

    public override void Initialize(CharacterObject pet)
    {
        base.Initialize(pet);
        agent = GetComponent<NavMeshAgent>();

        agent.updatePosition = false;
        agent.updateRotation = false;
    }
    public override void UpdateAI(CharacterObject pet)
    {
        bool inSight = PlayerInRange(pet, petData.sightRange);
        bool inInteractRange = PlayerInRange(pet, petData.interactRange);

        if (!inSight && !inInteractRange) { pet.target = null; currentSubState = PetSubState.Idle; return; }
        if (inSight && !inInteractRange) currentSubState = PetSubState.Chase;
        if (inSight && inInteractRange) currentSubState = PetSubState.Interact;

        Debug.Log(currentSubState.ToString());

        switch (currentSubState)
        {
            case PetSubState.Chase:
                Chase(pet);
                break;
            case PetSubState.Interact:
               // Attacking(pet);
                break;
        }
    }
    public void Chase(CharacterObject _pet)
    {
        if (_pet._playerTrans == null)
            return;
        Debug.Log("chasing player");

        agent.nextPosition = _pet.transform.position; // Sync agent with enemy

        // Set destination if needed
        if (agent.destination != _pet._playerTrans.position)
            agent.SetDestination(_pet._playerTrans.position);

        Debug.Log(_pet._playerTrans.position);
        // Make sure there's a valid path with more than the starting point
        if (agent.hasPath && agent.path.corners.Length > 1)
        {
            Vector3 nextCorner = agent.path.corners[1];
            Vector3 dir = nextCorner - _pet.transform.position;
            dir.y = 0;
            dir.Normalize();

            // Apply velocity directly????
            // MAKE SURE NO CONFLICTS WITH OTHER VELOCITY
            // METHOD MIGHT BE SAFER?
            float chaseSpeed = 0.02f * Time.deltaTime * 60 * _pet.localTimescale;
            _pet.velocity += dir * chaseSpeed;

        }
    }

    public void OldChase(CharacterObject _enemy)
    {
        Vector3 velDir = new Vector3(0, 0, 0);

        velDir = GetFlatDirectionToPlayer(_enemy);

        velDir *= 0.02f;

        _enemy.velocity += velDir * Time.deltaTime * 60 * _enemy.localTimescale;
    }
}