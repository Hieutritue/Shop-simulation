using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị state phiên thanh toán: subtotal, paid, change còn nợ, patience bar.
/// Ẩn root khi không có session active.
/// </summary>
public class CheckoutUI : MonoBehaviour
{
    [SerializeField] private CheckoutCounter _counter;
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _subtotalText;
    [SerializeField] private TMP_Text _paidText;
    [SerializeField] private TMP_Text _changeOwedText;
    [SerializeField] private TMP_Text _unscannedCountText;
    [SerializeField] private Slider _patienceBar;

    private void Awake()
    {
        if (_counter == null) _counter = Object.FindFirstObjectByType<CheckoutCounter>();
    }

    private void Update()
    {
        if (_counter == null) return;
        var session = _counter.CurrentSession;

        // Chỉ hiện panel khi player đã engage (ngồi ở quầy) + có session.
        if (session == null || !_counter.IsPlayerEngaged)
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
            return;
        }

        if (_root != null && !_root.activeSelf) _root.SetActive(true);

        if (_subtotalText != null) _subtotalText.text = $"${session.Subtotal}";
        if (_paidText != null) _paidText.text = $"${session.Paid}";
        if (_changeOwedText != null) _changeOwedText.text = $"${session.ChangeRemaining}";
        if (_unscannedCountText != null) _unscannedCountText.text = $"{session.Unscanned.Count} chưa scan";

        if (_patienceBar != null && session.PatienceTotal > 0f)
        {
            _patienceBar.value = session.PatienceRemaining / session.PatienceTotal;
        }
    }
}
