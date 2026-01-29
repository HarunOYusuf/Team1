using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Updated InteractionManager that integrates with GameManager.
/// Handles character animations, mask display, and scene transitions.
/// 
/// SETUP: Place this in your Shop scene.
/// </summary>
public class InteractionManagerUpdated : MonoBehaviour
{
    [Header("Actors")]
    public SpriteRenderer npcRenderer;    // Order 1
    public SpriteRenderer wizardRenderer; // Order 3
    public CustomerCutsceneUI cutsceneUI;
    public GameObject createButtonPanel;

    [Header("Mask Display")]
    [Tooltip("UI Image to show the mask the customer wants")]
    public Image maskDisplayImage;
    [Tooltip("Parent object to enable/disable for mask display")]
    public GameObject maskDisplayPanel;
    [Tooltip("How long to show the mask (overridden by mask difficulty if GameData exists)")]
    public float defaultMemoryTime = 5f;

    [Header("Animation Settings")]
    public float moveDuration = 2.0f; // Faster for game flow
    public float bobAmplitude = 0.12f;
    public float bobFrequency = 14f;

    [Header("Positions")]
    public float npcOffScreenR = 10f;
    public float npcAtCounter = 3f;
    public float wizOffScreenL = -10f;
    public float wizAtCounter = -3f;

    [Header("Customer Reaction")]
    public Sprite happyNPCSprite;
    public Sprite sadNPCSprite;
    public Sprite normalNPCSprite;

    [Header("Scene Names")]
    public string paintingSceneName = "Harun";

    private float originalY;
    private bool isReturningFromPainting = false;

    void Start()
    {
        originalY = npcRenderer.transform.position.y;
        createButtonPanel.SetActive(false);
        
        if (maskDisplayPanel != null)
            maskDisplayPanel.SetActive(false);

        // Check if we're returning from painting scene
        if (GameData.Instance != null && GameData.Instance.lastScore > 0)
        {
            isReturningFromPainting = true;
            StartCoroutine(ReactionSequence());
        }
        else
        {
            // Fresh start - begin customer sequence
            StartCoroutine(CustomerArrivalSequence());
        }
    }

    #region Customer Arrival

    IEnumerator CustomerArrivalSequence()
    {
        // Get mask from GameData (or use random if no GameManager)
        MaskData currentMask = null;
        string dialogue = "Wizard, I require a mask for the festival...";
        
        if (GameData.Instance != null)
        {
            currentMask = GameData.Instance.GetRandomMaskForCurrentDay();
            if (currentMask != null)
            {
                dialogue = currentMask.customerRequest;
            }
        }

        // Flip sprites to face each other as they walk in
        npcRenderer.flipX = true;     // NPC looks Left
        wizardRenderer.flipX = false; // Wizard looks Right

        // Reset NPC sprite to normal
        if (normalNPCSprite != null)
            npcRenderer.sprite = normalNPCSprite;

        yield return StartCoroutine(MoveWithSteps(npcOffScreenR, npcAtCounter, wizOffScreenL, wizAtCounter));

        // Show dialogue
        if (cutsceneUI != null)
        {
            cutsceneUI.PlayCutscene(null, dialogue);
        }

        yield return new WaitForSeconds(1.5f);
        
        // Show the mask the customer wants
        if (currentMask != null && maskDisplayPanel != null && maskDisplayImage != null)
        {
            ShowMaskToPlayer(currentMask);
        }
        else
        {
            // No mask data - just show create button
            createButtonPanel.SetActive(true);
        }
    }

    void ShowMaskToPlayer(MaskData mask)
    {
        // Display the mask image
        if (mask.displayImage != null)
        {
            Sprite maskSprite = Sprite.Create(
                mask.displayImage,
                new Rect(0, 0, mask.displayImage.width, mask.displayImage.height),
                new Vector2(0.5f, 0.5f)
            );
            maskDisplayImage.sprite = maskSprite;
        }
        
        maskDisplayPanel.SetActive(true);
        
        // Start memory countdown
        float memoryTime = mask.GetMemoryTime();
        StartCoroutine(MemoryCountdown(memoryTime));
    }

