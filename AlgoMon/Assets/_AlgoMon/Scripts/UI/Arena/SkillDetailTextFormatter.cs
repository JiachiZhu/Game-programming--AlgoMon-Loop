using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// Defense note: SkillDetailTextFormatter formats gameplay data into readable UI text.
internal static class SkillDetailTextFormatter
{
    // Defense note: Builds the body data or UI structure.
    public static string BuildBody(string metaLine, params string[] sections)
    {
        var builder = new StringBuilder();
        builder.Append(FormatMetaLine(metaLine));

        if (sections != null)
        {
            foreach (string section in sections)
            {
                if (string.IsNullOrWhiteSpace(section))
                    continue;

                builder.AppendLine();
                builder.AppendLine();
                builder.Append(Highlight(section.Trim()));
            }
        }

        return builder.ToString().Trim();
    }

    // Defense note: Runs the format meta line helper used by this script.
    public static string FormatMetaLine(string metaLine)
    {
        return $"<b><size=17>{Highlight(metaLine)}</size></b>";
    }

    // Defense note: Runs the highlight helper used by this script.
    public static string Highlight(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return SkillDetailEmphasisRegex.Replace(text, match =>
        {
            string value = match.Value;
            if (string.Equals(value, "Counter", StringComparison.OrdinalIgnoreCase))
                return $"<b><color=#FFD75E>{value}</color></b>";
            if (SkillDetailNumberRegex.IsMatch(value))
                return $"<b><color=#7EF7FF>{value}</color></b>";
            return $"<b><color=#FFB45C>{value}</color></b>";
        });
    }

    // Defense note: Builds the readable description data or UI structure.
    public static string BuildReadableDescription(SkillData skill)
    {
        if (skill == null || string.IsNullOrWhiteSpace(skill.description))
            return string.Empty;

        string text = skill.description.Trim();
        if (skill.canCounter)
            text = CounterDescriptionRegex.Replace(text, string.Empty).Trim();

        return NormalizeWhitespace(text);
    }

    // Defense note: Builds the counter summary data or UI structure.
    public static string BuildCounterSummary(SkillData skill)
    {
        if (skill == null || !skill.canCounter)
            return string.Empty;

        var parts = new List<string>();

        if (skill.counterNullifies)
            parts.Add("nullify the opposing skill");
        if (skill.counterBlockPercent > 0f)
            parts.Add($"block {FormatPercent(skill.counterBlockPercent)} incoming damage");
        if (Mathf.Abs(skill.counterSelfDamageMultiplier - 1f) > 0.001f && skill.counterSelfDamageMultiplier > 0f)
            parts.Add($"this skill damage x{FormatMultiplier(skill.counterSelfDamageMultiplier)}");
        if (skill.counterDrainOpponentCP > 0)
            parts.Add($"drain {skill.counterDrainOpponentCP} CP from opponent");
        if (skill.counterShredOpponentFirewall > 0f)
            parts.Add($"shred opponent Firewall by {FormatPercent(skill.counterShredOpponentFirewall)}");

        AddStatusPart(
            parts,
            "opponent",
            skill.counterApplyToOpponent,
            skill.counterOpponentStatusStacks,
            skill.counterOpponentStatusDurationType,
            skill.counterOpponentStatusDuration);

        if (skill.counterForceOpponentLast)
            parts.Add("force opponent to act last next turn");

        AddStatusPart(
            parts,
            "self",
            skill.counterApplyToSelf,
            skill.counterSelfStatusStacks,
            skill.counterSelfStatusDurationType,
            skill.counterSelfStatusDuration);
        AddStatusPart(
            parts,
            "self",
            skill.counterApplyToSelfSecondary,
            skill.counterSelfSecondaryStatusStacks,
            skill.counterSelfSecondaryStatusDurationType,
            skill.counterSelfSecondaryStatusDuration);

        if (skill.counterRecast)
            parts.Add("recast this skill once at 0 CP");
        if (skill.counterSelfCPDiscount > 0)
            parts.Add($"all own skill CP costs -{skill.counterSelfCPDiscount}{FormatDuration(skill.counterCPDiscountDurationType, skill.counterCPDiscountDuration)}");
        if (skill.counterPermanentCPReduce > 0)
            parts.Add($"this skill future CP cost -{skill.counterPermanentCPReduce} permanently");
        if (skill.counterNextPriorityBonus != 0)
            parts.Add($"next priority {FormatSigned(skill.counterNextPriorityBonus)}");
        if (skill.counterNextBasePowerBonus != 0)
            parts.Add($"next BP {FormatSigned(skill.counterNextBasePowerBonus)}");
        if (skill.counterSelfHealPercent > 0f)
            parts.Add($"restore {FormatPercent(skill.counterSelfHealPercent)} Battery");
        if (skill.counterClearsOwnDebuffs)
            parts.Add("clear own temporary debuffs");

        if (parts.Count > 0)
            return $"Counter: {string.Join("; ", parts)}.";

        string fallback = ExtractCounterDescription(skill.description);
        if (string.IsNullOrWhiteSpace(fallback))
            return string.Empty;

        return EndsWithSentencePunctuation(fallback)
            ? $"Counter: {fallback}"
            : $"Counter: {fallback}.";
    }

