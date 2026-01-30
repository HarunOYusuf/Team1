using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Main Menu UI Controller
/// Handles New Game and Exit buttons
/// Options menu is handled by teammate's script
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Name of your game scene (must be added to Build Settings)")]
    [SerializeField] private string gameSceneName = "GameScene";
    
    /// <summary>
    /// Called when New Game button is pressed
    /// </summary>
    public void OnNewGamePressed()
    {
        Debug.Log("[MainMenu] Starting new game...");
        SceneManager.LoadScene(gameSceneName);
    }
    
    /// <summary>
    /// Called when Exit button is pressed
    /// </summary>
    public void OnExitPressed()
    {
        Debug.Log("[MainMenu] Exiting game...");
        
        #if UNITY_EDITOR
            // Stop play mode in editor
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            // Quit application in build
            Application.Quit();
        #endif
    }
}