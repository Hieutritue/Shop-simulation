using System.Collections.Generic;
using UnityEngine;

public class ItemBox : MonoBehaviour, IInteractable
{
    [SerializeField] private int _maxCapacity = 10;
    private List<ItemObject> _items = new List<ItemObject>();

    public bool IsFull => _items.Count >= _maxCapacity;
    public bool IsEmpty => _items.Count == 0;

    public void AddItem(ItemObject item)
    {
        if (IsFull) return;
        _items.Add(item);
        
        item.transform.SetParent(this.transform);
        item.transform.localPosition = Vector3.zero;
    }

    public ItemObject GetNextItem()
    {
        if (IsEmpty) return null;
        
        ItemObject item = _items[0];
        _items.RemoveAt(0);
        return item;
    }

    public void Interact(PlayerCarry player)
    {
        if (player.HeldObject == null)
        {
            player.CarryObject(this.gameObject);
            transform.SetParent(player.transform);
            transform.localPosition = new Vector3(0, 1f, 0.5f); // Carry position
            transform.localRotation = Quaternion.identity;
            
            if (TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            Debug.Log($"Nhặt Hộp hàng. Đang có: {_items.Count}/{_maxCapacity} items.");
        }
    }
}
