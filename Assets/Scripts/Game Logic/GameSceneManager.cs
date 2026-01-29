using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Main Game Scene Manager - Controls the entire game loop
/// Customer → Memory → Painting → Scoring → Reaction → Loop
/// </summary>
public class GameSceneManager : MonoBehaviour
{
    public static GameSceneManager Instance { get; private set; }

    [Header("Game State")]
    public GamePhase currentState = GamePhase.Idle;
    
    [Header("Views")]
    [SerializeField] private GameObject shopView;
    [SerializeField] private GameObject paintingView;
    
    [Header("UI Panels")]
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject maskDisplayPanel;
    [SerializeField] private GameObject paintingUIPanel;
    [SerializeField] private GameObject maskSelectionPanel;
    [SerializeField] private GameObject endOfDayPanel;
    
    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI dayTimerText;
    [SerializeField] private TextMeshProUGUI moneyText;
    
    [Header("Mask Display (Memory Phase)")]
    [SerializeField] private Image maskDisplayImage;
    [SerializeField] private TextMeshProUGUI memoryTimerText;
    
    [Header("Mask Selection Buttons")]
    [SerializeField] private Button[] maskOptionButtons;  // 4 buttons for mask selection
    [SerializeField] private Image[] maskOptionImages;    // Images on those buttons
    
