using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeliveryJobEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI themeNameText;
    //[SerializeField] private TextMeshProUGUI pickupText;
    //[SerializeField] private TextMeshProUGUI dropoffText;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image accentColorImage;

    private DeliveryJob job;
    private DeliveryJobUIController controller;

    public void Setup(DeliveryJob deliveryJob, DeliveryJobUIController uiController)
    {
        job = deliveryJob;
        controller = uiController;

        if (job.memoryType != null)
        {
            themeNameText.text = job.memoryType.themeName;
            if (accentColorImage != null)
                accentColorImage.color = job.memoryType.accentColor;
        }

        themeNameText.text = job.memoryType.themeName;
        // pickupText.text = $"Pickup: {job.pickupLocation}";
        // dropoffText.text = $"Dropoff: {job.dropoffLocation}";
        // F0 formats to no decimal places
        // F2 formats to two decimal places
        distanceText.text = $"{job.distance:F0}km";
        rewardText.text = $"${job.rewardAmount:F2}";
    }

    public void OnSelectJob()
    {
        controller?.SelectJob(job);
    }
}