    IEnumerator MemoryCountdown(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        // Hide mask display
        if (maskDisplayPanel != null)
            maskDisplayPanel.SetActive(false);
        
        // Show create button
        createButtonPanel.SetActive(true);
    }

    #endregion

    #region Painting Transition

    // Link this to your "Create" Button OnClick()
    public void OnCreatePressed()
    {
        createButtonPanel.SetActive(false);
        StartCoroutine(WizardGoesToWorkshop());
    }

    IEnumerator WizardGoesToWorkshop()
    {
        // Wizard turns to face the left door
        wizardRenderer.flipX = true;

        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float wizX = Mathf.Lerp(wizAtCounter, wizOffScreenL, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            wizardRenderer.transform.position = new Vector3(wizX, originalY + stepBob, 0);
            yield return null;
        }

        // Change scene to the painting workbench
        SceneManager.LoadScene(paintingSceneName);
    }

    #endregion

    #region Customer Reaction (After Painting)

    IEnumerator ReactionSequence()
    {
        // Position characters (they should already be at counter)
        npcRenderer.transform.position = new Vector3(npcAtCounter, originalY, 0);
        wizardRenderer.transform.position = new Vector3(wizAtCounter, originalY, 0);
        
        npcRenderer.flipX = true;
        wizardRenderer.flipX = false;

        yield return new WaitForSeconds(0.5f);

        // Get result from GameData
        bool satisfied = GameData.Instance.lastCustomerSatisfied;
        float score = GameData.Instance.lastScore;
        int payment = 0;
        
        if (satisfied && GameData.Instance.currentMask != null)
        {
            payment = GameData.Instance.currentMask.CalculatePayment(score);
        }

        // Show reaction
        if (satisfied)
        {
            // Happy customer
            if (happyNPCSprite != null)
                npcRenderer.sprite = happyNPCSprite;
            
            if (cutsceneUI != null)
            {
                cutsceneUI.PlayCutscene(null, $"Wonderful! Here's ${payment} for your work!");
            }
        }
        else
        {
            // Sad customer
            if (sadNPCSprite != null)
                npcRenderer.sprite = sadNPCSprite;
            
            if (cutsceneUI != null)
            {
                cutsceneUI.PlayCutscene(null, "This isn't what I asked for... I'm leaving!");
            }
        }

        yield return new WaitForSeconds(2.5f);

        // NPC exits
        yield return StartCoroutine(NPCExitSequence());

        // Reset for next customer
        isReturningFromPainting = false;
        GameData.Instance.lastScore = 0; // Reset so next Start() doesn't think we're returning
        
        // Check if day is still going
        if (GameData.Instance.dayInProgress && GameData.Instance.dayTimeRemaining > 0)
        {
            // Next customer!
            yield return new WaitForSeconds(1f);
            StartCoroutine(CustomerArrivalSequence());
        }
        else
        {
            // Day ended
            Debug.Log("[InteractionManager] Day ended!");
            // TEAMMATE: Show day end UI here
        }
    }

    IEnumerator NPCExitSequence()
    {
        // NPC turns to face the right exit
        npcRenderer.flipX = false;

        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float npcX = Mathf.Lerp(npcAtCounter, npcOffScreenR, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            npcRenderer.transform.position = new Vector3(npcX, originalY + stepBob, 0);
            yield return null;
        }
    }

    #endregion

    #region Shared Animation

    IEnumerator MoveWithSteps(float nStart, float nEnd, float wStart, float wEnd)
    {
        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float nX = Mathf.Lerp(nStart, nEnd, smoothT);
            float wX = Mathf.Lerp(wStart, wEnd, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            npcRenderer.transform.position = new Vector3(nX, originalY + stepBob, 0);
            wizardRenderer.transform.position = new Vector3(wX, originalY + stepBob, 0);
            yield return null;
        }
    }

    #endregion
}