using System.Collections.Generic;
using UnityEngine;

public class TruckingMinigame : MinigameRoot
{

    float timer;

    protected override void OnBegin()
    {

    }
    private void Awake()
    {

    }

    private void Start()
    {

    }

    void Update()
    {

    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Truck"))
        {
            Succeed();
        }
    }
}