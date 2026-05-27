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

    public void Interact(PlayerCarry player)
    {
        if (player == null) return;

        // Check if player is holding an item
        if (player.HeldObject != null && player.HeldObject.TryGetComponent<ItemObject>(out var itemToPlace))
        {
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
        // If player is not holding anything, try to take an item from the shelf
        else if (player.HeldObject == null)
        {
            ItemObject takenItem = TakeItem();
            if (takenItem != null)
            {
                // Tái sử dụng pipeline carry chuẩn của ItemObject (smooth-follow CarryPoint).
                takenItem.Interact(player);
                Debug.Log($"Took {takenItem.ItemData?.itemName ?? "item"} from shelf.");
            }
            else
            {
                Debug.Log("Shelf is empty!");
            }
        }
    }
}
