using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Shape comparison using Hu Moments - a mathematical fingerprint for shapes
/// that is invariant to position, scale, and rotation.
/// </summary>
public class HuMomentsTest : MonoBehaviour
{
    [Header("Painting Setup")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 10;
    [SerializeField] private bool autoBrushSize = true;  // Automatically match target line thickness
    
    [Header("Debug - Auto Brush Detection")]
    [SerializeField] private float detectedLineThickness;
    [SerializeField] private int autoBrushSizeResult;
    
    [Header("Target Shape")]
    [SerializeField] private Texture2D targetShapeTexture;  // The shape player needs to draw
    
    [Header("Detectable Colors")]
    [SerializeField] private Color detectColor = Color.red;
    [SerializeField] private float colorTolerance = 0.3f;
    
    [Header("Scoring")]
    [SerializeField] private float passingScore = 60f;
    [SerializeField] [Range(0f, 1f)] private float shapeWeight = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float locationWeight = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float sizeWeight = 0.15f;
    [SerializeField] private bool useExpandedZoneScoring = true;  // Use simple zone scoring instead of Hu Moments
    
    [Header("UI References")]
    [SerializeField] private GameObject targetDisplay;
    [SerializeField] private Image targetImage;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("Memory Phase")]
    [SerializeField] private float viewDuration = 5f;
    
    [Header("Debug - Target Hu Moments")]
    [SerializeField] private double[] targetHuMoments = new double[7];
    [SerializeField] private int targetPixelCount;
    [SerializeField] private Vector2 targetCenter;
    
    [Header("Debug - Player Hu Moments")]
    [SerializeField] private double[] playerHuMoments = new double[7];
    [SerializeField] private int playerPixelCount;
    [SerializeField] private Vector2 playerCenter;
    
    [Header("Debug - Comparison Results")]
    [SerializeField] private float shapeSimilarity;
    [SerializeField] private float locationScore;
    [SerializeField] private float sizeScore;
    [SerializeField] private float finalScore;
    
    [Header("Debug - Perfect Score Reference")]
    [SerializeField] private float perfectShapeSimilarity;
    [SerializeField] private float perfectFinalScore;
    [SerializeField] private bool showPerfectScoreOnStart = true;
    
    [Header("Debug - Target Overlay")]
    [SerializeField] private bool showTargetOverlay = false;  // Press T to toggle
    [SerializeField] private Color targetOverlayColor = new Color(0f, 1f, 0f, 0.5f);  // Green semi-transparent
    [SerializeField] private int overlayThickness = 10;  // How much to expand the target zone
    private bool overlayApplied = false;
    private HashSet<Vector2Int> expandedTargetPixels;  // Store expanded zone for scoring
    
    private Texture2D paintTexture;
    private Texture2D originalBaseTexture;
    private Camera mainCamera;
    private bool canPaint = false;
    private float timer;
    private bool memoryPhaseActive = false;
    private Color backgroundColor = Color.white;
    
    // Target shape data
    private List<Vector2Int> targetPixels;
    private Rect targetBounds;
    
