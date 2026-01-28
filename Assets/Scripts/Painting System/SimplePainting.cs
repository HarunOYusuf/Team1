using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class MaskZone
{
    public string zoneName;
    public Rect zoneBounds;
    public Vector2 shapeCenter;
    public Texture2D extractedShape;
    public int shapePixelCount;
    public Color shapeColor;
    
    [Header("Runtime Scores")]
    public float shapeScore;
    public float locationScore;
    public float sizeScore;
    public float zoneScore;
    
    [Header("Debug Info")]
    public float coveragePercent;
    public float precisionPercent;
    public float overflowPenalty;
}

public class SimplePainting : MonoBehaviour
{
    [Header("Painting Setup")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 10;
    
    [Header("Target & Comparison")]
    [SerializeField] private Texture2D fullMaskTexture;
    [SerializeField] private float passingScore = 50f;
    
    [Header("Detectable Colors")]
    [SerializeField] private Color detectRed = Color.red;
    [SerializeField] private Color detectBlue = Color.blue;
    [SerializeField] private Color detectYellow = Color.yellow;
    [SerializeField] private float colorDetectionTolerance = 0.3f;
    
    [Header("Tolerance Settings")]
    [SerializeField] private int shapeToleranceRadius = 8;
    [SerializeField] private float locationFullScoreRadius = 30f;
    [SerializeField] private float sizeToleranceLower = 0.7f;
    [SerializeField] private float sizeToleranceUpper = 1.3f;
    
    [Header("Shape Strictness")]
    [SerializeField] [Range(0f, 1f)] private float minimumCoverageRequired = 0.4f;  // Must cover at least 40% of original
    [SerializeField] [Range(0f, 1f)] private float minimumPrecisionRequired = 0.5f; // At least 50% of paint must be on target
    [SerializeField] [Range(0f, 2f)] private float overflowPenaltyMultiplier = 1.5f; // Penalty for painting outside
    
    [Header("Scoring Weights")]
    [SerializeField] [Range(0f, 1f)] private float shapeWeight = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float locationWeight = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float sizeWeight = 0.25f;
    
    [Header("Auto Zone Detection")]
    [SerializeField] private int zonePadding = 20;
    [SerializeField] private int minShapePixels = 50;
    
    [Header("UI References")]
    [SerializeField] private GameObject targetDisplay;
    [SerializeField] private Image targetImage;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("Memory Phase")]
    [SerializeField] private float viewDuration = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool showZoneOverlay = true;
    [SerializeField] private bool showDetailedScores = true;
    [SerializeField] private Color zoneOverlayColor = new Color(0f, 1f, 0f, 0.2f);
    [SerializeField] private Color shapeOverlayColor = new Color(1f, 1f, 0f, 0.5f);
    
    [Header("Detected Zones (Auto-populated)")]
    [SerializeField] private List<MaskZone> zones = new List<MaskZone>();
    
    private Texture2D paintTexture;
    private Texture2D expandedMaskTexture;
    private Texture2D originalBaseTexture;  // Store original for reset
    private Camera mainCamera;
    private bool canPaint = false;
    private float timer;
    private bool memoryPhaseActive = false;
    private Color backgroundColor = Color.white;
    
