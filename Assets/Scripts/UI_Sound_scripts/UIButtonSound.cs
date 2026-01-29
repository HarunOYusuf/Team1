using UnityEngine;
using FMODUnity;


public class UIButtonSound : MonoBehaviour

{
    [SerializeField]
    private EventReference buttonPressEvent;

    public void PlayButtonSound()
    {
        RuntimeManager.PlayOneShot(buttonPressEvent);
    }
}