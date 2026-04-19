using System;
using System.Collections.Generic;

/// <summary>
/// A type-safe global Event Bus implementing the Observer Pattern.
/// Systems publish and subscribe to events without holding direct references
/// to each other, keeping all modules fully decoupled.
///
/// Usage:
///   Subscribe:   EventBus.Subscribe<DamageEvent>(OnDamage);
///   Publish:     EventBus.Publish(new DamageEvent { Amount = 30 });
///   Unsubscribe: EventBus.Unsubscribe<DamageEvent>(OnDamage);
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers =
        new Dictionary<Type, List<Delegate>>();

    public static void Subscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();

        _handlers[type].Add(handler);
    }

    public static void Unsubscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    public static void Publish<T>(T eventData)
    {
        Type type = typeof(T);
        if (!_handlers.ContainsKey(type)) return;

        foreach (Delegate handler in _handlers[type].ToArray())
            (handler as Action<T>)?.Invoke(eventData);
    }

    /// <summary>
    /// Clears all subscriptions. Call this on scene unload to prevent
    /// stale references from destroyed objects.
    /// </summary>
    public static void Clear()
    {
        _handlers.Clear();
    }
}
