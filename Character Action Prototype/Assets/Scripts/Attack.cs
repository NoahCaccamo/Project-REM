using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Attack
{
    // overlap with events gross
    // move to char state/events?
    // or keep separate?
    public float start;
    public float length;
    public float hitstun;
    public float hitStop;

    public Vector2 hitAni;
    public Vector3 knockback;

    public Vector3 hitboxPos;
    public Vector3 hitboxScale;

    public float cancelWindow;

    public float playerBoost;
}
