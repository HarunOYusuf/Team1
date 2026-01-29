using UnityEngine;

public class CustomerSlideIn : MonoBehaviour
{
    public Vector3 startPos = new Vector3(10f, -2f, 0f);
    public Vector3 targetPos = new Vector3(3f, -2f, 0f);
    public float speed = 5f;

    public SpeechBubbleWorld speechBubble;

    bool arrived = false;

    void Start()
    {
        transform.position = startPos;
        if (speechBubble != null)
            speechBubble.Hide();
    }

    void Update()
    {
        if (arrived) return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPos,
            speed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, targetPos) < 0.01f)
        {
            arrived = true;
            //speechBubble.Show("Hi");
            speechBubble.Show("Can you make me a mask?");
            //speechBubble.Show("I want a red fox mask with gold trim and angry eyes.");

        }
    }
}
