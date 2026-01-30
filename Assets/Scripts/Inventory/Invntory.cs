using UnityEngine;
using TMPro;

public class Invntory : MonoBehaviour
{
    [Header("Paints")]public int redPaint;
    public int yellowPaint;
    public int bluePaint;
    [Header("Text References")]
    public TMP_Text redText;
    public TMP_Text yellowText;
    public TMP_Text blueText;

    public void UpdateUI()
    {
        redText.text = redPaint.ToString();
        yellowText.text = yellowPaint.ToString();
        blueText.text = bluePaint.ToString();
    }

    public void FullRestock()
    {
        redPaint = 100;
        yellowPaint = 100;
        bluePaint = 100;
        UpdateUI();
    }

   public void PurchaseRedPaint(int amount)
   {
    if(redPaint + amount >= 100) redPaint = 100;
    else redPaint += amount;
    UpdateUI();
   }

   public void PurchaseBluePaint(int amount)
   {
    if(bluePaint + amount >= 100) bluePaint = 100;
    else bluePaint += amount;
    UpdateUI();
   }

   public void PurchaseYellowPaint(int amount)
   {
    if(yellowPaint + amount >= 100) yellowPaint = 100;
    else yellowPaint += amount;
    UpdateUI();
   }

   public void UseRedPaint(int amount)
   {
    if(redPaint - amount <= 0) redPaint = 0;
    else redPaint -= amount;
    UpdateUI();
   }

   public void UseBluePaint(int amount)
   {
    if(bluePaint - amount <= 0) bluePaint = 0;
    else bluePaint -= amount;
    UpdateUI();
   }

   public void UseYellowPaint(int amount)
   {
    if(yellowPaint - amount <= 0) yellowPaint = 0;
    else yellowPaint -= amount;
    UpdateUI();
   }
}
