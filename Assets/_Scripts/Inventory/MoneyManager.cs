using UnityEngine;

/// <summary>
/// Singleton quản lý tổng tiền cửa hàng.
/// Khởi điểm $500. Raise MoneyChangedEvent qua EventBus mỗi khi thay đổi.
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [SerializeField] private int _startingMoney = 500;
    private int _money;

    public int Money => _money;

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

    private void Start()
    {
        // Phát event lần đầu để HUD/listener subscribe sau Awake vẫn nhận được giá trị khởi tạo.
        EventBus.Raise(new MoneyChangedEvent(0, _money));
    }

    public void AddMoney(int amount)
    {
        if (amount <= 0) return;
        _money += amount;
        EventBus.Raise(new MoneyChangedEvent(amount, _money));
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0 || _money < amount) return false;
        _money -= amount;
        EventBus.Raise(new MoneyChangedEvent(-amount, _money));
        return true;
    }
}
