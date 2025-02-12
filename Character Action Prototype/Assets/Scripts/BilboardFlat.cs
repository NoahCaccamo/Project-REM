using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BilboardFlat : MonoBehaviour
{

    public bool reverse;

    public Vector3 rotMin;
    public Vector3 rotMax;

    public bool strikeAngle;
    [UnityEngine.HideInInspector]
    Vector3 rotationOffset;

    // Start is called before the first frame update
    void Start()
    {
        rotationOffset = new Vector3(Random.Range(rotMin.x, rotMax.x), Random.Range(rotMin.y, rotMax.y), Random.Range(rotMin.z, rotMax.z));
    }

    // Update is called once per frame
    void Update()
    {
        if (reverse)
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * -Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
        else
        {
            transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                Camera.main.transform.rotation * Vector3.up);
        }
        //transform.localRotation *= Quaternion.Euiler(rotationOffset);
        transform.Rotate(rotationOffset);
    }
}
