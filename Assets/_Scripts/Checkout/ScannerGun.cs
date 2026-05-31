using UnityEngine;

/// <summary>
/// Súng quét pickupable + smooth follow giống ItemObject.
/// Khi player cầm, PlayerInteract.TryInteract delegate sang TryScan (raycast forward).
/// Right-click khi cầm → ReturnHome (về vị trí ban đầu trên quầy).
/// </summary>
public class ScannerGun : MonoBehaviour, IInteractable
{
    [Header("Scan")]
    [SerializeField] private float _scanRange = 3f;
    [SerializeField] private LayerMask _scanLayer = ~0;
    [SerializeField] private CheckoutCounter _counter;

    [Header("Follow Settings")]
    [SerializeField] private float _positionSmoothTime = 0.08f;
    [SerializeField] private float _rotationSpeed = 10f;

    private Transform _carryTarget;
    private bool _isCarried = false;
    private Vector3 _velocity = Vector3.zero;
    private Rigidbody _rb;
    private PlayerCarry _carryingPlayer;

    // Home state — captured Awake() để ReturnHome reset chuẩn.
    private Transform _homeParent;
    private Vector3 _homeLocalPos;
    private Quaternion _homeLocalRot;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _homeParent = transform.parent;
        _homeLocalPos = transform.localPosition;
        _homeLocalRot = transform.localRotation;
        if (_counter == null) _counter = Object.FindFirstObjectByType<CheckoutCounter>();
    }

    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject != null) return;

        player.CarryObject(this.gameObject);

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

        if (_carryingPlayer != null && _carryingPlayer.HeldObject != this.gameObject)
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

    /// <summary>Reset scanner về vị trí ban đầu trên quầy (gọi từ right-click).</summary>
    public void ReturnHome()
    {
        StopCarry();
        if (_rb != null)
        {
            _rb.isKinematic = true;
            _rb.detectCollisions = true; // raycast lại phải hit được để pick up lần nữa
        }
        if (_homeParent != null) transform.SetParent(_homeParent);
        transform.localPosition = _homeLocalPos;
        transform.localRotation = _homeLocalRot;
    }

    /// <returns>true nếu scan thành công 1 item.</returns>
    public bool TryScan(Vector3 origin, Vector3 direction)
    {
        if (_counter == null || _counter.CurrentSession == null) return false;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, _scanRange, _scanLayer, QueryTriggerInteraction.Ignore))
            return false;

        ItemObject item = hit.collider.GetComponentInParent<ItemObject>();
        if (item == null) return false;

        return _counter.TryScanItem(item);
    }
}
