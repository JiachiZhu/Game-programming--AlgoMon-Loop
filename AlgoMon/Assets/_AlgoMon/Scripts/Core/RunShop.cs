using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum RunBuffType
{
    PacketAccelerator,
    CpCache,
    ShieldLayer,
    DataSiphon,
    VolatileOverclock
}

[Serializable]
public sealed class RunShopOffer
{
    public RunShopOffer(
        RunBuffType buffType,
        string displayName,
        string shortLabel,
        string description,
        int computeCost,
        float outgoingDamageBonus,
        float incomingDamageBonus,
        int skillCostReduction,
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
    public float ExpRewardBonus { get; private set; }
    public bool HighRisk { get; private set; }
}

public static class RunShopCatalog
{
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
            false),
        new RunShopOffer(
            RunBuffType.DataSiphon,
            "Data Siphon",
            "EXP+25%",
            "User and AlgoMon EXP rewards are increased by 25% this run.",
            8,
            0f,
            0f,
            0,
            0.25f,
            false),
        new RunShopOffer(
            RunBuffType.VolatileOverclock,
            "Volatile Overclock",
            "RISK DMG+35%",
            "High risk: player attacks deal 35% more damage, but incoming damage rises by 20%.",
            4,
            0.35f,
            0.20f,
            0,
            0f,
            true)
    };

    public static RunShopOffer[] Offers
    {
        get { return offers; }
    }

    public static RunShopOffer Find(RunBuffType type)
    {
        for (int i = 0; i < offers.Length; i++)
        {
            if (offers[i].BuffType == type)
                return offers[i];
        }

        return null;
    }

    public static float OutgoingDamageMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0f, 1f + Sum(activeBuffs, offer => offer.OutgoingDamageBonus));
    }

    public static float IncomingDamageMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0.1f, 1f + Sum(activeBuffs, offer => offer.IncomingDamageBonus));
    }

    public static int SkillCostReduction(List<RunBuffType> activeBuffs)
    {
        int total = 0;
        if (activeBuffs == null)
            return total;

        for (int i = 0; i < activeBuffs.Count; i++)
        {
            RunShopOffer offer = Find(activeBuffs[i]);
            if (offer != null)
                total += Mathf.Max(0, offer.SkillCostReduction);
        }

        return total;
    }

    public static float ExpRewardMultiplier(List<RunBuffType> activeBuffs)
    {
        return Mathf.Max(0f, 1f + Sum(activeBuffs, offer => offer.ExpRewardBonus));
    }

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
        }

        return builder.Length > 0 ? builder.ToString() : "No run buffs active.";
    }

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
