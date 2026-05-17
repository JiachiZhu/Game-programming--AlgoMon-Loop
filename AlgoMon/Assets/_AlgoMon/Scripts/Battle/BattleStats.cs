using UnityEngine;

/// <summary>
/// Runtime combat stats after battle-only modifiers are applied.
/// AlgoMonInstance remains the permanent data source; this snapshot lets
/// BattleManager resolve temporary status effects without mutating capture data.
/// </summary>
public struct BattleStats
{
    public int ClockSpeed;
    public int ComputingPower;
    public int Throughput;
    public int Firewall;
    public int Encryption;

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

    public static int ApplyPercent(int value, float multiplier)
    {
        return Mathf.Max(1, Mathf.RoundToInt(value * Mathf.Max(0.05f, multiplier)));
    }
}
