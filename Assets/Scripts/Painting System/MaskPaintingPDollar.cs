using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using PDollarGestureRecognizer;

/// <summary>
/// Mask Painting System using $P Point-Cloud Recognizer
/// Compares player's drawing against a reference mask pattern PNG
/// </summary>
public class MaskPaintingPDollar : MonoBehaviour
{
    [Header("Painting Setup")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 8;
    
    [Header("Reference Pattern")]
    [SerializeField] private Texture2D maskDisplayImage;      // The full mask shown to player (Image 1)
    [SerializeField] private Texture2D referencePatternPNG;   // The pattern-only PNG for comparison (Image 2)
    [SerializeField] private Color patternColor = Color.red;  // Color to detect in the pattern
    [SerializeField] private Color[] referencePatternColors = new Color[] { Color.red };  // All colors in the pattern
    [SerializeField] private float colorTolerance = 0.3f;
    
    [Header("GameManager Integration")]
    [SerializeField] private bool useGameData = true;  // If true, load mask from GameData instead of Inspector
    [SerializeField] private string shopSceneName = "ShopScene";  // Scene to return to after scoring
    
    [Header("Scoring")]
    [SerializeField] private float passingScore = 60f;
    [SerializeField] private int samplePointCount = 128;  // More points = more accurate but slower
    [SerializeField] [Range(0f, 1f)] private float shapeWeight = 0.5f;    // Weight for $P shape matching
    [SerializeField] [Range(0f, 1f)] private float quadrantWeight = 0.5f; // Weight for quadrant pixel matching
    [SerializeField] private int gridDivisions = 2;  // 2 = 4 quadrants (2x2), 3 = 9 sections (3x3)
    [SerializeField] [Range(0f, 100f)] private float emptyQuadrantPenalty = 50f;  // Penalty for painting in empty quadrants (0 = no penalty, 100 = full penalty)
    
    [Header("UI References")]
    [SerializeField] private GameObject targetDisplay;
    [SerializeField] private Image targetImage;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("Memory Phase")]
    [SerializeField] private float viewDuration = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showQuadrantOverlay = false;  // Press D to toggle
    [SerializeField] private Color quadrantLineColor = Color.yellow;
    [SerializeField] private int referencePointCount;
    [SerializeField] private int playerPointCount;
    [SerializeField] private float rawDistance;
    [SerializeField] private float shapeScore;
    [SerializeField] private float quadrantScore;
    [SerializeField] private float finalScore;
    
    // Quadrant pixel counts for debugging
    [SerializeField] private int[] referenceQuadrantCounts;
    [SerializeField] private int[] playerQuadrantCounts;
    
    // Store original alpha values for transparent area protection
    private float[,] originalAlphaMap;
    
    // Private variables
    private Texture2D paintTexture;
    private Texture2D originalBaseTexture;
    private Camera mainCamera;
    private bool canPaint = false;
    private float timer;
    private bool memoryPhaseActive = false;
    private Color backgroundColor = Color.white;
    
    // $P Recognizer data
    private Gesture referenceGesture;
    private List<Vector2> playerStrokePoints = new List<Vector2>();
    private int currentStrokeID = 0;

    public UnityEvent<float> onPass;
    public UnityEvent<float> onFail;
    
