using UnityEngine;

/// <summary>
/// Gắn vào prefab 3D của vật phẩm khi xuất hiện trong game (trên tay khách, trên kệ, v.v.).
/// </summary>
public class ItemObject : MonoBehaviour
{
    [SerializeField] private ItemSO _itemData;

    public ItemSO ItemData => _itemData;

    public void Setup(ItemSO data)
    {
        _itemData = data;
        // Có thể thêm logic đổi mesh/material ở đây nếu dùng 1 prefab chung, 
        // nhưng thường mỗi item sẽ có 1 prefab riêng biệt.
    }
}
