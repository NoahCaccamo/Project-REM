using UnityEngine;

public class MiniJumper : MonoBehaviour
{
    public CharacterController myController;

    public CharacterObject realPlayer;

    private Vector3 velocity;
    public Vector3 gravity = new Vector3(0, -0.03f, 0);
    public Vector3 friction = new Vector3(0.95f, 0.99f, 0.95f);

    private int jumps = 2;
    private int jumpMax = 2;
    bool aerialFlag = false;
    float aerialTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        UpdatePhysics();

        if (Input.GetButtonDown("Jump"))
        {
            if (jumps > 0)
            {
                velocity.y = 0.5f;
                jumps--;
            }
        }
    }

    void UpdatePhysics()
    {
        velocity += gravity * Time.unscaledDeltaTime * 60;//* localTimescale;

        myController.Move(velocity * 60 * Time.unscaledDeltaTime);

        if ((myController.collisionFlags & CollisionFlags.Below) != 0)
        {
            velocity.y = 0;
            jumps = jumpMax;
        }
        else
        {
            if (!aerialFlag)
            {
                aerialTimer += Time.unscaledDeltaTime * 60;
            }
            if (aerialTimer >= 3)
            {
                aerialFlag = true;
                /*
                if (aniAerialState <= 1f)
                {
                    aniAerialState += 0.1f * 60 * Time.unscaledDeltaTime; // 0.05 is 20 frames i think since 0.1 is 10
                }
                */
                if (jumps == jumpMax) { jumps--; }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.name.Contains("Obstacle"))
        {
            realPlayer.MinigameFail();
        }
        if (other.name.Contains("Goal"))
        {
            realPlayer.MinigameSuccess();
        }
    }
}
