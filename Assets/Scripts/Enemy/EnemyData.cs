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

    public int maxArmor = 0;

    public float sightRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 0.02f;
    public float attackCooldownDuration = 60f;

}