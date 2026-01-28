using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
    [SerializeField] private float colorTolerance = 0.3f;
    
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
    [SerializeField] private int referencePointCount;
    [SerializeField] private int playerPointCount;
    [SerializeField] private float rawDistance;
    [SerializeField] private float shapeScore;
    [SerializeField] private float quadrantScore;
    [SerializeField] private float finalScore;
    
    // Quadrant pixel counts for debugging
    [SerializeField] private int[] referenceQuadrantCounts;
    [SerializeField] private int[] playerQuadrantCounts;
    
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
    
    void Start()
    {
        Debug.Log("=== MaskPaintingPDollar START ===");
        
        mainCamera = Camera.main;
        Debug.Log($"[1] Camera: {(mainCamera != null ? "OK" : "NULL")}");
        
        // Check sprite renderer
        if (spriteRenderer == null)
        {
            Debug.LogError("[2] SpriteRenderer is NULL!");
            return;
        }
        Debug.Log($"[2] SpriteRenderer: OK");
        
        if (spriteRenderer.sprite == null)
        {
            Debug.LogError("[3] SpriteRenderer.sprite is NULL!");
            return;
        }
        Debug.Log($"[3] Sprite: OK");
        
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
        
        // Setup UI
        if (submitButton == null)
        {
            Debug.LogError("[9] Submit Button is NULL!");
            return;
        }
        submitButton.onClick.AddListener(OnSubmit);
        submitButton.gameObject.SetActive(false);
        Debug.Log("[9] Submit button: OK");
        
        if (resultText == null)
        {
            Debug.LogError("[10] Result Text is NULL!");
            return;
        }
        resultText.gameObject.SetActive(false);
        Debug.Log("[10] Result text: OK");
        
        Debug.Log("[11] Starting memory phase...");
        StartMemoryPhase();
        Debug.Log("=== START COMPLETE ===");
    }
    
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
    /// Compare reference quadrant counts to player quadrant counts
    /// Returns a score from 0-100
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
        
        for (int i = 0; i < totalCells; i++)
        {
            float refNormalized = (float)referenceQuadrantCounts[i] / maxRefCount;
            float playerNormalized = (float)playerQuadrantCounts[i] / maxRefCount;
            
            // Check for empty quadrant violation: reference is empty but player painted there
            bool isRefEmpty = referenceQuadrantCounts[i] < 10;  // Less than 10 pixels = effectively empty
            bool playerPaintedHere = playerQuadrantCounts[i] > 50;  // More than 50 pixels = significant paint
            
            if (isRefEmpty && playerPaintedHere)
            {
                emptyQuadrantViolations++;
                // Apply penalty proportional to how much they painted in the empty quadrant
                float violationAmount = (float)playerQuadrantCounts[i] / maxPlayerCount;
                totalPenalty += violationAmount * (emptyQuadrantPenalty / 100f);
                
                Debug.Log($"Empty quadrant violation in Q{i}: Player painted {playerQuadrantCounts[i]} pixels where reference has {referenceQuadrantCounts[i]}");
            }
            
            // Calculate how close they are (1 = perfect match, 0 = completely different)
            float diff = Mathf.Abs(refNormalized - playerNormalized);
            float cellScore = 1f - Mathf.Min(diff, 1f);
            
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
            Debug.Log($"Quadrant score before penalty: {baseScore:F1}%, after: {finalQuadrantScore:F1}%");
        }
        
        return finalQuadrantScore;
    }
    
    #endregion

    #region Results & Reset
    
    void ShowResult(float score, string details)
    {
        bool passed = score >= passingScore;
        
        if (passed)
        {
            resultText.text = $"PASS! Score: {score:F1}%\n{details}";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = $"FAIL! Score: {score:F1}% (Need {passingScore}%)\n{details}";
            resultText.color = Color.red;
        }
        
        resultText.gameObject.SetActive(true);
        instructionText.text = "Press Space to try again";
        
        Debug.Log($"=== FINAL RESULT ===");
        Debug.Log($"Score: {score:F1}%");
        Debug.Log($"Result: {(passed ? "PASSED" : "FAILED")}");
        Debug.Log($"====================");
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
}