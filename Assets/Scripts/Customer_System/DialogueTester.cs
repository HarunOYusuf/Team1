using UnityEngine;

public class DialogueTester : MonoBehaviour
{
    public SpeechBubbleWorld bubble; // drag SpeechBubble here

    [TextArea(2, 5)]
    public string[] lines;

    int i = -1;

    void Start()
    {
        if (bubble != null) bubble.Hide();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (bubble == null || lines == null || lines.Length == 0) return;

            i = (i + 1) % lines.Length;
            bubble.Show(lines[i]);
        }
    }
}
