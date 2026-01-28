using UnityEngine;
using UnityEngine.UI;
using FMODUnity;
using FMOD.Studio;

public class MusicVolumeController_FMOD : MonoBehaviour
{
    [SerializeField] private Slider volumeSlider;

    private Bus musicBus;
    private const string VolumeKey = "MusicVolume";

    private void Awake()
    {
        musicBus = RuntimeManager.GetBus("bus:/Music");
    }

    private void Start()
    {
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);

        volumeSlider.value = savedVolume;
        SetVolume(savedVolume);

        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    public void SetVolume(float value)
    {
        musicBus.setVolume(value);
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }
}