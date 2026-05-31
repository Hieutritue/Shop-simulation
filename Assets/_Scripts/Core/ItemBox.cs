using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Hộp carton chứa tối đa 6 ItemObject. Pickupable + smooth follow như ItemObject.
/// Player cầm box → click item: tween jump vào slot trong hộp (no physics);
/// click shelf: xếp 1 item lên shelf; right-click: thả hộp; F: ném items ra random forward.
/// </summary>
public class ItemBox : MonoBehaviour, IInteractable
{
    private const int MAX_CAPACITY = 6;
    private const float ADD_TWEEN_DURATION = 0.25f;
    private const float ADD_TWEEN_HEIGHT = 0.5f;
    private const float THROW_FORCE = 4f;
    private const float THROW_UPWARD = 0.4f;

    [Header("Slots trong hộp (max 6)")]
    [Tooltip("Vị trí xếp items trong hộp — Transform markers.")]
    [SerializeField] private Transform[] _slots;

    [Header("Follow settings")]
    [SerializeField] private float _positionSmoothTime = 0.08f;
    [SerializeField] private float _rotationSpeed = 10f;

    private readonly List<ItemObject> _items = new List<ItemObject>();
    private Rigidbody _rb;
    private Transform _carryTarget;
    private bool _isCarried;
    private Vector3 _velocity;
    private PlayerCarry _carryingPlayer;

    public int ItemCount => _items.Count;
    public bool HasItems => _items.Count > 0;
    public bool IsFull => _items.Count >= MAX_CAPACITY;
    public int MaxCapacity => MAX_CAPACITY;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject != null) return;

        player.CarryObject(gameObject);
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
        }
        _carryTarget = player.CarryPoint;
        _carryingPlayer = player;
        _isCarried = true;
        _velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (!_isCarried || _carryTarget == null) return;

        transform.position = Vector3.SmoothDamp(transform.position, _carryTarget.position, ref _velocity, _positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, _carryTarget.rotation, Time.deltaTime * _rotationSpeed);

        if (_carryingPlayer != null && _carryingPlayer.HeldObject != gameObject)
        {
            StopCarry();
        }
    }

    public void StopCarry()
    {
        _isCarried = false;
        _carryTarget = null;
        _carryingPlayer = null;
        _velocity = Vector3.zero;
    }

    /// <summary>Gom 1 ItemObject vào box bằng tween jump (no physics).</summary>
    public bool TryAddItem(ItemObject item)
    {
        if (item == null || IsFull) return false;
        if (_items.Contains(item)) return false;

        item.StopCarry();

        Transform slot = (_slots != null && _items.Count < _slots.Length)
            ? _slots[_items.Count] : transform;

        // Reserve trước để IsFull/Index đúng nếu có thêm click trong lúc tween.
        _items.Add(item);

        item.transform.SetParent(null);
        Rigidbody rb = null;
        if (item.TryGetComponent(out rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false; // no physics trong box
        }

        Vector3 end = slot.position;
        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(item.transform, end, ADD_TWEEN_DURATION, ADD_TWEEN_HEIGHT))
            .OnComplete(() =>
            {
                if (item == null || slot == null) return;
                item.transform.SetParent(slot);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            });

        return true;
    }

    /// <summary>Xếp 1 item từ box lên shelf (tween qua ShelfSlot.PlaceItem).</summary>
    public bool PlaceOneItemToShelf(ShelfController shelf)
    {
        if (shelf == null || _items.Count == 0) return false;
        if (shelf.IsFull()) return false;

        int idx = _items.Count - 1;
        ItemObject item = _items[idx];
        if (item == null)
        {
            _items.RemoveAt(idx);
            return false;
        }

        item.transform.SetParent(null);
        if (shelf.AddItem(item))
        {
            _items.RemoveAt(idx);
            return true;
        }
        // Fail — re-attach back to box.
        TryAddItem(item);
        return false;
    }

    /// <summary>Ném tất cả items ra random forward (F key).</summary>
    public void ThrowAllItems()
    {
        if (_items.Count == 0) return;

        Vector3 forward = _carryingPlayer != null
            ? _carryingPlayer.transform.forward
            : transform.forward;

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            ItemObject item = _items[i];
            if (item == null) continue;

            item.transform.SetParent(null);
            if (item.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                Vector3 spread = Random.insideUnitSphere * 0.6f;
                Vector3 dir = (forward + spread + Vector3.up * THROW_UPWARD).normalized;
                rb.AddForce(dir * THROW_FORCE, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * THROW_FORCE, ForceMode.Impulse);
            }
        }
        _items.Clear();
    }
}
