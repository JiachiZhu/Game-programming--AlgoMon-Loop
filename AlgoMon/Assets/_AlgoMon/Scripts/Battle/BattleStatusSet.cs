using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Runtime status container for one active battle unit.
/// It tracks stacks, source/caster, duration, and battle-only modifiers.
/// </summary>
// Defense note: BattleStatusSet is the main battle status set type used by this part of the project.
public sealed class BattleStatusSet
{
    // Defense note: StatusApplyResult groups small runtime values that are passed around together.
    public struct StatusApplyResult
    {
        // Defense note: Initializes the StatusApplyResult instance and its default runtime state.
        public StatusApplyResult(
            int addedStacks,
            int finalStacks,
            StatusDurationType durationType,
            int duration)
        {
            AddedStacks = addedStacks;
            FinalStacks = finalStacks;
            DurationType = durationType;
            Duration = duration;
        }

        public int AddedStacks { get; }
        public int FinalStacks { get; }
        public StatusDurationType DurationType { get; }
        public int Duration { get; }
    }

    private const int MaxFreezeStacks = 3;
    private const int MaxLeechStacks = 3;
    private const float BurnDamagePerStack = 0.02f;
    private const float LeechDamagePerStack = 0.03f;
    private const float FreezeClockPenaltyPerStack = 0.15f;
    private const float OffensiveStatBuffPerStack = 0.12f;
    private const float DefensiveStatBuffPerStack = 0.10f;

    // Defense note: StatusState is the main status state type used by this part of the project.
    private sealed class StatusState
    {
        public StatusType Type;
        public int Stacks;
        public StatusDurationType DurationType;
        public int RemainingTurns;
        public int AppliedRound;
        public AlgoMonInstance Source;
    }

    // Defense note: TimedIntModifier groups small runtime values that are passed around together.
    private struct TimedIntModifier
    {
        public int Amount;
        public StatusDurationType DurationType;
        public int RemainingTurns;
        public int AppliedRound;
    }

    // Defense note: TimedFloatModifier groups small runtime values that are passed around together.
    private struct TimedFloatModifier
    {
        public float Amount;
        public StatusDurationType DurationType;
        public int RemainingTurns;
        public int AppliedRound;
    }

    private readonly Dictionary<StatusType, StatusState> states =
        new Dictionary<StatusType, StatusState>();

    private TimedIntModifier cpDiscount;
    private TimedIntModifier nextPriorityBonus;
    private TimedIntModifier nextBasePowerBonus;
    private TimedFloatModifier firewallShred;

    public float BurnDamagePerLayer => BurnDamagePerStack;
    public float LeechDamagePerLayer => LeechDamagePerStack;

    // Defense note: Runs the clear helper used by this script.
    public void Clear()
    {
        states.Clear();
        cpDiscount = default;
        nextPriorityBonus = default;
        nextBasePowerBonus = default;
        firewallShred = default;
    }

    // Defense note: Runs the has helper used by this script.
    public bool Has(StatusType status)
    {
        return GetStacks(status) > 0;
    }

    // Defense note: Retrieves the stacks value used by this system.
    public int GetStacks(StatusType status)
    {
        return states.TryGetValue(status, out StatusState state) ? state.Stacks : 0;
    }

    // Defense note: Retrieves the source value used by this system.
    public AlgoMonInstance GetSource(StatusType status)
    {
        return states.TryGetValue(status, out StatusState state) ? state.Source : null;
    }

    // Defense note: Applies the status change to gameplay or UI state.
    public StatusApplyResult ApplyStatus(
        StatusType status,
        int stacks,
        StatusDurationType durationType,
        int duration,
        int currentRound,
        AlgoMonInstance source)
    {
        if (stacks <= 0)
            return new StatusApplyResult(0, GetStacks(status), durationType, duration);

        NormalizeDuration(status, ref durationType, ref duration);

        bool isNew = !states.TryGetValue(status, out StatusState state);
        if (isNew)
        {
            state = new StatusState { Type = status };
            states.Add(status, state);
        }

        int before = state.Stacks;
        int cap = MaxStacks(status);
        state.Stacks = cap > 0
            ? Mathf.Min(cap, state.Stacks + stacks)
            : state.Stacks + stacks;

        state.Source = source ?? state.Source;
        MergeDuration(state, durationType, duration, currentRound, isNew);

        return new StatusApplyResult(
            state.Stacks - before,
            state.Stacks,
            state.DurationType,
            state.RemainingTurns);
    }