    /// <summary>
    /// Builds a highlighted one-line effect summary for a species Subroutine (passive),
    /// using the same status/duration vocabulary as the skill counter summary so numbers
    /// and effect terms light up via Highlight().
    /// </summary>
    // Defense note: Builds the subroutine summary data or UI structure.
    public static string BuildSubroutineSummary(SubroutineData subroutine)
    {
        if (subroutine == null)
            return string.Empty;

        var parts = new List<string>();

        if (subroutine.drainOpponentCP > 0)
            parts.Add($"drain {subroutine.drainOpponentCP} CP from opponent");
        if (subroutine.shredOpponentFirewall > 0f)
            parts.Add($"shred opponent Firewall by {FormatPercent(subroutine.shredOpponentFirewall)}{FormatDuration(subroutine.firewallShredDurationType, subroutine.firewallShredDuration)}");

        AddStatusPart(
            parts,
            "opponent",
            subroutine.applyToOpponent,
            subroutine.opponentStatusStacks,
            subroutine.opponentStatusDurationType,
            subroutine.opponentStatusDuration);

        if (subroutine.forceOpponentLast)
            parts.Add("force opponent to act last next turn");

        AddStatusPart(
            parts,
            "self",
            subroutine.applyToSelf,
            subroutine.selfStatusStacks,
            subroutine.selfStatusDurationType,
            subroutine.selfStatusDuration);

        if (subroutine.selfCPDiscount > 0)
            parts.Add($"all own skill CP costs -{subroutine.selfCPDiscount}{FormatDuration(subroutine.cpDiscountDurationType, subroutine.cpDiscountDuration)}");
        if (subroutine.nextPriorityBonus != 0)
            parts.Add($"next priority {FormatSigned(subroutine.nextPriorityBonus)}");
        if (subroutine.nextBasePowerBonus != 0)
            parts.Add($"next BP {FormatSigned(subroutine.nextBasePowerBonus)}");
        if (subroutine.selfHealPercent > 0f)
            parts.Add($"restore {FormatPercent(subroutine.selfHealPercent)} Battery");
        if (subroutine.clearsOwnDebuffs)
            parts.Add("clear own temporary debuffs");

        if (parts.Count == 0)
            return string.Empty;

        string joined = string.Join("; ", parts);
        return $"Effect: {char.ToUpperInvariant(joined[0])}{joined.Substring(1)}.";
    }

