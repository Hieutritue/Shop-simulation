using TMPro;
using UnityEngine;

/// <summary>
/// Hiển thị tổng tiền hiện tại. Subscribe MoneyChangedEvent qua EventBus.
/// </summary>
public class MoneyHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text _moneyText;
    [SerializeField] private string _format = "${0:N0}";

    private void OnEnable() => EventBus.Subscribe<MoneyChangedEvent>(OnMoneyChanged);
    private void OnDisable() => EventBus.Unsubscribe<MoneyChangedEvent>(OnMoneyChanged);

    private void OnMoneyChanged(MoneyChangedEvent evt)
    {
        if (_moneyText != null) _moneyText.text = string.Format(_format, evt.NewTotal);
    }
}