    void Start()
    {
        Debug.Log("=== MaskPaintingPDollar START ===");
        
        mainCamera = Camera.main;
        Debug.Log($"[1] Camera: {(mainCamera != null ? "OK" : "NULL")}");
        
        // Check sprite renderer
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("[2] SpriteRenderer is NULL!");
                return;
            }
        }
        Debug.Log($"[2] SpriteRenderer: OK");
        
        // Try to load mask from GameData if enabled
        if (useGameData && GameData.Instance != null && GameData.Instance.currentMask != null)
        {
            LoadMaskFromGameData();
        }
        
        // If sprite is set (either from Inspector or GameData), initialize painting
        if (spriteRenderer.sprite != null)
        {
            Debug.Log($"[3] Sprite: OK - {spriteRenderer.sprite.name}");
            InitializeFromCurrentSprite();
        }
        else
        {
            // No sprite yet - GameSceneManager will call SetBaseImage() later
            Debug.Log("[3] No sprite yet - waiting for GameSceneManager to set base image");
        }
        
        // Setup UI (optional - may be null if GameSceneManager controls UI)
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(OnSubmit);
            submitButton.gameObject.SetActive(false);
            Debug.Log("[9] Submit button: OK");
        }
        
        if (resultText != null)
        {
            resultText.gameObject.SetActive(false);
            Debug.Log("[10] Result text: OK");
        }
        
        Debug.Log("=== START COMPLETE ===");
    }
    
    /// <summary>
    /// Initialize painting system from current sprite
    /// </summary>
    void InitializeFromCurrentSprite()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("[InitializeFromCurrentSprite] No sprite to initialize from!");
            return;
        }
        
        // Store original texture
        Texture2D baseTexture = spriteRenderer.sprite.texture;
        Debug.Log($"[4] Base texture: {baseTexture.width}x{baseTexture.height}");
        
        try
        {
            originalBaseTexture = new Texture2D(baseTexture.width, baseTexture.height);
            originalBaseTexture.SetPixels(baseTexture.GetPixels());
            originalBaseTexture.Apply();
            Debug.Log("[5] Original texture copied: OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[5] Failed to copy base texture: {e.Message}");
            Debug.LogError("Make sure the CANVAS texture has Read/Write Enabled!");
            return;
        }
        
        // Create paintable texture
        paintTexture = new Texture2D(baseTexture.width, baseTexture.height);
        paintTexture.SetPixels(baseTexture.GetPixels());
        paintTexture.Apply();
        Debug.Log("[6] Paint texture created: OK");
        
        // Store original alpha values for transparent area protection
        originalAlphaMap = new float[baseTexture.width, baseTexture.height];
        for (int x = 0; x < baseTexture.width; x++)
        {
            for (int y = 0; y < baseTexture.height; y++)
            {
                originalAlphaMap[x, y] = baseTexture.GetPixel(x, y).a;
            }
        }
        Debug.Log("[6b] Alpha map stored for transparent area protection");
        
        Sprite newSprite = Sprite.Create(
            paintTexture,
            new Rect(0, 0, paintTexture.width, paintTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteRenderer.sprite = newSprite;
        Debug.Log("[7] New sprite assigned: OK");
        
        // Convert reference PNG to $P Gesture
        CreateReferenceGesture();
        Debug.Log("[8] Reference gesture creation attempted");
        
        // If using GameData, skip memory phase (already happened in shop scene)
        if (useGameData)
        {
            Debug.Log("[11] Using GameData - skipping memory phase, going straight to painting");
            StartPaintingPhase();
        }
        else
        {
            Debug.Log("[11] Starting memory phase...");
            StartMemoryPhase();
        }
    }
    
    void StartPaintingPhase()
    {
        // Hide target display if it exists
        if (targetDisplay != null)
            targetDisplay.SetActive(false);
        
        // Enable painting
        canPaint = true;
        memoryPhaseActive = false;
        currentStrokeID = 0;
        playerStrokePoints.Clear();
        
        // Show UI
        if (instructionText != null)
            instructionText.text = "Paint the pattern you memorized!";
        
        if (submitButton != null)
            submitButton.gameObject.SetActive(true);
        
        Debug.Log("[StartPaintingPhase] Ready to paint!");
    }
    
    #region Public Methods for GameSceneManager
    
    /// <summary>
    /// Set the paint color (called by GameSceneManager when player selects color)
    /// </summary>
    public void SetPaintColor(Color color)
    {
        paintColor = color;
        Debug.Log($"[MaskPaintingPDollar] Paint color set to: {color}");
    }
    
    /// <summary>
    /// Completely clear the canvas (sprite and textures)
    /// </summary>
    public void ClearCanvas()
    {
        // Clear textures
        if (paintTexture != null)
        {
            Color[] clearPixels = new Color[paintTexture.width * paintTexture.height];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = Color.clear;
            paintTexture.SetPixels(clearPixels);
            paintTexture.Apply();
        }
        
        originalBaseTexture = null;
        paintTexture = null;
        originalAlphaMap = null;
        
        // Clear sprite
        if (spriteRenderer != null)
            spriteRenderer.sprite = null;
        
        // Clear tracking
        playerStrokePoints.Clear();
        currentStrokeID = 0;
        
        Debug.Log("[MaskPaintingPDollar] Canvas cleared completely");
    }
    
    /// <summary>
    /// Enable or disable painting
    /// </summary>
    public void EnablePainting(bool enable)
    {
        canPaint = enable;
        Debug.Log($"[MaskPaintingPDollar] Painting enabled: {enable}");
    }
    
    /// <summary>
    /// Reset paint to original (clear player's drawing)
    /// </summary>
    public void ResetPaint()
    {
        if (paintTexture != null && originalBaseTexture != null)
        {
            paintTexture.SetPixels(originalBaseTexture.GetPixels());
            paintTexture.Apply();
            
            // Clear stroke tracking
            playerStrokePoints.Clear();
            currentStrokeID = 0;
            
            Debug.Log("[MaskPaintingPDollar] Paint reset");
        }
    }
    
    /// <summary>
    /// Set the reference pattern to compare against (called by GameSceneManager)
    /// </summary>
    public void SetReferencePattern(Texture2D pattern, Color color)
    {
        referencePatternPNG = pattern;
        patternColor = color;
        referencePatternColors = new Color[] { color };
        
        // Recreate reference gesture
        CreateReferenceGesture();
        
        Debug.Log($"[MaskPaintingPDollar] Reference pattern set: {(pattern != null ? pattern.name : "null")}");
    }
    
    /// <summary>
    /// Set the reference pattern with multiple colors (called by GameSceneManager)
    /// </summary>
    public void SetReferencePattern(Texture2D pattern, Color[] colors)
    {
        referencePatternPNG = pattern;
        referencePatternColors = colors;
        if (colors.Length > 0)
            patternColor = colors[0];
        
        // Recreate reference gesture and color map
        CreateReferenceGesture();
        CreateReferenceColorMap();
        
        Debug.Log($"[MaskPaintingPDollar] Reference pattern set with {colors.Length} colors");
    }
    
    // Store color information per quadrant from reference
    private Color[] referenceQuadrantColors;
    
    /// <summary>
    /// Create a map of which colors are in which quadrants of the reference
    /// </summary>
    void CreateReferenceColorMap()
    {
        if (referencePatternPNG == null) return;
        
        int totalCells = gridDivisions * gridDivisions;
        referenceQuadrantColors = new Color[totalCells];
        
        int cellWidth = referencePatternPNG.width / gridDivisions;
        int cellHeight = referencePatternPNG.height / gridDivisions;
        
        // For each quadrant, find the dominant color
        for (int cellY = 0; cellY < gridDivisions; cellY++)
        {
            for (int cellX = 0; cellX < gridDivisions; cellX++)
            {
                int cellIndex = cellY * gridDivisions + cellX;
                Dictionary<Color, int> colorCounts = new Dictionary<Color, int>();
                
                int startX = cellX * cellWidth;
                int startY = cellY * cellHeight;
                
                for (int x = startX; x < startX + cellWidth && x < referencePatternPNG.width; x++)
                {
                    for (int y = startY; y < startY + cellHeight && y < referencePatternPNG.height; y++)
                    {
                        Color pixel = referencePatternPNG.GetPixel(x, y);
                        
                        if (pixel.a > 0.1f)  // Not transparent
                        {
                            // Match to closest reference color
                            Color matchedColor = GetClosestReferenceColor(pixel);
                            if (matchedColor != Color.clear)
                            {
                                if (colorCounts.ContainsKey(matchedColor))
                                    colorCounts[matchedColor]++;
                                else
                                    colorCounts[matchedColor] = 1;
                            }
                        }
                    }
                }
                
                // Find dominant color in this quadrant
                Color dominantColor = Color.clear;
                int maxCount = 0;
                foreach (var kvp in colorCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        dominantColor = kvp.Key;
                    }
                }
                
                referenceQuadrantColors[cellIndex] = dominantColor;
            }
        }
        
        Debug.Log($"[MaskPaintingPDollar] Reference color map created for {totalCells} quadrants");
    }
    
    Color GetClosestReferenceColor(Color pixel)
    {
        foreach (Color refColor in referencePatternColors)
        {
            float rDiff = Mathf.Abs(pixel.r - refColor.r);
            float gDiff = Mathf.Abs(pixel.g - refColor.g);
            float bDiff = Mathf.Abs(pixel.b - refColor.b);
            
            if (rDiff < colorTolerance && gDiff < colorTolerance && bDiff < colorTolerance)
                return refColor;
        }
        return Color.clear;
    }
    
    /// <summary>
    /// Set the base image for the painting canvas
    /// </summary>
    public void SetBaseImage(Texture2D baseImage)
    {
        if (baseImage == null)
        {
            Debug.LogError("[MaskPaintingPDollar] SetBaseImage called with null texture!");
            return;
        }
        
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("[MaskPaintingPDollar] No SpriteRenderer found on this object!");
                return;
            }
        }
        
        // Ensure camera is set
        if (mainCamera == null)
            mainCamera = Camera.main;
        
        Sprite baseSprite = Sprite.Create(
            baseImage,
            new Rect(0, 0, baseImage.width, baseImage.height),
            new Vector2(0.5f, 0.5f),
            100f  // Pixels per unit
        );
        spriteRenderer.sprite = baseSprite;
        
        // Auto-resize BoxCollider2D to match the new sprite
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider != null)
        {
            boxCollider.size = baseSprite.bounds.size;
            boxCollider.offset = Vector2.zero;
            Debug.Log($"[MaskPaintingPDollar] BoxCollider2D resized to: {boxCollider.size}");
        }
        
        // Reinitialize textures
        InitializePaintTexture();
        
        Debug.Log($"[MaskPaintingPDollar] Base image set: {baseImage.name} ({baseImage.width}x{baseImage.height})");
    }
    
    /// <summary>
    /// Initialize the paint texture from current sprite
    /// </summary>
    void InitializePaintTexture()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        
        Texture2D baseTexture = spriteRenderer.sprite.texture;
        
        // Store original
        originalBaseTexture = new Texture2D(baseTexture.width, baseTexture.height);
        originalBaseTexture.SetPixels(baseTexture.GetPixels());
        originalBaseTexture.Apply();
        
        // Create paintable texture
        paintTexture = new Texture2D(baseTexture.width, baseTexture.height);
        paintTexture.SetPixels(baseTexture.GetPixels());
        paintTexture.Apply();
        
        // Store alpha map
        originalAlphaMap = new float[baseTexture.width, baseTexture.height];
        for (int x = 0; x < baseTexture.width; x++)
        {
            for (int y = 0; y < baseTexture.height; y++)
            {
                originalAlphaMap[x, y] = baseTexture.GetPixel(x, y).a;
            }
        }
        
        // Create new sprite
        Sprite newSprite = Sprite.Create(
            paintTexture,
            new Rect(0, 0, paintTexture.width, paintTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteRenderer.sprite = newSprite;
        
        // Clear tracking
        playerStrokePoints.Clear();
        currentStrokeID = 0;
    }
    
    /// <summary>
    /// Calculate and return the score without showing UI
    /// </summary>
    public float CalculateScore()
    {
        // Convert player's stroke points to $P Points
        List<PDollarGestureRecognizer.Point> playerPoints = ConvertPlayerDrawingToPoints();
        
        if (playerPoints.Count < 10)
        {
            Debug.Log("[MaskPaintingPDollar] Not enough drawing detected - score 0");
            return 0f;
        }
        
        playerPointCount = playerPoints.Count;
        
        // Sample if needed
        if (playerPoints.Count > samplePointCount)
        {
            playerPoints = SamplePoints(playerPoints, samplePointCount);
        }
        
        // Create player gesture
        Gesture playerGesture = new Gesture(playerPoints.ToArray(), "PlayerDrawing");
        
        // === PART 1: $P Shape Score ===
        float distance = CalculateGestureDistance(referenceGesture, playerGesture);
        rawDistance = distance;
        shapeScore = ConvertDistanceToScore(distance);
        
        // === PART 2: Quadrant Pixel Score ===
        playerQuadrantCounts = CalculateQuadrantPixelCounts(paintTexture);
        quadrantScore = CalculateQuadrantMatchScore();
        
        // === COMBINED SCORE ===
        finalScore = (shapeScore * shapeWeight) + (quadrantScore * quadrantWeight);
        
        Debug.Log($"[MaskPaintingPDollar] Score calculated - Shape: {shapeScore:F1}%, Quadrant: {quadrantScore:F1}%, Final: {finalScore:F1}%");
        
        return finalScore;
    }
    
    #endregion

    #region GameData Integration
    
    void LoadMaskFromGameData()
    {
        MaskData mask = GameData.Instance.currentMask;
        
        Debug.Log($"[GameData] Loading mask: {mask.maskName}");
        
        // Set the textures from MaskData
        if (mask.displayImage != null)
            maskDisplayImage = mask.displayImage;
        
        if (mask.patternImage != null)
            referencePatternPNG = mask.patternImage;
        
        if (mask.patternColor != default)
            patternColor = mask.patternColor;
        
        // Load base image onto the painting canvas
        if (mask.baseImage != null && spriteRenderer != null)
        {
            Sprite baseSprite = Sprite.Create(
                mask.baseImage,
                new Rect(0, 0, mask.baseImage.width, mask.baseImage.height),
                new Vector2(0.5f, 0.5f),
                100f
            );
            spriteRenderer.sprite = baseSprite;
            Debug.Log($"[GameData] Base image loaded: {mask.baseImage.name}");
        }
        
        // Set memory duration based on mask difficulty
        viewDuration = mask.GetMemoryTime();
        
        Debug.Log($"[GameData] Mask loaded - Memory time: {viewDuration}s, Pattern color: {patternColor}");
    }
    
    void SendScoreToGameData(float score)
    {
        if (GameData.Instance != null)
        {
            GameData.Instance.RecordScore(score);
            Debug.Log($"[GameData] Score recorded: {score:F1}%");
        }
        
        // Notify GameManager if it exists
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPaintingComplete(score);
        }
    }
    
    /// <summary>
    /// Return to shop scene (call from UI button or automatically)
    /// </summary>
    public void ReturnToShop()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(shopSceneName);
    }
    
    #endregion

    #region Reference Pattern Extraction
    
    void CreateReferenceGesture()
    {
        Debug.Log("--- CreateReferenceGesture ---");
        
        if (referencePatternPNG == null)
        {
            Debug.LogError("No reference pattern PNG assigned!");
            return;
        }
        Debug.Log($"Reference PNG: {referencePatternPNG.name} ({referencePatternPNG.width}x{referencePatternPNG.height})");
        Debug.Log($"Looking for color: R={patternColor.r:F2} G={patternColor.g:F2} B={patternColor.b:F2}");
        Debug.Log($"Color tolerance: {colorTolerance}");
        
        // Extract points from the reference pattern
        List<PDollarGestureRecognizer.Point> points = new List<PDollarGestureRecognizer.Point>();
        
        // Sample some pixels to debug color detection
        int sampleCount = 0;
        int matchCount = 0;
        
        try
        {
            for (int x = 0; x < referencePatternPNG.width; x++)
            {
                for (int y = 0; y < referencePatternPNG.height; y++)
                {
                    Color pixel = referencePatternPNG.GetPixel(x, y);
                    
                    // Log first 5 non-transparent pixels we find
                    if (pixel.a > 0.1f && sampleCount < 5)
                    {
                        Debug.Log($"Sample pixel at ({x},{y}): R={pixel.r:F2} G={pixel.g:F2} B={pixel.b:F2} A={pixel.a:F2}");
                        sampleCount++;
                    }
                    
                    if (IsPatternColor(pixel))
                    {
                        points.Add(new PDollarGestureRecognizer.Point(x, y, 0));
                        matchCount++;
                    }
                }
            }
            Debug.Log($"Scanned {referencePatternPNG.width * referencePatternPNG.height} pixels, found {matchCount} matching points");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to read reference texture: {e.Message}");
            Debug.LogError("Make sure the REFERENCE PATTERN PNG has Read/Write Enabled!");
            return;
        }
        
        if (points.Count < 10)
        {
            Debug.LogError($"Not enough points extracted from reference pattern! Found: {points.Count}");
            Debug.LogError("Check that Pattern Color matches the color in your PNG!");
            return;
        }
        
        referencePointCount = points.Count;
        
        // Sample points if there are too many (for performance)
        if (points.Count > samplePointCount)
        {
            points = SamplePoints(points, samplePointCount);
        }
        
        // Create the reference gesture
        referenceGesture = new Gesture(points.ToArray(), "ReferencePattern");
        
        // Calculate reference quadrant pixel counts
        referenceQuadrantCounts = CalculateQuadrantPixelCounts(referencePatternPNG);
        Debug.Log($"Reference quadrant counts: [{string.Join(", ", referenceQuadrantCounts)}]");
        
        Debug.Log($"SUCCESS: Reference gesture created with {referencePointCount} original points, sampled to {points.Count}");
    }
    
    List<PDollarGestureRecognizer.Point> ExtractPointsFromTexture(Texture2D texture)
    {
        List<PDollarGestureRecognizer.Point> points = new List<PDollarGestureRecognizer.Point>();
        
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                Color pixel = texture.GetPixel(x, y);
                
                if (IsPatternColor(pixel))
                {
                    // Add point (using strokeID 0 for all points from PNG)
                    points.Add(new PDollarGestureRecognizer.Point(x, y, 0));
                }
            }
        }
        
        return points;
    }
    
    bool IsPatternColor(Color c)
    {
        if (c.a < 0.1f) return false;
        
        float rDiff = Mathf.Abs(c.r - patternColor.r);
        float gDiff = Mathf.Abs(c.g - patternColor.g);
        float bDiff = Mathf.Abs(c.b - patternColor.b);
        
        return (rDiff < colorTolerance && gDiff < colorTolerance && bDiff < colorTolerance);
    }
    
    List<PDollarGestureRecognizer.Point> SamplePoints(List<PDollarGestureRecognizer.Point> points, int targetCount)
    {
        if (points.Count <= targetCount) return points;
        
        List<PDollarGestureRecognizer.Point> sampled = new List<PDollarGestureRecognizer.Point>();
        float step = (float)points.Count / targetCount;
        
        for (int i = 0; i < targetCount; i++)
        {
            int index = Mathf.Min((int)(i * step), points.Count - 1);
            sampled.Add(points[index]);
        }
        
        return sampled;
    }
    
    #endregion
    
    #region Game Flow
    
    void Update()
    {
        if (memoryPhaseActive)
        {
            timer -= Time.deltaTime;
            instructionText.text = $"Memorize this pattern! {Mathf.Ceil(timer)}s";
            
            if (timer <= 0)
            {
                EndMemoryPhase();
            }
        }
        
        // Track strokes
        if (canPaint)
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Start new stroke
                currentStrokeID++;
            }
            
            if (Input.GetMouseButton(0))
            {
                Paint();
            }
        }
        
        // Toggle quadrant overlay with D key
        if (Input.GetKeyDown(KeyCode.D))
        {
            showQuadrantOverlay = !showQuadrantOverlay;
            Debug.Log($"Quadrant overlay: {(showQuadrantOverlay ? "ON" : "OFF")}");
        }
        
        // Restart
        if (Input.GetKeyDown(KeyCode.Space) && !canPaint && !memoryPhaseActive)
        {
            Restart();
        }
    }
    
    void StartMemoryPhase()
    {
        Debug.Log("--- StartMemoryPhase ---");
        
        // Use maskDisplayImage for showing to player, fallback to referencePatternPNG if not set
        Texture2D displayTexture = maskDisplayImage != null ? maskDisplayImage : referencePatternPNG;
        
        if (displayTexture == null)
        {
            Debug.LogError("Cannot start memory phase: No display image assigned!");
            return;
        }
        
        if (targetImage == null)
        {
            Debug.LogError("Cannot start memory phase: targetImage is NULL");
            return;
        }
        
        if (targetDisplay == null)
        {
            Debug.LogError("Cannot start memory phase: targetDisplay is NULL");
            return;
        }
        
        // Show the full mask to the player
        Sprite targetSprite = Sprite.Create(
            displayTexture,
            new Rect(0, 0, displayTexture.width, displayTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        targetImage.sprite = targetSprite;
        targetDisplay.SetActive(true);
        Debug.Log($"Showing display image: {displayTexture.name}");
        
        timer = viewDuration;
        memoryPhaseActive = true;
        canPaint = false;
        
        if (instructionText != null)
        {
            instructionText.text = "Memorize this pattern!";
        }
        
        Debug.Log($"Memory phase started. Duration: {viewDuration}s");
    }
    
    void EndMemoryPhase()
    {
        targetDisplay.SetActive(false);
        memoryPhaseActive = false;
        canPaint = true;
        currentStrokeID = 0;
        playerStrokePoints.Clear();
        
        instructionText.text = "Now draw the pattern!";
        submitButton.gameObject.SetActive(true);
    }
    
    #endregion
    
    #region Painting
    
    void Paint()
    {
        // Ensure camera exists
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[MaskPaintingPDollar] No main camera found!");
                return;
            }
        }
        
        // Ensure sprite renderer exists
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("[MaskPaintingPDollar] SpriteRenderer or sprite is null!");
            return;
        }
        
        // Ensure paint texture exists
        if (paintTexture == null)
        {
            Debug.LogError("[MaskPaintingPDollar] Paint texture is null! Call InitializePaintTexture first.");
            return;
        }
        
        Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
        
        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            Vector2 localPoint = transform.InverseTransformPoint(hit.point);
            
            float spriteWidth = spriteRenderer.sprite.bounds.size.x;
            float spriteHeight = spriteRenderer.sprite.bounds.size.y;
            
            int pixelX = (int)((localPoint.x / spriteWidth + 0.5f) * paintTexture.width);
            int pixelY = (int)((localPoint.y / spriteHeight + 0.5f) * paintTexture.height);
            
            PaintAtPixel(pixelX, pixelY);
            
            // Record point for $P comparison
            playerStrokePoints.Add(new Vector2(pixelX, pixelY));
        }
    }
    
    void PaintAtPixel(int centerX, int centerY)
    {
        for (int x = centerX - brushSize; x <= centerX + brushSize; x++)
        {
            for (int y = centerY - brushSize; y <= centerY + brushSize; y++)
            {
                if (x >= 0 && x < paintTexture.width && y >= 0 && y < paintTexture.height)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
                    
                    if (distance <= brushSize)
                    {
                        // Check if this pixel was originally transparent (eye holes, etc.)
                        if (originalAlphaMap != null && originalAlphaMap[x, y] < 0.1f)
                        {
                            // Skip painting on transparent areas
                            continue;
                        }
                        
                        paintTexture.SetPixel(x, y, paintColor);
                    }
                }
            }
        }
        
        paintTexture.Apply();
    }
    
    #endregion
    
    #region Scoring with $P
    
    public void OnSubmit()
    {
        canPaint = false;
        submitButton.gameObject.SetActive(false);
        
        // Convert player's stroke points to $P Points
        List<PDollarGestureRecognizer.Point> playerPoints = ConvertPlayerDrawingToPoints();
        
        if (playerPoints.Count < 10)
        {
            ShowResult(0, "Not enough drawing detected!");
            return;
        }
        
        playerPointCount = playerPoints.Count;
        
        // Sample if needed
        if (playerPoints.Count > samplePointCount)
        {
            playerPoints = SamplePoints(playerPoints, samplePointCount);
        }
        
        // Create player gesture
        Gesture playerGesture = new Gesture(playerPoints.ToArray(), "PlayerDrawing");
        
        // === PART 1: $P Shape Score ===
        float distance = CalculateGestureDistance(referenceGesture, playerGesture);
        rawDistance = distance;
        shapeScore = ConvertDistanceToScore(distance);
        
        // === PART 2: Quadrant Pixel Score ===
        playerQuadrantCounts = CalculateQuadrantPixelCounts(paintTexture);
        quadrantScore = CalculateQuadrantMatchScore();
        
        // === COMBINED SCORE ===
        finalScore = (shapeScore * shapeWeight) + (quadrantScore * quadrantWeight);
        
        Debug.Log($"=== SCORING BREAKDOWN ===");
        Debug.Log($"Shape Score ($P): {shapeScore:F1}% (weight: {shapeWeight})");
        Debug.Log($"Quadrant Score: {quadrantScore:F1}% (weight: {quadrantWeight})");
        Debug.Log($"Final Score: {finalScore:F1}%");
        Debug.Log($"Reference Quadrants: [{string.Join(", ", referenceQuadrantCounts)}]");
        Debug.Log($"Player Quadrants: [{string.Join(", ", playerQuadrantCounts)}]");
        Debug.Log($"=========================");
        
        string details = showDebugInfo 
            ? $"Shape: {shapeScore:F1}% | Quadrant: {quadrantScore:F1}%\nRef Q: [{string.Join(", ", referenceQuadrantCounts)}]\nYour Q: [{string.Join(", ", playerQuadrantCounts)}]"
            : "";
        
        ShowResult(finalScore, details);
    }
    
    List<PDollarGestureRecognizer.Point> ConvertPlayerDrawingToPoints()
    {
        List<PDollarGestureRecognizer.Point> points = new List<PDollarGestureRecognizer.Point>();
        
        // Option 1: Use stroke points recorded while drawing
        // This captures the actual drawing motion
        int strokeID = 0;
        Vector2 lastPos = Vector2.zero;
        
        foreach (Vector2 pos in playerStrokePoints)
        {
            // Detect new stroke (large jump in position)
            if (points.Count > 0 && Vector2.Distance(pos, lastPos) > 50)
            {
                strokeID++;
            }
            
            points.Add(new PDollarGestureRecognizer.Point(pos.x, pos.y, strokeID));
            lastPos = pos;
        }
        
        // If stroke recording didn't capture enough, fall back to scanning the texture
        if (points.Count < 10)
        {
            points.Clear();
            points = ExtractPointsFromTexture(paintTexture);
        }
        
        return points;
    }
    
    /// <summary>
    /// Calculate distance between two gestures using $P's greedy cloud matching
    /// </summary>
    float CalculateGestureDistance(Gesture g1, Gesture g2)
    {
        // Use the same algorithm as PointCloudRecognizer but return the distance
        return GreedyCloudMatch(g1.Points, g2.Points);
    }
    
    float GreedyCloudMatch(PDollarGestureRecognizer.Point[] points1, PDollarGestureRecognizer.Point[] points2)
    {
        int n = points1.Length;
        float eps = 0.5f;
        int step = (int)Mathf.Floor(Mathf.Pow(n, 1.0f - eps));
        float minDistance = float.MaxValue;
        
        for (int i = 0; i < n; i += step)
        {
            float dist1 = CloudDistance(points1, points2, i);
            float dist2 = CloudDistance(points2, points1, i);
            minDistance = Mathf.Min(minDistance, Mathf.Min(dist1, dist2));
        }
        
        return minDistance;
    }
    
    float CloudDistance(PDollarGestureRecognizer.Point[] points1, PDollarGestureRecognizer.Point[] points2, int startIndex)
    {
        int n = points1.Length;
        bool[] matched = new bool[n];
        
        float sum = 0;
        int i = startIndex;
        
        do
        {
            int index = -1;
            float minDistance = float.MaxValue;
            
            for (int j = 0; j < n; j++)
            {
                if (!matched[j])
                {
                    float dist = SqrEuclideanDistance(points1[i], points2[j]);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        index = j;
                    }
                }
            }
            
            matched[index] = true;
            float weight = 1.0f - ((i - startIndex + n) % n) / (1.0f * n);
            sum += weight * minDistance;
            i = (i + 1) % n;
            
        } while (i != startIndex);
        
        return sum;
    }
    
    float SqrEuclideanDistance(PDollarGestureRecognizer.Point a, PDollarGestureRecognizer.Point b)
    {
        return (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
    }
    
    /// <summary>
    /// Convert $P distance to a 0-100 similarity score
    /// </summary>
    float ConvertDistanceToScore(float distance)
    {
        // $P distances for normalized gestures typically range from 0 (identical) to ~2 (very different)
        // We'll use an exponential decay to convert to percentage
        
        // Adjust these values to tune difficulty:
        float maxDistance = 1.5f;  // Distance at which score becomes 0
        
        if (distance >= maxDistance)
            return 0f;
        
        // Linear conversion (simple)
        // float score = (1f - (distance / maxDistance)) * 100f;
        
        // Exponential conversion (more forgiving for small errors)
        float normalized = distance / maxDistance;
        float score = (1f - Mathf.Pow(normalized, 0.7f)) * 100f;
        
        return Mathf.Clamp(score, 0f, 100f);
    }
    
    #endregion
    
    #region Quadrant Pixel Matching
    
    /// <summary>
    /// Divide the texture into a grid and count pattern-colored pixels in each cell
    /// </summary>
    int[] CalculateQuadrantPixelCounts(Texture2D texture)
    {
        int totalCells = gridDivisions * gridDivisions;
        int[] counts = new int[totalCells];
        
        int cellWidth = texture.width / gridDivisions;
        int cellHeight = texture.height / gridDivisions;
        
        for (int x = 0; x < texture.width; x++)
        {
            for (int y = 0; y < texture.height; y++)
            {
                Color pixel = texture.GetPixel(x, y);
                
                if (IsPatternColor(pixel))
                {
                    // Determine which grid cell this pixel belongs to
                    int cellX = Mathf.Min(x / cellWidth, gridDivisions - 1);
                    int cellY = Mathf.Min(y / cellHeight, gridDivisions - 1);
                    int cellIndex = cellY * gridDivisions + cellX;
                    
                    counts[cellIndex]++;
                }
            }
        }
        
        return counts;
    }
    
    /// <summary>
    /// Compare reference quadrant counts to player quadrant counts WITH COLOR CHECKING
    /// Returns a score from 0-100
    /// 
    /// Scoring:
    /// - Right location + Right color = 1.0x multiplier
    /// - Right location + Wrong color = 0.5x multiplier
    /// - Wrong location = penalized
    /// </summary>
    float CalculateQuadrantMatchScore()
    {
        if (referenceQuadrantCounts == null || playerQuadrantCounts == null)
            return 0f;
        
        int totalCells = gridDivisions * gridDivisions;
        float totalScore = 0f;
        float totalPenalty = 0f;
        
        // Find the max pixel count for normalization
        int maxRefCount = 1;
        foreach (int count in referenceQuadrantCounts)
            if (count > maxRefCount) maxRefCount = count;
        
        int maxPlayerCount = 1;
        foreach (int count in playerQuadrantCounts)
            if (count > maxPlayerCount) maxPlayerCount = count;
        
        int emptyQuadrantViolations = 0;
        
        // Calculate color-aware quadrant scores
        var playerQuadrantColorData = CalculatePlayerQuadrantColors();
        
        for (int i = 0; i < totalCells; i++)
        {
            float refNormalized = (float)referenceQuadrantCounts[i] / maxRefCount;
            float playerNormalized = (float)playerQuadrantCounts[i] / maxRefCount;
            
            // Check for empty quadrant violation: reference is empty but player painted there
            bool isRefEmpty = referenceQuadrantCounts[i] < 10;
            bool playerPaintedHere = playerQuadrantCounts[i] > 50;
            
            if (isRefEmpty && playerPaintedHere)
            {
                emptyQuadrantViolations++;
                float violationAmount = (float)playerQuadrantCounts[i] / maxPlayerCount;
                totalPenalty += violationAmount * (emptyQuadrantPenalty / 100f);
                
                Debug.Log($"Empty quadrant violation in Q{i}: Player painted {playerQuadrantCounts[i]} pixels where reference has {referenceQuadrantCounts[i]}");
                continue;
            }
            
            // Calculate location match (1 = perfect match, 0 = completely different)
            float diff = Mathf.Abs(refNormalized - playerNormalized);
            float locationScore = 1f - Mathf.Min(diff, 1f);
            
            // Calculate color match multiplier
            float colorMultiplier = 1.0f;
            
            if (referenceQuadrantColors != null && i < referenceQuadrantColors.Length && playerPaintedHere)
            {
                Color refColor = referenceQuadrantColors[i];
                Color playerDominantColor = playerQuadrantColorData[i];
                
                if (refColor != Color.clear && playerDominantColor != Color.clear)
                {
                    bool colorMatches = ColorsMatch(refColor, playerDominantColor);
                    
                    if (colorMatches)
                    {
                        colorMultiplier = 1.0f;  // Right location + Right color
                        Debug.Log($"Q{i}: Color MATCH - Reference: {refColor}, Player: {playerDominantColor}");
                    }
                    else
                    {
                        colorMultiplier = 0.5f;  // Right location + Wrong color
                        Debug.Log($"Q{i}: Color MISMATCH - Reference: {refColor}, Player: {playerDominantColor}");
                    }
                }
            }
            
            // Apply color multiplier to location score
            float cellScore = locationScore * colorMultiplier;
            
            // Weight cells with more reference pixels as more important
            float cellWeight = refNormalized > 0.1f ? 1f : 0.3f;
            
            totalScore += cellScore * cellWeight;
        }
        
        // Normalize to 0-100
        float maxPossibleScore = 0f;
        for (int i = 0; i < totalCells; i++)
        {
            float refNormalized = (float)referenceQuadrantCounts[i] / maxRefCount;
            float cellWeight = refNormalized > 0.1f ? 1f : 0.3f;
            maxPossibleScore += cellWeight;
        }
        
        float baseScore = (totalScore / maxPossibleScore) * 100f;
        
        // Apply penalty for painting in empty quadrants
        float penaltyMultiplier = 1f - Mathf.Min(totalPenalty, 1f);
        float finalQuadrantScore = baseScore * penaltyMultiplier;
        
        if (emptyQuadrantViolations > 0)
        {
            Debug.Log($"Empty quadrant violations: {emptyQuadrantViolations}, Penalty applied: {(1f - penaltyMultiplier) * 100f:F1}%");
        }
        
        Debug.Log($"Quadrant score (with color): {finalQuadrantScore:F1}%");
        
        return finalQuadrantScore;
    }
    
    /// <summary>
    /// Calculate the dominant color in each quadrant of the player's painting
    /// </summary>
    Color[] CalculatePlayerQuadrantColors()
    {
        int totalCells = gridDivisions * gridDivisions;
        Color[] quadrantColors = new Color[totalCells];
        
        if (paintTexture == null) return quadrantColors;
        
        int cellWidth = paintTexture.width / gridDivisions;
        int cellHeight = paintTexture.height / gridDivisions;
        
        for (int cellY = 0; cellY < gridDivisions; cellY++)
        {
            for (int cellX = 0; cellX < gridDivisions; cellX++)
            {
                int cellIndex = cellY * gridDivisions + cellX;
                
                // Count each color type in this quadrant
                int redCount = 0, yellowCount = 0, blueCount = 0;
                
                int startX = cellX * cellWidth;
                int startY = cellY * cellHeight;
                
                for (int x = startX; x < startX + cellWidth && x < paintTexture.width; x++)
                {
                    for (int y = startY; y < startY + cellHeight && y < paintTexture.height; y++)
                    {
                        Color pixel = paintTexture.GetPixel(x, y);
                        
                        // Skip if this was part of the original base (not painted)
                        if (originalAlphaMap != null && x < originalAlphaMap.GetLength(0) && y < originalAlphaMap.GetLength(1))
                        {
                            // Only count if it's painted (different from original or on white area)
                        }
                        
                        if (pixel.a > 0.5f)  // Not transparent
                        {
                            // Categorize the color
                            if (IsRedColor(pixel)) redCount++;
                            else if (IsYellowColor(pixel)) yellowCount++;
                            else if (IsBlueColor(pixel)) blueCount++;
                        }
                    }
                }
                
                // Find dominant color
                if (redCount >= yellowCount && redCount >= blueCount && redCount > 0)
                    quadrantColors[cellIndex] = Color.red;
                else if (yellowCount >= redCount && yellowCount >= blueCount && yellowCount > 0)
                    quadrantColors[cellIndex] = Color.yellow;
                else if (blueCount > 0)
                    quadrantColors[cellIndex] = Color.blue;
                else
                    quadrantColors[cellIndex] = Color.clear;
            }
        }
        
        return quadrantColors;
    }
    
    bool IsRedColor(Color c)
    {
        return c.r > 0.7f && c.g < 0.4f && c.b < 0.4f;
    }
    
    bool IsYellowColor(Color c)
    {
        return c.r > 0.7f && c.g > 0.7f && c.b < 0.4f;
    }
    
    bool IsBlueColor(Color c)
    {
        return c.r < 0.4f && c.g < 0.4f && c.b > 0.7f;
    }
    
    bool ColorsMatch(Color a, Color b)
    {
        // Check if both are the same category (red, yellow, or blue)
        if (IsRedColor(a) && IsRedColor(b)) return true;
        if (IsYellowColor(a) && IsYellowColor(b)) return true;
        if (IsBlueColor(a) && IsBlueColor(b)) return true;
        return false;
    }
    
    #endregion

    #region Results & Reset
    
    public void ShowResult(float score, string details)
    {
        bool passed = score >= passingScore;
        
        if (passed)
        {
            //resultText.text = $"PASS! Score: {score:F1}%\n{details}";
           //resultText.color = Color.green;

            Debug.Log("attempting to invoke pass event");
            onPass.Invoke(score);
        }
        else
        {
            //resultText.text = $"FAIL! Score: {score:F1}% (Need {passingScore}%)\n{details}";
            //resultText.color = Color.red;

            Debug.Log("attempting to invoke fail event");
            onFail.Invoke(score);
        }
        
        //resultText.gameObject.SetActive(true);
        
        // Send score to GameData for persistence between scenes
        if (useGameData)
        {
            SendScoreToGameData(score);
            //instructionText.text = "Returning to shop...";
            
            // Auto-return to shop after delay
            StartCoroutine(AutoReturnToShop(2f));
        }
        else
        {
            //instructionText.text = "Press Space to try again";
        }
        
        Debug.Log($"=== FINAL RESULT ===");
        Debug.Log($"Score: {score:F1}%");
        Debug.Log($"Result: {(passed ? "PASSED" : "FAILED")}");
        Debug.Log($"====================");
    }
    
    IEnumerator AutoReturnToShop(float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToShop();
    }
    
    void Restart()
    {
        // Reset canvas
        paintTexture.SetPixels(originalBaseTexture.GetPixels());
        paintTexture.Apply();
        
        // Reset tracking
        playerStrokePoints.Clear();
        currentStrokeID = 0;
        rawDistance = 0;
        shapeScore = 0;
        quadrantScore = 0;
        finalScore = 0;
        playerPointCount = 0;
        playerQuadrantCounts = null;
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    #endregion
    
    #region Debug Overlay
    
    void OnGUI()
    {
        if (!showQuadrantOverlay) return;
        if (spriteRenderer == null) return;
        
        // Get sprite bounds in screen space
        Bounds bounds = spriteRenderer.bounds;
        
        Vector3 bottomLeft = mainCamera.WorldToScreenPoint(bounds.min);
        Vector3 topRight = mainCamera.WorldToScreenPoint(bounds.max);
        
        // Flip Y because GUI coordinates are inverted
        bottomLeft.y = Screen.height - bottomLeft.y;
        topRight.y = Screen.height - topRight.y;
        
        float left = bottomLeft.x;
        float right = topRight.x;
        float top = topRight.y;
        float bottom = bottomLeft.y;
        
        float width = right - left;
        float height = bottom - top;
        
        // Create a texture for drawing lines
        Texture2D lineTex = new Texture2D(1, 1);
        lineTex.SetPixel(0, 0, quadrantLineColor);
        lineTex.Apply();
        
        // Draw grid lines
        for (int i = 1; i < gridDivisions; i++)
        {
            // Vertical lines
            float xPos = left + (width / gridDivisions) * i;
            GUI.DrawTexture(new Rect(xPos - 1, top, 3, height), lineTex);
            
            // Horizontal lines
            float yPos = top + (height / gridDivisions) * i;
            GUI.DrawTexture(new Rect(left, yPos - 1, width, 3), lineTex);
        }
        
        // Draw border
        GUI.DrawTexture(new Rect(left, top, width, 3), lineTex);  // Top
        GUI.DrawTexture(new Rect(left, bottom - 3, width, 3), lineTex);  // Bottom
        GUI.DrawTexture(new Rect(left, top, 3, height), lineTex);  // Left
        GUI.DrawTexture(new Rect(right - 3, top, 3, height), lineTex);  // Right
        
        // Draw quadrant labels with pixel counts
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 14;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.normal.textColor = quadrantLineColor;
        
        float cellWidth = width / gridDivisions;
        float cellHeight = height / gridDivisions;
        
        for (int row = 0; row < gridDivisions; row++)
        {
            for (int col = 0; col < gridDivisions; col++)
            {
                int index = row * gridDivisions + col;
                float cellX = left + col * cellWidth + 5;
                float cellY = top + row * cellHeight + 5;
                
                string refCount = referenceQuadrantCounts != null && index < referenceQuadrantCounts.Length 
                    ? referenceQuadrantCounts[index].ToString() 
                    : "?";
                string playerCount = playerQuadrantCounts != null && index < playerQuadrantCounts.Length 
                    ? playerQuadrantCounts[index].ToString() 
                    : "?";
                
                GUI.Label(new Rect(cellX, cellY, cellWidth - 10, 40), $"Q{index}\nRef:{refCount}\nYou:{playerCount}", labelStyle);
            }
        }
        
        // Cleanup
        Destroy(lineTex);
    }
    
    #endregion
}