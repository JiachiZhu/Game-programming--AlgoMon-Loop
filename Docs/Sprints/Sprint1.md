# Sprint 1 — Apr 19 to Apr 25

## Goal
Establish the complete project foundation: data models, messaging system, and core battle logic are all in place so that every future system has a stable, decoupled base to build on.

---

## Planned Issues

| # | Title | Status |
|---|---|---|
| #1 | [Setup] Select Unity template & initialize project folder structure | ✅ Done |
| #2 | [Core] Implement EventBus (Observer Pattern) | ✅ Done |
| #3 | [Data] Define AlgoMonData ScriptableObject | ✅ Done |
| #4 | [Core] Create GameManager singleton | ✅ Done |
| #5 | [Battle] Implement PriorityQueue (min-heap) | ✅ Done |
| #6 | [Battle] Implement CombatResolver (ASD counter + element matrix) | ✅ Done |
| #7 | [Fix] TurnQueue priority inverted — slowest unit acts first | ✅ Done |

## Unplanned Issues (added mid-sprint)

| # | Title | Status |
|---|---|---|
| #8 | [Battle] Integrate basePower into CombatResolver damage formula | ✅ Done |
| #9 | [Battle] Add skill priority tier to SkillData and TurnQueue | ✅ Done |
| #10 | [Data] Design global skill pool and species learnsets | 🔄 Carry-over |
| #11 | [Data] Add Subroutine (passive ability) field to AlgoMonData | ✅ Done |
| #12 | [Battle] Replace subtraction defence with ratio-based formula | ✅ Done |

---

## Decisions & Notes

### Architecture
- **Unity template:** Chose Universal 2D (URP) over Built-In pipeline. Rationale: URP supports Shader Graph and post-processing effects suited to the cyberpunk terminal aesthetic.
- **Stat naming:** Renamed `Bandwidth` → `Throughput` for clearer domain semantics.
- **Payload vs Party:** Two-tier roster — Payload is the unlimited warehouse (QuickSort in Lab); Party is the active run squad capped at 6.

### Battle System
- **Damage formula:** Replaced flat subtraction with ratio-based defence: `damage = Floor(raw × 50 / (50 + defence))`. Softcap at defence=50 halves incoming damage without hard-zeroing. Validated: basePower ≈ 45 gives 5-round neutral / 3-round advantage at BST=600 lv50.
- **ASD system redesign:** Counter check is opt-in per skill via `canCounter` flag — skills without it resolve by speed/priority only. All Defense skills must set `canCounter = true`. Defense has a 1-turn cooldown to prevent passive looping.
- **CounterSuccessType:** Four types defined — `None` (delayed), `Nullify` (cancel + no CP cost), `Block` (damage reduced by `counterBlockPercent`), `SelfBuff` (extra buff stacks on self). Effects are per-skill, not tied to instruction type.
- **Skill priority tier:** `skill.priority × 10000 + ClockSpeed` as effective heap key. +1 first-strike always beats normal; ASD counter winner still overrides all priority tiers via `ForceAfter()`.
- **CP system:** Max 10 CP per AlgoMon. Recharge is a universal Status skill (0 CP, +5 CP, `canCounter = true`). High-cost skills (5–6 CP) are high-risk; if countered, CP depends on counterSuccessType.

### Species & Skills
- **BST = 600** for all 6 species; average IV = 100 per stat dimension.
- **Subroutine (passive ability):** Each species has a hardwired `SubroutineData` asset. Trigger + effect system defined; BattleManager logic deferred to Sprint 2.
- **Skill pool expanded to 33 skills** (18 Attack + 8 Defense + 7 Status, covering all 6 elements plus Normal type).
- **StatusType expanded:** Added `Burn` (5%/layer, max 4), `Freeze` (−15% speed/layer, max 3, no Fire-type clearance), `Leech` (5%/layer, max 3), `Ensnare`, `Concurrent`, `BufferLoad`, stat-buff types (`ComputingUp`, `ThroughputUp`, `FirewallUp`, `EncryptionUp`).
- **SkillData finalised:** All fields locked — `basePower`, `cpCost`, `priority`, `canCounter`, `counterSuccessType`, `counterBlockPercent`, `counterCPDrain`, `counterCPDiscount`, `counterPermanentCPCostReduce`, `onHitFirewallShred`, `onHitStatus` family.

---

## Outcome

Original 7 issues completed as planned. Sprint scope expanded significantly with 5 additional issues:

**Core systems delivered:**
- EventBus, GameManager, AlgoMonData/Instance, PriorityQueue, TurnQueue, CombatResolver — all stable and decoupled
- Ratio-based damage formula validated against BST=600 balance targets
- Full ASD battle design finalised: per-skill opt-in, asymmetric counter effects, CP resource system documented
- Subroutine (passive ability) data layer complete
- SkillData schema fully locked with all 33-skill pool requirements covered
- StatusType expanded to support all designed status conditions

**Assets produced:**
- 6 species sprite portraits (base + evolved), imported and .meta tracked
- Battle background artwork generated (TheArena scene)

---

## Carry-over

| Item | Target Sprint |
|------|-------------|
| #10 — LearnsetEntry struct + AlgoMonInstance.knownSkills (code) | Sprint 2 |
| #10 — 33 SkillData ScriptableObject assets + SkillPool.md | Sprint 2 |
| #10 — 6 species learnset assignment | Sprint 2 |
| 6 SubroutineData assets (one per species) | Sprint 2 |
| TheArena scene + BattleManager | Sprint 2 |
| TheGrid DAG generator | Sprint 2 |
