using System.Collections.Generic;

/// <summary>
/// Manages battle turn order for a set of AlgoMon combatants.
///
/// Wraps <see cref="PriorityQueue{T}"/> and maps each AlgoMon's ClockSpeed
/// to a negated integer priority:
///
///   priority = -ClockSpeed
///
/// Because the underlying structure is a min-heap, the unit with the
/// highest ClockSpeed always holds the smallest (most-negative) value
/// and is extracted first — highest speed acts first.
///
/// Typical battle loop:
///   1. Initialize(party)           — load all combatants once
///   2. current = Dequeue()         — who acts this turn
///   3. … resolve action …
///   4. if alive: Enqueue(current)  — re-insert for the next round
///   5. repeat from step 2
/// </summary>
public class TurnQueue
{
    private readonly PriorityQueue<AlgoMonInstance> _queue =
        new PriorityQueue<AlgoMonInstance>();

    public int  Count   => _queue.Count;
    public bool IsEmpty => _queue.IsEmpty;

    // ------------------------------------------------------------------ //
    //  Public API                                                          //
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Adds a combatant to the queue.
    /// Priority = -ClockSpeed so the min-heap surfaces the fastest unit first.
    /// </summary>
    public void Enqueue(AlgoMonInstance mon) =>
        _queue.Enqueue(mon, -mon.ClockSpeed);

    /// <summary>Removes and returns the next combatant to act.</summary>
    public AlgoMonInstance Dequeue() => _queue.Dequeue();

    /// <summary>Returns the next combatant to act without removing them.</summary>
    public AlgoMonInstance Peek() => _queue.Peek();

    /// <summary>Removes all combatants.</summary>
    public void Clear() => _queue.Clear();

    /// <summary>
    /// Clears the queue and loads a fresh set of combatants.
    /// Call once at the start of every battle.
    /// </summary>
    public void Initialize(IEnumerable<AlgoMonInstance> combatants)
    {
        Clear();
        foreach (AlgoMonInstance mon in combatants)
            Enqueue(mon);
    }
}
