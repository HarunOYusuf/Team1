using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

[System.Serializable]
public class MaskZone
{
    public string zoneName;
    public Rect zoneBounds;                    // The box area on the canvas (in pixels)
    public Vector2 shapeCenter;                // Auto-calculated: where the shape is in this zone
    public Texture2D extractedShape;           // Auto-extracted: the shape from the full mask
    public int shapePixelCount;                // Auto-calculated: how many pixels in this shape
    
    [Header("Runtime Scores")]
    public float shapeScore;
    public float locationScore;
    public float zoneScore;
}

public class SimplePainting : MonoBehaviour
{
    [Header("Painting Setup")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 10;
    
    [Header("Target & Comparison")]
    [SerializeField] private Texture2D fullMaskTexture;    // The complete mask design
    [SerializeField] private float passingScore = 50f;
    
    [Header("Tolerance Settings")]
    [SerializeField] private int shapeToleranceRadius = 10;     // Forgiveness for shape matching
    [SerializeField] private float locationFullScoreRadius = 30f; // Distance for 100% location score
    
    [Header("Scoring Weights")]
    [SerializeField] [Range(0f, 1f)] private float shapeWeight = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float locationWeight = 0.3f;
    
    [Header("Auto Zone Detection")]
    [SerializeField] private int zonePadding = 20;              // Extra padding around detected shapes
    [SerializeField] private int minShapePixels = 50;           // Minimum pixels to count as a shape
    
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
    
    // Auto-detected zones
    [Header("Detected Zones (Auto-populated)")]
    [SerializeField] private List<MaskZone> zones = new List<MaskZone>();
    
    private Texture2D paintTexture;
    private Texture2D expandedMaskTexture;     // For precision checking
    private Camera mainCamera;
    private bool canPaint = false;
    private float timer;
    private bool memoryPhaseActive = false;
    private Color backgroundColor = Color.white;
    
    void Start()
    {
        mainCamera = Camera.main;
        
        // Create paintable texture
        Texture2D whiteTexture = spriteRenderer.sprite.texture;
        paintTexture = new Texture2D(whiteTexture.width, whiteTexture.height);
        paintTexture.SetPixels(whiteTexture.GetPixels());
        paintTexture.Apply();
        
        // Set up sprite
        Sprite newSprite = Sprite.Create(
            paintTexture,
            new Rect(0, 0, paintTexture.width, paintTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteRenderer.sprite = newSprite;
        
        // Auto-detect zones from the full mask
        AutoDetectZones();
        
        // Generate expanded mask for precision
        GenerateExpandedMask();
        
        // Set up UI
        submitButton.onClick.AddListener(OnSubmit);
        submitButton.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
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
        
        // Track which pixels we've already assigned to a zone
        bool[,] visited = new bool[width, height];
        
        // Find all connected shapes using flood fill
        int zoneCount = 0;
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visited[x, y]) continue;
                
                Color pixel = fullMaskTexture.GetPixel(x, y);
                
                if (IsShapePixel(pixel))
                {
                    // Found a new shape, flood fill to find all connected pixels
                    List<Vector2Int> shapePixels = FloodFill(x, y, visited);
                    
                    if (shapePixels.Count >= minShapePixels)
                    {
                        // Create a zone for this shape
                        MaskZone zone = CreateZoneFromPixels(shapePixels, zoneCount);
                        zones.Add(zone);
                        zoneCount++;
                        
                        Debug.Log($"Detected zone '{zone.zoneName}' with {shapePixels.Count} pixels at {zone.zoneBounds}");
                    }
                }
                else
                {
                    visited[x, y] = true;
                }
            }
        }
        
        Debug.Log($"Auto-detected {zones.Count} zones from mask");
    }
    
    List<Vector2Int> FloodFill(int startX, int startY, bool[,] visited)
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
            
            if (!IsShapePixel(pixel))
            {
                visited[x, y] = true;
                continue;
            }
            
            visited[x, y] = true;
            pixels.Add(current);
            
            // Check 8 neighbors (including diagonals for better connectivity)
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
    
