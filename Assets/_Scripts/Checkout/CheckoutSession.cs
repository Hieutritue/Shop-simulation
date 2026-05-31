using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Trạng thái 1 phiên thanh toán: subtotal, paid, change đã đưa, patience.
/// Pure class — không phải MonoBehaviour. CheckoutCounter giữ và tick.
/// </summary>
public class CheckoutSession
{
    private readonly List<ItemObject> _unscanned = new List<ItemObject>();

    public CustomerAgent Customer { get; private set; }
    public int Subtotal { get; private set; }
    public int Paid { get; private set; }
    public int ChangeGiven { get; private set; }

    public float PatienceTotal { get; private set; }
    public float PatienceRemaining { get; private set; }

    public IReadOnlyList<ItemObject> Unscanned => _unscanned;
    public int ChangeOwed => Mathf.Max(0, Paid - Subtotal);
    public int ChangeRemaining => Mathf.Max(0, ChangeOwed - ChangeGiven);
    public bool AllScanned => _unscanned.Count == 0;
    public bool IsComplete => AllScanned && Paid > 0 && ChangeRemaining == 0;

    public CheckoutSession(CustomerAgent customer, IReadOnlyList<ItemObject> items, float patience)
    {
        Customer = customer;
        if (items != null) _unscanned.AddRange(items);
        PatienceTotal = patience;
        PatienceRemaining = patience;
    }

    /// <returns>true nếu item nằm trong unscanned và được scan thành công.</returns>
    public bool TryScan(ItemObject item)
    {
        if (item == null) return false;
        int idx = _unscanned.IndexOf(item);
        if (idx < 0) return false;
        _unscanned.RemoveAt(idx);
        int price = item.ItemData != null ? Mathf.RoundToInt(item.ItemData.sellPrice) : 0;
        Subtotal += price;
        return true;
    }

    public void RegisterPayment(int amount)
    {
        if (amount <= 0) return;
        Paid += amount;
    }

    public void RegisterChangeGiven(int amount)
    {
        if (amount <= 0) return;
        ChangeGiven += amount;
    }

    public void TickPatience(float dt)
    {
        if (PatienceRemaining > 0f) PatienceRemaining = Mathf.Max(0f, PatienceRemaining - dt);
    }

    public bool PatienceExpired => PatienceRemaining <= 0f;
}
