using UnityEngine;

public class ShelfController : MonoBehaviour, IInteractable
{
    [SerializeField] private ShelfSlot[] _slots;

    public ShelfSlot[] Slots => _slots;

    private void Awake()
    {
        // Automatically find slots in children if not assigned in Inspector
        if (_slots == null || _slots.Length == 0)
        {
            _slots = GetComponentsInChildren<ShelfSlot>();
        }
    }

    public bool IsEmpty()
    {
        foreach (var slot in _slots)
        {
            if (slot.IsOccupied) return false;
        }
        return true;
    }

    public bool IsFull()
    {
        foreach (var slot in _slots)
        {
            if (!slot.IsOccupied) return false;
        }
        return true;
    }

    public bool AddItem(ItemObject item)
    {
        if (item == null) return false;

        // Find the first empty slot
        foreach (var slot in _slots)
        {
            if (!slot.IsOccupied)
            {
                return slot.PlaceItem(item);
            }
        }
        return false;
    }

    public ItemObject TakeItem()
    {
        // Take item from the last occupied slot (stack behavior) or first occupied slot
        // Let's go backwards to take from top/end slot first, or just first occupied slot
        for (int i = _slots.Length - 1; i >= 0; i--)
        {
            if (_slots[i].IsOccupied)
            {
                return _slots[i].TakeItem();
            }
        }
        return null;
    }

    // Shelf chỉ phục vụ thao tác ĐẶT đồ. Lấy đồ → player nhắm trực tiếp vào ItemObject.
    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject == null) return;

        // Cầm ItemBox → xếp lần lượt 1 item từ box lên shelf mỗi click.
        if (player.HeldObject.TryGetComponent<ItemBox>(out var box))
        {
            box.PlaceOneItemToShelf(this);
            return;
        }

        if (!player.HeldObject.TryGetComponent<ItemObject>(out var itemToPlace)) return;

        if (AddItem(itemToPlace))
        {
            player.ClearHeldObject();
            Debug.Log($"Placed {itemToPlace.ItemData?.itemName ?? "item"} onto shelf.");
        }
        else
        {
            Debug.Log("Shelf is full!");
        }
    }
}
