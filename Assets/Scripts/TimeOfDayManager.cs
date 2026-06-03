using UnityEngine;
using System;
using System.Collections.Generic;

namespace ProjectREM
{
    [System.Serializable]
    public class DirectionalLightSettings
    {
        [Header("Light Properties")]
        public Color color = Color.white;
        [Range(0f, 8f)]
        public float intensity = 1f;
        [Range(0f, 360f)]
        public float rotationX = 50f;
        [Range(0f, 360f)]
        public float rotationY = 0f;

        [Header("Shadow Settings")]
        public LightShadows shadowType = LightShadows.Soft;
        [Range(0f, 1f)]
        public float shadowStrength = 1f;
    }

    [System.Serializable]
    public class LightGroup
    {
        public string groupName = "Light Group";
        public List<Light> lights = new List<Light>();

        [Header("Activation Time")]
        [Tooltip("Hour (0-24) when lights turn on")]
        [Range(0f, 24f)]
        public float activationTime = 18f;

        [Tooltip("Hour (0-24) when lights turn off")]
        [Range(0f, 24f)]
        public float deactivationTime = 6f;

        [Header("Light Settings")]
        [Range(0f, 8f)]
        public float targetIntensity = 1f;
        public Color targetColor = Color.white;

        [Header("Transition")]
        [Tooltip("Time in seconds to fade lights in/out")]
        public float fadeDuration = 2f;

        [HideInInspector]
        public bool isActive = false;
        [HideInInspector]
        public float currentFadeProgress = 0f;
    }

    public class TimeOfDayManager : MonoBehaviour
    {
        [Header("Time Settings")]
        [Tooltip("Current in-game time in hours (0-24)")]
        [Range(0f, 24f)]
        public float currentTime = 12f;

        [Tooltip("How many real seconds equal one in-game hour")]
        public float secondsPerHour = 60f;

        [Tooltip("Time scale multiplier (1 = normal speed, 2 = double speed, etc.)")]
        public float timeScale = 1f;

        [Tooltip("If true, time will progress automatically")]
        public bool autoAdvanceTime = true;

        [Header("Day/Night Cycle")]
        [Tooltip("Hour when day transitions to night")]
        [Range(0f, 24f)]
        public float nightStartTime = 18f;

        [Tooltip("Hour when night transitions to day")]
        [Range(0f, 24f)]
        public float dayStartTime = 6f;

        [Tooltip("Duration of transition between day and night in game hours")]
        public float transitionDuration = 1f;

        [Header("Directional Light")]
        public Light directionalLight;
        public DirectionalLightSettings daylightSettings = new DirectionalLightSettings();
        public DirectionalLightSettings nightSettings = new DirectionalLightSettings();

        [Header("Light Groups")]
        public List<LightGroup> lightGroups = new List<LightGroup>();

        [Header("Events")]
        public UnityEngine.Events.UnityEvent onDayStart;
        public UnityEngine.Events.UnityEvent onNightStart;

        private bool _wasDay = true;
        private float _dayNightBlend = 0f; // 0 = day, 1 = night

        public int CurrentDay { get; private set; } = 1;
        public int CurrentHour => Mathf.FloorToInt(currentTime);
        public int CurrentMinute => Mathf.FloorToInt((currentTime - CurrentHour) * 60f);
        public bool IsDay => _dayNightBlend < 0.5f;

        private void Start()
        {
            if (directionalLight == null)
            {
                Debug.LogWarning("TimeOfDayManager: No directional light assigned!");
            }

            // Initialize light groups
            foreach (LightGroup group in lightGroups)
            {
                UpdateLightGroup(group, true);
            }

            UpdateLighting();
            _wasDay = IsDay; // Initialize to prevent false event triggers
        }

        private void Update()
        {
            if (autoAdvanceTime)
            {
                AdvanceTime(Time.deltaTime * timeScale);
            }

            UpdateLighting();
            UpdateLightGroups();
        }

        public void AdvanceTime(float realTimeSeconds)
        {
            float hoursToAdd = (realTimeSeconds / secondsPerHour);
            currentTime += hoursToAdd;

            if (currentTime >= 24f)
            {
                currentTime -= 24f;
                CurrentDay++;
            }
        }

        public void SetTime(float hour, bool immediate = false)
        {
            currentTime = Mathf.Clamp(hour, 0f, 24f);

            if (immediate)
            {
                UpdateLighting();
                foreach (LightGroup group in lightGroups)
                {
                    UpdateLightGroup(group, true);
                }
            }
        }

