using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

[RequireComponent(typeof(PlayerCarry))]
public class PlayerInteract : MonoBehaviour
{
    private const string LAYER_INTERACTABLE_OUTLINED = "InteractableOutlined";

    [Header("Interaction Settings")]
    [SerializeField] private float _interactRadius = 2f;
    [Tooltip("Phải include cả 'Interactable' và 'InteractableOutlined' — vì layer bị đổi khi highlight, raycast vẫn cần nhận diện.")]
    [SerializeField] private LayerMask _interactLayer;
    [SerializeField] private float _raycastInterval = 0.1f;
    [SerializeField] private Transform _cameraTransform;

    private PlayerCarry _playerCarry;
    private float _raycastTimer = 0f;

    private int _layerInteractableOutlined = -1;
    private GameObject _highlightedRoot;
    // Cache layer gốc của mọi descendant khi highlight → restore chính xác lúc clear,
    // hoạt động bất kể prefab có gắn renderer ở child layer Default hay Interactable.
    private readonly List<KeyValuePair<Transform, int>> _highlightCache = new List<KeyValuePair<Transform, int>>();
    private GameObject _debugLastHitObject;

    /// <summary>Interactable hiện player đang nhìn (raycast forward). Null nếu không có.</summary>
    public IInteractable CurrentInteractable { get; private set; }

    /// <summary>Tên item phía trước (cho UI prompt). Null nếu target không phải item / shelf trống.</summary>
    public string CurrentItemName { get; private set; }

    public bool IsLookingAtInteractable => CurrentInteractable != null;

    private void Awake()
    {
        _playerCarry = GetComponent<PlayerCarry>();
        _layerInteractableOutlined = LayerMask.NameToLayer(LAYER_INTERACTABLE_OUTLINED);
    }

    private void OnDisable()
    {
        ClearHighlight();
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
        Ray ray = new Ray(_cameraTransform.position, _cameraTransform .forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (CanInteractWith(interactable))
            {
                CurrentInteractable = interactable;
                CurrentItemName = ResolveItemName(interactable);
                SetHighlight((interactable as MonoBehaviour)?.gameObject);
                Debug.Log($"[PlayerInteract] Nhìn thấy interactable: {(interactable as MonoBehaviour).gameObject.name}");
                return;
            }
        }

        CurrentInteractable = null;
        CurrentItemName = null;
        ClearHighlight();
    }

    private void SetHighlight(GameObject root)
    {
        if (_highlightedRoot == root) return;
        ClearHighlight();
        if (root == null || _layerInteractableOutlined < 0) return;
        CacheAndApply(root.transform);
        _highlightedRoot = root;
    }

    private void ClearHighlight()
    {
        if (_highlightedRoot == null) return;
        for (int i = 0; i < _highlightCache.Count; i++)
        {
            var kvp = _highlightCache[i];
            if (kvp.Key != null) kvp.Key.gameObject.layer = kvp.Value;
        }
        _highlightCache.Clear();
        _highlightedRoot = null;
    }

    private void CacheAndApply(Transform t)
    {
        _highlightCache.Add(new KeyValuePair<Transform, int>(t, t.gameObject.layer));
        t.gameObject.layer = _layerInteractableOutlined;
        for (int i = 0; i < t.childCount; i++)
        {
            CacheAndApply(t.GetChild(i));
        }
    }

    /// <summary>
    /// Cầm ItemObject thường → chỉ Shelf còn slot.
    /// Cầm MoneyStack → chỉ Customer đang active (đưa tiền).
    /// Cầm ScannerGun → chỉ ItemObject trong tay Customer đang active (highlight để scan).
    /// Không cầm: pickup được mọi target trừ Shelf/Counter/Customer/đồ-trong-tay-customer.
    /// </summary>
    private bool CanInteractWith(IInteractable target)
    {
        if (target == null) return false;

        GameObject held = _playerCarry.HeldObject;
        if (held != null)
        {
            if (held.GetComponent<ScannerGun>() != null)
            {
                // Highlight item trong tay customer đang phục vụ (scan target).
                if (target is ItemObject scanItem)
                {
                    var owner = scanItem.GetComponentInParent<CustomerAgent>();
                    return owner != null && owner.IsActiveInSession;
                }
                return false;
            }

            if (held.GetComponent<MoneyStack>() != null)
            {
                return target is CustomerAgent customer && customer.IsActiveInSession;
            }
            if (target is ShelfController shelf) return !shelf.IsFull();
            return false;
        }

        if (target is ShelfController) return false;
        if (target is CheckoutCounter counter) return counter.CurrentSession != null;
        if (target is CustomerAgent) return false;

        if (target is ItemObject item && item.GetComponentInParent<CustomerAgent>() != null) return false;

        return true;
    }

    /// <summary>Lấy tên item để show trên PickupGuide. Trả null nếu target không phải item.</summary>
    private static string ResolveItemName(IInteractable target)
    {
        if (target is ItemObject item)
        {
            return item.ItemData != null ? item.ItemData.itemName : null;
        }
        return null;
    }

    private void TryInteract()
    {
        // Cầm scanner: ưu tiên scan thay vì interact với target.
        if (_playerCarry.HeldObject != null &&
            _playerCarry.HeldObject.TryGetComponent<ScannerGun>(out var scanner))
        {
            scanner.TryScan(_cameraTransform.position, _cameraTransform.forward);
            return;
        }

        Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (CanInteractWith(interactable)) interactable.Interact(_playerCarry);
        }
    }

    private void TryDrop()
    {
        if (_playerCarry.HeldObject == null) return;

        GameObject held = _playerCarry.HeldObject;

        // MoneyStack → hủy luôn (trả về drawer ngầm).
        if (held.GetComponent<MoneyStack>() != null)
        {
            _playerCarry.ClearHeldObject();
            Destroy(held);
            return;
        }

        // ScannerGun → reset về home position trên quầy.
        if (held.TryGetComponent<ScannerGun>(out var scanner))
        {
            _playerCarry.ClearHeldObject();
            scanner.ReturnHome();
            return;
        }

        DropHeldObject();
    }

    private const float DROP_ARC_DURATION = 0.1f; // mỗi nửa cung → tổng ~0.2s
    private const float DROP_ARC_HEIGHT = 0.4f;

    private void DropHeldObject()
    {
        GameObject heldObj = _playerCarry.HeldObject;
        _playerCarry.ClearHeldObject();

        if (heldObj.TryGetComponent<ItemObject>(out var item)) item.StopCarry();
        heldObj.transform.SetParent(null);

        Vector3 start = heldObj.transform.position;
        Vector3 end = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
        Vector3 apex = (start + end) * 0.5f + Vector3.up * DROP_ARC_HEIGHT;

        // Kinematic + tắt collision trong tween → tránh đẩy player. Bật physics khi chạm đất.
        Rigidbody rb = heldObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Sequence.Create()
            .Chain(Tween.Position(heldObj.transform, apex, DROP_ARC_DURATION, Ease.OutQuad))
            .Chain(Tween.Position(heldObj.transform, end, DROP_ARC_DURATION, Ease.InQuad))
            .OnComplete(() =>
            {
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.detectCollisions = true;
                }
            });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _interactRadius);
        Gizmos.DrawWireSphere(transform.position + transform.forward * _interactRadius, 0.05f);
    }
}
