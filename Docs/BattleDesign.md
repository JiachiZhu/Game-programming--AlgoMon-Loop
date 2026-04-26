# AlgoMon — Battle System Design Reference

> This document captures all battle mechanics, implicit rules, and design decisions
> for BattleManager implementation (Sprint 2). Any rule not stated here must be
> confirmed with the designer before assuming a default.

---

## 1. Battle Flow Overview

```
Round start
  ↓
Both sides declare instruction (A / S / D) + select a skill
  ↓
ASD check (only if BOTH or EITHER acting skill has canCounter = true)
  ↓
Compute effectivePriority for each unit:
  effectivePriority = skill.priority × 10000 + AlgoMon.ClockSpeed
  (ASD counter winner overrides via TurnQueue.ForceAfter())
  ↓
Execute actions in priority order
  ↓
Apply on-hit effects, counter effects, status ticks
  ↓
Check for KO / battle end
  ↓
Next round
```

---

## 2. Turn Order — Three-Tier Priority

| Tier | Mechanism | Implementation |
|------|-----------|---------------|
| 1 (highest) | ASD counter winner | `TurnQueue.ForceAfter(countered, counter)` |
| 2 | Skill priority tier | `effectivePriority = skill.priority × 10000 + ClockSpeed` |
| 3 (lowest) | ClockSpeed tiebreak | Falls out of the formula naturally |

- **`TurnQueue.Enqueue(mon, effectivePriority)`** — use this overload when a skill has non-zero priority.
- **`TurnQueue.Enqueue(mon)`** — fallback, uses ClockSpeed only (priority = 0 assumed).
- `ForceAfter()` hard-overrides both tier 2 and tier 3.

---

## 3. ASD Counter System

### When the check fires
The ASD check fires when **the acting skill has `canCounter = true`** AND its `instructionType` wins the matchup:
- Attack (A) beats Status (S)
- Status (S) beats Defense (D)
- Defense (D) beats Attack (A)

If `canCounter = false`, there is no RPS interaction — turn order is speed/priority only.

**All Defense skills must have `canCounter = true`.**

### What happens when a counter is won

`counterSuccessType` on the winning skill determines the result:

| Type | Opponent's skill | Opponent's CP | Extra effect on winner |
|------|-----------------|--------------|----------------------|
| **None** | Delayed via ForceAfter, still executes | Consumed | `counterSuccessMultiplier` applied to damage |
| **Nullify** | Fully cancelled | **Not consumed** | `counterSuccessMultiplier` applied to damage |
| **Block** | Still executes | Consumed | Opponent's damage × `(1 − counterBlockPercent)` |
| **SelfBuff** | Delayed via ForceAfter | Consumed | Apply `counterSelfStatus × counterBonusValue` to self |

**Additional counter win effects** (checked regardless of counterSuccessType):
- `counterCPDrain > 0` → drain that many CP from opponent
- `counterCPDiscount > 0` → reduce all own skill CP costs by that amount for `counterStatusDuration` turns
- `counterPermanentCPCostReduce > 0` → permanently reduce this skill's `cpCost` (min 0)
- `counterBonusValue > 0` → if counterSuccessType = Block AND counterBonusValue > 0, apply `counterSelfStatus × counterBonusValue` to **the attacker** (special case — see SkillPool.md Sleep Thread note)

### Special Case skills (custom BattleManager logic required)

| Skill | Required custom logic |
|-------|-----------------------|
| 点火循环 Ignite Loop | Counter win: re-cast this skill once at 0 CP |
| 短路火花 Short Circuit | Counter win: self gains "next attack priority +1 AND basePower +10" |
| 孢子脚本 Spore Script | Counter win: apply Leech to **opponent** (not self), despite using counterSelfStatus field |
| 绝对零度宕机 Absolute Zero Crash | Counter win: force opponent to act last next turn (inject priority −2) |
| 安全模式 Safe Mode | Counter win: heal self 8% max Battery AND clear all temporary negative statuses |
| 休眠线程 Sleep Thread | Counter win: apply Freeze stacks to **the attacker** (opponent), not self |

---

## 4. Computing Power (CP) System

- Each AlgoMon has **max 10 CP**.
- Skills consume CP when executed.
- **Recharge** (universal Status skill, 0 CP): restores 5 CP. `canCounter = false` — Recharge does not actively try to counter anything. However, it CAN BE countered by opponent Attack skills that have `canCounter = true` (A > S wins the matchup). If the opponent's Attack has Nullify, Recharge is cancelled and the turn is wasted.

### CP on counter loss

| Scenario | Loser's CP |
|----------|-----------|
| Skill cancelled (Nullify) | **Not consumed** — turn wasted |
| Skill blocked (Block) | **Consumed** — attack fires at reduced damage |
| Skill delayed (None/SelfBuff) | **Consumed** — skill still executes after delay |