    // Defense note: Updates the stacks state or visual value.
    public void SetStacks(StatusType status, int stacks)
    {
        if (stacks <= 0)
        {
            states.Remove(status);
            return;
        }

        if (states.TryGetValue(status, out StatusState state))
            state.Stacks = stacks;
    }

    // Defense note: Runs the remove helper used by this script.
    public bool Remove(StatusType status)
    {
        return states.Remove(status);
    }

    // Defense note: Clears the temporary debuffs state so it can be rebuilt safely.
    public int ClearTemporaryDebuffs()
    {
        int removed = 0;
        StatusType[] debuffs =
        {
            StatusType.Burn,
            StatusType.Freeze,
            StatusType.Leech,
            StatusType.Ensnare,
            StatusType.Throttle,
            StatusType.Corrupted
        };

        for (int i = 0; i < debuffs.Length; i++)
        {
            if (states.TryGetValue(debuffs[i], out StatusState state) &&
                state.DurationType != StatusDurationType.Permanent)
            {
                states.Remove(debuffs[i]);
                removed++;
            }
        }

        return removed;
    }

    // Defense note: Clears the swap limited effects state so it can be rebuilt safely.
    public int ClearSwapLimitedEffects()
    {
        int removed = 0;
        var remove = new List<StatusType>();

        foreach (KeyValuePair<StatusType, StatusState> pair in states)
        {
            if (pair.Value.DurationType != StatusDurationType.Permanent)
                remove.Add(pair.Key);
        }

        for (int i = 0; i < remove.Count; i++)
        {
            states.Remove(remove[i]);
            removed++;
        }

        if (ClearTemporaryModifier(ref cpDiscount))
            removed++;
        if (ClearTemporaryModifier(ref firewallShred))
            removed++;
        if (nextPriorityBonus.Amount != 0)
        {
            nextPriorityBonus = default;
            removed++;
        }
        if (nextBasePowerBonus.Amount != 0)
        {
            nextBasePowerBonus = default;
            removed++;
        }

        return removed;
    }

    // Defense note: Applies the to stats change to gameplay or UI state.
    public BattleStats ApplyToStats(BattleStats stats)
    {
        stats.ClockSpeed = BattleStats.ApplyPercent(
            stats.ClockSpeed,
            1f - GetStacks(StatusType.Freeze) * FreezeClockPenaltyPerStack);

        stats.ComputingPower = BattleStats.ApplyPercent(
            stats.ComputingPower,
            1f + GetStacks(StatusType.ComputingUp) * OffensiveStatBuffPerStack);

        stats.Throughput = BattleStats.ApplyPercent(
            stats.Throughput,
            1f + GetStacks(StatusType.ThroughputUp) * OffensiveStatBuffPerStack);

        stats.Firewall = BattleStats.ApplyPercent(
            stats.Firewall,
            1f + GetStacks(StatusType.FirewallUp) * DefensiveStatBuffPerStack - Mathf.Clamp01(firewallShred.Amount));

        stats.Encryption = BattleStats.ApplyPercent(
            stats.Encryption,
            1f + GetStacks(StatusType.EncryptionUp) * DefensiveStatBuffPerStack);

        return stats;
    }

    // Defense note: Runs the effective skill cost helper used by this script.
    public int EffectiveSkillCost(int baseCost, int currentRound)
    {
        int cost = Mathf.Max(0, baseCost);
        cost += GetStacks(StatusType.Freeze);

        if (IsActiveForSkillUse(StatusType.BufferLoad, currentRound))
            cost -= 4;

        if (IsTimedModifierActive(cpDiscount, currentRound))
            cost -= cpDiscount.Amount;

        return Mathf.Max(0, cost);
    }