    void Start()
    {
        mainCamera = Camera.main;
        
        // Store original base texture for reset
        Texture2D baseTexture = spriteRenderer.sprite.texture;
        originalBaseTexture = new Texture2D(baseTexture.width, baseTexture.height);
        originalBaseTexture.SetPixels(baseTexture.GetPixels());
        originalBaseTexture.Apply();
        
        // Create paintable texture
        paintTexture = new Texture2D(baseTexture.width, baseTexture.height);
        paintTexture.SetPixels(baseTexture.GetPixels());
        paintTexture.Apply();
        
        Sprite newSprite = Sprite.Create(
            paintTexture,
            new Rect(0, 0, paintTexture.width, paintTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteRenderer.sprite = newSprite;
        
        AutoDetectZones();
        GenerateExpandedMask();
        
        submitButton.onClick.AddListener(OnSubmit);
        submitButton.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    bool IsDetectableColor(Color c, out Color matchedColor)
    {
        matchedColor = Color.clear;
        
        if (c.a < 0.1f)
            return false;
        
        if (ColorsMatchWithTolerance(c, detectRed, colorDetectionTolerance))
        {
            matchedColor = detectRed;
            return true;
        }
        
        if (ColorsMatchWithTolerance(c, detectBlue, colorDetectionTolerance))
        {
            matchedColor = detectBlue;
            return true;
        }
        
        if (ColorsMatchWithTolerance(c, detectYellow, colorDetectionTolerance))
        {
            matchedColor = detectYellow;
            return true;
        }
        
        return false;
    }
    
    bool ColorsMatchWithTolerance(Color c1, Color c2, float tolerance)
    {
        float rDiff = Mathf.Abs(c1.r - c2.r);
        float gDiff = Mathf.Abs(c1.g - c2.g);
        float bDiff = Mathf.Abs(c1.b - c2.b);
        
        return (rDiff < tolerance && gDiff < tolerance && bDiff < tolerance);
    }
    
    void AutoDetectZones()
    {
        zones.Clear();
        
        if (fullMaskTexture == null)
        {
            Debug.LogError("No full mask texture assigned!");
            return;
        }
        
        int width = fullMaskTexture.width;
        int height = fullMaskTexture.height;
        
        bool[,] visited = new bool[width, height];
        int zoneCount = 0;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;
                
                Color pixel = fullMaskTexture.GetPixel(x, y);
                Color matchedColor;
                
                if (IsDetectableColor(pixel, out matchedColor))
                {
                    List<Vector2Int> shapePixels = FloodFillByColor(x, y, visited, matchedColor);
                    
                    if (shapePixels.Count >= minShapePixels)
                    {
                        MaskZone zone = CreateZoneFromPixels(shapePixels, zoneCount, matchedColor);
                        zones.Add(zone);
                        zoneCount++;
                        
                        string colorName = GetColorName(matchedColor);
                        Debug.Log($"Detected zone '{zone.zoneName}' ({colorName}) with {shapePixels.Count} pixels at {zone.zoneBounds}");
                    }
                }
                else
                {
                    visited[x, y] = true;
                }
            }
        }
        
        Debug.Log($"Auto-detected {zones.Count} colored zones from mask");
    }
    
    string GetColorName(Color c)
    {
        if (ColorsMatchWithTolerance(c, detectRed, colorDetectionTolerance))
            return "Red";
        if (ColorsMatchWithTolerance(c, detectBlue, colorDetectionTolerance))
            return "Blue";
        if (ColorsMatchWithTolerance(c, detectYellow, colorDetectionTolerance))
            return "Yellow";
        return "Unknown";
    }
    
    List<Vector2Int> FloodFillByColor(int startX, int startY, bool[,] visited, Color targetColor)
    {
        List<Vector2Int> pixels = new List<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        
        queue.Enqueue(new Vector2Int(startX, startY));
        
        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;
            
            if (x < 0 || x >= fullMaskTexture.width || y < 0 || y >= fullMaskTexture.height)
                continue;
            
            if (visited[x, y])
                continue;
            
            Color pixel = fullMaskTexture.GetPixel(x, y);
            
            if (!ColorsMatchWithTolerance(pixel, targetColor, colorDetectionTolerance))
            {
                visited[x, y] = true;
                continue;
            }
            
            visited[x, y] = true;
            pixels.Add(current);
            
            queue.Enqueue(new Vector2Int(x + 1, y));
            queue.Enqueue(new Vector2Int(x - 1, y));
            queue.Enqueue(new Vector2Int(x, y + 1));
            queue.Enqueue(new Vector2Int(x, y - 1));
            queue.Enqueue(new Vector2Int(x + 1, y + 1));
            queue.Enqueue(new Vector2Int(x - 1, y - 1));
            queue.Enqueue(new Vector2Int(x + 1, y - 1));
            queue.Enqueue(new Vector2Int(x - 1, y + 1));
        }
        
        return pixels;
    }
    
    MaskZone CreateZoneFromPixels(List<Vector2Int> pixels, int zoneIndex, Color shapeColor)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        float sumX = 0, sumY = 0;
        
        foreach (Vector2Int p in pixels)
        {
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
            sumX += p.x;
            sumY += p.y;
        }
        
        Vector2 shapeCenter = new Vector2(sumX / pixels.Count, sumY / pixels.Count);
        
