using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton that persists between scenes.
/// Holds the current game state, selected mask, scores, etc.
/// </summary>
public class GameData : MonoBehaviour
{
    // Singleton instance
    public static GameData Instance { get; private set; }
    
    [Header("Current Game State")]
    public int currentDay = 1;
    public int totalMoney = 0;
    public int customersServedToday = 0;
    public int customersSatisfiedToday = 0;
    
    [Header("Current Customer")]
    public MaskData currentMask;
    public float lastScore = 0f;
    public bool lastCustomerSatisfied = false;
    
    [Header("Day Settings")]
    public float dayDuration = 120f; // 2 minutes per day
    public float dayTimeRemaining;
    public bool dayInProgress = false;
    
    [Header("All Available Masks")]
    public List<MaskData> allMasks = new List<MaskData>();
    
    [Header("Day Results")]
    public List<DayResult> dayResults = new List<DayResult>();
    
    void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeGame();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void InitializeGame()
    {
        currentDay = 1;
        totalMoney = 0;
        dayTimeRemaining = dayDuration;
        dayResults.Clear();
        
        Debug.Log("[GameData] Initialized - Day 1 starting");
    }
    
    /// <summary>
    /// Start a new day
    /// </summary>
    public void StartDay(int day)
    {
        currentDay = day;
        dayTimeRemaining = dayDuration;
        dayInProgress = true;
        customersServedToday = 0;
        customersSatisfiedToday = 0;
        
        Debug.Log($"[GameData] Day {day} started - {dayDuration} seconds");
    }
    
    /// <summary>
    /// Get a random mask appropriate for the current day
    /// </summary>
    public MaskData GetRandomMaskForCurrentDay()
    {
        List<MaskData> availableMasks = new List<MaskData>();
        
        foreach (MaskData mask in allMasks)
        {
            if (mask.availableOnDay <= currentDay)
            {
                availableMasks.Add(mask);
            }
        }
        
        if (availableMasks.Count == 0)
        {
            Debug.LogError("[GameData] No masks available for day " + currentDay);
            return null;
        }
        
        int randomIndex = Random.Range(0, availableMasks.Count);
        currentMask = availableMasks[randomIndex];
        
        Debug.Log($"[GameData] Selected mask: {currentMask.maskName}");
        return currentMask;
    }
    
    /// <summary>
    /// Record the result of a mask painting
    /// </summary>
    public void RecordScore(float score)
    {
        lastScore = score;
        lastCustomerSatisfied = score >= 60f;
        customersServedToday++;
        
        if (lastCustomerSatisfied)
        {
            customersSatisfiedToday++;
            int payment = currentMask.CalculatePayment(score);
            totalMoney += payment;
            
            Debug.Log($"[GameData] Customer satisfied! Score: {score:F1}%, Payment: ${payment}");
        }
        else
        {
            Debug.Log($"[GameData] Customer unsatisfied. Score: {score:F1}%");
        }
    }
    
    /// <summary>
    /// End the current day and record results
    /// </summary>
    public void EndDay()
    {
        dayInProgress = false;
        
        DayResult result = new DayResult
        {
            day = currentDay,
            customersServed = customersServedToday,
            customersSatisfied = customersSatisfiedToday,
            moneyEarned = totalMoney // This should be calculated differently if tracking per-day
        };
        
        dayResults.Add(result);
        
        Debug.Log($"[GameData] Day {currentDay} ended - Served: {customersServedToday}, Satisfied: {customersSatisfiedToday}");
    }
    
    /// <summary>
    /// Check if game is over (all 3 days complete)
    /// </summary>
    public bool IsGameOver()
    {
        return currentDay > 3 && !dayInProgress;
    }
    
    /// <summary>
    /// Reset for new game
    /// </summary>
    public void ResetGame()
    {
        InitializeGame();
    }
}

/// <summary>
/// Stores results for a single day
/// </summary>
[System.Serializable]
public class DayResult
{
    public int day;
    public int customersServed;
    public int customersSatisfied;
    public int moneyEarned;
}