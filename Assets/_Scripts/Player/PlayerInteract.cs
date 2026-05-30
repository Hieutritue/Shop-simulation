using UnityEngine;

[RequireComponent(typeof(PlayerCarry))]
public class PlayerInteract : MonoBehaviour
{
    private const string LAYER_INTERACTABLE = "Interactable";
    private const string LAYER_INTERACTABLE_OUTLINED = "InteractableOutlined";

    [Header("Interaction Settings")]
    [SerializeField] private float _interactRadius = 2f;
    [Tooltip("Phải include cả 'Interactable' và 'InteractableOutlined' — vì layer bị đổi khi highlight, raycast vẫn cần nhận diện.")]
    [SerializeField] private LayerMask _interactLayer;
    [SerializeField] private float _raycastInterval = 0.1f;

    private PlayerCarry _playerCarry;
    private float _raycastTimer = 0f;

    private int _layerInteractable = -1;
    private int _layerInteractableOutlined = -1;
    private GameObject _highlightedRoot;

    /// <summary>Interactable hiện player đang nhìn (raycast forward). Null nếu không có.</summary>
    public IInteractable CurrentInteractable { get; private set; }

    /// <summary>Tên item phía trước (cho UI prompt). Null nếu target không phải item / shelf trống.</summary>
    public string CurrentItemName { get; private set; }

    public bool IsLookingAtInteractable => CurrentInteractable != null;

    private void Awake()
    {
        _playerCarry = GetComponent<PlayerCarry>();
        _layerInteractable = LayerMask.NameToLayer(LAYER_INTERACTABLE);
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
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (CanInteractWith(interactable))
            {
                CurrentInteractable = interactable;
                CurrentItemName = ResolveItemName(interactable);
                SetHighlight((interactable as MonoBehaviour)?.gameObject);
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
        if (root == null || _layerInteractable < 0 || _layerInteractableOutlined < 0) return;
        SwapLayerRecursive(root.transform, _layerInteractable, _layerInteractableOutlined);
        _highlightedRoot = root;
    }

    private void ClearHighlight()
    {
        if (_highlightedRoot == null) return;
        if (_layerInteractable >= 0 && _layerInteractableOutlined >= 0)
        {
            SwapLayerRecursive(_highlightedRoot.transform, _layerInteractableOutlined, _layerInteractable);
        }
        _highlightedRoot = null;
    }

    // Chỉ đổi những GameObject đang ở fromLayer — children ở layer khác (Default, UI, ...) không bị động vào.
    private static void SwapLayerRecursive(Transform t, int fromLayer, int toLayer)
    {
        if (t.gameObject.layer == fromLayer) t.gameObject.layer = toLayer;
        for (int i = 0; i < t.childCount; i++)
        {
            SwapLayerRecursive(t.GetChild(i), fromLayer, toLayer);
        }
    }

    /// <summary>
    /// Cầm đồ: chỉ Shelf còn slot trống (để đặt). Không cầm: mọi target trừ Shelf
    /// (lấy đồ phải nhắm trực tiếp vào ItemObject, không qua shelf).
    /// </summary>
    private bool CanInteractWith(IInteractable target)
    {
        if (target == null) return false;
        if (_playerCarry.HeldObject != null)
        {
            return target is ShelfController shelf && !shelf.IsFull();
        }
        return !(target is ShelfController);
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
