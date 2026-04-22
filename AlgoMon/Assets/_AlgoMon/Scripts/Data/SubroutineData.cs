using UnityEngine;

/// <summary>
/// ScriptableObject defining a species' built-in passive ability (Subroutine).
///
/// A Subroutine is a background process hardwired into the AlgoMon's firmware.
/// It cannot be chosen or replaced — it activates automatically when its
/// trigger condition is met during battle.
///
/// BattleManager is responsible for checking triggers and applying effects.
/// This class is data-only; no logic lives here.
/// </summary>
[CreateAssetMenu(fileName = "New Subroutine", menuName = "AlgoMon/Subroutine Data")]
public class SubroutineData : ScriptableObject
{
    [Header("Identity")]
    public string subroutineName;
    [TextArea] public string description;

    [Header("Trigger")]
    [Tooltip("The battle condition that activates this subroutine.")]
    public SubroutineTrigger trigger;

    [Header("Effect")]
    [Tooltip("What happens when the trigger fires.")]
    public SubroutineEffect effect;

    [Tooltip("Magnitude of the effect. " +
             "For PriorityBoost: integer added to skill priority. " +
             "For stat boosts and HealSelf: percentage (e.g. 20 = 20%). " +
             "For ApplyStatus: unused (see statusType).")]
    public int value;

    [Tooltip("Only used when effect = ApplyStatus.")]
    public StatusType statusType;

    [Tooltip("Duration in turns. 0 = instant / permanent until battle end.")]
    public int duration;
}
