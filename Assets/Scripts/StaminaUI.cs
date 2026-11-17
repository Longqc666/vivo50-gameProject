using UnityEngine;
using UnityEngine.UI;

public class StaminaUI : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Slider staminaSlider;

    private void Start()
    {
        if (player == null || staminaSlider == null)
        {
            Debug.LogError("StaminaUI：请连接 Player 和 Stamina Slider。", this);
            enabled = false;
            return;
        }

        staminaSlider.minValue = 0f;
        staminaSlider.maxValue = 1f;
        staminaSlider.wholeNumbers = false;
        staminaSlider.interactable = false;
        staminaSlider.value = player.StaminaRatio;
    }

    private void Update()
    {
        staminaSlider.value = player.StaminaRatio;
    }
}