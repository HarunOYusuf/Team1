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
        // Defensive check to prevent NullReferenceException
        if (brightnessOverlay == null || brightnessSlider == null)
        {
            Debug.LogError(
                "BrightnessController ERROR: Missing references.\n" +
                "Ensure BrightnessOverlay (Image) and BrightnessSlider (Slider) are assigned."
            );
            return;
        }

        // Load saved brightness (default = middle)
        float savedBrightness = PlayerPrefs.GetFloat(BrightnessKey, 0.5f);

        // Set slider without firing events
        brightnessSlider.SetValueWithoutNotify(savedBrightness);

        // Apply brightness once at startup
        ApplyBrightness(savedBrightness);

        // Listen for changes AFTER setup
        brightnessSlider.onValueChanged.AddListener(ApplyBrightness);
    }

    public void ApplyBrightness(float value)
    {
        // Invert so slider UP = brighter
        float invertedValue = 1f - value;

        // Map to safe darkness range
        float darkness = Mathf.Lerp(minDarkness, maxDarkness, invertedValue);

        // Apply overlay alpha
        Color color = brightnessOverlay.color;
        color.a = darkness;
        brightnessOverlay.color = color;

        // Save preference
        PlayerPrefs.SetFloat(BrightnessKey, value);
        PlayerPrefs.Save();
    }
}