    MaskZone CreateZoneFromPixels(List<Vector2Int> pixels, int zoneIndex)
    {
        // Find bounding box
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
        
        // Calculate center of the shape
        Vector2 shapeCenter = new Vector2(sumX / pixels.Count, sumY / pixels.Count);
        
        // Create zone bounds with padding
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
        
        // Extract the shape texture for this zone
        int shapeWidth = maxX - minX + 1;
        int shapeHeight = maxY - minY + 1;
        Texture2D extractedShape = new Texture2D(shapeWidth, shapeHeight);
        
        // Fill with white first
        Color[] clearColors = new Color[shapeWidth * shapeHeight];
        for (int i = 0; i < clearColors.Length; i++)
            clearColors[i] = Color.white;
        extractedShape.SetPixels(clearColors);
        
        // Copy shape pixels
        foreach (Vector2Int p in pixels)
        {
            int localX = p.x - minX;
            int localY = p.y - minY;
            extractedShape.SetPixel(localX, localY, fullMaskTexture.GetPixel(p.x, p.y));
        }
        extractedShape.Apply();
        
        MaskZone zone = new MaskZone
        {
            zoneName = $"Shape {zoneIndex + 1}",
            zoneBounds = zoneBounds,
            shapeCenter = shapeCenter,
            extractedShape = extractedShape,
            shapePixelCount = pixels.Count
        };
        
        return zone;
    }
    
    void GenerateExpandedMask()
    {
        expandedMaskTexture = new Texture2D(fullMaskTexture.width, fullMaskTexture.height);
        
        bool[,] isShapePixel = new bool[fullMaskTexture.width, fullMaskTexture.height];
        
        for (int x = 0; x < fullMaskTexture.width; x++)
        {
            for (int y = 0; y < fullMaskTexture.height; y++)
            {
                Color c = fullMaskTexture.GetPixel(x, y);
                isShapePixel[x, y] = IsShapePixel(c);
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
                            
                            if (distance <= shapeToleranceRadius && isShapePixel[checkX, checkY])
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
        // Not white and not transparent
        return c.a >= 0.1f && !ColorsMatch(c, backgroundColor);
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
        
        if (Input.GetKeyDown(KeyCode.Alpha1)) paintColor = Color.red;
        
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
        instructionText.text = "Paint the design from memory! (D = Debug Overlay)";
        
        submitButton.gameObject.SetActive(true);
        
        if (showZoneOverlay)
        {
            ApplyDebugOverlay();
        }
        
        Debug.Log("Memory phase ended! Now paint from memory.");
    }
    
    void ApplyDebugOverlay()
    {
        // Show zone boundaries and shape areas
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                if (!ColorsMatch(currentColor, backgroundColor))
                    continue; // Don't overwrite player's paint
                
                // Check if in expanded shape area (yellow)
                Color expandedColor = expandedMaskTexture.GetPixel(x, y);
                if (!ColorsMatch(expandedColor, backgroundColor))
                {
                    paintTexture.SetPixel(x, y, shapeOverlayColor);
                    continue;
                }
                
                // Check if in any zone bounds (green)
                foreach (MaskZone zone in zones)
                {
                    if (zone.zoneBounds.Contains(new Vector2(x, y)))
                    {
                        paintTexture.SetPixel(x, y, zoneOverlayColor);
                        break;
                    }
                }
            }
        }
        
        paintTexture.Apply();
        Debug.Log("Debug overlay ON - Green = zone bounds, Yellow = shape tolerance area");
    }
    
