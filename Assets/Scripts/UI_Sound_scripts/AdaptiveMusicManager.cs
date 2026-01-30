using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AdaptiveMusicController : MonoBehaviour
{
    [Header("View References")]
    public GameObject shopView;       // "SHOP VIEW"
    public GameObject paintingView;   // "PAINTING VIEW"

    [Header("FMOD")]
    public StudioEventEmitter musicEmitter;
    public string musicParameterName = "MusicState";

    private EventInstance musicInstance;
    private int lastState = -1;

    void Start()
    {
        // Safety checks
        if (musicEmitter == null)
        {
            Debug.LogError("AdaptiveMusicController: Music Emitter not assigned.");
            return;
        }

        musicInstance = musicEmitter.EventInstance;

        // Game ALWAYS starts in shop view
        SetMusicState(0);
    }

    void Update()
    {
        if (shopView != null && shopView.activeInHierarchy)
        {
            SetMusicState(0);
        }
        else if (paintingView != null && paintingView.activeInHierarchy)
        {
            SetMusicState(1);
        }
    }

    void SetMusicState(int state)
    {
        if (state == lastState) return;

        musicInstance.setParameterByName(musicParameterName, state);
        lastState = state;
    }
}