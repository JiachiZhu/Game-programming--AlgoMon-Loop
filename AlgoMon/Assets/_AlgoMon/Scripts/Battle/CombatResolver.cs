using UnityEngine;

/// <summary>
/// Resolves one round of combat between two combatants.
///
/// Two independent combat layers are applied in sequence:
///
///   1. ASD Counter Check (RPS layer)
///      Attack > Status > Defense > Attack
///      BattleManager owns the counter decision and passes whether the attacker
///      won that check. The countered unit is forced after the countering unit
///      there; this class only resolves final damage numbers and events.
///
///   2. Element Type Chart (matrix lookup, O(1))
///      6 types: Water / Fire / Grass / Ice / Electric / Ground
///      Strong x1.5 | Neutral x1.0 | Weak x0.75
///
/// Damage formula (ratio-based defence):
///   raw   = rawAttack × (basePower / 100) × elementMult × counterMult
///   damage = Max(1, Floor(raw × 50 / (50 + defence)))
///
/// The constant 50 is a softcap — defence equal to 50 halves incoming damage.
/// Reference values:
///   defence =   0 → ×1.00   defence =  50 → ×0.50
///   defence = 100 → ×0.33   defence = 150 → ×0.25
/// </summary>
// Defense note: CombatResolver is the main combat resolver type used by this part of the project.
public static class CombatResolver
{
    // ----------------------------------------------------------------
    // Element type chart  [attacker element][defender element]
    // Rows and columns follow the ElementType enum order:
    //   0=Water  1=Fire  2=Grass  3=Ice  4=Electric  5=Ground

    private static readonly float[,] ElementChart = new float[6, 6]
    {
        //          Water   Fire   Grass   Ice   Electric  Ground
        /* Water  */ { 1.0f,  1.5f,  0.75f, 1.0f,  0.75f,   1.0f },
        /* Fire   */ { 0.75f, 1.0f,  1.5f,  1.5f,  1.0f,    1.0f },
        /* Grass  */ { 1.5f,  0.75f, 1.0f,  0.75f, 1.0f,    1.0f },
        /* Ice    */ { 1.0f,  0.75f, 1.5f,  1.0f,  1.0f,    1.0f },
        /* Elec   */ { 1.5f,  1.0f,  1.0f,  1.0f,  1.0f,    0.75f},
        /* Ground */ { 1.0f,  1.0f,  1.0f,  1.0f,  1.5f,    1.0f },
    };

    // ----------------------------------------------------------------
    // Public API

    /// <summary>
    /// Checks if attacker's instruction counters defender's instruction.
    /// A > S > D > A
    /// </summary>
    // Defense note: Returns whether this value is counter.
    public static bool IsCounter(InstructionType attacker, InstructionType defender)
    {
        return (attacker == InstructionType.Attack  && defender == InstructionType.Status)
            || (attacker == InstructionType.Status  && defender == InstructionType.Defense)
            || (attacker == InstructionType.Defense && defender == InstructionType.Attack);
    }

    /// <summary>
    /// Resolves damage dealt by attackerSkill from attacker to defender.
    /// Publishes DamageEvent via EventBus. CounterEvent is published by
    /// BattleManager when the ASD check is resolved.
    /// Returns 0 if the skill has DamageType.None (Defense / Status skills).
    /// </summary>
    // Defense note: Resolves the damage step and updates dependent state.
    public static int ResolveDamage(
        AlgoMonInstance attacker,
        AlgoMonInstance defender,
        BattleStats     attackerStats,
        BattleStats     defenderStats,
        SkillData       attackerSkill,
        InstructionType defenderAction,
        bool attackerWonCounter = false,
        float finalDamageMultiplier = 1f,
        int basePowerBonus = 0,
        string attackerEventId = null,
        string defenderEventId = null)
    {
        if (attackerSkill.damageType == DamageType.None)
            return 0;

        float counterMult  = attackerWonCounter ? attackerSkill.counterSelfDamageMultiplier : 1f;
        float elementMult  = GetElementMultiplier(attackerSkill.elementType, defender.data.elementType);

        int rawAttack = attackerSkill.damageType == DamageType.Computing
            ? attackerStats.ComputingPower
            : attackerStats.Throughput;

        int defence = attackerSkill.damageType == DamageType.Computing
            ? defenderStats.Firewall
            : defenderStats.Encryption;

        int effectiveBasePower = Mathf.Max(0, attackerSkill.basePower + basePowerBonus);
        float baseMult = effectiveBasePower / 100f;
        float raw      = rawAttack * baseMult * elementMult * counterMult;
        int damage     = Mathf.Max(1, Mathf.FloorToInt(raw * 50f / (50f + defence)));
        damage          = Mathf.Max(0, Mathf.FloorToInt(damage * Mathf.Max(0f, finalDamageMultiplier)));

        EventBus.Publish(new DamageEvent
        {
            AttackerId         = EventIdOrNickname(attackerEventId, attacker),
            TargetId           = EventIdOrNickname(defenderEventId, defender),
            Amount             = damage,
            DmgType            = attackerSkill.damageType,
            SkillElement       = attackerSkill.elementType,
            TargetElement      = defender.data.elementType,
            ElementMultiplier  = elementMult
        });

        return damage;
    }

    // Defense note: Runs the event id or nickname helper used by this script.
    private static string EventIdOrNickname(string eventId, AlgoMonInstance fallback)
    {
        if (!string.IsNullOrWhiteSpace(eventId))
            return eventId.Trim();
        return fallback != null ? fallback.nickname : string.Empty;
    }

    /// <summary>
    /// Looks up the element multiplier from the 6x6 chart.
    /// Normal type is always neutral (x1.0) against everything.
    /// </summary>
    // Defense note: Retrieves the element multiplier value used by this system.
    public static float GetElementMultiplier(ElementType attackElement, ElementType defendElement)
    {
        if (attackElement == ElementType.Normal || defendElement == ElementType.Normal)
            return 1.0f;

        // Subtract 1 to offset the Normal entry at index 0
        int row = (int)attackElement - 1;
        int col = (int)defendElement - 1;
        return ElementChart[row, col];
    }
}
