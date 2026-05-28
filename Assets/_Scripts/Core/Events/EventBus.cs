using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static typed event bus: pub/sub theo C# type, zero-alloc với struct events.
/// Hỗ trợ Subscribe/Unsubscribe an toàn ngay trong handler (multicast delegate immutable).
/// </summary>
/// <example>
/// <code>
/// EventBus.Subscribe&lt;SaleCompletedEvent&gt;(OnSale);
/// EventBus.Raise(new SaleCompletedEvent(customer, 50));
/// EventBus.Unsubscribe&lt;SaleCompletedEvent&gt;(OnSale);
/// </code>
/// </example>
public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>(32);

    public static void Subscribe<T>(Action<T> handler)
    {
        if (handler == null) return;
        Type t = typeof(T);
        _handlers[t] = _handlers.TryGetValue(t, out var existing)
            ? Delegate.Combine(existing, handler)
            : handler;
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        if (handler == null) return;
        Type t = typeof(T);
        if (!_handlers.TryGetValue(t, out var existing)) return;

        Delegate result = Delegate.Remove(existing, handler);
        if (result == null) _handlers.Remove(t);
        else _handlers[t] = result;
    }

    public static void Raise<T>(T evt)
    {
        if (!_handlers.TryGetValue(typeof(T), out var d)) return;
        try
        {
            (d as Action<T>)?.Invoke(evt);
        }
        catch (Exception e)
        {
            // Một handler crash không được làm break các handler khác hoặc systems khác.
            Debug.LogException(e);
        }
    }

    public static int HandlerCount<T>() =>
        _handlers.TryGetValue(typeof(T), out var d) ? d.GetInvocationList().Length : 0;

    /// <summary>Reset toàn bộ subscription khi enter Play Mode (kể cả khi tắt Domain Reload).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearOnPlayModeEnter() => _handlers.Clear();
}
