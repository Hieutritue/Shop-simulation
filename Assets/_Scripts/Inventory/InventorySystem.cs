using System;
using System.Collections.Generic;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    public static InventorySystem Instance { get; private set; }

    // Dictionary lưu trữ ItemSO và số lượng đang có trong kho
    private Dictionary<ItemSO, int> _inventory = new Dictionary<ItemSO, int>();

    public event Action<ItemSO, int> OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void AddItem(ItemSO item, int amount)
    {
        if (_inventory.ContainsKey(item))
        {
            _inventory[item] += amount;
        }
        else
        {
            _inventory.Add(item, amount);
        }
        
        OnInventoryChanged?.Invoke(item, _inventory[item]);
    }

    public bool RemoveItem(ItemSO item, int amount)
    {
        if (_inventory.ContainsKey(item) && _inventory[item] >= amount)
        {
            _inventory[item] -= amount;
            OnInventoryChanged?.Invoke(item, _inventory[item]);
            return true;
        }
        return false; // Không đủ hàng
    }

    public int GetItemAmount(ItemSO item)
    {
        if (_inventory.ContainsKey(item))
        {
            return _inventory[item];
        }
        return 0;
    }

    public Dictionary<ItemSO, int> GetAllItems()
    {
        return _inventory;
    }
}
