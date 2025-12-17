using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RelativeCameraMovement : MonoBehaviour
{
    // UNTESTED

    // https://youtu.be/IpfqBWBF3pY?si=fDUOlJlIp2gsAXem&t=1505

    public float moveSpeed = 0.1f;
    public string leftStickX = "Horizontal";
    public string leftStickY = "Vertical";
    public float deadzone = 0.15f; // Deadzone for control stick
    public Camera myCamera;
    Vector3 velHelp;
    Vector3 stickHelp;
    Vector3 velDir;
    Vector3 curVelocity;
    public CharacterController myController;
    public Vector3 friction = new Vector3(0.9f, 0.99f, 0.9f);

    public float gravity = -0.015f; // turn off gravity (set to 0) if you dont have world collision set up yet and just want to test

    // Start is called before the first frame update
    void Start()
    {
        myCamera = Camera.main; // Assign as needed by game
        myController = GetComponent<CharacterController>();
    }

    // Update is called once per frame
    void Update()
    {
        // update input
        UpdateInput();
        // update physics
        UpdatePhysics();
    }

    public void UpdateInput()
    {
        curVelocity += CameraRelativeVelocity() * moveSpeed;
    }

    public void UpdatePhysics()
    {
        curVelocity += new Vector3(0, gravity, 0);
        curVelocity.Scale(friction);

        if (myController != null)
        {
            myController.Move(curVelocity);
        }
    }

    // Translates 2D stick input to 3d spacial movement based on position and facing/rotation of camera
    // REDUNDANT THIS IS IN CHARACTEROBJECT
    public Vector3 CameraRelativeVelocity()
    {
        velHelp = new Vector3(0, 0, 0);
        stickHelp = new Vector2(Input.GetAxisRaw(leftStickX), Input.GetAxisRaw(leftStickY));

        if (stickHelp.x > deadzone || stickHelp.x < -deadzone || stickHelp.y > deadzone || stickHelp.y < -deadzone)
        {

            if (stickHelp.sqrMagnitude > 1) { stickHelp.Normalize(); }

            velDir = myCamera.transform.forward;
            velDir.y = 0;
            velDir.Normalize();
            velHelp += velDir * stickHelp.y;

            velHelp += myCamera.transform.right * stickHelp.x;
            velHelp.y = 0;

            // velHelp *= moveSpeed;
        }

        return new Vector3(velHelp.x, velHelp.y, velHelp.z);
    }
}
