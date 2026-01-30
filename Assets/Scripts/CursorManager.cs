using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public Texture2D cursorTexture;
    public Vector2 hotSpot = Vector2.zero; // Usually (0,0) for top-left or center for crosshairs

    void Start()
    {
        // Set the cursor. CursorMode.Auto uses hardware rendering for no lag.
        Cursor.SetCursor(cursorTexture, hotSpot, CursorMode.Auto);
    }
}