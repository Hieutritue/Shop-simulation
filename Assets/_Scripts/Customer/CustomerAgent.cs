using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine: GoToShelf → PickItem → WaitInQueue → WaitForScanAndPay →
/// (LeaveHappy | AngryLeave) → Done.
/// Customer giữ items trong tay xuyên suốt; scanner gun lấy item khỏi tay.
/// Patience hết → quăng items đang cầm về phía trước rồi bỏ về.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAgent : MonoBehaviour, IInteractable
{
    private enum State { GoToShelf, PickItem, WaitInQueue, WaitForScanAndPay, LeaveHappy, AngryLeave, Done }

    [Header("Behavior")]
    [SerializeField] private float _pickItemDuration = 1.5f;
    [SerializeField] private int _minPickCount = 1;
    [SerializeField] private int _maxPickCount = 1;
    [SerializeField] private float _facingTurnSpeed = 540f;
    [SerializeField] private Transform _hand;
    [SerializeField] private float _handStackOffsetY = 0.15f;

    [Header("Wallet")]
    [Tooltip("Tiền dư khách trả thêm (random 0..max). Player phải đưa change này.")]
    [SerializeField] private int _maxExtraPay = 30;

    [Header("Angry Throw")]
    [SerializeField] private float _throwForce = 12f;
    [SerializeField] private float _throwUpwardBias = 0.4f;

    [Header("Runtime (assigned by spawner)")]
    [SerializeField] private Transform _exitPoint;
    [SerializeField] private CheckoutCounter _checkout;

    private NavMeshAgent _agent;
    private State _state;
    private ShelfController _targetShelf;
    private readonly List<ItemObject> _heldItems = new List<ItemObject>();
    private float _pickTimer;
    private int _queueIndex = -1;
    private bool _hasPaid;

    public List<ItemObject> HeldItems => _heldItems;
    public Transform Hand => _hand != null ? _hand : transform;

    /// <summary>True khi customer này là khách đang phục vụ ở session active của counter.</summary>
    public bool IsActiveInSession =>
        _checkout != null && _checkout.CurrentSession != null && _checkout.CurrentSession.Customer == this;

    // Player cầm MoneyStack click → tween money đến customer rồi destroy + cộng tiền.
    // Reject nếu denom > số tiền thừa còn thiếu.
    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject == null) return;
        if (!player.HeldObject.TryGetComponent<MoneyStack>(out var money)) return;
        if (!IsActiveInSession) return;

        int remaining = _checkout.CurrentSession.ChangeRemaining;
        if (money.Denomination > remaining)
        {
            Debug.Log($"[Customer] Từ chối ${money.Denomination} — chỉ còn thiếu ${remaining}.");
            return;
        }

        int denom = money.Denomination;
        GameObject obj = player.HeldObject;
        player.ClearHeldObject();
        money.StopCarry();

        obj.transform.SetParent(null);
        if (obj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Vector3 end = Hand.position;
        CheckoutSession session = _checkout.CurrentSession;
        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(obj.transform, end, 0.3f, 0.4f))
            .OnComplete(() =>
            {
                session?.RegisterChangeGiven(denom);
                if (obj != null) Destroy(obj);
            });
    }

    public void Init(Transform exitPoint, CheckoutCounter checkout)
    {
        _exitPoint = exitPoint;
        _checkout = checkout;
    }

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        EnterGoToShelf();
    }

    private void Update()
    {
        switch (_state)
        {
            case State.GoToShelf:           TickGoToShelf();           break;
            case State.PickItem:            TickPickItem();            break;
            case State.WaitInQueue:         TickWaitInQueue();         break;
            case State.WaitForScanAndPay:   TickWaitForScanAndPay();   break;
            case State.LeaveHappy:          TickLeaving();             break;
            case State.AngryLeave:          TickLeaving();             break;
        }
    }

    // ───────── Shelf ─────────
    private void EnterGoToShelf()
    {
        _state = State.GoToShelf;
        _targetShelf = FindStockedShelf();
        if (_targetShelf == null) { EnterAngryLeaveSilent(); return; }
        _agent.SetDestination(_targetShelf.transform.position);
    }

    private void TickGoToShelf()
    {
        if (_targetShelf == null || _targetShelf.IsEmpty()) { EnterGoToShelf(); return; }
        if (HasArrived()) EnterPickItem();
    }

    private void EnterPickItem()
    {
        _state = State.PickItem;
        _pickTimer = _pickItemDuration;
        _agent.ResetPath();
    }

    private void TickPickItem()
    {
        _pickTimer -= Time.deltaTime;
        if (_pickTimer > 0f) return;

        int want = Random.Range(_minPickCount, _maxPickCount + 1);
        for (int i = 0; i < want; i++)
        {
            if (_targetShelf == null || _targetShelf.IsEmpty()) break;
            ItemObject taken = _targetShelf.TakeItem();
            if (taken == null) break;
            AttachItemToHand(taken, _heldItems.Count);
            _heldItems.Add(taken);
        }

        if (_heldItems.Count > 0) EnterWaitInQueue();
        else EnterAngryLeaveSilent();
    }

    private void AttachItemToHand(ItemObject item, int stackIndex)
    {
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = true; // scanner cần raycast hit được
        }
        Transform parent = _hand != null ? _hand : transform;
        item.transform.SetParent(parent);
        item.transform.localPosition = Vector3.up * (stackIndex * _handStackOffsetY);
        item.transform.localRotation = Quaternion.identity;
    }

    // ───────── Queue ─────────
    private void EnterWaitInQueue()
    {
        if (_checkout == null) _checkout = Object.FindFirstObjectByType<CheckoutCounter>();
        if (_checkout == null) { EnterAngryLeaveSilent(); return; }

        _state = State.WaitInQueue;
        _queueIndex = _checkout.JoinQueue(this);
        MoveToQueueSpot();
    }

    private void TickWaitInQueue()
    {
        FaceCounter();
        if (_queueIndex != 0 || !HasArrived()) return;
        if (_checkout == null || _checkout.ActiveCustomer != null) return;

        if (_checkout.TryStartSession(this, _heldItems))
        {
            _state = State.WaitForScanAndPay;
            _hasPaid = false;
        }
    }

    private void MoveToQueueSpot()
    {
        Transform spot = _checkout != null ? _checkout.GetQueuePosition(_queueIndex) : null;
        if (spot != null) _agent.SetDestination(spot.position);
    }

    public void OnQueuePositionChanged(int newIndex)
    {
        _queueIndex = newIndex;
        if (_state == State.WaitInQueue) MoveToQueueSpot();
    }

    // ───────── Wait for scan & pay ─────────
    private void TickWaitForScanAndPay()
    {
        FaceCounter();

        var session = _checkout?.CurrentSession;
        if (session == null) return;

        // Items vẫn thuộc về customer (jump qua counter rồi quay về tay sau scan).
        // Trigger pay khi mọi item đã scanned (session.AllScanned dựa trên Unscanned.Count == 0).
        if (!_hasPaid && session.AllScanned)
        {
            int extra = Random.Range(0, _maxExtraPay + 1);
            int payAmount = session.Subtotal + extra;
            session.RegisterPayment(payAmount);
            _hasPaid = true;
        }
        // session.IsComplete → counter sẽ gọi TriggerLeaveHappy() lên customer này.
    }

    // ───────── Leave (happy / angry) ─────────
    public void TriggerLeaveHappy()
    {
        if (_state == State.LeaveHappy || _state == State.AngryLeave || _state == State.Done) return;
        _state = State.LeaveHappy;
        if (_exitPoint != null) _agent.SetDestination(_exitPoint.position);
    }

    public void TriggerAngryLeave()
    {
        if (_state == State.AngryLeave || _state == State.Done) return;
        ThrowHeldItems();
        _state = State.AngryLeave;
        if (_exitPoint != null) _agent.SetDestination(_exitPoint.position);
    }

    private void EnterAngryLeaveSilent()
    {
        // Edge case: không vào được flow checkout (no shelf, no exit) → bỏ về không quăng.
        _state = State.AngryLeave;
        if (_exitPoint != null) _agent.SetDestination(_exitPoint.position);
    }

    private void ThrowHeldItems()
    {
        Vector3 dir = (transform.forward + Vector3.up * _throwUpwardBias).normalized;
        for (int i = 0; i < _heldItems.Count; i++)
        {
            ItemObject item = _heldItems[i];
            if (item == null) continue;

            item.transform.SetParent(null);
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.AddForce(dir * _throwForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * _throwForce, ForceMode.Impulse);
            }
        }
        _heldItems.Clear();
    }

    private void TickLeaving()
    {
        if (!HasArrived()) return;
        // Despawn còn items đang cầm (chỉ happy mới còn — customer đem theo "túi" tượng trưng).
        for (int i = 0; i < _heldItems.Count; i++)
        {
            if (_heldItems[i] != null) Destroy(_heldItems[i].gameObject);
        }
        _heldItems.Clear();
        _state = State.Done;
        Destroy(gameObject);
    }

    // ───────── Helpers ─────────
    private void FaceCounter()
    {
        if (_checkout == null) return;
        Vector3 dir = _checkout.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _facingTurnSpeed * Time.deltaTime);
    }

    private bool HasArrived()
    {
        if (_agent.pathPending) return false;
        return _agent.remainingDistance <= _agent.stoppingDistance + 0.1f;
    }

    private ShelfController FindStockedShelf()
    {
        ShelfController[] shelves = Object.FindObjectsByType<ShelfController>(FindObjectsSortMode.None);
        List<ShelfController> stocked = new List<ShelfController>();
        foreach (var s in shelves)
            if (!s.IsEmpty()) stocked.Add(s);
        if (stocked.Count == 0) return null;
        return stocked[Random.Range(0, stocked.Count)];
    }
}
