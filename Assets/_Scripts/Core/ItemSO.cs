using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "ShopSimulator/Item")]
public class ItemSO : ScriptableObject
{
    public string itemID;
    public string itemName;
    public float buyPrice;
    public float sellPrice;
    public Sprite icon;
    public GameObject prefab3D;
}
