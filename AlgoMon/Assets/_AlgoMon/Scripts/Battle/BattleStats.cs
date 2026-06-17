using UnityEngine;

/// <summary>
/// Runtime combat stats after battle-only modifiers are applied.
/// AlgoMonInstance remains the permanent data source; this snapshot lets
/// BattleManager resolve temporary status effects without mutating capture data.
/// </summary>
// Defense note: BattleStats groups small runtime values that are passed around together.
public struct BattleStats
{
    public int ClockSpeed;
    public int ComputingPower;
    public int Throughput;
    public int Firewall;
    public int Encryption;

    // Defense note: Runs the from helper used by this script.
    public static BattleStats From(AlgoMonInstance instance)
    {
        return new BattleStats
        {
            ClockSpeed = instance.ClockSpeed,
            ComputingPower = instance.ComputingPower,
            Throughput = instance.Throughput,
            Firewall = instance.Firewall,
            Encryption = instance.Encryption
        };
    }

    // Defense note: Applies the percent change to gameplay or UI state.
    public static int ApplyPercent(int value, float multiplier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0.05f, multiplier)));
    }
}
