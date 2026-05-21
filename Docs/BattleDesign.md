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
| **None** | Delayed via ForceAfter, still executes | Consumed | `counterSelfDamageMultiplier` applied to damage |
| **Nullify** | Fully cancelled | **Not consumed** | `counterSelfDamageMultiplier` applied to damage |
| **Block** | Still executes | Consumed | Opponent's damage × `(1 − counterBlockPercent)` |
| **SelfBuff** | Delayed via ForceAfter | Consumed | Apply `counterSelfStatus × counterBonusValue` to self |

**Additional counter win effects** (checked regardless of counterSuccessType):
- `counterDrainOpponentCP > 0` → drain that many CP from opponent
- `counterSelfCPDiscount > 0` → reduce all own skill CP costs by that amount for its configured duration
- `counterPermanentCPReduce > 0` → permanently reduce this skill's future runtime CP cost for this battle (min 0)
- `counterRecast = true` → re-cast this skill once at 0 CP after the first cast resolves
- `counterNextPriorityBonus != 0` → modify this unit's next action priority
- `counterNextBasePowerBonus != 0` → modify this unit's next action basePower
- `counterForceOpponentLast = true` → force the opponent's next action to resolve last
- `counterSelfHealPercent > 0` and `counterClearsOwnDebuffs = true` → generic heal / cleanse hooks
- Explicit counter target fields apply status effects to either self or opponent, so reversed-target effects do not need custom BattleManager branches.

These hooks intentionally keep former special-case skills data-driven: Ignite Loop uses `counterRecast`, Short Circuit uses `counterSelfDamageMultiplier` plus a next-priority buff, and Absolute Zero Crash uses `counterForceOpponentLast`.

Counter-win and Subroutine-trigger effects both resolve through BattleManager's
shared `BattleEffectBundle` path. The standard order is:
drain CP -> shred Firewall -> apply opponent status -> force opponent last ->
apply self status -> CP discount / permanent CP reduction -> next priority /
next basePower -> heal -> clear temporary debuffs.

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
Defense skills have a **1-turn cooldown** after successful execution. A unit that used a Defense skill last turn cannot use a Defense skill this turn. If a Defense skill never executes, it does not enter cooldown. This prevents passive looping.

---

## 5. Damage Formula

```
raw    = rawAttack × (skill.basePower / 100) × elementMult × counterMult
damage = Max(1, Floor(raw × 50 / (50 + defence)))
```

- **rawAttack**: `AlgoMon.ComputingPower` if `skill.damageType == Computing`; `AlgoMon.Throughput` if Throughput.
- **defence**: `AlgoMon.Firewall` for Computing skills; `AlgoMon.Encryption` for Throughput skills.
- **elementMult**: from 6×6 `ElementChart` in `CombatResolver`. Normal type always returns 1.0.
- **counterMult**: `skill.counterSelfDamageMultiplier` if this skill won the ASD check; else 1.0.
- Skills with `damageType == None` (Defense / Status) return 0 from `ResolveDamage()`.

---

## 6. Status Effect System

### Status types and mechanics

| Status | Effect per layer | Max layers | Cleared by |
|--------|-----------------|-----------|-----------|
| **Burn** | −2% max Battery per layer per tick. After each Burn tick, stacks become `Floor(stacks / 2)` | No cap | Stacks reach 0, or swap |
| **Freeze** | −15% ClockSpeed and +1 skill CP cost per layer | 3 | Swap or special cleanse only; NOT cleared by Fire hits |
| **Leech** | Target loses 3% max Battery per layer per turn; caster heals the same amount | 3 | Duration expiry or swap |
| **Ensnare** | Cannot swap this AlgoMon out | — | Duration expiry only |
| **Concurrent** | Next skill fires twice (costs 2× CP) | — | Clears immediately after activation |
| **BufferLoad** | Next skill CP cost −4 (min 0) | 1 | Clears immediately after activation |
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

**Burn exception:** Burn does not use a turn-duration countdown. Treat Burn applications as stack-only effects, stored with `StatusDurationType.WhileOnField` so swapping still clears them. Applying Burn during a round only adds stacks; Burn damage and stack-halving happen together at round end.

For `Turns` statuses, the round in which the status is applied does not consume one duration count. A 3-turn status applied during Round 1 remains through the next three round-end countdowns unless it is cleared early.

### Status persistence on swap ⚠️ CRITICAL RULE

When an AlgoMon is swapped out:
- All **temporary** statuses (duration > 0) are **immediately cleared**.
- All **permanent** statuses (duration = 0) **survive the swap** and remain active when the AlgoMon returns.

**Implication**: Ensnare's strategic value is preventing the opponent from clearing their own temporary debuffs by swapping. Permanent FirewallUp stacks (Hardcode Armor) persist through swaps and accumulate across the whole battle.

### Runtime tick timing

At the end of each full round, after both queued actions have resolved:
1. Burn deals damage from the stacks currently on the target, then halves its stacks.
2. Leech deals damage and heals the recorded caster by the same actual amount.
3. Turn-duration statuses decrement and expire if their remaining count reaches 0.

Status applications take effect immediately for stat and CP calculations after they are applied, but the current action queue is not re-sorted mid-round.

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
| `OnTurnStart` | Start of this unit's queued action |
| `OnCounterWin` | After winning ASD check |
| `OnCounterLose` | After losing ASD check |
| `OnDamageTaken` | After direct skill damage reduces this unit's Battery |
| `OnAllyFainted` | When any party ally reaches 0 Battery |
| `OnLowBattery` | Once per battle when Battery crosses from above 25% to 25% or below |

Issue #25 implements `OnTurnStart`, `OnCounterLose`, `OnDamageTaken`, and `OnLowBattery`.
`OnAllyFainted` remains data-only until Sprint 4 party switching creates more than one allied battle unit to observe.

Concrete timing:

- `OnBattleStart` fires once after both combatants are initialized and before the first player instruction.
- `OnTurnStart` fires when a non-cancelled queued action begins, before CP is spent.
- `OnCounterWin` fires after the winning unit's current counter action resolves.
- `OnCounterLose` fires immediately after the winner's counter effects resolve and before the turn queue executes.
- `OnDamageTaken` fires after direct skill damage is logged, before on-hit CP drain, shred, or status effects.
- `OnLowBattery` is checked after `OnDamageTaken` for direct hits, and after Burn / Leech status damage. It fires only once per battle for each unit.
- `OnAllyFainted` needs party switching / bench combatants before it can target a real surviving ally.

Direct skill counter damage modifiers still affect the current action, while Subroutine stat/status rewards create pressure for later actions rather than retroactively changing the just-resolved hit.

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