    void RemoveDebugOverlay()
    {
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                if (ColorsMatch(currentColor, zoneOverlayColor) || ColorsMatch(currentColor, shapeOverlayColor))
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
        
        // Calculate scores for each zone
        float totalScore = 0f;
        string detailedResults = "";
        
        foreach (MaskZone zone in zones)
        {
            CalculateZoneScore(zone);
            totalScore += zone.zoneScore;
            
            if (showDetailedScores)
            {
                detailedResults += $"\n{zone.zoneName}: Shape {zone.shapeScore:F0}% | Loc {zone.locationScore:F0}% = {zone.zoneScore:F0}%";
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
        // Find player's drawing within this zone
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
                
                if (IsShapePixel(playerColor))
                {
                    playerPixelsInZone.Add(new Vector2Int(x, y));
                    sumX += x;
                    sumY += y;
                }
            }
        }
        
        // If player didn't draw anything in this zone
        if (playerPixelsInZone.Count == 0)
        {
            zone.shapeScore = 0f;
            zone.locationScore = 0f;
            zone.zoneScore = 0f;
            return;
        }
        
        // Calculate player's drawing center
        Vector2 playerCenter = new Vector2(sumX / playerPixelsInZone.Count, sumY / playerPixelsInZone.Count);
        
        // --- SHAPE SCORE ---
        // Compare player's drawing to the expected shape using expanded tolerance
        int coveredPixels = 0;
        int playerInExpandedZone = 0;
        
        // Count original shape pixels in this zone
        int originalShapePixels = 0;
        for (int x = zoneMinX; x <= zoneMaxX; x++)
        {
            for (int y = zoneMinY; y <= zoneMaxY; y++)
            {
                if (x < 0 || x >= fullMaskTexture.width || y < 0 || y >= fullMaskTexture.height)
                    continue;
                
                Color originalColor = fullMaskTexture.GetPixel(x, y);
                if (IsShapePixel(originalColor))
                {
                    originalShapePixels++;
                    
                    // Check if player painted here
                    Color playerColor = paintTexture.GetPixel(x, y);
                    if (IsShapePixel(playerColor))
                    {
                        coveredPixels++;
                    }
                }
            }
        }
        
        // Check precision (player paint in expanded zone)
        foreach (Vector2Int p in playerPixelsInZone)
        {
            Color expandedColor = expandedMaskTexture.GetPixel(p.x, p.y);
            if (!ColorsMatch(expandedColor, backgroundColor))
            {
                playerInExpandedZone++;
            }
        }
        
        float coverage = originalShapePixels > 0 ? (float)coveredPixels / originalShapePixels : 0f;
        float precision = playerPixelsInZone.Count > 0 ? (float)playerInExpandedZone / playerPixelsInZone.Count : 0f;
        
        // Shape score combines coverage and precision
        zone.shapeScore = (coverage * 0.6f + precision * 0.4f) * 100f;
        
        // --- LOCATION SCORE ---
        // Compare center of player's drawing to center of expected shape
        float distance = Vector2.Distance(playerCenter, zone.shapeCenter);
        
        if (distance <= locationFullScoreRadius)
        {
            zone.locationScore = 100f;
        }
        else
        {
            // Calculate max possible distance (diagonal of zone)
            float maxDistance = Mathf.Sqrt(zone.zoneBounds.width * zone.zoneBounds.width + 
                                           zone.zoneBounds.height * zone.zoneBounds.height);
            
            // Score decreases linearly after the full score radius
            float adjustedDistance = distance - locationFullScoreRadius;
            float adjustedMax = maxDistance - locationFullScoreRadius;
            
            zone.locationScore = Mathf.Max(0f, (1f - (adjustedDistance / adjustedMax)) * 100f);
        }
        
        // --- COMBINED ZONE SCORE ---
        zone.zoneScore = (zone.shapeScore * shapeWeight) + (zone.locationScore * locationWeight);
        
        Debug.Log($"{zone.zoneName}: Coverage={coverage*100:F1}%, Precision={precision*100:F1}%, " +
                  $"Shape={zone.shapeScore:F1}%, Location={zone.locationScore:F1}%, Zone={zone.zoneScore:F1}%");
    }
    
    bool ColorsMatch(Color c1, Color c2)
    {
        float rDiff = Mathf.Abs(c1.r - c2.r);
        float gDiff = Mathf.Abs(c1.g - c2.g);
        float bDiff = Mathf.Abs(c1.b - c2.b);
        
        return (rDiff < 0.1f && gDiff < 0.1f && bDiff < 0.1f);
    }
    
    void RestartDemo()
    {
        Color[] resetColors = new Color[paintTexture.width * paintTexture.height];
        for (int i = 0; i < resetColors.Length; i++)
        {
            resetColors[i] = backgroundColor;
        }
        paintTexture.SetPixels(resetColors);
        paintTexture.Apply();
        
        // Reset zone scores
        foreach (MaskZone zone in zones)
        {
            zone.shapeScore = 0f;
            zone.locationScore = 0f;
            zone.zoneScore = 0f;
        }
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    // Editor helper to visualize zones in Scene view
    void OnDrawGizmosSelected()
    {
        if (zones == null || zones.Count == 0) return;
        
        foreach (MaskZone zone in zones)
        {
            // Draw zone bounds
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(
                zone.zoneBounds.x + zone.zoneBounds.width / 2,
                zone.zoneBounds.y + zone.zoneBounds.height / 2,
                0
            );
            Vector3 size = new Vector3(zone.zoneBounds.width, zone.zoneBounds.height, 0);
            Gizmos.DrawWireCube(center, size);
            
            // Draw shape center
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(new Vector3(zone.shapeCenter.x, zone.shapeCenter.y, 0), 5f);
        }
    }
}