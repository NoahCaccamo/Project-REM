using UnityEngine;

[System.Serializable]
public class CharacterStats
{
    public float moveSpeed = 10f;
    public float jumpHeight = 5f;
    public Vector3 gravity = new Vector3(0f, -30f, 0f);
    public int maxJumps = 1;

    // Copy constructor for safety
    public CharacterStats(CharacterStats other)
    {
        moveSpeed = other.moveSpeed;
        jumpHeight = other.jumpHeight;
        gravity = other.gravity;
        maxJumps = other.maxJumps;
    }

    public CharacterStats() { }
}
