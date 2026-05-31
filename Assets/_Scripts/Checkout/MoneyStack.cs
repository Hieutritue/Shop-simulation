using UnityEngine;

/// <summary>
/// Một xấp tiền có mệnh giá, pickupable + smooth follow giống ItemObject.
/// Spawn từ CashDrawer; player Interact CheckoutCounter (cầm money) để đưa change.
/// </summary>
public class MoneyStack : MonoBehaviour, IInteractable
{
    [SerializeField] private int _denomination = 1;

    [Header("Follow Settings")]
    [SerializeField] private float _positionSmoothTime = 0.08f;
    [SerializeField] private float _rotationSpeed = 10f;

    public int Denomination => _denomination;

    public void SetDenomination(int v) => _denomination = Mathf.Max(0, v);

    private Transform _carryTarget;
    private bool _isCarried = false;
    private Vector3 _velocity = Vector3.zero;
    private Rigidbody _rb;
    private PlayerCarry _carryingPlayer;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
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

        // Smooth follow: KHÔNG parent — chỉ SmoothDamp world position tới CarryPoint.
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
}
