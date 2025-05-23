using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class MyCameraControl : MonoBehaviour
{
    // note: have exploratory cam mode where it cuts off more behind character to have more front view?
    // combat mode is more zoomed/down so more back view

    // implement collision with terrain (pull cam in when too far down, push out when up?)

    public string horizontalAxis = "RightStickX"; // As defined in your Unity Editor: Edit > Project Settings > Input
    public string verticalAxis = "RightStickY";
    public GameObject rig;
    public GameObject transformTarget;
    public GameObject lookTarget;
    public Vector3 transformOffset;
    public GameObject myCamera;
    public float orbH;
    public float orbV;
    public float orbVMin = -89f;
    public float orbVMax = 89f;
    public float rotSpeed = 2;
    public float rotBoost = 0f;
    public float rotAccel = 0.075f;
    public float rotBoostMax = 5;
    public float orbSpeed = 0.5f;
    public float targetOrbH;
    public float targetOrbV;
    public float orbitHelp = 1.05f; // less good for combat, better for exploration

    public float camDistance = 8f;

    public float deadzone = 0.5f;
    public float invertX = 1;
    public float invertY = 1;

    public Vector3 translateSpeed = new Vector3(0.75f, 0.075f, 0.75f); // X and Z should be the same generally as this is the horizontal plane

    public bool focusTarget;

    public Vector3 lookEuler;

    private void Awake()
    {
        DontDestroyOnLoad(transform.gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        //transformTarget = GetMaincharobject
        // or assign in editor?
    }

    // Update is called once per frame
    void LateUpdate()
    {
        //transformTarget = getmaincharobject

        rig.transform.localPosition = new Vector3(0, 0, -camDistance);
        OrbitView();
        Translate();
        SettleCameras();
    }

    void SettleCameras()
    {
        myCamera.transform.localPosition += (Vector3.zero - myCamera.transform.localPosition) * 0.25f;
    }

    void Translate()
    {
        if (transformTarget == null) { return; }
        transform.position += Vector3.Scale((transformTarget.transform.position + (Quaternion.LookRotation(new Vector3(transform.forward.x, 0f, transform.forward.z), Vector3.up) * transformOffset) - transform.position), translateSpeed);
    }

    // clean this up later
    // separate boost directions? (boost accelarates cam as you continue holding a direction)
    void OrbitView()
    {
        if (Input.GetAxisRaw(horizontalAxis) > deadzone)
        {
            targetOrbH += (rotBoost + rotSpeed) * invertX;
            rotBoost += rotAccel;
        }
        if (Input.GetAxisRaw(horizontalAxis) < -deadzone)
        {
            targetOrbH += -(rotBoost + rotSpeed) * invertX;
            rotBoost += rotAccel;
        }
        if (Input.GetAxisRaw(verticalAxis) > deadzone) // DOWN
        {
            targetOrbV += (rotBoost + rotSpeed) * invertY;
            rotBoost += rotAccel;
        }
        if (Input.GetAxisRaw(verticalAxis) < -deadzone) // UP
        {
            targetOrbV += -(rotBoost * rotSpeed) * invertY;
            rotBoost += rotAccel;
        }

        if (Input.GetAxisRaw(horizontalAxis) < deadzone &&
            Input.GetAxisRaw(horizontalAxis) > -deadzone &&
            Input.GetAxisRaw(verticalAxis) < deadzone &&
            Input.GetAxisRaw(verticalAxis) > -deadzone)
        {
            rotBoost = 0;
        }
        if (rotBoost > rotBoostMax) { rotBoost = rotBoostMax; }
        targetOrbV = Mathf.Clamp(targetOrbV, orbVMin, orbVMax);

        // in progress can ignore/improve?
        if (!focusTarget) { targetOrbH -= LookAtOffset() * orbitHelp * Mathf.Lerp(1f, 0.025f, Mathf.InverseLerp(0f, 90f, Mathf.Abs(orbV)));  } // * 180 / Mathf.PI;

        // Ease Orbiting
        orbH += (targetOrbH - orbH) * orbSpeed;
        orbV += (targetOrbV - orbV) * orbSpeed;

        transform.rotation = Quaternion.Euler(orbV, orbH, 0);

        if (focusTarget) { transform.rotation = Quaternion.LookRotation(lookTarget.transform.position - transform.position, Vector3.up) * Quaternion.Euler(orbV, 0, 0); }
    }

    public float LookAtOffset()
    {
        if (transformTarget == null) { return 0f; }
        if (rig == null) { return 0f; }
        float lookAtOffset = 0;

        Vector3 offsetLook = Quaternion.LookRotation(new Vector3(transform.forward.x, 0f, transform.forward.z), Vector3.up) * transformOffset;
        Vector2 currentLook = new Vector2(transform.position.x, transform.position.z) - new Vector2(rig.transform.position.x, rig.transform.position.z) - new Vector2(offsetLook.x, offsetLook.z);
        Vector2 newLook = new Vector2(transformTarget.transform.position.x, transformTarget.transform.position.z) - new Vector2(rig.transform.position.x, rig.transform.position.z);
        Vector3 cross = Vector3.Cross(currentLook, newLook);
        lookAtOffset = Vector2.Angle(currentLook, newLook);
        if (cross.z > 0) { return lookAtOffset; }
        else { return -lookAtOffset; }
    }
}
