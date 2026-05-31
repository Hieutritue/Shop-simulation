using System.Linq;
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
    [SerializeField] private GameObject _guideleft;
    [SerializeField] private TMP_Text _guideLeftLabel;
    [SerializeField] private GameObject _guideright;
    [SerializeField] private TMP_Text _guideRightLabel;
    [SerializeField] private GameObject _guideF;
    [SerializeField] private TMP_Text _guideFLabel;

    [Header("Action labels")]
    [SerializeField] private string _labelPlaceOnShelf = "Đặt đồ";
    [SerializeField] private string _labelStockShelf = "Xếp đồ";
    [SerializeField] private string _labelGather = "Gom đồ";
    [SerializeField] private string _labelCheckout = "Tính tiền";
    [SerializeField] private string _labelPickup = "Lấy";
    [SerializeField] private string _labelScan = "Quét giá";
    [SerializeField] private string _labelPay = "Trả tiền";
    [SerializeField] private string _labelDrop = "Thả đồ";
    [SerializeField] private string _labelDropBox = "Thả hộp";
    [SerializeField] private string _labelCancel = "Hủy";
    [SerializeField] private string _labelLeaveCounter = "Ra khỏi quầy";
    [SerializeField] private string _labelDropItems = "Bỏ đồ";

    [Header("Pickup labels")]
    [SerializeField] private string _labelCounter = "Quầy thanh toán";
    [SerializeField] private string _labelScanner = "Máy quét";
    [SerializeField] private string _labelItemBox = "Hộp carton";

    [Header("Optional refs (auto-find nếu để trống)")]
    [SerializeField] private PlayerInteract _playerInteract;
    [SerializeField] private PlayerCarry _playerCarry;
    [SerializeField] private CheckoutCounter _counter;

    private void Awake()
    {
        if (_playerInteract == null) _playerInteract = FindObjectOfType<PlayerInteract>();
        if (_playerCarry == null) _playerCarry = FindObjectOfType<PlayerCarry>();
        if (_counter == null) _counter = FindObjectOfType<CheckoutCounter>();
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
        GameObject heldObj = _playerCarry.HeldObject;
        bool holdingBox = holding && heldObj.GetComponent<ItemBox>() != null;
        string leftText = null;
        if (holdingBox && target is ItemObject) leftText = _labelGather;
        else if (holdingBox && target is ShelfController) leftText = _labelStockShelf;
        else if (holding && target is ShelfController) leftText = _labelPlaceOnShelf;
        else if (holding && heldObj.GetComponent<ScannerGun>() != null &&
                 target is ItemObject scanItem &&
                 _counter != null && _counter.CurrentSession != null &&
                 _counter.CurrentSession.Unscanned.Contains(scanItem))
            leftText = _labelScan;
        else if (holding && heldObj.GetComponent<MoneyStack>() != null &&
                 target is CustomerAgent payCustomer &&
                 payCustomer.IsActiveInSession)
            leftText = _labelPay;
        else if (!holding && target is CheckoutCounter) leftText = _labelCheckout;
        else if (!holding && (target is ItemObject || target is ScannerGun || target is CashDrawer || target is ItemBox))
            leftText = _labelPickup;

        bool showLeft = leftText != null;
        bool showRight = holding;
        
        if (_guideleft != null) _guideleft.SetActive(showLeft);
        if (showLeft && _guideLeftLabel != null) _guideLeftLabel.text = leftText;
        if (_guideright != null) _guideright.SetActive(showRight);

        if (showRight && _guideRightLabel != null)
        {
            GameObject held = _playerCarry.HeldObject;
            bool isCancel = held != null &&
                (held.GetComponent<MoneyStack>() != null || held.GetComponent<ScannerGun>() != null);
            bool isBox = held != null && held.GetComponent<ItemBox>() != null;
            _guideRightLabel.text = isCancel ? _labelCancel : (isBox ? _labelDropBox : _labelDrop);
        }

        // F Guide: engage counter → "Ra khỏi quầy"; cầm ItemBox → "Bỏ đồ".
        bool engagedAtCounter = _counter != null && _counter.IsPlayerEngaged;
        bool showF = engagedAtCounter || holdingBox;
        if (_guideF != null) _guideF.SetActive(showF);
        if (showF && _guideFLabel != null)
        {
            _guideFLabel.text = holdingBox ? _labelDropItems : _labelLeaveCounter;
        }
    }

    private void HideAll()
    {
        if (_pickupGuide != null) _pickupGuide.SetActive(false);
        if (_guideleft != null) _guideleft.SetActive(false);
        if (_guideright != null) _guideright.SetActive(false);
        if (_guideF != null) _guideF.SetActive(false);
    }

    private string ResolvePickupLabel(IInteractable target)
    {
        if (target == null) return null;
        if (target is ItemObject item) return item.ItemData != null ? item.ItemData.itemName : null;
        if (target is CheckoutCounter) return _labelCounter;
        if (target is ScannerGun) return _labelScanner;
        if (target is MoneyStack money) return $"${money.Denomination}";
        if (target is CashDrawer drawer) return $"${drawer.Denomination}";
        if (target is ItemBox) return _labelItemBox;
        return null;
    }
}
