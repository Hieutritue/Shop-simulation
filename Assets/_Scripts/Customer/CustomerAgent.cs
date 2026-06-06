using System.Collections.Generic;
using Pathfinding;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Customer FSM theo mô hình 5 trạng thái:
/// SPAWN → MOVING → DECISION → CHECKOUT (WaitInQueue + WaitForScanAndPay) → LEAVING.
///
/// Di chuyển bằng A* Pathfinding Pro (IAstarAI — AIPath/RichAI/FollowerEntity tuỳ component gắn).
/// Mỗi khách được cấp ngẫu nhiên: xác suất mua, số lần kiên nhẫn tìm đồ, sức chịu xếp hàng, tốc độ đi.
///
/// Global rule: khi đang browse (chưa cầm đồ) mà toàn bộ kệ hết sạch → lập tức LEAVING.
/// Customer giữ items trong tay xuyên suốt; scanner gun lấy item khỏi tay khi thanh toán.
/// </summary>
public class CustomerAgent : MonoBehaviour, IInteractable
{
    private enum State { Moving, Decision, WaitInQueue, WaitForScanAndPay, Leaving, Done }

    [Header("Decision Stats (randomized per customer)")]
    [Tooltip("Xác suất chốt đơn khi đứng trước kệ có đồ.")]
    [SerializeField] private Vector2 _buyProbabilityRange = new Vector2(0.5f, 0.9f);
    [Tooltip("Số lần tối đa chịu đổi sang kệ khác khi hết hàng / không ưng.")]
    [SerializeField] private Vector2Int _searchPatienceRange = new Vector2Int(1, 3);
    [Tooltip("Thời gian (giây) tối đa chịu chờ trong hàng trước khi đến lượt.")]
    [SerializeField] private Vector2 _queueToleranceRange = new Vector2(15f, 30f);
    [Tooltip("Tốc độ đi (gán vào IAstarAI.maxSpeed).")]
    [SerializeField] private Vector2 _moveSpeedRange = new Vector2(2.5f, 4f);
    [Tooltip("Thời gian đứng xem đồ trước khi ra quyết định.")]
    [SerializeField] private Vector2 _viewDurationRange = new Vector2(1f, 2f);

    [Header("Behavior")]
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

    private IAstarAI _ai;
    private State _state;
    private ShelfController _targetShelf;
    private ShelfController[] _allShelves;
    private readonly List<ItemObject> _heldItems = new List<ItemObject>();

    // Per-customer randomized stats.
    private float _buyProbability;
    private int _searchPatience;
    private float _queueToleranceTimer;

