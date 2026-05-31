using System.Collections.Generic;
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

    [Header("Queue")]
    [SerializeField] private Transform[] _queuePositions;

    [Header("Zones")]
    [Tooltip("Slot đặt đồ đã scan (visual). Tween từ tay customer → đây.")]
    [SerializeField] private Transform[] _scannedZone;
    [SerializeField] private Transform _changeZone;

    [Header("Session")]
    [SerializeField] private float _defaultPatience = 30f;

    private readonly List<CustomerAgent> _queue = new List<CustomerAgent>();
    private int _scannedSlotCursor;

    public CheckoutSession CurrentSession { get; private set; }
    public CustomerAgent ActiveCustomer => CurrentSession?.Customer;
    public int QueueLength => _queue.Count;

    private void Update()
    {
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
        return true;
    }

    /// <summary>Scanner gọi qua đây — tween item về ScannedZone nếu scan thành công.</summary>
    public bool TryScanItem(ItemObject item)
    {
        if (CurrentSession == null) return false;
        if (!CurrentSession.TryScan(item)) return false;
        MoveItemToScannedZone(item);
        return true;
    }

    private void MoveItemToScannedZone(ItemObject item)
    {
        if (item == null) return;

        Transform target = NextScannedSlot();
        Vector3 end = target != null ? target.position : transform.position;

        item.transform.SetParent(null);
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(item.transform, end, SCAN_TWEEN_DURATION, SCAN_TWEEN_HEIGHT))
            .OnComplete(() =>
            {
                if (item == null) return;
                if (target != null)
                {
                    item.transform.SetParent(target);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                }
            });
    }

    private Transform NextScannedSlot()
    {
        if (_scannedZone == null || _scannedZone.Length == 0) return null;
        Transform t = _scannedZone[_scannedSlotCursor % _scannedZone.Length];
        _scannedSlotCursor++;
        return t;
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
    }

    // ───────── Player IInteractable: chỉ chấp nhận đặt MoneyStack vào ChangeZone ─────────
    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject == null) return;
        if (!player.HeldObject.TryGetComponent<MoneyStack>(out var money)) return;
        if (CurrentSession == null) return;

        CurrentSession.RegisterChangeGiven(money.Denomination);

        GameObject obj = player.HeldObject;
        player.ClearHeldObject();
        obj.transform.SetParent(_changeZone != null ? _changeZone : transform);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        // Trả change về drawer (visually) — destroy sau short delay.
        Destroy(obj, 0.8f);
    }
}
