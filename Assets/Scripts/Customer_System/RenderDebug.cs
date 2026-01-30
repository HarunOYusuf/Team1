using UnityEngine;

public class RenderDebug : MonoBehaviour
{
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        if (!gameObject.activeInHierarchy) Debug.Log("Character GameObject INACTIVE");
        if (sr != null && !sr.enabled) Debug.Log("SpriteRenderer DISABLED");
    }
}