    [Header("Painting UI")]
    [SerializeField] private TextMeshProUGUI paintingTimerText;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button craftButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("End of Day UI")]
    [SerializeField] private TextMeshProUGUI daySummaryText;
    [SerializeField] private Button replayButton;
    
    [Header("Painting System")]
    [SerializeField] private MaskPaintingPDollar paintingSystem;
    [SerializeField] private SpriteRenderer paintableCanvas;
    
    [Header("Customer System - TEAMMATE INTEGRATION")]
    [Tooltip("Reference to teammate's customer/interaction script")]
    [SerializeField] private MonoBehaviour customerScript;  // Assign teammate's script here
    
    [Header("Timing Settings")]
    [SerializeField] private float memoryPhaseDuration = 2f;
    [SerializeField] private float paintingTimerDuration = 15f;
    [SerializeField] private float dayDuration = 120f;  // 2 minutes
    [SerializeField] private float customerEnterDuration = 2f;  // Time for customer to walk in
    [SerializeField] private float customerExitDuration = 2f;   // Time for customer to walk out
    [SerializeField] private float resultDisplayDuration = 2f;  // Time to show result before customer reacts
    
    [Header("Scoring Settings")]
    [SerializeField] private float perfectScoreThreshold = 60f;   // 60%+ = full pay
    [SerializeField] private float okayScoreThreshold = 45f;      // 45-59% = half pay
    [SerializeField] private int perfectPayment = 60;
    [SerializeField] private int okayPayment = 30;
    [SerializeField] private int failPayment = 0;
    
    [Header("Audio - Optional")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip successSound;
    [SerializeField] private AudioClip okaySound;
    [SerializeField] private AudioClip failSound;
    
    // Private variables
    private int currentDay = 1;
    private int totalMoney = 0;
    private int customersServedToday = 0;
    private float dayTimeRemaining;
    private float paintingTimeRemaining;
    private bool dayInProgress = false;
    private bool paintingTimerActive = false;
    private bool hasSubmitted = false;
    
    private MaskData currentRequestedMask;      // The mask customer wants
    private MaskData currentSelectedMask;       // The mask player selected
    private List<MaskData> availableMasks = new List<MaskData>();
    private List<MaskData> dayMasks = new List<MaskData>();  // Masks for current day
    
    private float lastScore = 0f;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        // Get masks from GameData if available
        if (GameData.Instance != null)
        {
            availableMasks = new List<MaskData>(GameData.Instance.allMasks);
        }
        
        // Setup button listeners
        SetupButtons();
        
        // Hide all panels initially
        HideAllPanels();
        
        // Start Day 1
        StartDay(1);
    }

    void Update()
    {
        // Day timer
        if (dayInProgress)
        {
            dayTimeRemaining -= Time.deltaTime;
            UpdateDayTimerUI();
            
            if (dayTimeRemaining <= 0)
            {
                dayTimeRemaining = 0;
                EndDay();
            }
        }
        
        // Painting timer
        if (paintingTimerActive)
        {
            paintingTimeRemaining -= Time.deltaTime;
            UpdatePaintingTimerUI();
            
            if (paintingTimeRemaining <= 0 && !hasSubmitted)
            {
                paintingTimeRemaining = 0;
                // Auto-fail - time ran out
                OnPaintingTimeOut();
            }
        }
    }

    #region Setup

    void SetupButtons()
    {
        // Craft button
        if (craftButton != null)
            craftButton.onClick.AddListener(OnCraftPressed);
        
        // Reset button
        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetPressed);
        
        // Replay button
        if (replayButton != null)
            replayButton.onClick.AddListener(OnReplayPressed);
        
        // Mask selection buttons
        for (int i = 0; i < maskOptionButtons.Length; i++)
        {
            int index = i;  // Capture for lambda
            if (maskOptionButtons[i] != null)
                maskOptionButtons[i].onClick.AddListener(() => OnMaskSelected(index));
        }
    }

    void HideAllPanels()
    {
        if (maskDisplayPanel != null) maskDisplayPanel.SetActive(false);
        if (paintingUIPanel != null) paintingUIPanel.SetActive(false);
        if (maskSelectionPanel != null) maskSelectionPanel.SetActive(false);
        if (endOfDayPanel != null) endOfDayPanel.SetActive(false);
        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    #endregion

    #region Day Management

    public void StartDay(int day)
    {
        currentDay = day;
        dayTimeRemaining = dayDuration;
        dayInProgress = true;
        customersServedToday = 0;
        
        // Get masks for this day
        dayMasks.Clear();
        foreach (MaskData mask in availableMasks)
        {
            if (mask.availableOnDay <= currentDay)
            {
                dayMasks.Add(mask);
            }
        }
        
        Debug.Log($"[GameSceneManager] Day {currentDay} started with {dayMasks.Count} masks available");
        
        // Update UI
        if (dayText != null) dayText.text = $"Day {currentDay}";
        UpdateMoneyUI();
        
        // Show shop view
        ShowShopView();
        
        // Start first customer
        StartCoroutine(CustomerLoop());
    }

    void EndDay()
    {
        dayInProgress = false;
        paintingTimerActive = false;
        
        Debug.Log($"[GameSceneManager] Day {currentDay} ended! Customers served: {customersServedToday}, Money: ${totalMoney}");
        
        StopAllCoroutines();
        
        // Show end of day panel
        ShowEndOfDayPanel();
    }

    void ShowEndOfDayPanel()
    {
        HideAllPanels();
        shopView.SetActive(true);
        paintingView.SetActive(false);
        
        if (endOfDayPanel != null)
        {
            endOfDayPanel.SetActive(true);
            
            if (daySummaryText != null)
            {
                daySummaryText.text = $"Day {currentDay} Complete!\n\n" +
                                      $"Customers Served: {customersServedToday}\n" +
                                      $"Total Money: ${totalMoney}\n\n" +
                                      $"Press Replay to try again!";
            }
        }
    }

    #endregion

    #region Main Game Loop

    IEnumerator CustomerLoop()
    {
        while (dayInProgress && dayTimeRemaining > 0)
        {
            // 1. Customer enters
            yield return StartCoroutine(CustomerEnterPhase());
            
            if (!dayInProgress) yield break;
            
            // 2. Show mask (memory phase)
            yield return StartCoroutine(MemoryPhase());
            
            if (!dayInProgress) yield break;
            
            // 3. Switch to painting
            yield return StartCoroutine(PaintingPhase());
            
            if (!dayInProgress) yield break;
            
            // 4. Show result and customer reaction
            yield return StartCoroutine(ReactionPhase());
            
            if (!dayInProgress) yield break;
            
            // 5. Customer exits
            yield return StartCoroutine(CustomerExitPhase());
            
            customersServedToday++;
            
            // Brief pause before next customer
            yield return new WaitForSeconds(0.5f);
        }
    }

    #endregion

    #region Phase: Customer Enter

    IEnumerator CustomerEnterPhase()
    {
        currentState = GamePhase.CustomerEnter;
        Debug.Log("[GameSceneManager] Customer entering...");
        
        ShowShopView();
        
        // Select random mask for this customer
        if (dayMasks.Count > 0)
        {
            currentRequestedMask = dayMasks[Random.Range(0, dayMasks.Count)];
            Debug.Log($"[GameSceneManager] Customer wants: {currentRequestedMask.maskName}");
        }
        else
        {
            Debug.LogError("[GameSceneManager] No masks available for today!");
            yield break;
        }
        
        // TEAMMATE INTEGRATION: Trigger customer enter animation
        // TODO: Call teammate's customer enter function
        // Example: customerScript.SendMessage("CustomerEnter", SendMessageOptions.DontRequireReceiver);
        
        yield return new WaitForSeconds(customerEnterDuration);
    }

    #endregion

    #region Phase: Memory

    IEnumerator MemoryPhase()
    {
        currentState = GamePhase.MemoryPhase;
        Debug.Log("[GameSceneManager] Memory phase - showing mask...");
        
        // Show the mask
        if (maskDisplayPanel != null && maskDisplayImage != null && currentRequestedMask != null)
        {
            // Create sprite from display image
            if (currentRequestedMask.displayImage != null)
            {
                Sprite maskSprite = Sprite.Create(
                    currentRequestedMask.displayImage,
                    new Rect(0, 0, currentRequestedMask.displayImage.width, currentRequestedMask.displayImage.height),
                    new Vector2(0.5f, 0.5f)
                );
                maskDisplayImage.sprite = maskSprite;
            }
            
            maskDisplayPanel.SetActive(true);
            
            // Countdown
            float timeLeft = memoryPhaseDuration;
            while (timeLeft > 0)
            {
                if (memoryTimerText != null)
                    memoryTimerText.text = Mathf.Ceil(timeLeft).ToString();
                
                timeLeft -= Time.deltaTime;
                yield return null;
            }
            
            maskDisplayPanel.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(memoryPhaseDuration);
        }
    }

    #endregion

    #region Phase: Painting

    IEnumerator PaintingPhase()
    {
        currentState = GamePhase.Painting;
        Debug.Log("[GameSceneManager] Painting phase started");
        
        // Switch to painting view
        ShowPaintingView();
        
        // Show mask selection
        ShowMaskSelection();
        
        // Update instruction
        if (instructionText != null)
            instructionText.text = "Select the mask template:";
        
        // Wait for player to select a mask
        currentSelectedMask = null;
        while (currentSelectedMask == null && dayInProgress)
        {
            yield return null;
        }
        
        if (!dayInProgress) yield break;
        
        // Hide selection, show painting UI
        if (maskSelectionPanel != null) maskSelectionPanel.SetActive(false);
        
        // Load selected mask onto canvas
        LoadMaskOntoCanvas(currentSelectedMask);
        
        // Start painting timer
        hasSubmitted = false;
        paintingTimeRemaining = paintingTimerDuration;
        paintingTimerActive = true;
        
        // Enable painting
        if (paintingSystem != null)
            paintingSystem.EnablePainting(true);
        
        // Show craft/reset buttons
        if (craftButton != null) craftButton.gameObject.SetActive(true);
        if (resetButton != null) resetButton.gameObject.SetActive(true);
        
        if (instructionText != null)
            instructionText.text = "Paint the pattern!";
        
        // Wait for submit or timeout
        while (!hasSubmitted && paintingTimerActive && dayInProgress)
        {
            yield return null;
        }
        
        paintingTimerActive = false;
        
        if (paintingSystem != null)
            paintingSystem.EnablePainting(false);
    }

    void ShowMaskSelection()
    {
        if (maskSelectionPanel != null)
            maskSelectionPanel.SetActive(true);
        
        // Populate mask options with day's masks (up to 4)
        for (int i = 0; i < maskOptionButtons.Length; i++)
        {
            if (i < dayMasks.Count)
            {
                maskOptionButtons[i].gameObject.SetActive(true);
                
                // Set the mask image on the button
                if (maskOptionImages != null && i < maskOptionImages.Length && maskOptionImages[i] != null)
                {
                    MaskData mask = dayMasks[i];
                    if (mask.displayImage != null)
                    {
                        Sprite sprite = Sprite.Create(
                            mask.displayImage,
                            new Rect(0, 0, mask.displayImage.width, mask.displayImage.height),
                            new Vector2(0.5f, 0.5f)
                        );
                        maskOptionImages[i].sprite = sprite;
                    }
                }
            }
            else
            {
                maskOptionButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void OnMaskSelected(int index)
    {
        if (index < dayMasks.Count)
        {
            currentSelectedMask = dayMasks[index];
            Debug.Log($"[GameSceneManager] Player selected: {currentSelectedMask.maskName}");
        }
    }

    void LoadMaskOntoCanvas(MaskData mask)
    {
        if (paintableCanvas != null && mask.baseImage != null)
        {
            Sprite baseSprite = Sprite.Create(
                mask.baseImage,
                new Rect(0, 0, mask.baseImage.width, mask.baseImage.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            paintableCanvas.sprite = baseSprite;
        }
        
        // Tell painting system which pattern to compare against
        if (paintingSystem != null)
        {
            paintingSystem.SetReferencePattern(currentRequestedMask.patternImage, currentRequestedMask.patternColor);
        }
    }

    void OnCraftPressed()
    {
        if (hasSubmitted) return;
        
        hasSubmitted = true;
        Debug.Log("[GameSceneManager] Craft pressed - calculating score...");
        
        // Calculate score
        if (paintingSystem != null)
        {
            lastScore = paintingSystem.CalculateScore();
        }
        else
        {
            lastScore = 0f;
        }
        
        // Check if player selected wrong mask
        if (currentSelectedMask != currentRequestedMask)
        {
            Debug.Log("[GameSceneManager] Wrong mask selected! Applying penalty...");
            lastScore *= 0.5f;  // 50% penalty for wrong mask
        }
        
        Debug.Log($"[GameSceneManager] Final score: {lastScore:F1}%");
    }

    void OnResetPressed()
    {
        Debug.Log("[GameSceneManager] Reset paint pressed");
        
        if (paintingSystem != null)
        {
            paintingSystem.ResetPaint();
        }
        
        // Timer keeps running - only paint is reset
    }

    void OnPaintingTimeOut()
    {
        Debug.Log("[GameSceneManager] Time's up! Auto-fail.");
        
        hasSubmitted = true;
        lastScore = 0f;
        paintingTimerActive = false;
    }

    #endregion

    #region Phase: Reaction

    IEnumerator ReactionPhase()
    {
        currentState = GamePhase.CustomerReact;
        Debug.Log($"[GameSceneManager] Reaction phase - Score: {lastScore:F1}%");
        
        // Determine payment and reaction
        int payment = 0;
        string reactionMessage = "";
        
        if (lastScore >= perfectScoreThreshold)
        {
            // Perfect! Customer loves it
            payment = perfectPayment;
            reactionMessage = $"Perfect! +${payment}";
            if (resultText != null) resultText.color = Color.green;
            
            if (audioSource != null && successSound != null)
                audioSource.PlayOneShot(successSound);
        }
        else if (lastScore >= okayScoreThreshold)
        {
            // Okay - customer accepts but not happy
            payment = okayPayment;
            reactionMessage = $"It's... okay. +${payment}";
            if (resultText != null) resultText.color = Color.yellow;
            
            if (audioSource != null && okaySound != null)
                audioSource.PlayOneShot(okaySound);
        }
        else
        {
            // Fail - customer rejects
            payment = failPayment;
            reactionMessage = "This is terrible! I'm not paying for this!";
            if (resultText != null) resultText.color = Color.red;
            
            if (audioSource != null && failSound != null)
                audioSource.PlayOneShot(failSound);
        }
        
        // Add payment
        totalMoney += payment;
        UpdateMoneyUI();
        
        // Show result
        if (resultText != null)
        {
            resultText.text = $"Score: {lastScore:F1}%\n{reactionMessage}";
            resultText.gameObject.SetActive(true);
        }
        
        yield return new WaitForSeconds(resultDisplayDuration);
        
        // Switch back to shop view
        ShowShopView();
        
        // TEAMMATE INTEGRATION: Show customer reaction animation
        // TODO: Call teammate's reaction function based on score
        // Example: customerScript.SendMessage("ShowReaction", lastScore >= okayScoreThreshold);
        
        yield return new WaitForSeconds(1f);
        
        if (resultText != null)
            resultText.gameObject.SetActive(false);
    }

    #endregion

    #region Phase: Customer Exit

    IEnumerator CustomerExitPhase()
    {
        currentState = GamePhase.CustomerExit;
        Debug.Log("[GameSceneManager] Customer exiting...");
        
        // TEAMMATE INTEGRATION: Trigger customer exit animation
        // TODO: Call teammate's customer exit function
        // Example: customerScript.SendMessage("CustomerExit", SendMessageOptions.DontRequireReceiver);
        
        yield return new WaitForSeconds(customerExitDuration);
    }

    #endregion

    #region View Switching

    void ShowShopView()
    {
        if (shopView != null) shopView.SetActive(true);
        if (paintingView != null) paintingView.SetActive(false);
        
        // Hide painting UI
        if (paintingUIPanel != null) paintingUIPanel.SetActive(false);
        if (maskSelectionPanel != null) maskSelectionPanel.SetActive(false);
        if (craftButton != null) craftButton.gameObject.SetActive(false);
        if (resetButton != null) resetButton.gameObject.SetActive(false);
    }

    void ShowPaintingView()
    {
        if (shopView != null) shopView.SetActive(false);
        if (paintingView != null) paintingView.SetActive(true);
        
        // Show painting UI
        if (paintingUIPanel != null) paintingUIPanel.SetActive(true);
        if (resultText != null) resultText.gameObject.SetActive(false);
    }

    #endregion

    #region UI Updates

    void UpdateDayTimerUI()
    {
        if (dayTimerText != null)
        {
            int minutes = Mathf.FloorToInt(dayTimeRemaining / 60);
            int seconds = Mathf.FloorToInt(dayTimeRemaining % 60);
            dayTimerText.text = $"{minutes}:{seconds:00}";
        }
    }

    void UpdatePaintingTimerUI()
    {
        if (paintingTimerText != null)
        {
            paintingTimerText.text = $"Time: {Mathf.Ceil(paintingTimeRemaining)}s";
            
            // Flash red when low
            if (paintingTimeRemaining <= 5f)
            {
                paintingTimerText.color = Color.red;
            }
            else
            {
                paintingTimerText.color = Color.white;
            }
        }
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
        {
            moneyText.text = $"${totalMoney}";
        }
    }

    #endregion

    #region Replay

    void OnReplayPressed()
    {
        Debug.Log("[GameSceneManager] Replay pressed - restarting Day 1");
        
        // Reset everything
        totalMoney = 0;
        currentDay = 1;
        
        HideAllPanels();
        
        // Restart
        StartDay(1);
    }

    #endregion
}

/// <summary>
/// All possible game phases
/// </summary>
public enum GamePhase
{
    Idle,
    CustomerEnter,
    MemoryPhase,
    Painting,
    CustomerReact,
    CustomerExit,
    DayEnd
}