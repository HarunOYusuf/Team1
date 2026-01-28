using UnityEngine;
using UnityEngine.UI;

public class BrightnessController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image brightnessOverlay;
    [SerializeField] private Slider brightnessSlider;

    private const string BrightnessKey = "Brightness";

    [Header("Brightness Limits")]
    [Tooltip("How bright the screen can get (lower = brighter)")]
    [SerializeField] private float minDarkness = 0.15f;

    [Tooltip("How dark the screen can get (higher = darker)")]
    [SerializeField] private float maxDarkness = 0.75f;

    private void Start()
    {
        // Load saved brightness or default to middle
        float savedBrightness = PlayerPrefs.GetFloat(BrightnessKey, 0.5f);

        // Set slider without triggering callbacks
        brightnessSlider.SetValueWithoutNotify(savedBrightness);

        // Apply brightness once on startup
        ApplyBrightness(savedBrightness);

        // Listen for user changes AFTER setup
        brightnessSlider.onValueChanged.AddListener(ApplyBrightness);
    }

    public void ApplyBrightness(float value)
    {
        // Invert so slider UP = brighter
        float invertedValue = 1f - value;

        // Map into safe darkness range
        float darkness = Mathf.Lerp(minDarkness, maxDarkness, invertedValue);

        // Apply overlay alpha
        Color color = brightnessOverlay.color;
        color.a = darkness;
        brightnessOverlay.color = color;

        // Save setting
        PlayerPrefs.SetFloat(BrightnessKey, value);
        PlayerPrefs.Save();
    }
}