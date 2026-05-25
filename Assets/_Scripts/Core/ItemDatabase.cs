using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lưu trữ danh sách tất cả các mặt hàng có trong game.
/// Dễ dàng truy xuất thông tin item khi cần thiết.
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "ShopSimulator/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemSO> allItems = new List<ItemSO>();

    public ItemSO GetItemByID(string id)
    {
        foreach (var item in allItems)
        {
            if (item.itemID == id)
                return item;
        }
        return null;
    }
}
