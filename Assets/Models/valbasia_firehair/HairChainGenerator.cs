using UnityEngine;

public class HairChainGenerator : MonoBehaviour
{
    [Header("Chain Settings")]
    public GameObject hairSegmentPrefab;
    public int chainLength = 10;
    public float boneSpacing = 0.5f;

    [Header("Initial Offset Direction")]
    public Vector3 initialDirection = Vector3.down;

    [Header("Parenting")]
    public bool makeChildrenOfGenerator = true;

    private Transform[] bones;

    void Start()
    {
        GenerateChain();
    }

    void GenerateChain()
    {
        if (!hairSegmentPrefab)
        {
            Debug.LogError("No hair segment prefab assigned.");
            return;
        }

        bones = new Transform[chainLength];

        Vector3 currentPos = transform.position;
        Transform previousBone = transform;

        for (int i = 0; i < chainLength; i++)
        {
            GameObject segment = Instantiate(hairSegmentPrefab, currentPos, Quaternion.identity);
            segment.name = $"HairSegment_{i}";

            if (makeChildrenOfGenerator)
                segment.transform.parent = this.transform;

            // Set position along initial direction
            currentPos += initialDirection.normalized * boneSpacing;

            // Connect to previous
            HairBone boneScript = segment.GetComponent<HairBone>();
            if (boneScript)
            {
                boneScript.target = previousBone;
                boneScript.boneLength = boneSpacing;
            }

            previousBone = segment.transform;
            bones[i] = segment.transform;
        }
    }
}
