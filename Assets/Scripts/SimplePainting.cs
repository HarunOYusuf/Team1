using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SimplePainting : MonoBehaviour
{
    [Header("Painting Setup")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color paintColor = Color.red;
    [SerializeField] private int brushSize = 10;
    
    [Header("Target & Comparison")]
    [SerializeField] private Texture2D targetTexture;
    [SerializeField] private float passingScore = 70f;
    [SerializeField] private int toleranceRadius = 15; // How forgiving the detection is
    
    [Header("UI References")]
    [SerializeField] private GameObject targetDisplay;
    [SerializeField] private Image targetImage;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI resultText;
    
    [Header("Memory Phase")]
    [SerializeField] private float viewDuration = 5f;
    
    [Header("Debug")]
    [SerializeField] private bool showTargetOverlay = true;
    [SerializeField] private Color overlayColor = new Color(1f, 1f, 0f, 0.5f);
    
    private Texture2D paintTexture;
    private Texture2D expandedTargetTexture; // Target with tolerance applied
    private Camera mainCamera;
    private bool canPaint = false;
    private float timer;
    private bool memoryPhaseActive = false;
    
    void Start()
    {
        mainCamera = Camera.main;
        
        Texture2D whiteTexture = spriteRenderer.sprite.texture;
        paintTexture = new Texture2D(whiteTexture.width, whiteTexture.height);
        paintTexture.SetPixels(whiteTexture.GetPixels());
        paintTexture.Apply();
        
        Sprite newSprite = Sprite.Create(
            paintTexture,
            new Rect(0, 0, paintTexture.width, paintTexture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        spriteRenderer.sprite = newSprite;
        
        // Create expanded target texture with tolerance
        GenerateExpandedTarget();
        
        submitButton.onClick.AddListener(OnSubmit);
        submitButton.gameObject.SetActive(false);
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
    
    void GenerateExpandedTarget()
    {
        expandedTargetTexture = new Texture2D(targetTexture.width, targetTexture.height);
        Color backgroundColor = Color.white;
        
        // First, find all target pixels
        bool[,] isTargetPixel = new bool[targetTexture.width, targetTexture.height];
        
        for (int x = 0; x < targetTexture.width; x++)
        {
            for (int y = 0; y < targetTexture.height; y++)
            {
                Color targetColor = targetTexture.GetPixel(x, y);
                isTargetPixel[x, y] = targetColor.a >= 0.1f && !ColorsMatch(targetColor, backgroundColor);
            }
        }
        
        // Now expand: for each pixel, check if any target pixel is within tolerance radius
        for (int x = 0; x < targetTexture.width; x++)
        {
            for (int y = 0; y < targetTexture.height; y++)
            {
                bool withinTolerance = false;
                
                // Check surrounding pixels within tolerance radius
                for (int dx = -toleranceRadius; dx <= toleranceRadius && !withinTolerance; dx++)
                {
                    for (int dy = -toleranceRadius; dy <= toleranceRadius && !withinTolerance; dy++)
                    {
                        int checkX = x + dx;
                        int checkY = y + dy;
                        
                        if (checkX >= 0 && checkX < targetTexture.width && 
                            checkY >= 0 && checkY < targetTexture.height)
                        {
                            float distance = Mathf.Sqrt(dx * dx + dy * dy);
                            
                            if (distance <= toleranceRadius && isTargetPixel[checkX, checkY])
                            {
                                withinTolerance = true;
                            }
                        }
                    }
                }
                
                if (withinTolerance)
                {
                    expandedTargetTexture.SetPixel(x, y, Color.red);
                }
                else
                {
                    expandedTargetTexture.SetPixel(x, y, backgroundColor);
                }
            }
        }
        
        expandedTargetTexture.Apply();
        Debug.Log($"Expanded target generated with tolerance radius: {toleranceRadius}");
    }
    
    void Update()
    {
        if (memoryPhaseActive)
        {
            timer -= Time.deltaTime;
            instructionText.text = $"Memorize this circle! {Mathf.Ceil(timer)}s";
            
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
            showTargetOverlay = !showTargetOverlay;
            if (canPaint)
            {
                if (showTargetOverlay)
                {
                    ApplyTargetOverlay();
                }
                else
                {
                    RemoveTargetOverlay();
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
            targetTexture,
            new Rect(0, 0, targetTexture.width, targetTexture.height),
            new Vector2(0.5f, 0.5f)
        );
        targetImage.sprite = targetSprite;
        targetDisplay.SetActive(true);
        
        timer = viewDuration;
        memoryPhaseActive = true;
        canPaint = false;
        
        instructionText.text = "Memorize this circle!";
        Debug.Log("Memory phase started! Memorize the target.");
    }
    
    void EndMemoryPhase()
    {
        targetDisplay.SetActive(false);
        memoryPhaseActive = false;
        
        canPaint = true;
        instructionText.text = "Paint the circle from memory! (Press 1 for Red, D for Debug Overlay)";
        
        submitButton.gameObject.SetActive(true);
        
        if (showTargetOverlay)
        {
            ApplyTargetOverlay();
        }
        
        Debug.Log("Memory phase ended! Now paint from memory.");
    }
    
    void ApplyTargetOverlay()
    {
        Color backgroundColor = Color.white;
        
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color expandedColor = expandedTargetTexture.GetPixel(x, y);
                Color currentColor = paintTexture.GetPixel(x, y);
                
                bool isInExpandedTarget = !ColorsMatch(expandedColor, backgroundColor);
                
                if (isInExpandedTarget && ColorsMatch(currentColor, backgroundColor))
                {
                    paintTexture.SetPixel(x, y, overlayColor);
                }
            }
        }
        
        paintTexture.Apply();
        Debug.Log("Debug overlay ON - Yellow shows expanded target area with tolerance");
    }
    
    void RemoveTargetOverlay()
    {
        for (int x = 0; x < paintTexture.width; x++)
        {
            for (int y = 0; y < paintTexture.height; y++)
            {
                Color currentColor = paintTexture.GetPixel(x, y);
                
                if (ColorsMatch(currentColor, overlayColor))
                {
                    paintTexture.SetPixel(x, y, Color.white);
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
        
        if (showTargetOverlay)
        {
            RemoveTargetOverlay();
        }
        
        float score = CompareMasks(paintTexture, expandedTargetTexture);
        
        bool passed = score >= passingScore;
        
        if (passed)
        {
            resultText.text = $"PASS! Score: {score:F1}%";
            resultText.color = Color.green;
        }
        else
        {
            resultText.text = $"FAIL! Score: {score:F1}% (Need {passingScore}%)";
            resultText.color = Color.red;
        }
        
        resultText.gameObject.SetActive(true);
        instructionText.text = "Press Space to try again";
        
        Debug.Log($"Final Score: {score:F1}% - {(passed ? "PASSED" : "FAILED")}");
    }
    
    float CompareMasks(Texture2D playerMask, Texture2D targetMask)
    {
        if (playerMask.width != targetMask.width || playerMask.height != targetMask.height)
        {
            Debug.LogError("Textures must be same size!");
            return 0f;
        }

        int targetPixelCount = 0;
        int coveredPixels = 0;
        int playerPaintedCount = 0;

        Color backgroundColor = Color.white;

        for (int x = 0; x < playerMask.width; x++)
        {
            for (int y = 0; y < playerMask.height; y++)
            {
                Color playerColor = playerMask.GetPixel(x, y);
                Color targetColor = targetMask.GetPixel(x, y);

                bool targetHasPaint = targetColor.a >= 0.1f && !ColorsMatch(targetColor, backgroundColor);
                bool playerHasPaint = playerColor.a >= 0.1f && !ColorsMatch(playerColor, backgroundColor);

                if (targetHasPaint)
                {
                    targetPixelCount++;
                    
                    if (playerHasPaint)
                    {
                        coveredPixels++;
                    }
                }

                if (playerHasPaint)
                {
                    playerPaintedCount++;
                }
            }
        }

        if (targetPixelCount == 0) return 0f;
        if (playerPaintedCount == 0) return 0f;

        float coverage = (float)coveredPixels / targetPixelCount;
        float precision = (float)coveredPixels / playerPaintedCount;

        float finalScore = (coverage * precision) * 100f;

        Debug.Log($"Coverage: {coverage * 100:F1}% | Precision: {precision * 100:F1}% | Final: {finalScore:F1}%");

        return finalScore;
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
            resetColors[i] = Color.white;
        }
        paintTexture.SetPixels(resetColors);
        paintTexture.Apply();
        
        resultText.gameObject.SetActive(false);
        
        StartMemoryPhase();
    }
}