using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class InteractionManager : MonoBehaviour
{
    [Header("Actors")]
    public SpriteRenderer npcRenderer;    // Order 1
    public SpriteRenderer wizardRenderer; // Order 3
    public CustomerCutsceneUI cutsceneUI;
    public GameObject createButtonPanel;

    [Header("Animation Settings")]
    public float moveDuration = 5.0f; // Slow, face-to-face walk
    public float bobAmplitude = 0.12f;
    public float bobFrequency = 14f;

    [Header("Positions")]
    public float npcOffScreenR = 10f;
    public float npcAtCounter = 3f;
    public float wizOffScreenL = -10f;
    public float wizAtCounter = -3f;

    private float originalY;

    void Start()
    {
        originalY = npcRenderer.transform.position.y;
        createButtonPanel.SetActive(false);

        // Phase 1: Characters walk in together
        StartCoroutine(ArrivalSequence());
    }

    IEnumerator ArrivalSequence()
    {
        // Flip sprites to face each other as they walk in
        npcRenderer.flipX = true;     // NPC looks Left
        wizardRenderer.flipX = false; // Wizard looks Right

        yield return StartCoroutine(MoveWithSteps(npcOffScreenR, npcAtCounter, wizOffScreenL, wizAtCounter));

        // Show dialogue (link to your existing TMP script)
        cutsceneUI.PlayCutscene(null, "Wizard, I require a mask for the festival...");

        yield return new WaitForSeconds(1.5f);
        createButtonPanel.SetActive(true); // Now the player can click "Create"
    }

    // Link this to your "Create" Button OnClick()
    public void OnCreatePressed()
    {
        createButtonPanel.SetActive(false);
        StartCoroutine(WizardGoesToWorkshop());
    }

    IEnumerator WizardGoesToWorkshop()
    {
        // Wizard turns to face the left door
        wizardRenderer.flipX = true;

        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float wizX = Mathf.Lerp(wizAtCounter, wizOffScreenL, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            wizardRenderer.transform.position = new Vector3(wizX, originalY + stepBob, 0);
            yield return null;
        }

        // Change scene to the painting workbench
        SceneManager.LoadScene("Harun");
    }

    // Call this when you return from the painting scene
    public void NPCExitsShop()
    {
        StartCoroutine(NPCExitSequence());
    }

    IEnumerator NPCExitSequence()
    {
        // NPC turns to face the right exit
        npcRenderer.flipX = false;

        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float npcX = Mathf.Lerp(npcAtCounter, npcOffScreenR, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            npcRenderer.transform.position = new Vector3(npcX, originalY + stepBob, 0);
            yield return null;
        }
    }

    // Shared "Steps" animation logic
    IEnumerator MoveWithSteps(float nStart, float nEnd, float wStart, float wEnd)
    {
        float elapsed = 0;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / moveDuration;
            float smoothT = t * t * (3f - 2f * t);

            float nX = Mathf.Lerp(nStart, nEnd, smoothT);
            float wX = Mathf.Lerp(wStart, wEnd, smoothT);
            float stepBob = Mathf.Abs(Mathf.Sin(elapsed * bobFrequency)) * bobAmplitude;

            npcRenderer.transform.position = new Vector3(nX, originalY + stepBob, 0);
            wizardRenderer.transform.position = new Vector3(wX, originalY + stepBob, 0);
            yield return null;
        }
    }
}