    void Start()
    {
        mainCamera = Camera.main;
        
        // Store original texture
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
        
        // Extract target shape and calculate its Hu Moments
        ExtractTargetShape();
        
        // Setup UI
        submitButton.onClick.AddListener(OnSubmit);
        submitButton.gameObject.SetActive(false);
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    #region Target Shape Extraction
    
    void ExtractTargetShape()
    {
        if (targetShapeTexture == null)
        {
            Debug.LogError("No target shape texture assigned!");
            return;
        }
        
        targetPixels = new List<Vector2Int>();
        
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        float sumX = 0, sumY = 0;
        
        // Find all pixels of the target color
        for (int x = 0; x < targetShapeTexture.width; x++)
        {
            for (int y = 0; y < targetShapeTexture.height; y++)
            {
                Color pixel = targetShapeTexture.GetPixel(x, y);
                
                if (IsTargetColor(pixel))
                {
                    targetPixels.Add(new Vector2Int(x, y));
                    
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                    
                    sumX += x;
                    sumY += y;
                }
            }
        }
        
        if (targetPixels.Count == 0)
        {
            Debug.LogError("No target color pixels found in target texture!");
            return;
        }
        
        targetPixelCount = targetPixels.Count;
        targetCenter = new Vector2(sumX / targetPixels.Count, sumY / targetPixels.Count);
        targetBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        
        // Calculate Hu Moments for target
        targetHuMoments = CalculateHuMoments(targetPixels);
        
        Debug.Log($"Target shape extracted: {targetPixelCount} pixels");
        Debug.Log($"Target Hu Moments: [{string.Join(", ", System.Array.ConvertAll(targetHuMoments, m => m.ToString("E3")))}]");
        
        // Auto detect brush size from target line thickness
        if (autoBrushSize)
        {
            DetectAndSetBrushSize(targetPixels);
        }
        
        // Generate expanded target zone for scoring
        GenerateExpandedTargetZone();
        
        // Calculate perfect score (target compared to itself)
        if (showPerfectScoreOnStart)
        {
            CalculatePerfectScore();
        }
    }
    
    void GenerateExpandedTargetZone()
    {
        expandedTargetPixels = new HashSet<Vector2Int>();
        
        foreach (Vector2Int pixel in targetPixels)
        {
            for (int dx = -overlayThickness; dx <= overlayThickness; dx++)
            {
                for (int dy = -overlayThickness; dy <= overlayThickness; dy++)
                {
                    if (dx * dx + dy * dy <= overlayThickness * overlayThickness)
                    {
                        Vector2Int expandedPixel = new Vector2Int(pixel.x + dx, pixel.y + dy);
                        
                        if (expandedPixel.x >= 0 && expandedPixel.x < targetShapeTexture.width &&
                            expandedPixel.y >= 0 && expandedPixel.y < targetShapeTexture.height)
                        {
                            expandedTargetPixels.Add(expandedPixel);
                        }
                    }
                }
            }
        }
        
        Debug.Log($"Expanded target zone created: {expandedTargetPixels.Count} pixels (original: {targetPixels.Count}, expansion: {overlayThickness}px)");
    }
    
    void DetectAndSetBrushSize(List<Vector2Int> shapePixels)
    {
        if (shapePixels.Count == 0) return;
        
        // Create a hash set for fast lookup
        HashSet<Vector2Int> pixelSet = new HashSet<Vector2Int>(shapePixels);
        
        // For each shape pixel, find distance to nearest non-shape pixel
        float totalDistance = 0;
        int sampledPixels = 0;
        
        // Sample pixels (don't need to check every pixel for performance)
        int sampleStep = Mathf.Max(1, shapePixels.Count / 500);  // Sample ~500 pixels max
        
        for (int i = 0; i < shapePixels.Count; i += sampleStep)
        {
            Vector2Int pixel = shapePixels[i];
            float minDistToEdge = FindDistanceToEdge(pixel, pixelSet);
            
            if (minDistToEdge > 0)
            {
                totalDistance += minDistToEdge;
                sampledPixels++;
            }
        }
        
        if (sampledPixels > 0)
        {
            // Average distance to edge = half the line thickness
            float avgDistToEdge = totalDistance / sampledPixels;
            detectedLineThickness = avgDistToEdge * 2f;  // Full thickness
            
            // Set brush size to match (round up to be safe)
            autoBrushSizeResult = Mathf.CeilToInt(avgDistToEdge);
            brushSize = Mathf.Max(2, autoBrushSizeResult);  // Minimum brush size of 2
            
            Debug.Log($"=== AUTO BRUSH SIZE ===");
            Debug.Log($"Detected line thickness: {detectedLineThickness:F1} pixels");
            Debug.Log($"Brush size set to: {brushSize}");
            Debug.Log($"=======================");
        }
    }
    
    float FindDistanceToEdge(Vector2Int pixel, HashSet<Vector2Int> pixelSet)
    {
        // Search outward until we find a non-shape pixel
        int maxSearchRadius = 50;
        
        for (int radius = 1; radius <= maxSearchRadius; radius++)
        {
            // Check pixels at this radius (simplified - check in 8 directions)
            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(radius, 0),
                new Vector2Int(-radius, 0),
                new Vector2Int(0, radius),
                new Vector2Int(0, -radius),
                new Vector2Int(radius, radius),
                new Vector2Int(-radius, radius),
                new Vector2Int(radius, -radius),
                new Vector2Int(-radius, -radius)
            };
            
            foreach (Vector2Int dir in directions)
            {
                Vector2Int checkPixel = pixel + dir;
                
                // If this pixel is NOT in the shape, we found the edge
                if (!pixelSet.Contains(checkPixel))
                {
                    return radius;
                }
            }
        }
        
        // If we didn't find an edge, this pixel is deep inside a filled shape
        return maxSearchRadius;
    }
    
