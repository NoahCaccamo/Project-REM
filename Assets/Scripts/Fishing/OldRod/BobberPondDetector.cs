using System;
using UnityEngine;

public class BobberPondDetector : MonoBehaviour
{
    public Action<FishPond> onPondEntered;

    private void OnTriggerEnter(Collider other)
    {
        FishPond pond = other.GetComponent<FishPond>();
        if (pond != null)
        {
            onPondEntered?.Invoke(pond);
        }
    }
}