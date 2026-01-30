using UnityEngine;
using UnityEngine.UI;

public class PaintShop : MonoBehaviour
{
    public PaintItem selectedPaint; // Drag the currently selected paint here
    public Text statusText;         // Optional: To show the fill amount

    // This method is called by your "Purchase" button
    public void PurchaseFill()
    {
        if (selectedPaint != null)
        {
            // Add 10% fill, capping it at 100%
            selectedPaint.currentFill = Mathf.Clamp(selectedPaint.currentFill + 10f, 0f, 100f);
            
            Debug.Log($"{selectedPaint.paintName} fill is now {selectedPaint.currentFill}%");
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("No paint selected!");
        }
    }

    // Call this when clicking a paint icon/button to select it
    public void SelectPaint(PaintItem paint)
    {
        selectedPaint = paint;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (statusText != null && selectedPaint != null)
            statusText.text = $"Selected: {selectedPaint.paintName} ({selectedPaint.currentFill}%)";
    }
}