    void CalculatePerfectScore()
    {
        // Compare target to itself - this is the theoretical maximum
        perfectShapeSimilarity = CompareHuMoments(targetHuMoments, targetHuMoments);
        
        // Perfect location and size would be 1.0
        perfectFinalScore = (perfectShapeSimilarity * shapeWeight + 1f * locationWeight + 1f * sizeWeight) * 100f;
        
        Debug.Log($"=== PERFECT SCORE REFERENCE ===");
        Debug.Log($"Perfect Shape Similarity: {perfectShapeSimilarity * 100:F1}%");
        Debug.Log($"Perfect Final Score: {perfectFinalScore:F1}%");
        Debug.Log($"================================");
    }
    
    bool IsTargetColor(Color c)
    {
        if (c.a < 0.1f) return false;
        
        float rDiff = Mathf.Abs(c.r - detectColor.r);
        float gDiff = Mathf.Abs(c.g - detectColor.g);
        float bDiff = Mathf.Abs(c.b - detectColor.b);
        
        return (rDiff < colorTolerance && gDiff < colorTolerance && bDiff < colorTolerance);
    }
    
    #endregion
    
    #region Hu Moments Calculation
    
    /// <summary>
    /// Calculate the 7 Hu Moments for a set of pixels.
    /// These moments are invariant to translation, scale, and rotation.
    /// </summary>
    double[] CalculateHuMoments(List<Vector2Int> pixels)
    {
        if (pixels.Count == 0)
            return new double[7];
        
        // Step 1: Calculate raw moments
        double m00 = 0, m10 = 0, m01 = 0;
        double m20 = 0, m11 = 0, m02 = 0;
        double m30 = 0, m21 = 0, m12 = 0, m03 = 0;
        
        foreach (Vector2Int p in pixels)
        {
            double x = p.x;
            double y = p.y;
            
            m00 += 1;
            m10 += x;
            m01 += y;
            m20 += x * x;
            m11 += x * y;
            m02 += y * y;
            m30 += x * x * x;
            m21 += x * x * y;
            m12 += x * y * y;
            m03 += y * y * y;
        }
        
        // Step 2: Calculate centroid
        double cx = m10 / m00;
        double cy = m01 / m00;
        
        // Step 3: Calculate central moments (translation invariant)
        double mu00 = m00;
        double mu20 = m20 - cx * m10;
        double mu11 = m11 - cx * m01;
        double mu02 = m02 - cy * m01;
        double mu30 = m30 - 3 * cx * m20 + 2 * cx * cx * m10;
        double mu21 = m21 - 2 * cx * m11 - cy * m20 + 2 * cx * cx * m01;
        double mu12 = m12 - 2 * cy * m11 - cx * m02 + 2 * cy * cy * m10;
        double mu03 = m03 - 3 * cy * m02 + 2 * cy * cy * m01;
        
        // Step 4: Calculate normalized central moments (scale invariant)
        double norm20 = System.Math.Pow(mu00, 1 + (2 + 0) / 2.0);
        double norm02 = System.Math.Pow(mu00, 1 + (0 + 2) / 2.0);
        double norm11 = System.Math.Pow(mu00, 1 + (1 + 1) / 2.0);
        double norm30 = System.Math.Pow(mu00, 1 + (3 + 0) / 2.0);
        double norm21 = System.Math.Pow(mu00, 1 + (2 + 1) / 2.0);
        double norm12 = System.Math.Pow(mu00, 1 + (1 + 2) / 2.0);
        double norm03 = System.Math.Pow(mu00, 1 + (0 + 3) / 2.0);
        
        double nu20 = mu20 / norm20;
        double nu02 = mu02 / norm02;
        double nu11 = mu11 / norm11;
        double nu30 = mu30 / norm30;
        double nu21 = mu21 / norm21;
        double nu12 = mu12 / norm12;
        double nu03 = mu03 / norm03;
        
        // Step 5: Calculate Hu Moments (rotation invariant)
        double[] hu = new double[7];
        
        hu[0] = nu20 + nu02;
        
        hu[1] = System.Math.Pow(nu20 - nu02, 2) + 4 * System.Math.Pow(nu11, 2);
        
        hu[2] = System.Math.Pow(nu30 - 3 * nu12, 2) + System.Math.Pow(3 * nu21 - nu03, 2);
        
        hu[3] = System.Math.Pow(nu30 + nu12, 2) + System.Math.Pow(nu21 + nu03, 2);
        
        hu[4] = (nu30 - 3 * nu12) * (nu30 + nu12) * 
                (System.Math.Pow(nu30 + nu12, 2) - 3 * System.Math.Pow(nu21 + nu03, 2)) +
                (3 * nu21 - nu03) * (nu21 + nu03) * 
                (3 * System.Math.Pow(nu30 + nu12, 2) - System.Math.Pow(nu21 + nu03, 2));
        
        hu[5] = (nu20 - nu02) * (System.Math.Pow(nu30 + nu12, 2) - System.Math.Pow(nu21 + nu03, 2)) +
                4 * nu11 * (nu30 + nu12) * (nu21 + nu03);
        
        hu[6] = (3 * nu21 - nu03) * (nu30 + nu12) * 
                (System.Math.Pow(nu30 + nu12, 2) - 3 * System.Math.Pow(nu21 + nu03, 2)) -
                (nu30 - 3 * nu12) * (nu21 + nu03) * 
                (3 * System.Math.Pow(nu30 + nu12, 2) - System.Math.Pow(nu21 + nu03, 2));
        
        // Step 6: Apply log transform for better comparison (standard practice)
        for (int i = 0; i < 7; i++)
        {
            if (hu[i] != 0)
            {
                hu[i] = -System.Math.Sign(hu[i]) * System.Math.Log10(System.Math.Abs(hu[i]));
            }
        }
        
        return hu;
    }
    
