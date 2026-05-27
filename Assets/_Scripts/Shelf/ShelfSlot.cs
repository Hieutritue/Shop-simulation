using UnityEngine;

public class ShelfSlot : MonoBehaviour
{
    [SerializeField] private Transform _placementPoint;
    private ItemObject _currentItem;

    public ItemObject CurrentItem => _currentItem;
    public bool IsOccupied => _currentItem != null;

    private void Awake()
    {
        if (_placementPoint == null)
        {
            _placementPoint = this.transform;
        }
    }

    public bool PlaceItem(ItemObject item)
    {
        if (IsOccupied || item == null) return false;

        _currentItem = item;

        // Dừng smooth-follow trước khi snap, tránh LateUpdate kéo item về CarryPoint 1 frame nữa.
        item.StopCarry();

        // Disable physics while on shelf
        if (item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false; // Optional, to prevent player pushing it off
        }

        // Parent and snap to position
        item.transform.SetParent(_placementPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        return true;
    }

    public ItemObject TakeItem()
    {
        if (!IsOccupied) return null;

        ItemObject takenItem = _currentItem;
        _currentItem = null;

        // Unparent để không chịu transform của slot khi follow theo player.
        takenItem.transform.SetParent(null);

        // Re-enable physics tạm thời; ItemObject.Interact() bên ngoài sẽ set kinematic lại nếu được carry.
        if (takenItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        return takenItem;
    }
}
