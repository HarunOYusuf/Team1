using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AdaptiveMusicManager : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference musicEvent;

    private EventInstance musicInstance;

    private const string MusicStateParam = "MusicState";
    private const float SHOP_STATE = 0f;
    // private const float WORK_STATE = 1f; // for later

    private void Start()
    {
        // Create and start music
        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        musicInstance.start();

        // Force shop state (important!)
        musicInstance.setParameterByName(MusicStateParam, SHOP_STATE);
    }

    private void OnDestroy()
    {
        // Clean up when scene unloads
        musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        musicInstance.release();
    }
}