    /// <summary>
    /// Compare two sets of Hu Moments and return a similarity score (0-1)
    /// </summary>
    float CompareHuMoments(double[] moments1, double[] moments2)
    {
        if (moments1 == null || moments2 == null)
            return 0f;
        
        // Calculate the distance between moment sets
        // Using method similar to OpenCV's matchShapes with CV_CONTOURS_MATCH_I2
        double totalDiff = 0;
        
        for (int i = 0; i < 7; i++)
        {
            double diff = System.Math.Abs(moments1[i] - moments2[i]);
            totalDiff += diff;
        }
        
        // Convert distance to similarity (0-1)
        // Smaller distance = higher similarity
        // Using exponential decay for smoother scoring
        float similarity = Mathf.Exp(-(float)totalDiff * 0.5f);
        
        Debug.Log($"Hu Moments comparison - Total difference: {totalDiff:F4}, Similarity: {similarity * 100:F1}%");
        
        return similarity;
    }
    
    #endregion
    
    #region Game Flow
    
    void Update()
    {
        if (memoryPhaseActive)
        {
            timer -= Time.deltaTime;
            instructionText.text = $"Memorize this shape! {Mathf.Ceil(timer)}s";
            
            if (timer <= 0)
            {
                EndMemoryPhase();
            }
        }
        
        if (canPaint && Input.GetMouseButton(0))
        {
            Paint();
        }
        
        // Toggle target overlay with T key
        if (Input.GetKeyDown(KeyCode.T) && canPaint)
        {
            showTargetOverlay = !showTargetOverlay;
            if (showTargetOverlay)
            {
                ApplyTargetOverlay();
            }
            else
            {
                RemoveTargetOverlay();
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
        // Show target
        Sprite targetSprite = Sprite.Create(
            targetShapeTexture,
            new Rect(0, 0, targetShapeTexture.width, targetShapeTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        targetImage.sprite = targetSprite;
        targetDisplay.SetActive(true);
        
        timer = viewDuration;
        memoryPhaseActive = true;
        canPaint = false;
        
        instructionText.text = "Memorize this shape!";
    }
    
    void EndMemoryPhase()
    {
        targetDisplay.SetActive(false);
        memoryPhaseActive = false;
        canPaint = true;
        
        instructionText.text = "Now draw the shape! (Press T for guide)";
        submitButton.gameObject.SetActive(true);
    }
    
    #endregion
    
    #region Target Overlay
    
    void ApplyTargetOverlay()
    {
        if (targetPixels == null || targetPixels.Count == 0) return;
        
        // Create expanded target zone
        expandedTargetPixels = new HashSet<Vector2Int>();
        HashSet<Vector2Int> originalPixels = new HashSet<Vector2Int>(targetPixels);
        
        // For each target pixel, add all pixels within overlayThickness radius
        foreach (Vector2Int pixel in targetPixels)
        {
            for (int dx = -overlayThickness; dx <= overlayThickness; dx++)
            {
                for (int dy = -overlayThickness; dy <= overlayThickness; dy++)
                {
                    // Check if within circular radius
                    if (dx * dx + dy * dy <= overlayThickness * overlayThickness)
                    {
                        Vector2Int expandedPixel = new Vector2Int(pixel.x + dx, pixel.y + dy);
                        
                        if (expandedPixel.x >= 0 && expandedPixel.x < paintTexture.width &&
                            expandedPixel.y >= 0 && expandedPixel.y < paintTexture.height)
                        {
                            expandedTargetPixels.Add(expandedPixel);
                        }
                    }
                }
            }
        }
        
        // Draw the expanded target zone on the canvas as a guide
        foreach (Vector2Int pixel in expandedTargetPixels)
        {
            Color currentColor = paintTexture.GetPixel(pixel.x, pixel.y);
            
            // Don't overwrite player's red paint
            if (!IsTargetColor(currentColor))
            {
                paintTexture.SetPixel(pixel.x, pixel.y, targetOverlayColor);
            }
        }
        
        paintTexture.Apply();
        overlayApplied = true;
        Debug.Log($"Target overlay ON - Expanded by {overlayThickness}px. Green shows success zone ({expandedTargetPixels.Count} pixels)");
    }
    
    void RemoveTargetOverlay()
    {
        if (!overlayApplied) return;
        
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                // Remove overlay color but keep player's paint
                if (ColorsMatch(currentColor, targetOverlayColor))
                {
                    paintTexture.SetPixel(x, y, backgroundColor);
                }
            }
        }
        
        paintTexture.Apply();
        overlayApplied = false;
        Debug.Log("Target overlay OFF");
    }
    
    bool ColorsMatch(Color c1, Color c2)
    {
        return Mathf.Abs(c1.r - c2.r) < 0.1f && 
               Mathf.Abs(c1.g - c2.g) < 0.1f && 
               Mathf.Abs(c1.b - c2.b) < 0.1f &&
               Mathf.Abs(c1.a - c2.a) < 0.1f;
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
    
    #region Scoring
    
    public void OnSubmit()
    {
        canPaint = false;
        submitButton.gameObject.SetActive(false);
        
        // Remove overlay before scoring so it doesn't interfere
        if (overlayApplied)
        {
            RemoveTargetOverlay();
            showTargetOverlay = false;
        }
        
        // Extract player's drawing
        List<Vector2Int> playerPixels = new List<Vector2Int>();
        float sumX = 0, sumY = 0;
        
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color pixel = paintTexture.GetPixel(x, y);
                
                if (IsTargetColor(pixel))
                {
                    playerPixels.Add(new Vector2Int(x, y));
                    sumX += x;
                    sumY += y;
                }
            }
        }
        
        // Check if player drew anything
        if (playerPixels.Count < 10)
        {
            ShowResult(0, "No drawing detected!");
            return;
        }
        
        playerPixelCount = playerPixels.Count;
        playerCenter = new Vector2(sumX / playerPixels.Count, sumY / playerPixels.Count);
        
        // Calculate Hu Moments for player's drawing
        playerHuMoments = CalculateHuMoments(playerPixels);
        
        Debug.Log($"Player shape: {playerPixelCount} pixels");
        Debug.Log($"Player Hu Moments: [{string.Join(", ", System.Array.ConvertAll(playerHuMoments, m => m.ToString("E3")))}]");
        
        // Choose scoring method
        if (useExpandedZoneScoring)
        {
            // EXPANDED ZONE SCORING - simpler and more intuitive
            shapeSimilarity = CalculateExpandedZoneScore(playerPixels);
        }
        else
        {
            // HU MOMENTS SCORING - mathematical shape comparison
            shapeSimilarity = CompareHuMoments(targetHuMoments, playerHuMoments);
        }
        
        // Calculate location score (how close are the centers)
        float maxDistance = Mathf.Sqrt(paintTexture.width * paintTexture.width + paintTexture.height * paintTexture.height);
        float centerDistance = Vector2.Distance(playerCenter, targetCenter);
        locationScore = 1f - (centerDistance / maxDistance);
        locationScore = Mathf.Clamp01(locationScore);
        
        // Calculate size score
        float sizeRatio = (float)playerPixelCount / targetPixelCount;
        if (sizeRatio > 1f) sizeRatio = 1f / sizeRatio; // Penalize both too big and too small
        sizeScore = sizeRatio;
        
        // Combined score
        finalScore = (shapeSimilarity * shapeWeight + locationScore * locationWeight + sizeScore * sizeWeight) * 100f;
        
        string scoringMethod = useExpandedZoneScoring ? "Zone" : "Hu";
        string details = $"Shape ({scoringMethod}): {shapeSimilarity * 100:F1}% | Location: {locationScore * 100:F1}% | Size: {sizeScore * 100:F1}%";
        ShowResult(finalScore, details);
    }
    
    float CalculateExpandedZoneScore(List<Vector2Int> playerPixels)
    {
        if (expandedTargetPixels == null || expandedTargetPixels.Count == 0)
        {
            Debug.LogError("Expanded target zone not generated!");
            return 0f;
        }
        
        int playerPixelsInZone = 0;
        int playerPixelsOutsideZone = 0;
        
        // Count how many player pixels are inside the expanded zone
        foreach (Vector2Int pixel in playerPixels)
        {
            if (expandedTargetPixels.Contains(pixel))
            {
                playerPixelsInZone++;
            }
            else
            {
                playerPixelsOutsideZone++;
            }
        }
        
        // Count how many expanded zone pixels the player covered
        int zoneCovered = 0;
        foreach (Vector2Int pixel in expandedTargetPixels)
        {
            if (pixel.x >= 0 && pixel.x < paintTexture.width &&
                pixel.y >= 0 && pixel.y < paintTexture.height)
            {
                Color playerColor = paintTexture.GetPixel(pixel.x, pixel.y);
                if (IsTargetColor(playerColor))
                {
                    zoneCovered++;
                }
            }
        }
        
        // Calculate coverage: how much of the zone did player fill?
        float coverage = (float)zoneCovered / expandedTargetPixels.Count;
        
        // Calculate precision: how much of player's paint is in the zone?
        float precision = playerPixels.Count > 0 ? (float)playerPixelsInZone / playerPixels.Count : 0f;
        
        // Combined score (average of coverage and precision)
        float score = (coverage + precision) / 2f;
        
        Debug.Log($"=== EXPANDED ZONE SCORING ===");
        Debug.Log($"Zone size: {expandedTargetPixels.Count} pixels");
        Debug.Log($"Player pixels in zone: {playerPixelsInZone} / {playerPixels.Count}");
        Debug.Log($"Zone pixels covered: {zoneCovered} / {expandedTargetPixels.Count}");
        Debug.Log($"Coverage: {coverage * 100:F1}% | Precision: {precision * 100:F1}%");
        Debug.Log($"Final shape score: {score * 100:F1}%");
        Debug.Log($"=============================");
        
        return score;
    }
    
    void ShowResult(float score, string details)
    {
        finalScore = score;
        bool passed = score >= passingScore;
        
        // Calculate percentage of perfect score achieved
        float percentOfPerfect = perfectFinalScore > 0 ? (score / perfectFinalScore) * 100f : 0f;
        
        if (passed)
        {
            resultText.text = $"PASS! Score: {score:F1}%\n{details}\n(Perfect would be: {perfectFinalScore:F1}%)";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = $"FAIL! Score: {score:F1}% (Need {passingScore}%)\n{details}\n(Perfect would be: {perfectFinalScore:F1}%)";
            resultText.color = Color.red;
        }
        
        resultText.gameObject.SetActive(true);
        instructionText.text = "Press Space to try again";
        
        Debug.Log($"=== SCORE COMPARISON ===");
        Debug.Log($"Your Score: {score:F1}%");
        Debug.Log($"Perfect Score: {perfectFinalScore:F1}%");
        Debug.Log($"You achieved: {percentOfPerfect:F1}% of perfect");
        Debug.Log($"Result: {(passed ? "PASSED" : "FAILED")}");
        Debug.Log($"========================");
    }
    
    #endregion
    
    #region Reset
    
    void Restart()
    {
        // Reset canvas
        paintTexture.SetPixels(originalBaseTexture.GetPixels());
        paintTexture.Apply();
        
        // Reset debug values
        playerHuMoments = new double[7];
        playerPixelCount = 0;
        playerCenter = Vector2.zero;
        shapeSimilarity = 0;
        locationScore = 0;
        sizeScore = 0;
        finalScore = 0;
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    #endregion
}