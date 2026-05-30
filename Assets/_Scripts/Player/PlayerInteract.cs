using UnityEngine;

[RequireComponent(typeof(PlayerCarry))]
public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float _interactRadius = 2f;
    [SerializeField] private LayerMask _interactLayer;
    [SerializeField] private float _raycastInterval = 0.1f;

    private PlayerCarry _playerCarry;
    private float _raycastTimer = 0f;

    /// <summary>Interactable hiện player đang nhìn (raycast forward). Null nếu không có.</summary>
    public IInteractable CurrentInteractable { get; private set; }

    /// <summary>Tên item phía trước (cho UI prompt). Null nếu target không phải item / shelf trống.</summary>
    public string CurrentItemName { get; private set; }

    public bool IsLookingAtInteractable => CurrentInteractable != null;

    private void Awake()
    {
        _playerCarry = GetComponent<PlayerCarry>();
    }

    private void Update()
    {
        _raycastTimer += Time.deltaTime;
        if (_raycastTimer >= _raycastInterval)
        {
            _raycastTimer = 0f;
            CheckForInteractableRaycast();
        }

        // Chuột trái: tương tác. Chuột phải: drop.
        if (Input.GetMouseButtonDown(0)) TryInteract();
        if (Input.GetMouseButtonDown(1)) TryDrop();
    }

    private void CheckForInteractableRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (CanInteractWith(interactable))
            {
                CurrentInteractable = interactable;
                CurrentItemName = ResolveItemName(interactable);
                return;
            }
        }

        CurrentInteractable = null;
        CurrentItemName = null;
    }

    /// <summary>Khi đang cầm đồ, chỉ Shelf là target hợp lệ (đặt đồ). Khác (Checkout, item khác) bị filter.</summary>
    private bool CanInteractWith(IInteractable target)
    {
        if (target == null) return false;
        if (_playerCarry.HeldObject != null) return target is ShelfController;
        return true;
    }

    /// <summary>Lấy tên item để show trên PickupGuide. Trả null nếu target không có item.</summary>
    private static string ResolveItemName(IInteractable target)
    {
        if (target is ItemObject item)
        {
            return item.ItemData != null ? item.ItemData.itemName : null;
        }
        if (target is ShelfController shelf && shelf.Slots != null)
        {
            // Lấy tên item ở slot occupied đầu tiên (theo thứ tự take của shelf: cuối → đầu).
            for (int i = shelf.Slots.Length - 1; i >= 0; i--)
            {
                var s = shelf.Slots[i];
                if (s != null && s.IsOccupied && s.CurrentItem != null && s.CurrentItem.ItemData != null)
                    return s.CurrentItem.ItemData.itemName;
            }
        }
        return null;
    }

    private void TryInteract()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (CanInteractWith(interactable)) interactable.Interact(_playerCarry);
        }
    }

    private void TryDrop()
    {
        if (_playerCarry.HeldObject == null) return;
        DropHeldObject();
    }

    private void DropHeldObject()
    {
        GameObject heldObj = _playerCarry.HeldObject;
        _playerCarry.ClearHeldObject();

        heldObj.transform.SetParent(null);
        heldObj.transform.position = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

        if (heldObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _interactRadius);
        Gizmos.DrawWireSphere(transform.position + transform.forward * _interactRadius, 0.05f);
    }
}
