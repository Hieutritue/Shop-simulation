using System;
using UnityEngine;

/// <summary>
/// Singleton quản lý tổng tiền cửa hàng.
/// Khởi điểm $500, expose OnMoneyChanged để HUD/UI subscribe.
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField] private int _startingMoney = 500;
    private int _money;

    public int Money => _money;

    /// <summary>Args: (deltaAmount, newTotal). Delta âm khi tiêu, dương khi cộng.</summary>
    public event Action<int, int> OnMoneyChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _money = _startingMoney;
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        _money += amount;
        OnMoneyChanged?.Invoke(amount, _money);
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || _money < amount) return false;
        _money -= amount;
        OnMoneyChanged?.Invoke(-amount, _money);
        return true;
    }
}
