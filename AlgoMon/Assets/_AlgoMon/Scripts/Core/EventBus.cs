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
// Defense note: EventBus is the main event bus type used by this part of the project.
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> _handlers =
        new Dictionary<Type, List<Delegate>>();

    // Defense note: Runs the subscribe helper used by this script.
    public static void Subscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();

        // Guard against duplicate subscriptions: Unsubscribe only removes the
        // first matching delegate, so a double-Add would leak a handler that
        // fires twice per publish. Balanced OnEnable/OnDisable plus Clear() on
        // scene transitions normally prevent this, but the guard keeps the bus
        // correct if a lifecycle path ever re-subscribes without unsubscribing.
        List<Delegate> list = _handlers[type];
        if (!list.Contains(handler))
            list.Add(handler);
    }

    // Defense note: Runs the unsubscribe helper used by this script.
    public static void Unsubscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);
        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    // Defense note: Runs the publish helper used by this script.
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
    // Defense note: Runs the clear helper used by this script.
    public static void Clear()
    {
        _handlers.Clear();
    }
}
