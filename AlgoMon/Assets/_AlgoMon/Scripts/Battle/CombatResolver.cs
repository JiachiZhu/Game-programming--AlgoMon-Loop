using UnityEngine;

/// <summary>
/// Resolves one round of combat between two combatants.
///
/// Two independent combat layers are applied in sequence:
///
///   1. ASD Counter Check (RPS layer)
///      Attack > Status > Defense > Attack
///      If a counter occurs: CounterEvent is published. The countered unit
///      is forced to act after the countering unit regardless of ClockSpeed.
///      Damage uses the skill's counterSuccessMultiplier if the attacker won.
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
    public static bool IsCounter(InstructionType attacker, InstructionType defender)
    {
        return (attacker == InstructionType.Attack  && defender == InstructionType.Status)
            || (attacker == InstructionType.Status  && defender == InstructionType.Defense)
            || (attacker == InstructionType.Defense && defender == InstructionType.Attack);
    }

    /// <summary>
    /// Resolves damage dealt by attackerSkill from attacker to defender.
    /// Publishes DamageEvent (and CounterEvent if applicable) via EventBus.
    /// </summary>
    public static int ResolveDamage(
        AlgoMonInstance attacker,
        AlgoMonInstance defender,
        SkillData       attackerSkill,
        InstructionType defenderAction)
    {
        bool counter = IsCounter(attackerSkill.instructionType, defenderAction);

        if (counter)
        {
            EventBus.Publish(new CounterEvent
            {
                CounterId   = attacker.nickname,
                CounteredId = defender.nickname
            });
        }

        float counterMult  = counter ? attackerSkill.counterSuccessMultiplier : 1f;
        float elementMult  = GetElementMultiplier(attackerSkill.elementType, defender.data.elementType);

        int rawAttack = attackerSkill.damageType == DamageType.Computing
            ? attacker.ComputingPower
            : attacker.Throughput;

        int defence = attackerSkill.damageType == DamageType.Computing
            ? defender.Firewall
            : defender.Encryption;

        float baseMult = attackerSkill.basePower / 100f;
        float raw      = rawAttack * baseMult * elementMult * counterMult;
        int damage     = Mathf.Max(1, Mathf.FloorToInt(raw * 50f / (50f + defence)));

        EventBus.Publish(new DamageEvent
        {
            AttackerId   = attacker.nickname,
            TargetId     = defender.nickname,
            Amount       = damage,
            DmgType      = attackerSkill.damageType,
            SkillElement = attackerSkill.elementType
        });

        return damage;
    }

    /// <summary>
    /// Looks up the element multiplier from the 6x6 chart.
    /// </summary>
    public static float GetElementMultiplier(ElementType attackElement, ElementType defendElement)
    {
        return ElementChart[(int)attackElement, (int)defendElement];
    }
}
