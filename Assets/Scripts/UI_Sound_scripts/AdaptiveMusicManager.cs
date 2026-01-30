using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class AdaptiveMusicManager : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference musicEvent;

    private EventInstance musicInstance;

    // FMOD parameter
    private const string MUSIC_STATE_PARAM = "MusicState";

    // Music states
    private const float SHOP_VIEW = 0f;
    private const float PAINTING_VIEW = 1f;

    private void Awake()
    {
        // Ensure only one MusicManager exists
        if (FindObjectsOfType<AdaptiveMusicManager>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        musicInstance = RuntimeManager.CreateInstance(musicEvent);
        musicInstance.start();

        // Default to shop music
        SetShopViewMusic();
    }

    public void SetShopViewMusic()
    {
        musicInstance.setParameterByName(MUSIC_STATE_PARAM, SHOP_VIEW);
    }

    public void SetPaintingViewMusic()
    {
        musicInstance.setParameterByName(MUSIC_STATE_PARAM, PAINTING_VIEW);
    }

    private void OnDestroy()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }
    }
}