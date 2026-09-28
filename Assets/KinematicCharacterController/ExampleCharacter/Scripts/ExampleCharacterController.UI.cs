using UnityEngine;
using TMPro;

namespace KinematicCharacterController.Examples
{
    public partial class ExampleCharacterController
    {
        [SerializeField] private TextMeshProUGUI speedDisplay;


        private void SetupSpeedDisplay()
        {
            if (speedDisplay == null)
            {
                GameObject canvasObj = new GameObject("SpeedDisplayCanvas");
                Canvas canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 2;
                canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                GameObject textObj = new GameObject("SpeedText");
                textObj.transform.SetParent(canvasObj.transform, false);

                speedDisplay = textObj.AddComponent<TextMeshProUGUI>();
                speedDisplay.fontSize = 36;
                speedDisplay.alignment = TextAlignmentOptions.Center;
                speedDisplay.color = Color.white;

                RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 1f);
                rectTransform.anchoredPosition = new Vector2(0f, -100f);
                rectTransform.sizeDelta = new Vector2(400f, 100f);
            }
        }


        private void UpdateSpeedDisplay()
        {
            if (speedDisplay != null)
            {
                Vector3 horizontalVelocity = new Vector3(Motor.BaseVelocity.x, 0f, Motor.BaseVelocity.z);
                currentSpeed = horizontalVelocity.magnitude;

                string colorTag = GetSpeedColorTag(currentSpeed);

                string momentumBar = GetMomentumBar();
                speedDisplay.text = $"{colorTag}{currentSpeed:F1}</color> m/s\n{momentumBar}\nDashes: {_airDashesRemaining}/{MaxAirDashes}";

            }
        }


        private string GetMomentumBar()
        {
            float fillPercent = MaxMomentumTank > 0f ? Mathf.Clamp01(_momentumTank / MaxMomentumTank) : 0f;
            int filledSegments = Mathf.RoundToInt(fillPercent * 10f);

            string bar = "Momentum: [";
            for (int i = 0; i < 10; i++)
            {
                bar += i < filledSegments ? "<color=#00FFFF>●</color>" : "<color=#808080>○</color>";
            }
            bar += $"] {_momentumTank:F1}/{MaxMomentumTank:F1}";
            return bar;
        }

        private string GetSpeedColorTag(float speed)
        {
            if (speed < 5f) return "<color=#FFFFFF>";
            if (speed < 10f) return "<color=#00FF00>";
            if (speed < 15f) return "<color=#FFFF00>";
            if (speed < 20f) return "<color=#FFA500>";
            return "<color=#FF0000>";
        }

    }
}
