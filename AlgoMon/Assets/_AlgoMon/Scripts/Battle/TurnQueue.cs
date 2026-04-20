using System.Collections.Generic;

/// <summary>
/// Manages battle turn order for a set of AlgoMon combatants.
/// Wraps PriorityQueue and maps each AlgoMon's ClockSpeed as priority.
///
/// The underlying structure is a max-heap, so the unit with the
/// HIGHEST ClockSpeed is extracted first — fastest acts first.
///
/// ASD counter override:
///   Call ForceAfter(countered, counter) after an ASD counter is detected.
///   This re-inserts the countered unit with a priority just below the
///   counter unit, regardless of ClockSpeed.
///
/// Typical battle loop:
///   1. Initialize(combatants)      — load all combatants once
///   2. current = Dequeue()         — who acts this turn
///   3. resolve action
///   4. if alive: Enqueue(current)  — re-insert for next round
///   5. repeat from step 2
/// </summary>
public class TurnQueue
{
    private readonly PriorityQueue<AlgoMonInstance> _queue =
        new PriorityQueue<AlgoMonInstance>();

    public int  Count   => _queue.Count;
    public bool IsEmpty => _queue.IsEmpty;

    // ----------------------------------------------------------------
    // Public API

    /// <summary>
    /// Adds a combatant using ClockSpeed as priority.
    /// Higher ClockSpeed = extracted first.
    /// </summary>
    public void Enqueue(AlgoMonInstance mon) =>
        _queue.Enqueue(mon, mon.ClockSpeed);

    /// <summary>Removes and returns the next combatant to act.</summary>
    public AlgoMonInstance Dequeue() => _queue.Dequeue();

    /// <summary>Returns the next combatant without removing them.</summary>
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

    /// <summary>
    /// ASD counter override: forces the countered unit to act after the
    /// counter unit this round, regardless of ClockSpeed.
    /// Re-inserts countered with priority just below counter's ClockSpeed.
    /// </summary>
    public void ForceAfter(AlgoMonInstance countered, AlgoMonInstance counter)
    {
        float overridePriority = counter.ClockSpeed - 0.5f;
        _queue.Enqueue(countered, overridePriority);
    }
}
