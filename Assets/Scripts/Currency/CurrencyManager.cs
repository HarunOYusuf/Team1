using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Currency Settings")]
    [SerializeField] private int startingMoney = 0;

    private int currentMoney;

    private void Awake()
    {
        // Singleton (jam-safe)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentMoney = startingMoney;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (currencyText != null)
            currencyText.text = currentMoney.ToString();
    }

    // --- Core Logic ---

    // Called when a customer pays
    public void AddMoney(int amount)
    {
        currentMoney += amount;
        UpdateUI();
    }

    // Core spend logic (used by game systems)
    public bool SpendMoney(int amount)
    {
        if (currentMoney < amount)
            return false;

        currentMoney -= amount;
        UpdateUI();
        return true;
    }

    public int GetCurrentMoney()
    {
        return currentMoney;
    }

    // --- UI Wrapper Methods ---

    // Used for Button OnClick()
    public void SpendMoneyButton(int amount)
    {
        SpendMoney(amount);
    }

    // Optional test helper
    public void AddMoneyButton(int amount)
    {
        AddMoney(amount);
    }
}