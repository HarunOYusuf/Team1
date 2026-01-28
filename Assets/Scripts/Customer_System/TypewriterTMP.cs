using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterTMP : MonoBehaviour
{
    [SerializeField] private TMP_Text tmpText;
    [SerializeField] private float lettersPerSecond = 35f;

    Coroutine typingRoutine;

    public void SetInstant(string text)
    {
        StopTypingIfNeeded();
        tmpText.text = text;
    }

    public void Play(string text)
    {
        StopTypingIfNeeded();
        typingRoutine = StartCoroutine(TypeRoutine(text));
    }

    public void StopTypingIfNeeded()
    {
        if (typingRoutine != null) StopCoroutine(typingRoutine);
        typingRoutine = null;
    }

    IEnumerator TypeRoutine(string text)
    {
        tmpText.text = "";
        float delay = 1f / Mathf.Max(1f, lettersPerSecond);

        for (int i = 0; i < text.Length; i++)
        {
            tmpText.text += text[i];
            yield return new WaitForSeconds(delay);
        }

        typingRoutine = null;
    }
}
