using UnityEngine;
using System.Collections.Generic;
using static UnityEngine.GraphicsBuffer;

public static class LocationRegistry
{
    public static Dictionary<string, DeliveryLocation> DropoffLocs { get; private set; }
    public static Dictionary<string, DeliveryLocation> PickupLocs { get; private set; }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeRegistry()
    {
        DropoffLocs = new Dictionary<string, DeliveryLocation>();
        PickupLocs = new Dictionary<string, DeliveryLocation>();
    }

    public static void Register(DeliveryLocation point)
    {
        if (point.isPickup)
        {
            PickupLocs[point.LocationName] = point;
        }
        else
        {
            DropoffLocs[point.LocationName] = point;
        }
    }

    public static Transform ResolveDropoff(string name)
    {
        return DropoffLocs[name].transform;
    }

    public static Transform ResolvePickup(string name)
    {
        return PickupLocs[name].transform;
    }

    public static Transform FindNearestPickup(Transform playerPos)
    {
        float closestDist = 99999999999f;
        Transform closestTransform = null;
        foreach(var loc in PickupLocs)
        {
            float dist = Vector3.Distance(loc.Value.transform.position, playerPos.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestTransform = loc.Value.transform;
            }
        }

        return closestTransform;
    }
}