### Defense cooldown
Defense skills have a **1-turn cooldown** after use. A unit that used a Defense skill last turn cannot use a Defense skill this turn. This prevents passive looping.

---

## 5. Damage Formula

```
raw    = rawAttack × (skill.basePower / 100) × elementMult × counterMult
damage = Max(1, Floor(raw × 50 / (50 + defence)))
```

- **rawAttack**: `AlgoMon.ComputingPower` if `skill.damageType == Computing`; `AlgoMon.Throughput` if Throughput.
- **defence**: `AlgoMon.Firewall` for Computing skills; `AlgoMon.Encryption` for Throughput skills.
- **elementMult**: from 6×6 `ElementChart` in `CombatResolver`. Normal type always returns 1.0.
- **counterMult**: `skill.counterSuccessMultiplier` if this skill won the ASD check; else 1.0.
- Skills with `damageType == None` (Defense / Status) return 0 from `ResolveDamage()`.

---

## 6. Status Effect System

### Status types and mechanics

| Status | Effect per layer | Max layers | Cleared by |
|--------|-----------------|-----------|-----------|
| **Burn** | −5% max Battery per turn | 4 | Turn-end tick, or swap (if temporary) |
| **Freeze** | −15% ClockSpeed per layer | 3 | Turn-end roll to escape; NOT cleared by Fire hits |
| **Leech** | Steal 5% max Battery per turn from target to caster | 3 | Duration expiry or swap |
| **Ensnare** | Cannot swap this AlgoMon out | — | Duration expiry only |
| **Concurrent** | Next skill fires twice (costs 2× CP) | — | Clears immediately after activation |
| **BufferLoad** | Next skill CP cost −4 (min 0) | — | Clears immediately after activation |
| **ComputingUp** | Computing Power +10% per stack | — | See persistence rules |
| **ThroughputUp** | Throughput +10% per stack | — | See persistence rules |
| **FirewallUp** | Firewall +10% per stack | — | See persistence rules |
| **EncryptionUp** | Encryption +10% per stack | — | See persistence rules |

### Status duration convention

| `counterStatusDurationType` | Meaning |
|-----------------------------|---------|
| **Permanent** | Survives swaps, lasts until battle end |
| **WhileOnField** | No turn limit; cleared immediately when AlgoMon is swapped out |
| **Turns** | Lasts `counterStatusDuration` turns; also cleared on swap |

### Status persistence on swap ⚠️ CRITICAL RULE

When an AlgoMon is swapped out:
- All **temporary** statuses (duration > 0) are **immediately cleared**.
- All **permanent** statuses (duration = 0) **survive the swap** and remain active when the AlgoMon returns.

**Implication**: Ensnare's strategic value is preventing the opponent from clearing their own temporary debuffs by swapping. Permanent FirewallUp stacks (Hardcode Armor) persist through swaps and accumulate across the whole battle.

---

## 7. On-Hit Effects

Resolved after damage is dealt (if `damage > 0`):

- `cpDrain > 0` → steal that many CP from opponent
- `onHitFirewallShred > 0` → reduce opponent's Firewall by `onHitFirewallShred` fraction (e.g. 0.2 = −20%)
- `onHitStatusStacks > 0` → apply `onHitStatus × onHitStatusStacks` to opponent for `onHitStatusDuration` turns

---

## 8. Subroutine (Passive Ability)

Each species has one `SubroutineData` asset. BattleManager checks triggers each turn:

| Trigger | When to check |
|---------|--------------|
| `OnBattleStart` | Once at battle initialization |
| `OnTurnStart` | Start of this unit's turn |
| `OnCounterWin` | After winning ASD check |
| `OnCounterLose` | After losing ASD check |
| `OnDamageTaken` | After this unit's Battery is reduced |
| `OnAllyFainted` | When any party ally reaches 0 Battery |
| `OnLowBattery` | When Battery drops below 25% of max |

---

## 9. Species Reference

| Species | Element | DmgType | Role |
|---------|---------|---------|------|
| Sortex | Electric | Computing | High speed physical burst |
| Overflux | Fire | Computing | Medium speed physical attacker |
| Nullbyte | Water | Throughput | Medium speed magical attacker |
| Recursix | Grass | Throughput | Medium speed sustain / drain |
| Cachelon | Ice | Throughput | Medium speed debuffer / control |
| Heapion | Ground | Computing | Low speed tank / physical |

**BST = 600 for all species. Balance checkpoint: Lv50, basePower ≈ 45 gives 5-round neutral / 3-round advantage.**

---

## 10. Key References

- Full skill list with all field values: `Docs/SkillPool.md`
- Species learnsets: `Docs/SkillPool.md` — Recommended Learnsets section
- Sprint 2 issue list: `Docs/Sprints/Sprint2.md`
- Data scripts: `AlgoMon/Assets/_AlgoMon/Scripts/`
