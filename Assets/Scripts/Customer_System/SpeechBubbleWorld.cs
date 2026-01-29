using UnityEngine;
using TMPro;

public class SpeechBubbleWorld : MonoBehaviour
{
    public TextMeshPro text;
    public SpriteRenderer bubbleBG;

    [Header("Layout")]
    public Vector2 padding = new Vector2(0.8f, 0.5f);   // total padding added to bubble
    public float maxBubbleWidth = 4.2f;                 // bubble max width (world units)
    public Vector3 localOffset = new Vector3(0f, 1.5f, 0f);

    void LateUpdate()
    {
        transform.localPosition = localOffset;
    }

    public void Show(string msg)
    {
        text.text = msg;

        // Important: give TMP some internal margin so text doesn't touch borders
        text.margin = new Vector4(0.4f, 0.4f, 0.4f, 0.4f);


        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        ResizeBubble();

        text.enabled = true;
        bubbleBG.enabled = true;
    }

    public void Hide()
    {
        text.enabled = false;
        bubbleBG.enabled = false;
    }

    void ResizeBubble()
    {
        float innerMaxWidth = maxBubbleWidth - padding.x;

        // Set width constraint for wrapping
        var rt = text.rectTransform;
        rt.sizeDelta = new Vector2(innerMaxWidth, 1000f);

        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;

        // Update layout
        text.ForceMeshUpdate();

        // Now get exact preferred height for that width
        float prefH = text.preferredHeight;
        float prefW = Mathf.Min(text.preferredWidth, innerMaxWidth);

        // Bubble sizes
        float bubbleW = prefW + padding.x;
        float bubbleH = prefH + padding.y;

        // Apply bubble + text rect final sizes
        bubbleBG.size = new Vector2(bubbleW, bubbleH);
        rt.sizeDelta = new Vector2(bubbleW - padding.x, bubbleH - padding.y);

        // Center
        text.transform.localPosition = Vector3.zero;

        text.ForceMeshUpdate();
    }


}
