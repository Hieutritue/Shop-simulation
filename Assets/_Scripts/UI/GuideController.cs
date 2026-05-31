using TMPro;
using UnityEngine;

/// <summary>
/// Hiển thị guide tương tác theo trạng thái Player + Target hiện tại:
/// - Không cầm + nhìn item / shelf-có-đồ  → PickupGuide (Label_ItemName = tên).
/// - Không cầm + nhìn Checkout           → Mouse Left "Tính tiền".
/// - Cầm đồ + nhìn Shelf                 → Mouse Left "Đặt đồ".
/// - Cầm đồ (mọi target)                 → Mouse Right "Thả đồ".
/// </summary>
public class GuideController : MonoBehaviour
{
    [Header("Pickup state (không cầm)")]
    [SerializeField] private GameObject _pickupGuide;
    [SerializeField] private TMP_Text _pickupItemNameLabel;

    [Header("Action guides (Mouse Left / Right)")]
    [SerializeField] private GameObject _guideParent;
    [SerializeField] private GameObject _guideleft;
    [SerializeField] private TMP_Text _guideLeftLabel;
    [SerializeField] private GameObject _guideright;
    [SerializeField] private TMP_Text _guideRightLabel;
    [SerializeField] private GameObject _guideF;
    [SerializeField] private TMP_Text _guideFLabel;

    [Header("Action labels")]
    [SerializeField] private string _labelPlaceOnShelf = "Đặt đồ";
    [SerializeField] private string _labelCheckout = "Tính tiền";
    [SerializeField] private string _labelDrop = "Thả đồ";
    [SerializeField] private string _labelCancel = "Hủy";

    [Header("Pickup labels")]
    [SerializeField] private string _labelCounter = "Quầy thanh toán";
    [SerializeField] private string _labelScanner = "Máy quét";

    [Header("Optional refs (auto-find nếu để trống)")]
    [SerializeField] private PlayerInteract _playerInteract;
    [SerializeField] private PlayerCarry _playerCarry;

    private void Awake()
    {
        if (_playerInteract == null) _playerInteract = FindObjectOfType<PlayerInteract>();
        if (_playerCarry == null) _playerCarry = FindObjectOfType<PlayerCarry>();
    }

    private void Start()
    {
        HideAll();
    }

    private void Update()
    {
        if (_playerInteract == null || _playerCarry == null) return;

        bool holding = _playerCarry.HeldObject != null;
        IInteractable target = _playerInteract.CurrentInteractable;
        string pickupLabel = ResolvePickupLabel(target);

        // PickupGuide: chỉ khi không cầm + có label phù hợp.
        bool showPickup = !holding && !string.IsNullOrEmpty(pickupLabel);
        if (_pickupGuide != null) _pickupGuide.SetActive(showPickup);
        if (showPickup && _pickupItemNameLabel != null) _pickupItemNameLabel.text = pickupLabel;

        // Mouse Left: dynamic theo context.
        string leftText = null;
        if (holding && target is ShelfController) leftText = _labelPlaceOnShelf;
        else if (!holding && target is CheckoutCounter) leftText = _labelCheckout;

        bool showLeft = leftText != null;
        bool showRight = holding;
        bool showGuideParent = showLeft || showRight;

        if (_guideParent != null) _guideParent.SetActive(showGuideParent);
        if (_guideleft != null) _guideleft.SetActive(showLeft);
        if (showLeft && _guideLeftLabel != null) _guideLeftLabel.text = leftText;
        if (_guideright != null) _guideright.SetActive(showRight);

        if (showRight && _guideRightLabel != null)
        {
            GameObject held = _playerCarry.HeldObject;
            bool isCancel = held != null &&
                (held.GetComponent<MoneyStack>() != null || held.GetComponent<ScannerGun>() != null);
            _guideRightLabel.text = isCancel ? _labelCancel : _labelDrop;
        }
    }

    private void HideAll()
    {
        if (_pickupGuide != null) _pickupGuide.SetActive(false);
        if (_guideParent != null) _guideParent.SetActive(false);
        if (_guideleft != null) _guideleft.SetActive(false);
        if (_guideright != null) _guideright.SetActive(false);
    }

    private string ResolvePickupLabel(IInteractable target)
    {
        if (target == null) return null;
        if (target is ItemObject item) return item.ItemData != null ? item.ItemData.itemName : null;
        if (target is CheckoutCounter) return _labelCounter;
        if (target is ScannerGun) return _labelScanner;
        if (target is MoneyStack money) return $"${money.Denomination}";
        if (target is CashDrawer drawer) return $"${drawer.Denomination}";
        return null;
    }
}
