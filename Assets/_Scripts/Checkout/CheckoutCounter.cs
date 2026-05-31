using System.Collections.Generic;
using CodeMonkey.Toolkit.TFirstPersonController;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Quầy tính tiền: quản lý hàng chờ, 1 active CheckoutSession, các zone đặt đồ scanned/change.
/// Customer giữ items trong tay; scanner tween items sang ScannedZone khi scan.
/// Player cầm MoneyStack + Interact counter → đặt vào ChangeZone, cộng vào ChangeGiven.
/// </summary>
public class CheckoutCounter : MonoBehaviour, IInteractable
{
    private const float SCAN_TWEEN_DURATION = 0.35f;
    private const float SCAN_TWEEN_HEIGHT = 0.6f;
    private const float ENGAGE_DURATION = 0.35f;
    private const float DISENGAGE_DURATION = 0.25f;
    private const float DISENGAGE_BACK_OFFSET = 0.8f;

    [Header("Queue")]
    [SerializeField] private Transform[] _queuePositions;

    [Header("Zones")]
    [Tooltip("Slot đặt đồ đã scan (visual). Tween từ tay customer → đây.")]
    [SerializeField] private Transform[] _scannedZone;
    [SerializeField] private Transform _changeZone;
    [Tooltip("Vị trí + hướng player bị snap khi engage làm việc ở quầy.")]
    [SerializeField] private Transform _playerWorkSpot;

    [Header("Session")]
    [SerializeField] private float _defaultPatience = 30f;

    private readonly List<CustomerAgent> _queue = new List<CustomerAgent>();
    private int _scannedSlotCursor;
    private GameObject _engagedPlayer;
    private FirstPersonController _engagedFpc;
    private bool _isEngageTweening;
    private bool _isDisengageTweening;

    public bool IsPlayerEngaged => _engagedPlayer != null;

    public CheckoutSession CurrentSession { get; private set; }
    public CustomerAgent ActiveCustomer => CurrentSession?.Customer;
    public int QueueLength => _queue.Count;

    private void Update()
    {
        // F key → rời workSpot, dọn MoneyStack/ScannerGun đang cầm.
        if (IsPlayerEngaged && Input.GetKeyDown(KeyCode.F))
        {
            CleanupHeldThenDisengage();
        }

        if (CurrentSession == null) return;

        CurrentSession.TickPatience(Time.deltaTime);

        if (CurrentSession.PatienceExpired)
        {
            ActiveCustomer?.TriggerAngryLeave();
            EndSession(false);
            return;
        }

        if (CurrentSession.IsComplete)
        {
            int subtotal = CurrentSession.Subtotal;
            MoneyManager.Instance?.AddMoney(subtotal);
            ActiveCustomer?.TriggerLeaveHappy();
            EndSession(true);
        }
    }

    // ───────── Queue ─────────
    public int JoinQueue(CustomerAgent customer)
    {
        if (customer == null) return -1;
        int idx = _queue.IndexOf(customer);
        if (idx >= 0) return idx;
        _queue.Add(customer);
        return _queue.Count - 1;
    }

    public Transform GetQueuePosition(int index)
    {
        if (_queuePositions == null || _queuePositions.Length == 0) return transform;
        return _queuePositions[Mathf.Clamp(index, 0, _queuePositions.Length - 1)];
    }

    public bool IsFrontCustomer(CustomerAgent customer)
        => _queue.Count > 0 && _queue[0] == customer;

    public void LeaveQueue(CustomerAgent customer)
    {
        if (customer == null) return;
        if (!_queue.Remove(customer)) return;
        ShiftQueueForward();
    }