    // Defense note: Adds the status part entry into the target collection or UI.
    private static void AddStatusPart(
        List<string> parts,
        string target,
        StatusType status,
        int stacks,
        StatusDurationType durationType,
        int duration)
    {
        if (stacks <= 0)
            return;

        string durationText = FormatDuration(durationType, duration);
        bool self = string.Equals(target, "self", StringComparison.OrdinalIgnoreCase);

        switch (status)
        {
            case StatusType.ComputingUp:
                parts.Add($"{(self ? "gain" : "apply")} Computing Power +{12 * stacks}%{TargetSuffix(self)}{durationText}");
                break;
            case StatusType.ThroughputUp:
                parts.Add($"{(self ? "gain" : "apply")} Throughput +{12 * stacks}%{TargetSuffix(self)}{durationText}");
                break;
            case StatusType.FirewallUp:
                parts.Add($"{(self ? "gain" : "apply")} Firewall +{10 * stacks}%{TargetSuffix(self)}{durationText}");
                break;
            case StatusType.EncryptionUp:
                parts.Add($"{(self ? "gain" : "apply")} Encryption +{10 * stacks}%{TargetSuffix(self)}{durationText}");
                break;
            default:
                parts.Add($"{(self ? "gain" : "apply")} {StatusName(status)} x{stacks}{TargetSuffix(self)}{durationText}");
                break;
        }
    }

    // Defense note: Runs the target suffix helper used by this script.
    private static string TargetSuffix(bool self)
    {
        return self ? string.Empty : " to opponent";
    }

    // Defense note: Runs the status name helper used by this script.
    private static string StatusName(StatusType status)
    {
        switch (status)
        {
            case StatusType.BufferLoad:   return "Buffer Load";
            case StatusType.ComputingUp:  return "Computing Power";
            case StatusType.ThroughputUp: return "Throughput";
            case StatusType.FirewallUp:   return "Firewall";
            case StatusType.EncryptionUp: return "Encryption";
            default:                      return status.ToString();
        }
    }

    // Defense note: Runs the format duration helper used by this script.
    private static string FormatDuration(StatusDurationType durationType, int duration)
    {
        switch (durationType)
        {
            case StatusDurationType.Permanent:
                return " permanently";
            case StatusDurationType.WhileOnField:
                return " while on field";
            case StatusDurationType.Turns:
                int turns = Mathf.Max(1, duration);
                return turns == 1 ? " for 1 turn" : $" for {turns} turns";
            default:
                return string.Empty;
        }
    }

    // Defense note: Runs the format percent helper used by this script.
    private static string FormatPercent(float fraction)
    {
        float percent = Mathf.Max(0f, fraction) * 100f;
        return Mathf.Approximately(percent, Mathf.Round(percent))
            ? $"{Mathf.RoundToInt(percent)}%"
            : $"{percent.ToString("0.#", CultureInfo.InvariantCulture)}%";
    }

    // Defense note: Runs the format multiplier helper used by this script.
    private static string FormatMultiplier(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // Defense note: Runs the format signed helper used by this script.
    private static string FormatSigned(int value)
    {
        return value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }

    // Defense note: Runs the normalize whitespace helper used by this script.
    private static string NormalizeWhitespace(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : WhitespaceRegex.Replace(text.Trim(), " ");
    }

    // Defense note: Runs the extract counter description helper used by this script.
    private static string ExtractCounterDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return string.Empty;

        Match match = CounterDescriptionRegex.Match(description);
        if (!match.Success)
            return string.Empty;

        return NormalizeWhitespace(match.Groups["effect"].Value);
    }

    // Defense note: Ends the s with sentence punctuation flow and clears its runtime state.
    private static bool EndsWithSentencePunctuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        char last = text.TrimEnd()[text.TrimEnd().Length - 1];
        return last == '.' || last == '!' || last == '?';
    }

    private static readonly Regex CounterDescriptionRegex = new Regex(
        @"Counter\s*(?:win|effect)?\s*:\s*(?<effect>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex WhitespaceRegex = new Regex(
        @"\s+",
        RegexOptions.CultureInvariant);

    private static readonly Regex SkillDetailEmphasisRegex = new Regex(
        @"\bCounter\b|[-+]?\d+(?:\.\d+)?%?|\b(?:Burn(?:ed|ing)?|Freeze|Frozen|Leech|Ensnare|Concurrent|Buffer\s*Load|BufferLoad|Buffer|Computing(?: Power)?|Throughput|Firewall|Encryption|Overclock|Throttle|Corrupted|Priority|Battery|Damage|Status|CP|BP)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SkillDetailNumberRegex = new Regex(
        @"^[-+]?\d+(?:\.\d+)?%?$",
        RegexOptions.CultureInvariant);
}
