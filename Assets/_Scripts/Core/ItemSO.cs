using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "ShopSimulator/Item")]
public class ItemSO : ScriptableObject
{
    public string itemName;
    public float sellPrice;
    public Sprite icon;
    public GameObject prefab3D;
}