    private float _viewTimer;
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
        _ai = GetComponent<IAstarAI>();
        if (_ai == null)
            Debug.LogError("[Customer] Thiếu component A* (AIPath/RichAI/FollowerEntity) implement IAstarAI.");
    }

    private void Start()
    {
        EnterSpawn();
    }

    private void Update()
    {
        // Global rule: đang browse (chưa cầm đồ) mà hết sạch hàng → bỏ về ngay.
        if ((_state == State.Moving || _state == State.Decision)
            && _heldItems.Count == 0 && AllShelvesEmpty())
        {
            EnterLeaveSilent();
            return;
        }

        switch (_state)
        {
            case State.Moving:              TickMoving();              break;
            case State.Decision:            TickDecision();            break;
            case State.WaitInQueue:         TickWaitInQueue();         break;
            case State.WaitForScanAndPay:   TickWaitForScanAndPay();   break;
            case State.Leaving:             TickLeaving();             break;
        }
    }

    // ───────── [STATE 1] SPAWN ─────────
    private void EnterSpawn()
    {
        // Cấp ngẫu nhiên các chỉ số ra quyết định.
        _buyProbability = Random.Range(_buyProbabilityRange.x, _buyProbabilityRange.y);
        _searchPatience = Random.Range(_searchPatienceRange.x, _searchPatienceRange.y + 1);
        _queueToleranceTimer = Random.Range(_queueToleranceRange.x, _queueToleranceRange.y);
        if (_ai != null) _ai.maxSpeed = Random.Range(_moveSpeedRange.x, _moveSpeedRange.y);

        _allShelves = Object.FindObjectsByType<ShelfController>(FindObjectsSortMode.None);

        // Chọn ngẫu nhiên 1 kệ có đồ → MOVING.
        _targetShelf = FindStockedShelf(null);
        if (_targetShelf == null) { EnterLeaveSilent(); return; }
        EnterMoving();
    }

    // ───────── [STATE 2] MOVING ─────────
    private void EnterMoving()
    {
        _state = State.Moving;
        SetDestination(_targetShelf.transform.position);
    }

    private void TickMoving()
    {
        if (_targetShelf == null || _targetShelf.IsEmpty())
        {
            // Kệ mục tiêu vừa hết — chọn kệ khác có đồ.
            _targetShelf = FindStockedShelf(_targetShelf);
            if (_targetShelf == null) { EnterLeaveSilent(); return; }
            SetDestination(_targetShelf.transform.position);
            return;
        }

        // Đến sát kệ → đứng xem đồ 1-2s rồi DECISION.
        if (HasArrived()) EnterDecision();
    }

    // ───────── [STATE 3] DECISION ─────────
    private void EnterDecision()
    {
        _state = State.Decision;
        _viewTimer = Random.Range(_viewDurationRange.x, _viewDurationRange.y);
        Stop();
    }

    private void TickDecision()
    {
        _viewTimer -= Time.deltaTime;
        if (_viewTimer > 0f) return;

        bool shelfHasItem = _targetShelf != null && !_targetShelf.IsEmpty();

        // Kệ có đồ VÀ random trúng xác suất mua → nhặt đồ → CHECKOUT.
        if (shelfHasItem && Random.value <= _buyProbability)
        {
            int want = Random.Range(_minPickCount, _maxPickCount + 1);
            for (int i = 0; i < want; i++)
            {
                if (_targetShelf == null || _targetShelf.IsEmpty()) break;
                ItemObject taken = _targetShelf.TakeItem();
                if (taken == null) break;
                AttachItemToHand(taken, _heldItems.Count);
                _heldItems.Add(taken);
            }

            if (_heldItems.Count > 0) { EnterWaitInQueue(); return; }
        }

        // Kệ trống HOẶC random trượt → trừ 1 lần kiên nhẫn.
        _searchPatience--;
        if (_searchPatience > 0)
        {
            ShelfController next = FindStockedShelf(_targetShelf);
            if (next != null) { _targetShelf = next; EnterMoving(); return; }
        }

        // Hết kiên nhẫn (hoặc không còn kệ nào có đồ) → thất vọng, bỏ về.
        EnterLeaveSilent();
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

    // ───────── [STATE 4] CHECKOUT — Queue ─────────
    private void EnterWaitInQueue()
    {
        if (_checkout == null) _checkout = Object.FindFirstObjectByType<CheckoutCounter>();
        if (_checkout == null) { EnterLeaveSilent(); return; }

        _state = State.WaitInQueue;
        _queueIndex = _checkout.JoinQueue(this);
        MoveToQueueSpot();
    }

    private void TickWaitInQueue()
    {
        FaceCounter();

        // Sức chịu xếp hàng giảm dần khi đứng chờ "trước khi đến lượt".
        _queueToleranceTimer -= Time.deltaTime;
        if (_queueToleranceTimer <= 0f) { TriggerAngryLeave(); return; }

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
        if (spot != null) SetDestination(spot.position);
    }

    public void OnQueuePositionChanged(int newIndex)
    {
        _queueIndex = newIndex;
        if (_state == State.WaitInQueue) MoveToQueueSpot();
    }

    // ───────── [STATE 4] CHECKOUT — Wait for scan & pay ─────────
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

    // ───────── [STATE 5] LEAVING ─────────
    public void TriggerLeaveHappy()
    {
        if (_state == State.Leaving || _state == State.Done) return;
        EnterLeaving();
    }

    public void TriggerAngryLeave()
    {
        if (_state == State.Leaving || _state == State.Done) return;
        ThrowHeldItems();
        EnterLeaving();
    }

    private void EnterLeaveSilent()
    {
        // Browse-leave: chưa cầm đồ (hết hàng / thất vọng) → bỏ về không quăng.
        EnterLeaving();
    }

    private void EnterLeaving()
    {
        _state = State.Leaving;
        if (_exitPoint != null) SetDestination(_exitPoint.position);
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
    private void SetDestination(Vector3 pos)
    {
        if (_ai == null) return;
        _ai.isStopped = false;
        _ai.destination = pos;
        _ai.SearchPath();
    }

    private void Stop()
    {
        if (_ai == null) return;
        _ai.isStopped = true;
    }

    private bool HasArrived()
    {
        if (_ai == null) return true;
        if (_ai.pathPending || !_ai.hasPath) return false; // chưa có path → chưa thể "đã tới"
        return _ai.reachedEndOfPath;
    }

    private void FaceCounter()
    {
        if (_checkout == null) return;
        Vector3 dir = _checkout.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _facingTurnSpeed * Time.deltaTime);
    }

    private bool AllShelvesEmpty()
    {
        if (_allShelves == null) return false;
        foreach (var s in _allShelves)
            if (s != null && !s.IsEmpty()) return false;
        return true;
    }

    private ShelfController FindStockedShelf(ShelfController exclude)
    {
        if (_allShelves == null) return null;
        List<ShelfController> stocked = new List<ShelfController>();
        foreach (var s in _allShelves)
            if (s != null && s != exclude && !s.IsEmpty()) stocked.Add(s);

        // Nếu chỉ còn đúng kệ exclude có đồ thì vẫn cho phép chọn lại nó.
        if (stocked.Count == 0 && exclude != null && !exclude.IsEmpty()) return exclude;
        if (stocked.Count == 0) return null;
        return stocked[Random.Range(0, stocked.Count)];
    }
}
