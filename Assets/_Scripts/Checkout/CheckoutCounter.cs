using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quầy tính tiền: quản lý hàng chờ khách, player Interact để scan đồ của khách đầu hàng.
/// </summary>
public class CheckoutCounter : MonoBehaviour, IInteractable
{
    [Header("Queue")]
    [Tooltip("Các Transform đánh dấu vị trí khách đứng (index 0 = đầu hàng).")]
    [SerializeField] private Transform[] _queuePositions;

    private readonly List<CustomerAgent> _queue = new List<CustomerAgent>();

    public int QueueLength => _queue.Count;

    public int JoinQueue(CustomerAgent customer)
    {
        if (customer == null) return -1;
        int idx = _queue.IndexOf(customer);
        if (idx >= 0) return idx;
        _queue.Add(customer);
        return _queue.Count - 1;
    }

    public Transform GetQueuePosition(int index)
    {
        if (_queuePositions == null || _queuePositions.Length == 0) return transform;
        return _queuePositions[Mathf.Clamp(index, 0, _queuePositions.Length - 1)];
    }

    public void Interact(PlayerCarry player)
    {
        // Quầy không nhận item từ tay player — chỉ scan khách.
        if (_queue.Count == 0)
        {
            Debug.Log("[Checkout] Không có khách nào ở quầy.");
            return;
        }

        CustomerAgent front = _queue[0];
        if (front == null)
        {
            _queue.RemoveAt(0);
            ShiftQueueForward();
            return;
        }

        if (!front.IsReadyToBeServed)
        {
            Debug.Log("[Checkout] Khách đầu hàng chưa vào vị trí.");
            return;
        }

        int total = front.GetCarriedItemPrice();
        MoneyManager.Instance?.AddMoney(total);
        front.OnServed();

        _queue.RemoveAt(0);
        ShiftQueueForward();
        Debug.Log($"[Checkout] 💰 +${total}. Tổng: ${MoneyManager.Instance?.Money}");
    }

    private void ShiftQueueForward()
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i] != null) _queue[i].OnQueuePositionChanged(i);
        }
    }
}
