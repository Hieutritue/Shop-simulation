using UnityEngine;

/// <summary>
/// Súng quét: pickupable. Khi player đang cầm, PlayerInteract.TryInteract sẽ delegate sang TryScan
/// (raycast forward; nếu trúng ItemObject thuộc CheckoutSession active → session.TryScan).
/// </summary>
public class ScannerGun : MonoBehaviour, IInteractable
{
    [SerializeField] private float _scanRange = 3f;
    [SerializeField] private LayerMask _scanLayer = ~0;
    [SerializeField] private CheckoutCounter _counter;

    private void Awake()
    {
        if (_counter == null) _counter = Object.FindFirstObjectByType<CheckoutCounter>();
    }

    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject != null) return;

        player.CarryObject(this.gameObject);

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Transform anchor = player.CarryPoint != null ? player.CarryPoint : player.transform;
        transform.SetParent(anchor);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
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
