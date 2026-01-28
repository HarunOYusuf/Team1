using UnityEngine;

public class CutsceneTest : MonoBehaviour
{
    [SerializeField] private CustomerCutsceneUI cutscene;

    void Start()
    {
        if (cutscene == null)
        {
            Debug.LogError("CutsceneTest: cutscene reference is NOT set!");
            return;
        }

        cutscene.PlayCutscene(null, "Hello! I need a mask... something brave.");
    }
}
