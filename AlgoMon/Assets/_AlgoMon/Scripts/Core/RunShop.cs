using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Defense note: RunBuffType defines the valid run buff type options used by the gameplay systems.
public enum RunBuffType
{
    PacketAccelerator,
    CpCache,
    ShieldLayer,
    DataSiphon,
    VolatileOverclock,
    BorrowedCycles,
    BugBountyContract
}

[Serializable]
// Defense note: RunShopOffer is the main run shop offer type used by this part of the project.
public sealed class RunShopOffer
{
    // Defense note: Initializes the RunShopOffer instance and its default runtime state.
    public RunShopOffer(
        RunBuffType buffType,
        string displayName,
        string shortLabel,
        string description,
        int computeCost,
        float outgoingDamageBonus,
        float incomingDamageBonus,
        int skillCostReduction,
        float clockSpeedBonus,
        float expRewardBonus,
        bool highRisk)
    {
        BuffType = buffType;
        DisplayName = displayName;
        ShortLabel = shortLabel;
        Description = description;
        ComputeCost = computeCost;
        OutgoingDamageBonus = outgoingDamageBonus;
        IncomingDamageBonus = incomingDamageBonus;
        SkillCostReduction = skillCostReduction;
        ClockSpeedBonus = clockSpeedBonus;
        ExpRewardBonus = expRewardBonus;
        HighRisk = highRisk;
    }

    public RunBuffType BuffType { get; private set; }
    public string DisplayName { get; private set; }
    public string ShortLabel { get; private set; }
    public string Description { get; private set; }
    public int ComputeCost { get; private set; }
    public float OutgoingDamageBonus { get; private set; }
    public float IncomingDamageBonus { get; private set; }
    public int SkillCostReduction { get; private set; }
    public float ClockSpeedBonus { get; private set; }
    public float ExpRewardBonus { get; private set; }
    public bool HighRisk { get; private set; }

    public bool HasTradeoff
    {
        get
        {
            return HighRisk ||
                   IncomingDamageBonus > 0f ||
                   SkillCostReduction < 0 ||
                   ClockSpeedBonus < 0f;
        }
    }
}

// Defense note: RunShopCatalog stores lookup data so runtime systems can find the right assets.
public static class RunShopCatalog
{
    public const int OfferSlots = 3;
    public const int BaseRefreshCost = 5;

    private static readonly RunShopOffer[] offers =
    {
        new RunShopOffer(
            RunBuffType.PacketAccelerator,
            "Packet Accelerator",
            "DMG+15%",
            "Player attacks deal 15% more damage this run.",
            6,
            0.15f,
            0f,
            0,
            0f,
            0f,
            false),
        new RunShopOffer(
            RunBuffType.CpCache,
            "CP Cache",
            "CP COST -1",
            "Player skills cost 1 less CP this run.",
            5,
            0f,
            0f,
            1,
            0f,
            0f,
            false),
        new RunShopOffer(
            RunBuffType.ShieldLayer,
            "Shield Layer",
            "DMG TAKEN -15%",
            "Incoming damage to the player party is reduced by 15% this run.",
            5,
            0f,
            -0.15f,
            0,
            0f,
            0f,
            false),
        new RunShopOffer(
            RunBuffType.DataSiphon,
            "Data Siphon",
            "EXP+25%",
            "AlgoMon EXP rewards are increased by 25% this run.",
            8,
            0f,
            0f,
            0,
            0f,
            0.25f,
            false),
        new RunShopOffer(
            RunBuffType.VolatileOverclock,
            "Volatile Overclock",
            "TRADE DMG+45%",
            "Trade-off: player attacks deal 45% more damage, but incoming damage rises by 20%.",
            6,
            0.45f,
            0.20f,
            0,
            0f,
            0f,
            true),
        new RunShopOffer(
            RunBuffType.BorrowedCycles,
            "Borrowed Cycles",
            "TRADE CP-2",
            "Trade-off: player skills cost 2 less CP, but Clock Speed drops by 20%.",
            7,
            0f,
            0f,
            2,
            -0.20f,
            0f,
            true),
        new RunShopOffer(
            RunBuffType.BugBountyContract,
            "Bug Bounty Contract",
            "TRADE EXP+50%",
            "Trade-off: AlgoMon EXP rewards increase by 50%, but player skills cost 1 extra CP.",
            5,
            0f,
            0f,
            -1,
            0f,
            0.50f,
            true)
    };

    public static RunShopOffer[] Offers
    {
        get { return offers; }
    }

    // Defense note: Runs the find helper used by this script.
    public static RunShopOffer Find(RunBuffType type)
    {
        for (int i = 0; i < offers.Length; i++)
        {
            if (offers[i].BuffType == type)
                return offers[i];
        }

        return null;
    }

    // Defense note: Runs the outgoing damage multiplier helper used by this script.
    public static float OutgoingDamageMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0f, 1f + Sum(activeBuffs, offer => offer.OutgoingDamageBonus));
    }

    // Defense note: Runs the incoming damage multiplier helper used by this script.
    public static float IncomingDamageMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0.1f, 1f + Sum(activeBuffs, offer => offer.IncomingDamageBonus));
    }

    // Defense note: Runs the skill cost reduction helper used by this script.
    public static int SkillCostReduction(List<RunBuffType> activeBuffs)
    {
        int total = 0;
        if (activeBuffs == null)
            return total;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            RunShopOffer offer = Find(activeBuffs[i]);
            if (offer != null)
                total += offer.SkillCostReduction;
        }

        return total;
    }

    // Defense note: Runs the clock speed multiplier helper used by this script.
    public static float ClockSpeedMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0.1f, 1f + Sum(activeBuffs, offer => offer.ClockSpeedBonus));
    }

    // Defense note: Runs the exp reward multiplier helper used by this script.
    public static float ExpRewardMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0f, 1f + Sum(activeBuffs, offer => offer.ExpRewardBonus));
    }

    // Defense note: Builds the active summary data or UI structure.
    public static string BuildActiveSummary(List<RunBuffType> activeBuffs)
    {
        if (activeBuffs == null || activeBuffs.Count == 0)
            return "No run buffs active.";

        var builder = new StringBuilder();
        for (int i = 0; i < activeBuffs.Count; i++)
        {
            RunShopOffer offer = Find(activeBuffs[i]);
            if (offer == null)
                continue;

            if (builder.Length > 0)
                builder.AppendLine();
            builder.Append("- ");
            builder.Append(offer.DisplayName);
            builder.Append(": ");
            builder.Append(offer.ShortLabel);
            if (offer.HasTradeoff)
                builder.Append(" [TRADE]");
        }

        return builder.Length > 0 ? builder.ToString() : "No run buffs active.";
    }

    // Defense note: Runs the sum helper used by this script.
    private static float Sum(List<RunBuffType> activeBuffs, Func<RunShopOffer, float> selector)
    {
        float total = 0f;
        if (activeBuffs == null)
            return total;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            RunShopOffer offer = Find(activeBuffs[i]);
            if (offer != null)
                total += selector(offer);
        }

        return total;
    }
}
