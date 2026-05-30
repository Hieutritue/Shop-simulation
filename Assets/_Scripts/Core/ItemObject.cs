using UnityEngine;
/// <summary>
/// Gắn vào prefab 3D của vật phẩm khi xuất hiện trong game (trên tay khách, trên kệ, v.v.).
/// Smoothly follows the player's carry point instead of being parented instantly.
/// </summary>
public class ItemObject : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemSO _itemData;

    [Header("Follow Settings")]
    [Tooltip("Time used by SmoothDamp for positional smoothing (smaller = snappier)")]
    [SerializeField] private float _positionSmoothTime = 0.08f;
    [Tooltip("Rotation follow speed multiplier")]
    [SerializeField] private float _rotationSpeed = 10f;

    public ItemSO ItemData => _itemData;

    private Transform _carryTarget;
    private bool _isCarried = false;
    private Vector3 _velocity = Vector3.zero;
    private Rigidbody _rb;
    private PlayerCarry _carryingPlayer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Setup(ItemSO data)
    {
        _itemData = data;
        // Optional: update visuals here.
    }

    public void Interact(PlayerCarry player)
    {
        if (player.HeldObject != null) return;

        // Nếu đang nằm trên kệ → để slot tự release (clear _currentItem, unparent, re-enable physics)
        // trước khi pipeline carry bên dưới ghi đè kinematic/parent.
        var slot = GetComponentInParent<ShelfSlot>();
        if (slot != null && slot.CurrentItem == this)
        {
            slot.TakeItem();
        }

        player.CarryObject(this.gameObject);

        // Do NOT parent the item. Use a target carry transform and smooth towards it.
        _carryTarget = player.CarryPoint;
        _carryingPlayer = player;
        _isCarried = true;

        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
        }

        Debug.Log($"Nhặt {(_itemData != null ? _itemData.itemName : "Item trống")}");
    }

    private void LateUpdate()
    {
        if (_isCarried && _carryTarget != null)
        {
            // Smooth position
            transform.position = Vector3.SmoothDamp(transform.position, _carryTarget.position, ref _velocity, _positionSmoothTime);

            // Smooth rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, _carryTarget.rotation, Time.deltaTime * _rotationSpeed);

            // If the player no longer references this object, stop following (player dropped or cleared)
            if (_carryingPlayer != null && _carryingPlayer.HeldObject != this.gameObject)
            {
                StopBeingCarried();
            }

            // If physics was re-enabled externally (drop logic sets isKinematic = false), stop following
            if (_rb != null && !_rb.isKinematic)
            {
                StopBeingCarried();
            }
        }
    }

    private void StopBeingCarried()
    {
        _isCarried = false;
        _carryTarget = null;
        _carryingPlayer = null;
        _velocity = Vector3.zero;
    }

    // Gọi từ bên ngoài (Shelf, Checkout...) để dừng smooth-follow ngay lập tức
    // khi item được snap vào slot — tránh 1-frame glitch của LateUpdate.
    public void StopCarry()
    {
        StopBeingCarried();
    }
}