        int paddedMinX = Mathf.Max(0, minX - zonePadding);
        int paddedMinY = Mathf.Max(0, minY - zonePadding);
        int paddedMaxX = Mathf.Min(fullMaskTexture.width - 1, maxX + zonePadding);
        int paddedMaxY = Mathf.Min(fullMaskTexture.height - 1, maxY + zonePadding);
        
        Rect zoneBounds = new Rect(
            paddedMinX,
            paddedMinY,
            paddedMaxX - paddedMinX,
            paddedMaxY - paddedMinY
        );
        
        int shapeWidth = maxX - minX + 1;
        int shapeHeight = maxY - minY + 1;
        Texture2D extractedShape = new Texture2D(shapeWidth, shapeHeight);
        
        Color[] clearColors = new Color[shapeWidth * shapeHeight];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = Color.white;
        extractedShape.SetPixels(clearColors);
        
        foreach (Vector2Int p in pixels)
        {
            int localX = p.x - minX;
            int localY = p.y - minY;
            extractedShape.SetPixel(localX, localY, fullMaskTexture.GetPixel(p.x, p.y));
        }
        extractedShape.Apply();
        
        string colorName = GetColorName(shapeColor);
        
        MaskZone zone = new MaskZone
        {
            zoneName = $"{colorName} Shape {zoneIndex + 1}",
            zoneBounds = zoneBounds,
            shapeCenter = shapeCenter,
            extractedShape = extractedShape,
            shapePixelCount = pixels.Count,
            shapeColor = shapeColor
        };
        
