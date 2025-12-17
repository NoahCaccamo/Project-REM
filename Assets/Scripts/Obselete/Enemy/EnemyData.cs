using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("State References")]

    [Header("The state to enter when idle or doing nothing")]
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int initialState;

    [Header("Attack State")]
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int attackState;

    [Header("Chase State")]
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int chaseState;

    public List<EnemyAttackOption> attackOptions = new List<EnemyAttackOption>();

    public float hp = 5f;
    public int maxArmor = 0;

    public float sightRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 0.02f;
    public float attackCooldownDuration = 60f;


    public EnemyAttackOption GetAttackByName(string name)
    {
        foreach (var attack in this.attackOptions)
        {
            if (attack.name == name)
                return attack;
        }
        return null;
    }

}

[System.Serializable]
public class EnemyAttackOption
{
    public string name;
    [IndexedItem(IndexedItemAttribute.IndexedItemType.STATES)]
    public int stateIndex;
    public float minRange = 0f;
    public float maxRange = 5f;
    public int weight = 1;
    public float cooldown = 1f;
    [HideInInspector] public float cooldownTimer;
}
