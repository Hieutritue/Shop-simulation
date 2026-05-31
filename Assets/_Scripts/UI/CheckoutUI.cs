using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hiển thị state phiên thanh toán: subtotal, paid, change còn nợ, patience bar.
/// Show khi player engaged + session active. Ẩn ngược lại.
/// Lưu ý: nếu `_root` là chính GameObject này → SetActive(false) sẽ tắt Update → dùng
/// fallback toggle Image + children để giữ Update chạy.
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

    private Image _selfImage;
    private bool _lastVisible = true;

    private void Awake()
    {
        if (_counter == null) _counter = Object.FindFirstObjectByType<CheckoutCounter>();
        _selfImage = GetComponent<Image>();
    }

    private void Update()
    {
        if (_counter == null) return;
        var session = _counter.CurrentSession;
        bool show = session != null && _counter.IsPlayerEngaged;

        SetVisible(show);
        if (!show) return;

        if (_subtotalText != null) _subtotalText.text = $"Giá: ${session.Subtotal}";
        if (_paidText != null) _paidText.text = $"Khách trả: ${session.Paid}";
        if (_changeOwedText != null) _changeOwedText.text = $"Tiền thừa: ${session.ChangeRemaining}";
        if (_unscannedCountText != null) _unscannedCountText.text = $"{session.Unscanned.Count} chưa scan";

        if (_patienceBar != null && session.PatienceTotal > 0f)
        {
            _patienceBar.value = session.PatienceRemaining / session.PatienceTotal;
        }
    }

    private void SetVisible(bool visible)
    {
        if (visible == _lastVisible) return;
        _lastVisible = visible;

        // _root khác chính mình → toggle bình thường.
        if (_root != null && _root != gameObject)
        {
            _root.SetActive(visible);
            return;
        }

        // _root == self (hoặc null) → không tắt GameObject này (sẽ kill Update).
        // Toggle Image + tất cả children thay vì SetActive(self, false).
        if (_selfImage != null) _selfImage.enabled = visible;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child.gameObject.activeSelf != visible) child.gameObject.SetActive(visible);
        }
    }
}
