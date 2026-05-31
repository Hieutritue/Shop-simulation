using UnityEngine;

/// <summary>
/// Ngăn kéo tiền cố định 1 mệnh giá. Player Interact → spawn 1 MoneyStack vào tay player.
/// Đặt nhiều CashDrawer trên quầy (mỗi cái 1 denom: $1, $5, $10, $20, $50).
/// </summary>
public class CashDrawer : MonoBehaviour, IInteractable
{
    [SerializeField] private int _denomination = 1;
    [SerializeField] private MoneyStack _moneyStackPrefab;
    [SerializeField] private Transform _spawnPoint;

    private void Awake()
    {
        if (_spawnPoint == null) _spawnPoint = this.transform;
    }

    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject != null) return;
        if (_moneyStackPrefab == null)
        {
            Debug.LogWarning($"[CashDrawer] {name} thiếu MoneyStack prefab.");
            return;
        }

        MoneyStack stack = Instantiate(_moneyStackPrefab, _spawnPoint.position, _spawnPoint.rotation);
        stack.SetDenomination(_denomination);
        stack.Interact(player); // delegate sang carry pipeline của MoneyStack
    }
}
