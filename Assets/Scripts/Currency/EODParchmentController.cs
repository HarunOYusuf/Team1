using UnityEngine;
using TMPro;

public class EODParchmentController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Invntory inventory;

    [Header("Inventory Text")]
    [SerializeField] private TMP_Text redAmountText;
    [SerializeField] private TMP_Text blueAmountText;
    [SerializeField] private TMP_Text yellowAmountText;

    [Header("Currency Text")]
    [SerializeField] private TMP_Text eodCurrencyText;

    private const int refillAmount = 10;
    private const int refillCost = 5;

    private int startingRed;
    private int startingBlue;
    private int startingYellow;

    private void OnEnable()
    {
        startingRed = inventory.redPaint;
        startingBlue = inventory.bluePaint;
        startingYellow = inventory.yellowPaint;

        UpdateAllUI();
    }

    private void UpdateAllUI()
    {
        redAmountText.text = inventory.redPaint.ToString();
        blueAmountText.text = inventory.bluePaint.ToString();
        yellowAmountText.text = inventory.yellowPaint.ToString();

        eodCurrencyText.text =
            CurrencyManager.Instance.GetCurrentMoney().ToString();
    }

    // -------- ADD (+) --------

    public void AddRedPaint()
    {
        if (inventory.redPaint >= 100) return;
        if (!CurrencyManager.Instance.SpendMoney(refillCost)) return;

        inventory.PurchaseRedPaint(refillAmount);
        UpdateAllUI();
    }

    public void AddBluePaint()
    {
        if (inventory.bluePaint >= 100) return;
        if (!CurrencyManager.Instance.SpendMoney(refillCost)) return;

        inventory.PurchaseBluePaint(refillAmount);
        UpdateAllUI();
    }

    public void AddYellowPaint()
    {
        if (inventory.yellowPaint >= 100) return;
        if (!CurrencyManager.Instance.SpendMoney(refillCost)) return;

        inventory.PurchaseYellowPaint(refillAmount);
        UpdateAllUI();
    }

    // -------- REMOVE (−) --------

    public void RemoveRedPaint()
    {
        if (inventory.redPaint - refillAmount < startingRed) return;

        inventory.UseRedPaint(refillAmount);
        CurrencyManager.Instance.AddMoney(refillCost);
        UpdateAllUI();
    }

    public void RemoveBluePaint()
    {
        if (inventory.bluePaint - refillAmount < startingBlue) return;

        inventory.UseBluePaint(refillAmount);
        CurrencyManager.Instance.AddMoney(refillCost);
        UpdateAllUI();
    }

    public void RemoveYellowPaint()
    {
        if (inventory.yellowPaint - refillAmount < startingYellow) return;

        inventory.UseYellowPaint(refillAmount);
        CurrencyManager.Instance.AddMoney(refillCost);
        UpdateAllUI();
    }

    // -------- NEXT DAY --------

    public void BeginNextDay()
    {
        gameObject.SetActive(false);
        // Day system will hook here later
    }
}