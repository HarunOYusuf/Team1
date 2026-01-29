using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

/// <summary>
/// Simplified shop manager for proof of concept.
/// No animations, no dialogue - just the core game loop.
/// </summary>
public class SimpleShopManager : MonoBehaviour
{
    [Header("Mask Display")]
    [SerializeField] private GameObject maskDisplayPanel;
    [SerializeField] private Image maskDisplayImage;
    [SerializeField] private float memoryDuration = 5f;
    
    [Header("UI")]
    [SerializeField] private GameObject createButtonPanel;
    [SerializeField] private TextMeshProUGUI timerText;  // Optional: shows memory countdown
    [SerializeField] private TextMeshProUGUI dayTimerText;  // Optional: shows day time remaining
    [SerializeField] private TextMeshProUGUI moneyText;  // Optional: shows total money
    [SerializeField] private TextMeshProUGUI scoreText;  // Optional: shows last score briefly
    
    [Header("Scene")]
    [SerializeField] private string paintingSceneName = "Harun";
    
    [Header("Debug")]
    [SerializeField] private bool skipMemoryPhase = false;  // For testing - skip straight to create button
    
    private bool isReturningFromPainting = false;
    
    void Start()
    {
        // Ensure GameData exists
        if (GameData.Instance == null)
        {
            Debug.LogError("[SimpleShopManager] GameData not found! Create a GameData object.");
            return;
        }
        
        // Hide panels initially
        if (maskDisplayPanel != null) maskDisplayPanel.SetActive(false);
        if (createButtonPanel != null) createButtonPanel.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);
        
        // Check if returning from painting
        if (GameData.Instance.lastScore > 0)
        {
            isReturningFromPainting = true;
            StartCoroutine(HandleReturnFromPainting());
        }
        else
        {
            // Fresh start or next customer
            if (!GameData.Instance.dayInProgress)
            {
                GameData.Instance.StartDay(GameData.Instance.currentDay);
            }
            StartCoroutine(StartNextCustomer());
        }
        
        UpdateUI();
    }
    
    void Update()
    {
        // Update day timer
        if (GameData.Instance != null && GameData.Instance.dayInProgress)
        {
            GameData.Instance.dayTimeRemaining -= Time.deltaTime;
            UpdateUI();
            
            // Check for day end
            if (GameData.Instance.dayTimeRemaining <= 0)
            {
                GameData.Instance.EndDay();
                StartCoroutine(HandleDayEnd());
            }
        }
    }
    
    void UpdateUI()
    {
        if (dayTimerText != null && GameData.Instance != null)
        {
            float time = Mathf.Max(0, GameData.Instance.dayTimeRemaining);
            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);
            dayTimerText.text = $"Time: {minutes}:{seconds:00}";
        }
        
        if (moneyText != null && GameData.Instance != null)
        {
            moneyText.text = $"${GameData.Instance.totalMoney}";
        }
    }
    
    IEnumerator HandleReturnFromPainting()
    {
        // Show score briefly
        if (scoreText != null)
        {
            bool passed = GameData.Instance.lastCustomerSatisfied;
            float score = GameData.Instance.lastScore;
            
            if (passed)
            {
                int payment = GameData.Instance.currentMask != null 
                    ? GameData.Instance.currentMask.CalculatePayment(score) 
                    : 50;
                scoreText.text = $"PASS! +${payment}";
                scoreText.color = Color.green;
            }
            else
            {
                scoreText.text = $"FAIL! Score: {score:F0}%";
                scoreText.color = Color.red;
            }
            
            scoreText.gameObject.SetActive(true);
            yield return new WaitForSeconds(2f);
            scoreText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(1f);
        }
        
        // Reset for next customer
        GameData.Instance.lastScore = 0;
        isReturningFromPainting = false;
        
        // Check if day is still going
        if (GameData.Instance.dayInProgress && GameData.Instance.dayTimeRemaining > 0)
        {
            StartCoroutine(StartNextCustomer());
        }
        else
        {
            StartCoroutine(HandleDayEnd());
        }
    }
    
    IEnumerator StartNextCustomer()
    {
        yield return new WaitForSeconds(0.5f);
        
        // Get random mask for this customer
        MaskData mask = GameData.Instance.GetRandomMaskForCurrentDay();
        
        if (mask == null)
        {
            Debug.LogError("[SimpleShopManager] No masks available!");
            yield break;
        }
        
        Debug.Log($"[SimpleShopManager] Customer wants: {mask.maskName}");
        
        if (skipMemoryPhase)
        {
            // Skip straight to create button (for testing)
            if (createButtonPanel != null) createButtonPanel.SetActive(true);
            yield break;
        }
        
        // Show the mask
        if (maskDisplayPanel != null && maskDisplayImage != null && mask.displayImage != null)
        {
            // Create sprite from texture
            Sprite maskSprite = Sprite.Create(
                mask.displayImage,
                new Rect(0, 0, mask.displayImage.width, mask.displayImage.height),
                new Vector2(0.5f, 0.5f)
            );
            maskDisplayImage.sprite = maskSprite;
            maskDisplayPanel.SetActive(true);
            
            // Memory countdown
            float timeLeft = mask.GetMemoryTime();
            while (timeLeft > 0)
            {
                if (timerText != null)
                {
                    timerText.text = $"Memorize! {Mathf.Ceil(timeLeft)}s";
                }
                timeLeft -= Time.deltaTime;
                yield return null;
            }
            
            // Hide mask
            maskDisplayPanel.SetActive(false);
            if (timerText != null) timerText.text = "";
        }
        
        // Show create button
        if (createButtonPanel != null)
        {
            createButtonPanel.SetActive(true);
        }
    }
    
    /// <summary>
    /// Called when player clicks Create button
    /// </summary>
    public void OnCreatePressed()
    {
        if (createButtonPanel != null) createButtonPanel.SetActive(false);
        
        // Go to painting scene
        SceneManager.LoadScene(paintingSceneName);
    }
    
    IEnumerator HandleDayEnd()
    {
        Debug.Log($"[SimpleShopManager] Day {GameData.Instance.currentDay} ended!");
        Debug.Log($"[SimpleShopManager] Customers served: {GameData.Instance.customersServedToday}");
        Debug.Log($"[SimpleShopManager] Total money: ${GameData.Instance.totalMoney}");
        
        // Show day end message
        if (scoreText != null)
        {
            scoreText.text = $"Day {GameData.Instance.currentDay} Complete!\nCustomers: {GameData.Instance.customersServedToday}\nMoney: ${GameData.Instance.totalMoney}";
            scoreText.color = Color.white;
            scoreText.gameObject.SetActive(true);
        }
        
        yield return new WaitForSeconds(3f);
        
        // Check if game over or next day
        if (GameData.Instance.currentDay >= 3)
        {
            // Game over
            if (scoreText != null)
            {
                scoreText.text = $"GAME OVER!\n\nFinal Earnings: ${GameData.Instance.totalMoney}";
            }
        }
        else
        {
            // Start next day
            GameData.Instance.StartDay(GameData.Instance.currentDay + 1);
            if (scoreText != null) scoreText.gameObject.SetActive(false);
            StartCoroutine(StartNextCustomer());
        }
    }
}