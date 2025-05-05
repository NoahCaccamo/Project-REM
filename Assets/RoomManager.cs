using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{

    public GameObject doors;
    List<GameObject> listOfOpponents = new List<GameObject>();

    bool challengeIsActive = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.name == "Player")
        {
            if (!challengeIsActive)
            {
                if (GameObject.FindGameObjectsWithTag("Enemy").Length > 0)
                {
                    doors.SetActive(true);
                    listOfOpponents.AddRange(GameObject.FindGameObjectsWithTag("Enemy"));

                    challengeIsActive = true;

                    print(listOfOpponents.Count);
                }
            }
        }
    }

    public void KilledOponent(GameObject opponent)
    {
        if (listOfOpponents.Contains(opponent))
        {
            listOfOpponents.Remove(opponent);
        }

        if (AreOpponentsDead())
        {
            Time.timeScale = 0.10f;
            doors.SetActive(false);

            challengeIsActive = false;
            // GIVE REWARD HERE
        }
        print(listOfOpponents.Count);
    }

    public bool AreOpponentsDead()
    {
        if (listOfOpponents.Count <= 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
