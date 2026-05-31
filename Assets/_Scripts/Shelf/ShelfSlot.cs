using PrimeTween;
using UnityEngine;

public class ShelfSlot : MonoBehaviour
{
    private const float ARC_DURATION = 0.3f; // mỗi nửa cung → tổng ~0.2s
    private const float ARC_HEIGHT = 0.8f;

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

        // Dừng smooth-follow trước khi tween, tránh LateUpdate kéo item về CarryPoint.
        item.StopCarry();

        // Tắt collision trong lúc tween để item không hích player bay nhẹ.
        // Bật lại detectCollisions trong FinalizePlacement → raycast lấy đồ vẫn thấy.
        Rigidbody rb = null;
        if (item.TryGetComponent(out rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // Vòng cung: tay player → apex (giữa + lên cao) → slot.
        Vector3 start = item.transform.position;
        Vector3 end = _placementPoint.position;
        Vector3 apex = (start + end) * 0.5f + Vector3.up * ARC_HEIGHT;

        Sequence.Create()
            .Chain(PrimeTweenExtensions.Jump(item.transform,end,ARC_DURATION,ARC_HEIGHT))
            .OnComplete(() => FinalizePlacement(item, rb));

        return true;
    }

    private void FinalizePlacement(ItemObject item, Rigidbody rb)
    {
        // Slot có thể đã bị TakeItem trong lúc tween → bỏ qua để không lừa state.
        if (item == null || _currentItem != item) return;

        item.transform.SetParent(_placementPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        if (rb != null) rb.detectCollisions = true;
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