        private void UpdateLighting()
        {
            if (directionalLight == null) return;

            // Calculate day/night blend
            _dayNightBlend = CalculateDayNightBlend();

            // Lerp between day and night settings
            directionalLight.color = Color.Lerp(daylightSettings.color, nightSettings.color, _dayNightBlend);
            directionalLight.intensity = Mathf.Lerp(daylightSettings.intensity, nightSettings.intensity, _dayNightBlend);
            directionalLight.shadowStrength = Mathf.Lerp(daylightSettings.shadowStrength, nightSettings.shadowStrength, _dayNightBlend);

            // Lerp rotation
            float targetRotX = Mathf.Lerp(daylightSettings.rotationX, nightSettings.rotationX, _dayNightBlend);
            float targetRotY = Mathf.Lerp(daylightSettings.rotationY, nightSettings.rotationY, _dayNightBlend);
            // directionalLight.transform.rotation = Quaternion.Euler(targetRotX, targetRotY, 0f);

            // Handle shadow type (can't lerp enum, so switch at midpoint)
            directionalLight.shadows = _dayNightBlend < 0.5f ? daylightSettings.shadowType : nightSettings.shadowType;

            // Fire events on day/night transitions
            bool isDay = _dayNightBlend < 0.5f;
            if (isDay != _wasDay)
            {
                if (isDay)
                {
                    onDayStart?.Invoke();
                }
                else
                {
                    onNightStart?.Invoke();
                }
                _wasDay = isDay;
            }
        }

        private float CalculateDayNightBlend()
        {
            float blend = 0f;

            // Check if night wraps around midnight (e.g., 18:00 to 6:00)
            // This happens when nightStartTime > dayStartTime
            if (nightStartTime > dayStartTime)
            {
                // Night wraps around midnight (e.g., 18:00 to 6:00)
                if (currentTime >= nightStartTime || currentTime < dayStartTime)
                {
                    // We're in the night period or transitioning
                    if (currentTime >= nightStartTime)
                    {
                        // Evening: transitioning from day to night (18:00 -> 19:00)
                        float timeSinceNightStart = currentTime - nightStartTime;
                        blend = Mathf.Clamp01(timeSinceNightStart / transitionDuration);
                    }
                    else // currentTime < dayStartTime
                    {
                        // Early morning: could be full night or transitioning to day
                        float timeUntilDayStart = dayStartTime - currentTime;

                        if (timeUntilDayStart <= transitionDuration)
                        {
                            // Morning transition: blend from 1 (night) toward 0 (day)
                            blend = Mathf.Clamp01(timeUntilDayStart / transitionDuration);
                        }
                        else
                        {
                            // Full night (between transitions)
                            blend = 1f;
                        }
                    }
                }
                else
                {
                    // Full day (between dayStartTime and nightStartTime)
                    blend = 0f;
                }
            }
            else
            {
                // Unusual case: day wraps around midnight (nightStartTime < dayStartTime)
                // e.g., night = 6:00, day = 18:00 means night during day hours
                if (currentTime >= nightStartTime && currentTime < dayStartTime)
                {
                    float timeSinceNightStart = currentTime - nightStartTime;
                    blend = Mathf.Clamp01(timeSinceNightStart / transitionDuration);
                }
                else
                {
                    blend = 0f;
                }
            }

            return blend;
        }

        private void UpdateLightGroups()
        {
            foreach (LightGroup group in lightGroups)
            {
                UpdateLightGroup(group, false);
            }
        }

        private void UpdateLightGroup(LightGroup group, bool immediate)
        {
            bool shouldBeActive = ShouldLightGroupBeActive(group);

            if (immediate)
            {
                group.isActive = shouldBeActive;
                group.currentFadeProgress = shouldBeActive ? 1f : 0f;
                ApplyLightGroupSettings(group);
            }
            else
            {
                // Handle fading
                if (shouldBeActive != group.isActive)
                {
                    group.isActive = shouldBeActive;
                    // Start fade transition
                }

                // Update fade progress
                float fadeSpeed = group.fadeDuration > 0f ? 1f / group.fadeDuration : 1000f;
                if (group.isActive)
                {
                    group.currentFadeProgress = Mathf.Min(1f, group.currentFadeProgress + Time.deltaTime * fadeSpeed);
                }
                else
                {
                    group.currentFadeProgress = Mathf.Max(0f, group.currentFadeProgress - Time.deltaTime * fadeSpeed);
                }

                ApplyLightGroupSettings(group);
            }
        }

        private bool ShouldLightGroupBeActive(LightGroup group)
        {
            if (group.activationTime < group.deactivationTime)
            {
                // Active during normal hours (e.g., 8:00 to 18:00)
                return currentTime >= group.activationTime && currentTime < group.deactivationTime;
            }
            else
            {
                // Active across midnight (e.g., 18:00 to 6:00)
                return currentTime >= group.activationTime || currentTime < group.deactivationTime;
            }
        }

        private void ApplyLightGroupSettings(LightGroup group)
        {
            foreach (Light light in group.lights)
            {
                if (light == null) continue;

                light.enabled = group.currentFadeProgress > 0f;
                light.intensity = group.targetIntensity * group.currentFadeProgress;
                light.color = group.targetColor;
            }
        }

        public string GetFormattedTime()
        {
            return $"{CurrentHour:D2}:{CurrentMinute:D2}";
        }

        public string GetFormattedDateTime()
        {
            return $"Day {CurrentDay}, {GetFormattedTime()}";
        }

        private void OnValidate()
        {
            currentTime = Mathf.Clamp(currentTime, 0f, 24f);
            timeScale = Mathf.Max(0f, timeScale);
            secondsPerHour = Mathf.Max(0.1f, secondsPerHour);
            transitionDuration = Mathf.Max(0.01f, transitionDuration);
        }
    }
}