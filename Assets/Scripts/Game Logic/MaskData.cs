using UnityEngine;

/// <summary>
/// ScriptableObject that holds all data for a single mask design.
/// Create these in Unity: Right-click > Create > Maskquerade > Mask Data
/// </summary>
[CreateAssetMenu(fileName = "New Mask", menuName = "Maskquerade/Mask Data")]
public class MaskData : ScriptableObject
{
    [Header("Mask Identity")]
    public string maskName = "Unnamed Mask";
    public int maskID;
    
    [Header("Mask Images")]
    [Tooltip("The full mask shown to the player (base + pattern combined)")]
    public Texture2D displayImage;
    
    [Tooltip("Just the pattern on transparent background (for $P comparison)")]
    public Texture2D patternImage;
    
    [Tooltip("The blank mask base (what player paints on)")]
    public Texture2D baseImage;
    
    [Header("Gameplay Settings")]
    [Tooltip("Which day this mask is available (1, 2, or 3)")]
    [Range(1, 3)]
    public int availableOnDay = 1;
    
    [Tooltip("Difficulty rating 1-5 (affects memory time and scoring)")]
    [Range(1, 5)]
    public int difficulty = 1;
    
    [Tooltip("Base price customer pays for this mask")]
    public int basePrice = 50;
    
    [Header("Pattern Settings")]
    [Tooltip("The main color of the pattern (for detection)")]
    public Color patternColor = Color.red;
    
    [Header("Customer Dialogue (Optional)")]
    [TextArea(2, 4)]
    public string customerRequest = "I need a mask like this...";
    
    /// <summary>
    /// Calculate memory time based on difficulty
    /// </summary>
    public float GetMemoryTime()
    {
        // Easier masks = more time, harder masks = less time
        // Difficulty 1 = 8 seconds, Difficulty 5 = 4 seconds
        return Mathf.Lerp(8f, 4f, (difficulty - 1) / 4f);
    }
    
    /// <summary>
    /// Calculate final payment based on score
    /// </summary>
    public int CalculatePayment(float scorePercent)
    {
        if (scorePercent < 60f) return 0; // Failed, no payment
        
        // Scale payment: 60% score = 60% of base price, 100% = 100% + bonus
        float paymentMultiplier = scorePercent / 100f;
        
        // Bonus for high scores
        if (scorePercent >= 90f) paymentMultiplier += 0.25f; // 25% bonus
        else if (scorePercent >= 80f) paymentMultiplier += 0.1f; // 10% bonus
        
        return Mathf.RoundToInt(basePrice * paymentMultiplier);
    }
}