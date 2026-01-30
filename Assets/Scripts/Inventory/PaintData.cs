using UnityEngine;

[CreateAssetMenu(fileName = "New Paint", menuName = "Shop/Paint Item")]
public class PaintItem : ScriptableObject
{
    public string paintName;
    [Range(0, 100)]
    public float currentFill = 0f;
}