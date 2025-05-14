using UnityEngine;

public class MinigameManager : MonoBehaviour
{

    public GameObject obstacle;
    public GameObject goal;

    float timer;
    int obstaclesToGoal = 8;
    
    int obstacleCount = 0;

    float spawnInterval = 50f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        timer += Time.unscaledDeltaTime * 60;
        if (timer >= spawnInterval)
        {
            float yOffset = 0;
            float posRoll = Random.Range(0, 1f);
            if (posRoll > 0.5f)
            {
                yOffset = 2f;
            }
            if (obstacleCount >= obstaclesToGoal)
            {
                Instantiate(goal, new Vector3(transform.position.x, transform.position.y + yOffset, transform.position.z), transform.rotation);
                obstacleCount = 0;
            } else
            {
                Instantiate(obstacle, new Vector3(transform.position.x, transform.position.y + yOffset, transform.position.z), transform.rotation);
                obstacleCount++;
            }
            spawnInterval = Random.Range(30f, 60f);
            timer = 0;
        }
    }
}
