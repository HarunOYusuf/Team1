using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CustomerCutsceneUI : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private RectTransform customerRect;
    [SerializeField] private Image customerImage;         // optional, for swapping sprite
    [SerializeField] private RectTransform bubbleRect;
    [SerializeField] private CanvasGroup bubbleCanvasGroup;
    [SerializeField] private TypewriterTMP typewriter;

    [Header("Animation")]
    [SerializeField] private float slideDuration = 0.6f;
    [SerializeField] private float bubblePopDuration = 0.18f;

    [Header("Positions (anchoredPosition)")]
    [SerializeField] private Vector2 onScreenPos = new Vector2(191.1f, -198.2f);
    [SerializeField] private Vector2 offScreenPos = new Vector2(600f, -198.2f);

    void Awake()
    {
        // Start hidden / offscreen
        customerRect.anchoredPosition = offScreenPos;

        bubbleRect.localScale = Vector3.zero;
        bubbleCanvasGroup.alpha = 0f;
    }

    public void PlayCutscene(Sprite customerSprite, string line)
    {
        if (customerImage != null && customerSprite != null)
            customerImage.sprite = customerSprite;

        StopAllCoroutines();
        StartCoroutine(CutsceneRoutine(line));
    }

    IEnumerator CutsceneRoutine(string line)
    {
        // Slide customer in (from right/offscreen -> onScreenPos)
        yield return Slide(customerRect, offScreenPos, onScreenPos, slideDuration);

        // Bubble pop in
        yield return BubblePopIn();

        // Type text
        typewriter.Play(line);
    }

    IEnumerator Slide(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);

            // Smooth step easing
            u = u * u * (3f - 2f * u);

            rt.anchoredPosition = Vector2.LerpUnclamped(from, to, u);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    IEnumerator BubblePopIn()
    {
        bubbleCanvasGroup.alpha = 1f;

        float t = 0f;
        while (t < bubblePopDuration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / bubblePopDuration);

            // pop easing: fast overshoot, then settle (simple)
            float s = Mathf.Lerp(0f, 1.1f, u);
            bubbleRect.localScale = new Vector3(s, s, 1f);

            yield return null;
        }

        bubbleRect.localScale = Vector3.one;
    }
}
