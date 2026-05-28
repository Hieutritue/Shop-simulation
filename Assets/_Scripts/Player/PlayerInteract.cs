// csharp
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerCarry))]
public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float _interactRadius = 2f;
    [SerializeField] private LayerMask _interactLayer;
    [SerializeField] private float _raycastInterval = 0.1f;

    private PlayerCarry _playerCarry;
    private float _raycastTimer = 0f;

    // Exposed read-only flag for other systems (HUD) to read
    public bool IsLookingAtInteractable { get; private set; }

    private void Awake()
    {
        _playerCarry = GetComponent<PlayerCarry>();
    }

    private void Update()
    {
        // Periodic raycast check
        _raycastTimer += Time.deltaTime;
        if (_raycastTimer >= _raycastInterval)
        {
            _raycastTimer = 0f;
            CheckForInteractableRaycast();
        }

        // Detect interact input
        bool interactInputPressed = false;

        interactInputPressed = Input.GetKeyDown(KeyCode.Mouse0);

        if (interactInputPressed)
        {
            TryInteract();
        }
    }

    private void CheckForInteractableRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                IsLookingAtInteractable = true;
                Debug.Log($"Interactable detected: {interactable.GetType().Name} at distance {hit.distance:F2}");
                return;
            }
        }

        IsLookingAtInteractable = false;
        Debug.Log("No interactable detected");
    }

    private void TryInteract()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _interactRadius, _interactLayer, QueryTriggerInteraction.Ignore))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact(_playerCarry);
                return;
            }
        }

        // Bỏ đồ xuống đất nếu không tương tác với cái gì
        if (_playerCarry.HeldObject != null)
        {
            DropHeldObject();
        }
    }

    private void DropHeldObject()
    {
        GameObject heldObj = _playerCarry.HeldObject;
        _playerCarry.ClearHeldObject();
        
        heldObj.transform.SetParent(null);
        // Đặt xuống phía trước người chơi một chút
        heldObj.transform.position = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;
        
        if (heldObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }
        
        Debug.Log("Bỏ đồ xuống đất.");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw interaction ray in editor for easy debugging
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * _interactRadius);
        Gizmos.DrawWireSphere(transform.position + transform.forward * _interactRadius, 0.05f);
    }
}