        return zone;
    }
    
    void GenerateExpandedMask()
    {
        expandedMaskTexture = new Texture2D(fullMaskTexture.width, fullMaskTexture.height);
        
        bool[,] isDetectablePixel = new bool[fullMaskTexture.width, fullMaskTexture.height];
        
        for (int x = 0; x < fullMaskTexture.width; x++)
        {
            for (int y = 0; y < fullMaskTexture.height; y++)
            {
                Color c = fullMaskTexture.GetPixel(x, y);
                Color matchedColor;
                isDetectablePixel[x, y] = IsDetectableColor(c, out matchedColor);
            }
        }
        
        for (int x = 0; x < fullMaskTexture.width; x++)
        {
            for (int y = 0; y < fullMaskTexture.height; y++)
            {
                bool withinTolerance = false;
                
                for (int dx = -shapeToleranceRadius; dx <= shapeToleranceRadius && !withinTolerance; dx++)
                {
                    for (int dy = -shapeToleranceRadius; dy <= shapeToleranceRadius && !withinTolerance; dy++)
                    {
                        int checkX = x + dx;
                        int checkY = y + dy;
                        
                        if (checkX >= 0 && checkX < fullMaskTexture.width &&
                            checkY >= 0 && checkY < fullMaskTexture.height)
                        {
                            float distance = Mathf.Sqrt(dx * dx + dy * dy);
                            
                            if (distance <= shapeToleranceRadius && isDetectablePixel[checkX, checkY])
                            {
                                withinTolerance = true;
                            }
                        }
                    }
                }
                
                if (withinTolerance)
                {
                    expandedMaskTexture.SetPixel(x, y, Color.red);
                }
                else
                {
                    expandedMaskTexture.SetPixel(x, y, backgroundColor);
                }
            }
        }
        
        expandedMaskTexture.Apply();
        Debug.Log($"Generated expanded mask with tolerance radius: {shapeToleranceRadius}");
    }
    
    bool IsShapePixel(Color c)
    {
        Color matchedColor;
        return IsDetectableColor(c, out matchedColor);
    }
    
    void Update()
    {
        if (memoryPhaseActive)
        {
            timer -= Time.deltaTime;
            instructionText.text = $"Memorize this design! {Mathf.Ceil(timer)}s";
            
            if (timer <= 0)
            {
                EndMemoryPhase();
            }
        }
        
        if (canPaint && Input.GetMouseButton(0))
        {
            Paint();
        }
        
        if (Input.GetKeyDown(KeyCode.Alpha1)) 
        {
            paintColor = detectRed;
            Debug.Log("Switched to Red");
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) 
        {
            paintColor = detectBlue;
            Debug.Log("Switched to Blue");
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) 
        {
            paintColor = detectYellow;
            Debug.Log("Switched to Yellow");
        }
        
        if (Input.GetKeyDown(KeyCode.D))
        {
            showZoneOverlay = !showZoneOverlay;
            if (canPaint)
            {
                if (showZoneOverlay)
                {
                    ApplyDebugOverlay();
                }
                else
                {
                    RemoveDebugOverlay();
                }
            }
        }
        
        if (Input.GetKeyDown(KeyCode.Space) && !canPaint && !memoryPhaseActive)
        {
            RestartDemo();
        }
    }
    
    void StartMemoryPhase()
    {
        Sprite targetSprite = Sprite.Create(
            fullMaskTexture,
            new Rect(0, 0, fullMaskTexture.width, fullMaskTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        targetImage.sprite = targetSprite;
        targetDisplay.SetActive(true);
        
        timer = viewDuration;
        memoryPhaseActive = true;
        canPaint = false;
        
        instructionText.text = "Memorize this design!";
        Debug.Log("Memory phase started!");
    }
    
    void EndMemoryPhase()
    {
        targetDisplay.SetActive(false);
        memoryPhaseActive = false;
        
        canPaint = true;
        instructionText.text = "Paint! (1=Red, 2=Blue, 3=Yellow, D=Debug)";
        
        submitButton.gameObject.SetActive(true);
        
        if (showZoneOverlay)
        {
            ApplyDebugOverlay();
        }
        
        Debug.Log("Memory phase ended!");
    }
    
    void ApplyDebugOverlay()
    {
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                if (IsShapePixel(currentColor) || IsBlackOutline(currentColor))
                    continue;
                
                Color expandedColor = expandedMaskTexture.GetPixel(x, y);
                if (!ColorsMatchWithTolerance(expandedColor, backgroundColor, 0.1f))
                {
                    paintTexture.SetPixel(x, y, shapeOverlayColor);
                    continue;
                }
                
                foreach (MaskZone zone in zones)
                {
                    if (zone.zoneBounds.Contains(new Vector2(x, y)))
                    {
                        if (ColorsMatchWithTolerance(currentColor, backgroundColor, 0.1f))
                        {
                            paintTexture.SetPixel(x, y, zoneOverlayColor);
                        }
                        break;
                    }
                }
            }
        }
        
        paintTexture.Apply();
        Debug.Log("Debug overlay ON");
    }
    
    bool IsBlackOutline(Color c)
    {
        return c.a >= 0.1f && c.r < 0.2f && c.g < 0.2f && c.b < 0.2f;
    }
    
    void RemoveDebugOverlay()
    {
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                if (ColorsMatchWithTolerance(currentColor, zoneOverlayColor, 0.1f) || 
                    ColorsMatchWithTolerance(currentColor, shapeOverlayColor, 0.1f))
                {
                    paintTexture.SetPixel(x, y, backgroundColor);
                }
            }
        }
        
        paintTexture.Apply();
        Debug.Log("Debug overlay OFF");
    }
    
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
    
    public void OnSubmit()
    {
        canPaint = false;
        submitButton.gameObject.SetActive(false);
        
        if (showZoneOverlay)
        {
            RemoveDebugOverlay();
        }
        
        float totalScore = 0f;
        string detailedResults = "";
        
        foreach (MaskZone zone in zones)
        {
            CalculateZoneScore(zone);
            totalScore += zone.zoneScore;
            
            if (showDetailedScores)
            {
                detailedResults += $"\n{zone.zoneName}: Shape {zone.shapeScore:F0}% | Loc {zone.locationScore:F0}% | Size {zone.sizeScore:F0}% = {zone.zoneScore:F0}%";
            }
        }
        
        float finalScore = zones.Count > 0 ? totalScore / zones.Count : 0f;
        
        bool passed = finalScore >= passingScore;
        
        if (passed)
        {
            resultText.text = $"PASS! Score: {finalScore:F1}%{detailedResults}";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = $"FAIL! Score: {finalScore:F1}% (Need {passingScore}%){detailedResults}";
            resultText.color = Color.red;
        }
        
        resultText.gameObject.SetActive(true);
        instructionText.text = "Press Space to try again";
        
        Debug.Log($"Final Score: {finalScore:F1}% - {(passed ? "PASSED" : "FAILED")}");
    }
    
    void CalculateZoneScore(MaskZone zone)
    {
        // Find player's drawing within this zone that matches the expected color
        List<Vector2Int> playerPixelsInZone = new List<Vector2Int>();
        float sumX = 0, sumY = 0;
        
        int zoneMinX = (int)zone.zoneBounds.x;
        int zoneMinY = (int)zone.zoneBounds.y;
        int zoneMaxX = (int)(zone.zoneBounds.x + zone.zoneBounds.width);
        int zoneMaxY = (int)(zone.zoneBounds.y + zone.zoneBounds.height);
        
        for (int x = zoneMinX; x <= zoneMaxX; x++)
        {
            for (int y = zoneMinY; y <= zoneMaxY; y++)
            {
                if (x < 0 || x >= paintTexture.width || y < 0 || y >= paintTexture.height)
                    continue;
                
                Color playerColor = paintTexture.GetPixel(x, y);
                
                if (ColorsMatchWithTolerance(playerColor, zone.shapeColor, colorDetectionTolerance))
                {
                    playerPixelsInZone.Add(new Vector2Int(x, y));
                    sumX += x;
                    sumY += y;
                }
            }
        }
        
        // If player didn't draw anything
        if (playerPixelsInZone.Count == 0)
        {
            zone.shapeScore = 0f;
            zone.locationScore = 0f;
            zone.sizeScore = 0f;
            zone.zoneScore = 0f;
            zone.coveragePercent = 0f;
            zone.precisionPercent = 0f;
            zone.overflowPenalty = 0f;
            return;
        }
        
        Vector2 playerCenter = new Vector2(sumX / playerPixelsInZone.Count, sumY / playerPixelsInZone.Count);
        
        // --- STRICT SHAPE SCORE ---
        int coveredPixels = 0;
        int playerInExpandedZone = 0;
        int playerOutsideExpandedZone = 0;
        
        // Count original shape pixels and how many player covered
        int originalShapePixels = 0;
        for (int x = zoneMinX; x <= zoneMaxX; x++)
        {
            for (int y = zoneMinY; y <= zoneMaxY; y++)
            {
                if (x < 0 || x >= fullMaskTexture.width || y < 0 || y >= fullMaskTexture.height)
                    continue;
                
                Color originalColor = fullMaskTexture.GetPixel(x, y);
                if (ColorsMatchWithTolerance(originalColor, zone.shapeColor, colorDetectionTolerance))
                {
                    originalShapePixels++;
                    
                    Color playerColor = paintTexture.GetPixel(x, y);
                    if (ColorsMatchWithTolerance(playerColor, zone.shapeColor, colorDetectionTolerance))
                    {
                        coveredPixels++;
                    }
                }
            }
        }
        
        // Check each player pixel - is it in the expanded zone or not?
        foreach (Vector2Int p in playerPixelsInZone)
        {
            Color expandedColor = expandedMaskTexture.GetPixel(p.x, p.y);
            if (!ColorsMatchWithTolerance(expandedColor, backgroundColor, 0.1f))
            {
                playerInExpandedZone++;
            }
            else
            {
                playerOutsideExpandedZone++;
            }
        }
        
        // Calculate coverage and precision
        float coverage = originalShapePixels > 0 ? (float)coveredPixels / originalShapePixels : 0f;
        float precision = playerPixelsInZone.Count > 0 ? (float)playerInExpandedZone / playerPixelsInZone.Count : 0f;
        
        // Calculate overflow penalty - how much did they paint outside?
        float overflowRatio = playerPixelsInZone.Count > 0 ? (float)playerOutsideExpandedZone / playerPixelsInZone.Count : 0f;
        float overflowPenalty = overflowRatio * overflowPenaltyMultiplier;
        
        // Store debug info
        zone.coveragePercent = coverage * 100f;
        zone.precisionPercent = precision * 100f;
        zone.overflowPenalty = overflowPenalty * 100f;
        
        // STRICT SHAPE SCORING:
        // 1. Must meet minimum coverage
        // 2. Must meet minimum precision
        // 3. Apply overflow penalty
        
        float baseShapeScore = 0f;
        
        if (coverage >= minimumCoverageRequired && precision >= minimumPrecisionRequired)
        {
            // Good attempt - calculate actual score
            // Coverage counts for 50%, precision counts for 50%
            baseShapeScore = (coverage * 0.5f + precision * 0.5f) * 100f;
            
            // Apply overflow penalty
            baseShapeScore = baseShapeScore * (1f - Mathf.Min(overflowPenalty, 0.8f));
        }
        else if (coverage >= minimumCoverageRequired * 0.5f || precision >= minimumPrecisionRequired * 0.5f)
        {
            // Partial attempt - give some credit but heavily reduced
            baseShapeScore = (coverage * 0.5f + precision * 0.5f) * 50f;
            baseShapeScore = baseShapeScore * (1f - Mathf.Min(overflowPenalty, 0.8f));
        }
        else
        {
            // Poor attempt - very low score
            baseShapeScore = (coverage * 0.5f + precision * 0.5f) * 20f;
        }
        
        zone.shapeScore = Mathf.Clamp(baseShapeScore, 0f, 100f);
        
        // --- LOCATION SCORE ---
        float distance = Vector2.Distance(playerCenter, zone.shapeCenter);
        
        if (distance <= locationFullScoreRadius)
        {
            zone.locationScore = 100f;
        }
        else
        {
            float maxDistance = Mathf.Sqrt(zone.zoneBounds.width * zone.zoneBounds.width + 
                                           zone.zoneBounds.height * zone.zoneBounds.height);
            
            float adjustedDistance = distance - locationFullScoreRadius;
            float adjustedMax = maxDistance - locationFullScoreRadius;
            
            zone.locationScore = Mathf.Max(0f, (1f - (adjustedDistance / adjustedMax)) * 100f);
        }
        
        // --- SIZE SCORE ---
        int playerPixelCount = playerPixelsInZone.Count;
        int expectedPixelCount = zone.shapePixelCount;
        
        float sizeRatio = (float)playerPixelCount / expectedPixelCount;
        
        if (sizeRatio >= sizeToleranceLower && sizeRatio <= sizeToleranceUpper)
        {
            zone.sizeScore = 100f;
        }
        else if (sizeRatio < sizeToleranceLower)
        {
            zone.sizeScore = (sizeRatio / sizeToleranceLower) * 100f;
        }
        else
        {
            float excessRatio = (sizeRatio - sizeToleranceUpper) / sizeToleranceUpper;
            zone.sizeScore = Mathf.Max(0f, (1f - excessRatio) * 100f);
        }
        
        zone.sizeScore = Mathf.Clamp(zone.sizeScore, 0f, 100f);
        
        // --- COMBINED ZONE SCORE ---
        zone.zoneScore = (zone.shapeScore * shapeWeight) + 
                         (zone.locationScore * locationWeight) + 
                         (zone.sizeScore * sizeWeight);
        
        Debug.Log($"{zone.zoneName}: Coverage={coverage*100:F1}% (min:{minimumCoverageRequired*100}%), " +
                  $"Precision={precision*100:F1}% (min:{minimumPrecisionRequired*100}%), " +
                  $"Overflow={overflowPenalty*100:F1}%, Shape={zone.shapeScore:F1}%, " +
                  $"Location={zone.locationScore:F1}%, Size={zone.sizeScore:F1}%, Zone={zone.zoneScore:F1}%");
    }
    
    bool ColorsMatch(Color c1, Color c2)
    {
        return ColorsMatchWithTolerance(c1, c2, 0.1f);
    }
    
    void RestartDemo()
    {
        // Reset to original base texture (keeps mask outline)
        paintTexture.SetPixels(originalBaseTexture.GetPixels());
        paintTexture.Apply();
        
        foreach (MaskZone zone in zones)
        {
            zone.shapeScore = 0f;
            zone.locationScore = 0f;
            zone.sizeScore = 0f;
            zone.zoneScore = 0f;
            zone.coveragePercent = 0f;
            zone.precisionPercent = 0f;
            zone.overflowPenalty = 0f;
        }
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    void OnDrawGizmosSelected()
    {
        if (zones == null || zones.Count == 0) return;
        
        foreach (MaskZone zone in zones)
        {
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(
                zone.zoneBounds.x + zone.zoneBounds.width / 2,
                zone.zoneBounds.y + zone.zoneBounds.height / 2,
                0
            );
            Vector3 size = new Vector3(zone.zoneBounds.width, zone.zoneBounds.height, 0);
            Gizmos.DrawWireCube(center, size);
            
            Gizmos.color = zone.shapeColor;
            Gizmos.DrawSphere(new Vector3(zone.shapeCenter.x, zone.shapeCenter.y, 0), 5f);
        }
    }
}