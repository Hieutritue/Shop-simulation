using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// State machine: GoToShelf → PickItem → WaitInQueue (tại Checkout) → GoToExit → Despawn.
/// Nếu không có checkout hoặc không có hàng, đi thẳng ra exit.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class CustomerAgent : MonoBehaviour
{
    private enum State { GoToShelf, PickItem, WaitInQueue, GoToExit, Done }

    [Header("Behavior")]
    [SerializeField] private float _pickItemDuration = 1.5f;
    [Tooltip("Tốc độ xoay (deg/s) khi đứng xếp hàng — quay mặt về phía counter.")]
    [SerializeField] private float _facingTurnSpeed = 540f;
    [SerializeField] private Transform _hand;

    [Header("Runtime (assigned by spawner)")]
    [SerializeField] private Transform _exitPoint;
    [SerializeField] private CheckoutCounter _checkout;

    private NavMeshAgent _agent;
    private State _state;
    private ShelfController _targetShelf;
    private ItemObject _carriedItem;
    private float _pickTimer;
    private int _queueIndex = -1;

    /// <summary>True khi khách đang ở đầu hàng và đã đứng vào vị trí.</summary>
    public bool IsReadyToBeServed
        => _state == State.WaitInQueue && _queueIndex == 0 && HasArrived();

    public int GetCarriedItemPrice()
    {
        if (_carriedItem == null || _carriedItem.ItemData == null) return 0;
        return Mathf.RoundToInt(_carriedItem.ItemData.sellPrice);
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
            case State.GoToShelf:    TickGoToShelf();    break;
            case State.PickItem:     TickPickItem();     break;
            case State.WaitInQueue:  TickWaitInQueue();  break;
            case State.GoToExit:     TickGoToExit();     break;
        }
    }

    /// <summary>Khi đứng yên trong hàng → xoay mặt về phía counter.</summary>
    private void TickWaitInQueue()
    {
        if (!HasArrived() || _checkout == null) return;

        Vector3 dir = _checkout.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _facingTurnSpeed * Time.deltaTime);
    }

    // ───────── Shelf ─────────
    private void EnterGoToShelf()
    {
        _state = State.GoToShelf;
        _targetShelf = FindStockedShelf();
        if (_targetShelf == null) { EnterGoToExit(); return; }
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

        ItemObject taken = _targetShelf != null ? _targetShelf.TakeItem() : null;
        if (taken != null)
        {
            _carriedItem = taken;
            AttachItemToHand(taken);
            EnterWaitInQueue();
        }
        else
        {
            EnterGoToExit();
        }
    }

    private void AttachItemToHand(ItemObject item)
    {
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
        Transform parent = _hand != null ? _hand : transform;
        item.transform.SetParent(parent);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;
    }

    // ───────── Queue ─────────
    private void EnterWaitInQueue()
    {
        if (_checkout == null) _checkout = Object.FindFirstObjectByType<CheckoutCounter>();
        if (_checkout == null) { EnterGoToExit(); return; }

        _state = State.WaitInQueue;
        _queueIndex = _checkout.JoinQueue(this);
        MoveToQueueSpot();
    }

    private void MoveToQueueSpot()
    {
        Transform spot = _checkout != null ? _checkout.GetQueuePosition(_queueIndex) : null;
        if (spot != null) _agent.SetDestination(spot.position);
    }

    /// <summary>Gọi từ CheckoutCounter khi hàng dịch lên (khách trước rời).</summary>
    public void OnQueuePositionChanged(int newIndex)
    {
        _queueIndex = newIndex;
        if (_state == State.WaitInQueue) MoveToQueueSpot();
    }

    /// <summary>Gọi từ CheckoutCounter sau khi player scan xong.</summary>
    public void OnServed()
    {
        if (_carriedItem != null)
        {
            Destroy(_carriedItem.gameObject);
            _carriedItem = null;
        }
        _queueIndex = -1;
        EnterGoToExit();
    }

    // ───────── Exit ─────────
    private void EnterGoToExit()
    {
        _state = State.GoToExit;
        if (_exitPoint != null) _agent.SetDestination(_exitPoint.position);
    }

    private void TickGoToExit()
    {
        if (!HasArrived()) return;
        if (_carriedItem != null) Destroy(_carriedItem.gameObject);
        _state = State.Done;
        Destroy(gameObject);
    }

    // ───────── Helpers ─────────
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
