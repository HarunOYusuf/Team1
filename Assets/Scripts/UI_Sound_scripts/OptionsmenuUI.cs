using UnityEngine;

public class OptionsmenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject optionsPanel;

    public void OpenOptions()
    {
        optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
    }
}
