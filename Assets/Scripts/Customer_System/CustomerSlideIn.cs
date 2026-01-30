using UnityEngine;
using System.Collections;

public class CustomerSlideIn : MonoBehaviour
{
    public Vector3 startPos = new Vector3(10f, -2f, 0f);
    public Vector3 targetPos = new Vector3(3f, -2f, 0f);

    [Header("Cartoony Slide")]
    public float slideTime = 0.8f;          // how long to arrive
    public float overshoot = 0.4f;          // how far past target
    public float bounceBackTime = 0.18f;    // snap-back time

    public SpeechBubbleWorld speechBubble;

    void Start()
    {
        transform.position = startPos;
        if (speechBubble != null) speechBubble.Hide();
        StartCoroutine(SlideInRoutine());
    }

    IEnumerator SlideInRoutine()
    {
        Vector3 overshootPos = targetPos + Vector3.right * overshoot;

        // 1) Slide to overshoot with smooth easing
        yield return MoveEase(transform, startPos, overshootPos, slideTime);

        // 2) Bounce back quickly
        yield return MoveEase(transform, overshootPos, targetPos, bounceBackTime);

        // 3) Tiny settle wobble (optional)
        yield return MoveEase(transform, targetPos, targetPos + Vector3.right * 0.08f, 0.08f);
        yield return MoveEase(transform, targetPos + Vector3.right * 0.08f, targetPos, 0.08f);

        if (speechBubble != null)
            speechBubble.Show("Can you make me a mask?");
    }

    IEnumerator MoveEase(Transform t, Vector3 a, Vector3 b, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float x = Mathf.Clamp01(time / duration);

            float eased = x * x * (3f - 2f * x);

            float bob = Mathf.Sin(x * Mathf.PI * 2f) * 0.2f;

            Vector3 pos = Vector3.LerpUnclamped(a, b, eased);
            pos.y += bob;

            t.position = pos; // keep this only
            yield return null;
        }

        t.position = b;
    }

}