    private void ShiftQueueForward()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i] != null) _queue[i].OnQueuePositionChanged(i);
        }
    }

    // ───────── Session ─────────
    public bool TryStartSession(CustomerAgent customer, IReadOnlyList<ItemObject> items)
    {
        if (CurrentSession != null) return false;
        if (!IsFrontCustomer(customer)) return false;

        CurrentSession = new CheckoutSession(customer, items, _defaultPatience);
        _scannedSlotCursor = 0;

        // Tween từng item từ tay customer → slot trên counter (trình bày để scan).
        for (int i = 0; i < items.Count; i++)
        {
            Transform slot = _scannedZone != null && _scannedZone.Length > 0
                ? _scannedZone[i % _scannedZone.Length] : transform;
            TweenItemToPresentationSlot(items[i], slot);
        }
        return true;
    }

    private void TweenItemToPresentationSlot(ItemObject item, Transform slot)
    {
        if (item == null || slot == null) return;
        item.transform.SetParent(null);
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false; // tạm tắt — bật lại OnComplete để scan raycast thấy
        }

        Vector3 end = slot.position;
        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(item.transform, end, SCAN_TWEEN_DURATION, SCAN_TWEEN_HEIGHT))
            .OnComplete(() =>
            {
                if (item == null) return;
                item.transform.SetParent(slot);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
                if (item.TryGetComponent<Rigidbody>(out var rb2)) rb2.detectCollisions = true;
            });
    }

    /// <summary>Scanner gọi qua đây — tween item về tay customer sau scan.</summary>
    public bool TryScanItem(ItemObject item)
    {
        if (CurrentSession == null) return false;
        if (!CurrentSession.TryScan(item)) return false;
        TweenItemBackToCustomerHand(item);
        return true;
    }

    private void TweenItemBackToCustomerHand(ItemObject item)
    {
        if (item == null) return;
        Transform hand = CurrentSession?.Customer != null ? CurrentSession.Customer.Hand : null;
        if (hand == null) return;

        item.transform.SetParent(null);
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Vector3 end = hand.position;
        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(item.transform, end, SCAN_TWEEN_DURATION, SCAN_TWEEN_HEIGHT))
            .OnComplete(() =>
            {
                if (item == null || hand == null) return;
                item.transform.SetParent(hand);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            });
    }

    private void EndSession(bool happy)
    {
        // Dọn items đã scan: happy → customer mang đi (despawn nhẹ); angry → để litter.
        if (happy && _scannedZone != null)
        {
            foreach (var slot in _scannedZone)
            {
                if (slot == null) continue;
                for (int i = slot.childCount - 1; i >= 0; i--)
                {
                    Destroy(slot.GetChild(i).gameObject);
                }
            }
        }

        if (CurrentSession?.Customer != null) LeaveQueue(CurrentSession.Customer);
        CurrentSession = null;
        _scannedSlotCursor = 0;

        // Player giữ workSpot — phải bấm F để rời. Khách tiếp theo có thể bắt đầu session ngay.
    }

    // ───────── Lock-in player vào work spot ─────────
    public void EngagePlayer(GameObject playerGO)
    {
        if (playerGO == null || _engagedPlayer != null) return;
        if (_isEngageTweening || _isDisengageTweening) return;

        _engagedPlayer = playerGO;
        _engagedFpc = playerGO.GetComponent<FirstPersonController>();
        if (_engagedFpc != null) _engagedFpc.LockMovement(true);

        if (_playerWorkSpot == null)
        {
            Debug.LogWarning("[Checkout] _playerWorkSpot chưa assign — engage nhưng không teleport.");
            return;
        }

        // Tween mượt vị trí + xoay người về workSpot. Tắt CharacterController để tween world position.
        var cc = playerGO.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        _isEngageTweening = true;

        Sequence.Create()
            .Group(Tween.Position(playerGO.transform, _playerWorkSpot.position, ENGAGE_DURATION, Ease.OutQuad))
            .Group(Tween.Rotation(playerGO.transform, _playerWorkSpot.rotation, ENGAGE_DURATION, Ease.OutQuad))
            .OnComplete(() =>
            {
                if (cc != null) cc.enabled = true;
                _isEngageTweening = false;
            });
    }

    public void DisengagePlayer()
    {
        if (_engagedPlayer == null || _isDisengageTweening) return;

        GameObject playerGO = _engagedPlayer;
        FirstPersonController fpc = _engagedFpc;
        var cc = playerGO.GetComponent<CharacterController>();

        // Clear engaged ref ngay để UI ẩn + tránh re-engage trong lúc đẩy lùi.
        _engagedPlayer = null;
        _engagedFpc = null;

        // Đẩy player lùi theo hướng họ đang nhìn (negative forward).
        Vector3 backPos = playerGO.transform.position - playerGO.transform.forward * DISENGAGE_BACK_OFFSET;
        if (cc != null) cc.enabled = false;
        _isDisengageTweening = true;

        Tween.Position(playerGO.transform, backPos, DISENGAGE_DURATION, Ease.OutQuad)
            .OnComplete(() =>
            {
                if (cc != null) cc.enabled = true;
                if (fpc != null) fpc.LockMovement(false);
                _isDisengageTweening = false;
            });
    }

    // Cleanup khi rời workSpot bằng F: MoneyStack → destroy, ScannerGun → ReturnHome.
    private void CleanupHeldThenDisengage()
    {
        if (_engagedPlayer != null)
        {
            var carry = _engagedPlayer.GetComponent<PlayerCarry>();
            if (carry != null && carry.HeldObject != null)
            {
                GameObject held = carry.HeldObject;
                if (held.TryGetComponent<ScannerGun>(out var scanner))
                {
                    carry.ClearHeldObject();
                    scanner.ReturnHome();
                }
                else if (held.TryGetComponent<MoneyStack>(out var money))
                {
                    carry.ClearHeldObject();
                    money.CancelHome();
                }
                // Item thường khác — giữ lại trong tay player.
            }
        }
        DisengagePlayer();
    }

    // ───────── Player IInteractable ─────────
    // Không cầm gì → engage workSpot (cho phép cả khi không có session để player chuẩn bị).
    // Đưa change → click vào Customer (xem CustomerAgent.Interact), không qua counter.
    public void Interact(PlayerCarry player)
    {
        if (player == null) return;
        if (player.HeldObject != null) return;
        EngagePlayer(player.gameObject);
    }
}
