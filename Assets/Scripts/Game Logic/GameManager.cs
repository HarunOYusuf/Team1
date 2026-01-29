using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// Main game flow controller.
/// Manages game states, transitions, and coordinates between systems.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [Header("Game State")]
    public GameState currentState = GameState.Idle;
    
    [Header("Scene Names")]
    [Tooltip("The shop/customer scene name")]
    public string shopSceneName = "ShopScene";
    [Tooltip("The painting scene name")]
    public string paintingSceneName = "Harun";
    
    [Header("Day Timer UI")]
    [Tooltip("Text showing time remaining (optional)")]
    public TextMeshProUGUI dayTimerText;
    
    [Header("Settings")]
    public float memoryPhaseDuration = 5f; // Overridden by mask difficulty
    public float passingScore = 60f;
    
    [Header("Events - TEAMMATE: Connect your systems here")]
    // These can be UnityEvents if you want Inspector hookups
    public System.Action OnCustomerEnter;
    public System.Action OnShowMask;
    public System.Action OnPaintingStart;
    public System.Action<float> OnScoringComplete; // passes score
    public System.Action<bool> OnCustomerReaction; // passes satisfied (true/false)
    public System.Action OnCustomerExit;
    public System.Action OnDayEnd;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        // Ensure GameData exists
        if (GameData.Instance == null)
        {
            Debug.LogWarning("[GameManager] GameData not found! Creating one...");
            GameObject dataObj = new GameObject("GameData");
            dataObj.AddComponent<GameData>();
        }
    }
    
    void Update()
    {
        // Update day timer if day is in progress
        if (GameData.Instance != null && GameData.Instance.dayInProgress)
        {
            GameData.Instance.dayTimeRemaining -= Time.deltaTime;
            
            // Update timer UI
            if (dayTimerText != null)
            {
                float time = GameData.Instance.dayTimeRemaining;
                int minutes = Mathf.FloorToInt(time / 60);
                int seconds = Mathf.FloorToInt(time % 60);
                dayTimerText.text = $"{minutes}:{seconds:00}";
            }
            
            // Check for day end
            if (GameData.Instance.dayTimeRemaining <= 0)
            {
                EndDay();
            }
        }
    }
    
    #region Game Flow Methods
    
    /// <summary>
    /// Start a new game from Day 1
    /// </summary>
    public void StartNewGame()
    {
        Debug.Log("[GameManager] Starting new game");
        GameData.Instance.ResetGame();
        StartDay(1);
    }
    
    /// <summary>
    /// Start a specific day
    /// </summary>
    public void StartDay(int day)
    {
        Debug.Log($"[GameManager] Starting Day {day}");
        GameData.Instance.StartDay(day);
        currentState = GameState.DayStart;
        
        // Begin customer loop
        StartCoroutine(DayLoop());
    }
    
    /// <summary>
    /// Main day loop - spawns customers until time runs out
    /// </summary>
    IEnumerator DayLoop()
    {
        yield return new WaitForSeconds(1f); // Brief pause at day start
        
        while (GameData.Instance.dayInProgress && GameData.Instance.dayTimeRemaining > 0)
        {
            // Start next customer
            yield return StartCoroutine(CustomerSequence());
            
            // Brief pause between customers
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    /// <summary>
    /// Single customer sequence
    /// </summary>
    IEnumerator CustomerSequence()
    {
        // 1. Customer enters
        currentState = GameState.CustomerEnter;
        MaskData mask = GameData.Instance.GetRandomMaskForCurrentDay();
        
        if (mask == null)
        {
            Debug.LogError("[GameManager] No mask available!");
            yield break;
        }
        
        OnCustomerEnter?.Invoke();
        Debug.Log($"[GameManager] Customer wants: {mask.maskName}");
        
        // TEAMMATE: Your InteractionManager handles the walking animation
        // Wait for it to complete (or use a callback)
        yield return new WaitForSeconds(2f); // Placeholder wait
        
        // 2. Show mask
        currentState = GameState.ShowMask;
        OnShowMask?.Invoke();
        
        float memoryTime = mask.GetMemoryTime();
        Debug.Log($"[GameManager] Showing mask for {memoryTime} seconds");
        
        // TEAMMATE: Display the mask image here
        // Your code to show mask.displayImage
        
        yield return new WaitForSeconds(memoryTime);
        
        // 3. Transition to painting
        currentState = GameState.TransitionToPainting;
        OnPaintingStart?.Invoke();
        
        // Load painting scene
        SceneManager.LoadScene(paintingSceneName);
        
        // The painting scene will handle itself and call OnPaintingComplete when done
        yield break; // Exit this coroutine, painting scene takes over
    }
    
    /// <summary>
    /// Called by MaskPaintingPDollar when player submits their painting
    /// </summary>
    public void OnPaintingComplete(float score)
    {
        Debug.Log($"[GameManager] Painting complete - Score: {score:F1}%");
        
        // Record the score
        GameData.Instance.RecordScore(score);
        
        currentState = GameState.Scoring;
        OnScoringComplete?.Invoke(score);
        
        // Return to shop scene for customer reaction
        StartCoroutine(ReturnToShopSequence());
    }
    
    /// <summary>
    /// Return to shop and show customer reaction
    /// </summary>
    IEnumerator ReturnToShopSequence()
    {
        yield return new WaitForSeconds(1f); // Show score briefly
        
        // Load shop scene
        SceneManager.LoadScene(shopSceneName);
        
        yield return new WaitForSeconds(0.5f); // Wait for scene load
        
        // Show customer reaction
        currentState = GameState.CustomerReaction;
        bool satisfied = GameData.Instance.lastCustomerSatisfied;
        OnCustomerReaction?.Invoke(satisfied);
        
        // TEAMMATE: Trigger happy/sad animation here
        Debug.Log($"[GameManager] Customer {(satisfied ? "HAPPY" : "SAD")}");
        
        yield return new WaitForSeconds(2f); // Reaction time
        
        // Customer exits
        currentState = GameState.CustomerExit;
        OnCustomerExit?.Invoke();
        
        // TEAMMATE: Your InteractionManager.NPCExitsShop() here
        
        yield return new WaitForSeconds(1.5f); // Exit animation
        
        // Ready for next customer (DayLoop will continue)
    }
    
    /// <summary>
    /// End the current day
    /// </summary>
    public void EndDay()
    {
        Debug.Log("[GameManager] Day ended!");
        GameData.Instance.EndDay();
        currentState = GameState.DayEnd;
        
        OnDayEnd?.Invoke();
        
        // Show day summary
        // TEAMMATE: Display day results UI here
        
        // Check if game is over
        if (GameData.Instance.currentDay >= 3)
        {
            currentState = GameState.GameOver;
            Debug.Log("[GameManager] Game Over!");
            // Show final results
        }
    }
    
    /// <summary>
    /// Progress to next day (call from UI button)
    /// </summary>
    public void NextDay()
    {
        int nextDay = GameData.Instance.currentDay + 1;
        if (nextDay <= 3)
        {
            StartDay(nextDay);
        }
    }
    
    #endregion
    
    #region Helper Methods for Scene Communication
    
    /// <summary>
    /// Get the current mask (for painting scene to use)
    /// </summary>
    public MaskData GetCurrentMask()
    {
        return GameData.Instance?.currentMask;
    }
    
    /// <summary>
    /// Check if customer was satisfied (for shop scene reaction)
    /// </summary>
    public bool WasCustomerSatisfied()
    {
        return GameData.Instance?.lastCustomerSatisfied ?? false;
    }
    
    /// <summary>
    /// Get the last score
    /// </summary>
    public float GetLastScore()
    {
        return GameData.Instance?.lastScore ?? 0f;
    }
    
    #endregion
}

/// <summary>
/// All possible game states
/// </summary>
public enum GameState
{
    Idle,
    DayStart,
    CustomerEnter,
    ShowMask,
    TransitionToPainting,
    Painting,
    Scoring,
    CustomerReaction,
    CustomerExit,
    DayEnd,
    GameOver
}