    // Defense note: Runs the skill repeat count helper used by this script.
    public int SkillRepeatCount(int currentRound)
    {
        return IsActiveForSkillUse(StatusType.Concurrent, currentRound) ? 2 : 1;
    }

    // Defense note: Runs the priority bonus helper used by this script.
    public int PriorityBonus(int currentRound)
    {
        int bonus = IsActiveForSkillUse(StatusType.Overclock, currentRound)
            ? Mathf.Max(1, GetStacks(StatusType.Overclock))
            : 0;

        if (IsOneShotModifierActive(nextPriorityBonus, currentRound))
            bonus += nextPriorityBonus.Amount;

        return bonus;
    }

    // Defense note: Runs the base power bonus helper used by this script.
    public int BasePowerBonus(int currentRound)
    {
        return IsOneShotModifierActive(nextBasePowerBonus, currentRound)
            ? nextBasePowerBonus.Amount
            : 0;
    }

    // Defense note: Runs the consume skill use modifiers helper used by this script.
    public void ConsumeSkillUseModifiers(int currentRound)
    {
        if (IsActiveForSkillUse(StatusType.BufferLoad, currentRound))
            states.Remove(StatusType.BufferLoad);
        if (IsActiveForSkillUse(StatusType.Concurrent, currentRound))
            states.Remove(StatusType.Concurrent);
        if (IsActiveForSkillUse(StatusType.Overclock, currentRound))
            states.Remove(StatusType.Overclock);
        if (IsOneShotModifierActive(nextPriorityBonus, currentRound))
            nextPriorityBonus = default;
        if (IsOneShotModifierActive(nextBasePowerBonus, currentRound))
            nextBasePowerBonus = default;
    }

    // Defense note: Applies the cp discount change to gameplay or UI state.
    public void ApplyCPDiscount(
        int amount,
        StatusDurationType durationType,
        int duration,
        int currentRound)
    {
        if (amount <= 0)
            return;

        NormalizeModifierDuration(ref durationType, ref duration);

        bool isNew = cpDiscount.Amount <= 0;
        cpDiscount.Amount = Mathf.Max(cpDiscount.Amount, amount);
        cpDiscount.DurationType = isNew
            ? durationType
            : StrongerDuration(cpDiscount.DurationType, durationType);
        cpDiscount.RemainingTurns = Mathf.Max(cpDiscount.RemainingTurns, duration);
        cpDiscount.AppliedRound = currentRound;
    }

    // Defense note: Applies the firewall shred change to gameplay or UI state.
    public void ApplyFirewallShred(
        float amount,
        StatusDurationType durationType,
        int duration,
        int currentRound)
    {
        if (amount <= 0f)
            return;

        NormalizeModifierDuration(ref durationType, ref duration);

        bool isNew = firewallShred.Amount <= 0f;
        // Shreds stack additively (two 20% counters = 40%), capped at 100%; Max()
        // here made repeat counter wins silently do nothing.
        firewallShred.Amount = Mathf.Clamp01(firewallShred.Amount + amount);
        firewallShred.DurationType = isNew
            ? durationType
            : StrongerDuration(firewallShred.DurationType, durationType);
        firewallShred.RemainingTurns = Mathf.Max(firewallShred.RemainingTurns, duration);
        firewallShred.AppliedRound = currentRound;
    }

    // Defense note: Applies the next priority bonus change to gameplay or UI state.
    public void ApplyNextPriorityBonus(int amount, int currentRound)
    {
        if (amount == 0)
            return;

        nextPriorityBonus.Amount += amount;
        nextPriorityBonus.DurationType = StatusDurationType.WhileOnField;
        nextPriorityBonus.RemainingTurns = 0;
        nextPriorityBonus.AppliedRound = currentRound;
    }

    // Defense note: Applies the next base power bonus change to gameplay or UI state.
    public void ApplyNextBasePowerBonus(int amount, int currentRound)
    {
        if (amount == 0)
            return;

        nextBasePowerBonus.Amount += amount;
        nextBasePowerBonus.DurationType = StatusDurationType.WhileOnField;
        nextBasePowerBonus.RemainingTurns = 0;
        nextBasePowerBonus.AppliedRound = currentRound;
    }

