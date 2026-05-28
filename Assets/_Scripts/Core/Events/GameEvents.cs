/// <summary>
/// Event payload structs cho EventBus. Dùng struct để zero-alloc khi Raise.
/// Quy ước: tên class kết thúc bằng "Event", field readonly, constructor đầy đủ.
/// </summary>

// === Economy ===

public readonly struct MoneyChangedEvent
{
    public readonly int Delta;
    public readonly int NewTotal;
    public MoneyChangedEvent(int delta, int newTotal) { Delta = delta; NewTotal = newTotal; }
}

public readonly struct SaleCompletedEvent
{
    public readonly CustomerAgent Customer;
    public readonly int Amount;
    public SaleCompletedEvent(CustomerAgent customer, int amount) { Customer = customer; Amount = amount; }
}

// === Customer lifecycle ===

public readonly struct CustomerArrivedEvent
{
    public readonly CustomerAgent Customer;
    public CustomerArrivedEvent(CustomerAgent customer) { Customer = customer; }
}

public readonly struct CustomerLeftEvent
{
    public readonly CustomerAgent Customer;
    public readonly bool WasServed;
    public CustomerLeftEvent(CustomerAgent customer, bool wasServed) { Customer = customer; WasServed = wasServed; }
}

// === Shelf ===

public readonly struct ShelfStockChangedEvent
{
    public readonly ShelfController Shelf;
    public readonly int OccupiedSlots;
    public readonly int TotalSlots;
    public bool IsEmpty => OccupiedSlots == 0;
    public bool IsFull => OccupiedSlots == TotalSlots;

    public ShelfStockChangedEvent(ShelfController shelf, int occupied, int total)
    {
        Shelf = shelf; OccupiedSlots = occupied; TotalSlots = total;
    }
}

// === UI / Notification ===

public enum NotificationKind { Info, Success, Warning, Error }

public readonly struct NotificationRequestedEvent
{
    public readonly string Message;
    public readonly NotificationKind Kind;
    public NotificationRequestedEvent(string message, NotificationKind kind = NotificationKind.Info)
    {
        Message = message; Kind = kind;
    }
}
