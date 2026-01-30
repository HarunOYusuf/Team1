using UnityEngine;
using System.Collections;

public class CustomerExit : MonoBehaviour
{
    public SpeechBubbleWorld speechBubble;

    public void DoExitPass(float score){
        Debug.Log("MASK HAS PASSED");
        StartCoroutine(DoPass());
    }
    private IEnumerator DoPass(){
        speechBubble.Show("Thanks, I love it.");

        yield return new WaitForSeconds(3f);

        FindObjectOfType<CustomerSlideIn>().BeginSlideOut();
        FindObjectOfType<SpeechBubbleWorld>().Hide();

    }
    public void DoExitFail(float score){
        StartCoroutine(DoFail());
    }
    private IEnumerator DoFail(){
        Debug.Log("EPIC FAILURE");
        
        speechBubble.Show("I'm not taking that piece of trash!");

        yield return new WaitForSeconds(3f);

        FindObjectOfType<CustomerSlideIn>().BeginSlideOut();
        FindObjectOfType<SpeechBubbleWorld>().Hide();
    }
}