    // Defense note: Runs the tick durations helper used by this script.
    public List<string> TickDurations(int currentRound)
    {
        var expired = new List<string>();
        var remove = new List<StatusType>();

        foreach (KeyValuePair<StatusType, StatusState> pair in states)
        {
            StatusState state = pair.Value;
            if (state.DurationType != StatusDurationType.Turns)
                continue;
            if (state.AppliedRound >= currentRound)
                continue;

            state.RemainingTurns--;
            if (state.RemainingTurns <= 0)
                remove.Add(pair.Key);
        }

        for (int i = 0; i < remove.Count; i++)
        {
            expired.Add(remove[i].ToString());
            states.Remove(remove[i]);
        }

        TickModifierDuration(ref cpDiscount, "CP discount", currentRound, expired);
        TickModifierDuration(ref firewallShred, "Firewall shred", currentRound, expired);

        return expired;
    }

    // Read-only views of the timed modifiers so the HUD can render them as
    // status chips. Mirrors what BuildSummary prints; no activity-window logic.
    public int CPDiscountAmount => cpDiscount.Amount;
    public float FirewallShredAmount => firewallShred.Amount;
    public int NextPriorityBonusAmount => nextPriorityBonus.Amount;
    public int NextBasePowerBonusAmount => nextBasePowerBonus.Amount;

    // Defense note: Builds the summary data or UI structure.
    public string BuildSummary()
    {
        var builder = new StringBuilder();

        AppendStatus(builder, StatusType.Burn, "Burn");
        AppendStatus(builder, StatusType.Freeze, "Freeze");
        AppendStatus(builder, StatusType.Leech, "Leech");
        AppendStatus(builder, StatusType.Ensnare, "Ensnare");
        AppendStatus(builder, StatusType.Concurrent, "Concurrent");
        AppendStatus(builder, StatusType.BufferLoad, "Buffer");
        AppendStatus(builder, StatusType.ComputingUp, "CPU+");
        AppendStatus(builder, StatusType.ThroughputUp, "TP+");
        AppendStatus(builder, StatusType.FirewallUp, "FW+");
        AppendStatus(builder, StatusType.EncryptionUp, "ENC+");
        AppendStatus(builder, StatusType.Overclock, "Priority+");

        if (cpDiscount.Amount > 0)
            Append(builder, $"CP-{cpDiscount.Amount}");
        if (firewallShred.Amount > 0f)
            Append(builder, $"FW-{Mathf.RoundToInt(firewallShred.Amount * 100f)}%");
        if (nextPriorityBonus.Amount != 0)
            Append(builder, $"Next Priority {FormatSigned(nextPriorityBonus.Amount)}");
        if (nextBasePowerBonus.Amount != 0)
            Append(builder, $"Next PWR {FormatSigned(nextBasePowerBonus.Amount)}");

        return builder.ToString();
    }

    // Defense note: Returns whether this value is timed modifier active.
    private static bool IsTimedModifierActive(TimedIntModifier modifier, int currentRound)
    {
        if (modifier.Amount <= 0)
            return false;
        if (modifier.DurationType == StatusDurationType.Turns && modifier.RemainingTurns <= 0)
            return false;
        return modifier.AppliedRound < currentRound;
    }

    // Defense note: Returns whether this value is active for skill use.
    private bool IsActiveForSkillUse(StatusType status, int currentRound)
    {
        if (!states.TryGetValue(status, out StatusState state))
            return false;
        return state.AppliedRound < currentRound;
    }

    // Defense note: Returns whether this value is one shot modifier active.
    private static bool IsOneShotModifierActive(TimedIntModifier modifier, int currentRound)
    {
        return modifier.Amount != 0 && modifier.AppliedRound < currentRound;
    }

    // Defense note: Runs the append status helper used by this script.
    private void AppendStatus(StringBuilder builder, StatusType status, string label)
    {
        int stacks = GetStacks(status);
        if (stacks > 0)
            Append(builder, $"{label} {stacks}");
    }

