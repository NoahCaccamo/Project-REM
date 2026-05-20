using KinematicCharacterController.Examples;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages per-hand stamina for fish wrangling.
/// Stamina drains while grabbing, drains faster during bucking with two hands.
/// Visual feedback shows red tint as stamina depletes.
/// When stamina hits zero, hand slips off and cannot grab for cooldown period.
/// </summary>
public class HandStaminaSystem : MonoBehaviour
{
    [Header("Stamina Settings")]
    public float maxStamina = 100f;
    public float staminaDrainRate = 5f; // Per second while grabbing
    public float staminaRegenRate = 10f; // Per second when not grabbing
    public float slipCooldown = 2f;

    [Header("Visual Feedback")]
    public HandController leftHandController;
    public HandController rightHandController;

    // Current stamina
    private float leftStamina;
    private float rightStamina;

    // Slip state
    private bool leftHandSlipped = false;
    private bool rightHandSlipped = false;
    private float leftSlipTimer = 0f;
    private float rightSlipTimer = 0f;

    private ExampleCharacterController characterController;

    void Start()
    {
        characterController = GetComponent<ExampleCharacterController>();
        leftStamina = maxStamina;
        rightStamina = maxStamina;

        if (leftHandController == null)
        {
            leftHandController = characterController.LeftHandController;
        }

        if (rightHandController == null)
        {
            rightHandController = characterController.RightHandController;
        }
    }

    void Update()
    {
        UpdateStamina();
        UpdateSlipCooldowns();
        UpdateVisuals();
    }

    private void UpdateStamina()
    {
        // Left hand
        if (characterController.isGrabbingL && !leftHandSlipped)
        {
            leftStamina -= staminaDrainRate * Time.deltaTime;

            if (leftStamina <= 0f)
            {
                OnHandSlip(true);
            }
        }
        else if (!characterController.isGrabbingL)
        {
            leftStamina += staminaRegenRate * Time.deltaTime;
            leftStamina = Mathf.Clamp(leftStamina, 0f, maxStamina);
        }

        // Right hand
        if (characterController.isGrabbingR && !rightHandSlipped)
        {
            rightStamina -= staminaDrainRate * Time.deltaTime;

            if (rightStamina <= 0f)
            {
                OnHandSlip(false);
            }
        }
        else if (!characterController.isGrabbingR)
        {
            rightStamina += staminaRegenRate * Time.deltaTime;
            rightStamina = Mathf.Clamp(rightStamina, 0f, maxStamina);
        }
    }

    private void UpdateSlipCooldowns()
    {
        if (leftHandSlipped)
        {
            leftSlipTimer -= Time.deltaTime;
            if (leftSlipTimer <= 0f)
            {
                leftHandSlipped = false;
                leftStamina = maxStamina * 0.5f; // Recover to 50% after cooldown
            }
        }

        if (rightHandSlipped)
        {
            rightSlipTimer -= Time.deltaTime;
            if (rightSlipTimer <= 0f)
            {
                rightHandSlipped = false;
                rightStamina = maxStamina * 0.5f;
            }
        }
    }

    private void OnHandSlip(bool isLeftHand)
    {
        if (isLeftHand)
        {
            leftHandSlipped = true;
            leftSlipTimer = slipCooldown;
            leftStamina = 0f;

            // Force release hand
            characterController.isGrabbingL = false;
            if (leftHandController != null)
            {
                leftHandController.StopGrab();
            }

            Debug.Log("LEFT HAND SLIPPED! Stamina depleted!");
        }
        else
        {
            rightHandSlipped = true;
            rightSlipTimer = slipCooldown;
            rightStamina = 0f;

            // Force release hand
            characterController.isGrabbingR = false;
            if (rightHandController != null)
            {
                rightHandController.StopGrab();
            }

            Debug.Log("RIGHT HAND SLIPPED! Stamina depleted!");
        }
    }

    private void UpdateVisuals()
    {
        // Update left hand color (white to red based on stamina)
        if (leftHandController != null && leftHandController.spriteRenderer != null)
        {
            float t = 1f - (leftStamina / maxStamina);
            Color handColor = Color.Lerp(Color.white, Color.red, t);
            leftHandController.spriteRenderer.color = handColor;
        }

        // Update right hand color
        if (rightHandController != null && rightHandController.spriteRenderer != null)
        {
            float t = 1f - (rightStamina / maxStamina);
            Color handColor = Color.Lerp(Color.white, Color.red, t);
            rightHandController.spriteRenderer.color = handColor;
        }
    }

    public void DrainStamina(bool isLeftHand, float amount)
    {
        if (isLeftHand)
        {
            leftStamina -= amount;
            leftStamina = Mathf.Clamp(leftStamina, 0f, maxStamina);

            if (leftStamina <= 0f && !leftHandSlipped)
            {
                OnHandSlip(true);
            }
        }
        else
        {
            rightStamina -= amount;
            rightStamina = Mathf.Clamp(rightStamina, 0f, maxStamina);

            if (rightStamina <= 0f && !rightHandSlipped)
            {
                OnHandSlip(false);
            }
        }
    }

    public bool CanGrab(bool isLeftHand)
    {
        return isLeftHand ? !leftHandSlipped : !rightHandSlipped;
    }

    public float GetStamina(bool isLeftHand)
    {
        return isLeftHand ? leftStamina : rightStamina;
    }
}