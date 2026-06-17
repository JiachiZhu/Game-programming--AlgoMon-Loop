using System.Collections.Generic;

/// <summary>
/// Manages battle turn order for a set of AlgoMon combatants.
/// Wraps PriorityQueue and maps each AlgoMon's effective priority as the heap key.
/// Switch actions are resolved by BattleManager before this queue is built.
///
/// Three-tier priority (highest to lowest):
///   1. ASD counter winner  — ForceAfter() hard-overrides, always wins
///   2. Skill priority tier — effectivePriority = skill.priority * 10000 + ClockSpeed
///   3. ClockSpeed tiebreak — faster unit acts first within the same skill priority tier
///
/// BattleManager is responsible for computing effectivePriority from the declared
/// skill and calling Enqueue(mon, effectivePriority). The no-arg Enqueue(mon)
/// falls back to ClockSpeed only (skill priority = 0 assumed).
///
/// ASD counter override:
///   Call ForceAfter(countered, counter) after an ASD counter is detected.
///   This re-inserts the countered unit with a priority just below the
///   counter unit, overriding both ClockSpeed and skill priority.
///
/// Typical battle loop:
///   1. Initialize(combatants)                     — load all combatants once
///   2. Both sides declare skill
///   3. Enqueue(mon, skill.priority*10000+ClockSpeed) — or use ForceAfter after ASD check
///   4. current = Dequeue()                        — who acts this turn
///   5. resolve action
///   6. if alive: re-enqueue for next round
///   7. repeat from step 2
/// </summary>
// Defense note: TurnQueue is the main turn queue type used by this part of the project.
public class TurnQueue
{
    private readonly PriorityQueue<AlgoMonInstance> _queue =
        new PriorityQueue<AlgoMonInstance>();

    public int  Count   => _queue.Count;
    public bool IsEmpty => _queue.IsEmpty;

    // ----------------------------------------------------------------
    // Public API

    /// <summary>
    /// Adds a combatant using ClockSpeed as priority (skill priority = 0 assumed).
    /// Use the overload below when the declared skill has a non-zero priority.
    /// </summary>
    // Defense note: Runs the enqueue helper used by this script.
    public void Enqueue(AlgoMonInstance mon) =>
        _queue.Enqueue(mon, mon.ClockSpeed);

    /// <summary>
    /// Adds a combatant with a caller-supplied effective priority.
    /// BattleManager should pass: skill.priority * 10000f + mon.ClockSpeed
    /// so that skill priority tiers are respected before ClockSpeed tiebreak.
    /// </summary>
    // Defense note: Runs the enqueue helper used by this script.
    public void Enqueue(AlgoMonInstance mon, float effectivePriority) =>
        _queue.Enqueue(mon, effectivePriority);

    /// <summary>Removes and returns the next combatant to act.</summary>
    // Defense note: Runs the dequeue helper used by this script.
    public AlgoMonInstance Dequeue() => _queue.Dequeue();

    /// <summary>Returns the next combatant without removing them.</summary>
    // Defense note: Runs the peek helper used by this script.
    public AlgoMonInstance Peek() => _queue.Peek();

    /// <summary>Removes all combatants.</summary>
    // Defense note: Runs the clear helper used by this script.
    public void Clear() => _queue.Clear();

    /// <summary>
    /// Clears the queue and loads a fresh set of combatants.
    /// Call once at the start of every battle.
    /// </summary>
    // Defense note: Runs the initialize helper used by this script.
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
    // Defense note: Runs the force after helper used by this script.
    public void ForceAfter(AlgoMonInstance countered, AlgoMonInstance counter)
    {
        ForceAfter(countered, counter, counter.ClockSpeed);
    }

    /// <summary>
    /// ASD counter override for callers that already computed the countering
    /// unit's effective priority for this round.
    /// </summary>
    // Defense note: Runs the force after helper used by this script.
    public void ForceAfter(AlgoMonInstance countered, AlgoMonInstance counter, float counterPriority)
    {
        float overridePriority = counterPriority - 0.5f;
        _queue.Enqueue(countered, overridePriority);
    }
}