    // Defense note: Runs the append helper used by this script.
    private static void Append(StringBuilder builder, string text)
    {
        if (builder.Length > 0)
            builder.Append(", ");
        builder.Append(text);
    }

    // Defense note: Runs the format signed helper used by this script.
    private static string FormatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString();
    }

    // Defense note: Runs the max stacks helper used by this script.
    private static int MaxStacks(StatusType status)
    {
        switch (status)
        {
            case StatusType.Freeze:
                return MaxFreezeStacks;
            case StatusType.Leech:
                return MaxLeechStacks;
            case StatusType.Concurrent:
            case StatusType.BufferLoad:
                return 1;
            default:
                return 0;
        }
    }

    // Defense note: Runs the normalize duration helper used by this script.
    private static void NormalizeDuration(
        StatusType status,
        ref StatusDurationType durationType,
        ref int duration)
    {
        if (status == StatusType.Burn)
        {
            durationType = StatusDurationType.WhileOnField;
            duration = 0;
            return;
        }

        NormalizeModifierDuration(ref durationType, ref duration);
    }

    // Defense note: Runs the normalize modifier duration helper used by this script.
    private static void NormalizeModifierDuration(
        ref StatusDurationType durationType,
        ref int duration)
    {
        if (durationType == StatusDurationType.Turns)
            duration = Mathf.Max(1, duration);
        else
            duration = 0;
    }

    // Defense note: Runs the merge duration helper used by this script.
    private static void MergeDuration(
        StatusState state,
        StatusDurationType durationType,
        int duration,
        int currentRound,
        bool isNew)
    {
        state.DurationType = isNew
            ? durationType
            : StrongerDuration(state.DurationType, durationType);
        state.RemainingTurns = Mathf.Max(state.RemainingTurns, duration);
        state.AppliedRound = currentRound;
    }

    // Defense note: Runs the stronger duration helper used by this script.
    private static StatusDurationType StrongerDuration(
        StatusDurationType current,
        StatusDurationType incoming)
    {
        if (current == StatusDurationType.Permanent || incoming == StatusDurationType.Permanent)
            return StatusDurationType.Permanent;
        if (current == StatusDurationType.WhileOnField || incoming == StatusDurationType.WhileOnField)
            return StatusDurationType.WhileOnField;
        return StatusDurationType.Turns;
    }

    // Defense note: Runs the tick modifier duration helper used by this script.
    private static void TickModifierDuration(
        ref TimedIntModifier modifier,
        string label,
        int currentRound,
        List<string> expired)
    {
        if (modifier.Amount <= 0 ||
            modifier.DurationType != StatusDurationType.Turns ||
            modifier.AppliedRound >= currentRound)
        {
            return;
        }

        modifier.RemainingTurns--;
        if (modifier.RemainingTurns <= 0)
        {
            modifier = default;
            expired.Add(label);
        }
    }

    // Defense note: Runs the tick modifier duration helper used by this script.
    private static void TickModifierDuration(
        ref TimedFloatModifier modifier,
        string label,
        int currentRound,
        List<string> expired)
    {
        if (modifier.Amount <= 0f ||
            modifier.DurationType != StatusDurationType.Turns ||
            modifier.AppliedRound >= currentRound)
        {
            return;
        }

        modifier.RemainingTurns--;
        if (modifier.RemainingTurns <= 0)
        {
            modifier = default;
            expired.Add(label);
        }
    }

    // Defense note: Clears the temporary modifier state so it can be rebuilt safely.
    private static bool ClearTemporaryModifier(ref TimedIntModifier modifier)
    {
        if (modifier.Amount <= 0 || modifier.DurationType == StatusDurationType.Permanent)
            return false;

        modifier = default;
        return true;
    }

    // Defense note: Clears the temporary modifier state so it can be rebuilt safely.
    private static bool ClearTemporaryModifier(ref TimedFloatModifier modifier)
    {
        if (modifier.Amount <= 0f || modifier.DurationType == StatusDurationType.Permanent)
            return false;

        modifier = default;
        return true;
    }
}
