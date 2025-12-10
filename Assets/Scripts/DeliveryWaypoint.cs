using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DeliveryWaypoint : MonoBehaviour
{
    public Image img;
    public Vector3 target;
    public TextMeshProUGUI meter;
    public Vector3 offset;

    private bool enabled = false;

    void Update()
    {
        if (!enabled) { return; }
        if (target == null) { return; }

        float minX = img.GetPixelAdjustedRect().width / 2;
        float maxX = Screen.width - minX;

        float minY = img.GetPixelAdjustedRect().height / 2;
        float maxY = Screen.height - minY;

        Vector2 pos = Camera.main.WorldToScreenPoint(target + offset);

        if (Vector3.Dot((target - transform.position), transform.forward) < 0)
        {
            // Target is behind the player
            if (pos.x < Screen.width / 2)
            {
                pos.x = maxX;
            }
            else
            {
                pos.x = minX;
            }
        }

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = Mathf.Clamp(pos.y, minY, maxY);

        img.transform.position = pos;
        meter.text = ((int)Vector3.Distance(target, transform.position)).ToString() + "m";
    }

    public void Enable()
    {
        enabled = true;
        img.gameObject.SetActive(true);
    }

    public void Disable()
    {
        enabled = false;
        img.gameObject.SetActive(false);
    }
}
