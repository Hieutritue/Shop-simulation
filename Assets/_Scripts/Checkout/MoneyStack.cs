using UnityEngine;

/// <summary>
/// Một xấp tiền có mệnh giá, pickupable như ItemObject.
/// Spawn từ CashDrawer hoặc Customer khi trả tiền.
/// </summary>
public class MoneyStack : MonoBehaviour, IInteractable
{
    [SerializeField] private int _denomination = 1;

    public int Denomination => _denomination;

    public void SetDenomination(int v) => _denomination = Mathf.Max(0, v);

    public void Interact(PlayerCarry player)
    {
        if (player == null || player.HeldObject != null) return;

        player.CarryObject(this.gameObject);

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        Transform anchor = player.CarryPoint != null ? player.CarryPoint : player.transform;
        transform.SetParent(anchor);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
