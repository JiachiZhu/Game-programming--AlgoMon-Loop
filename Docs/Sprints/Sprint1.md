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

---

## Decisions & Notes

- **Unity template:** Chose Universal 2D (URP) over Built-In pipeline. Rationale: URP supports Shader Graph and post-processing effects suited to the cyberpunk terminal aesthetic; migrating from Built-In mid-project would be costly.
- **Stat naming:** Renamed `Bandwidth` → `Throughput` for clearer domain semantics. Throughput (network output volume) better represents the magic-attack equivalent than Data Bandwidth, which already carries a broader infrastructure meaning elsewhere in the design.
- **Payload vs Party:** Clarified two-tier roster system — Payload is the unlimited warehouse (sorted via QuickSort in the Lab); Party is the active run squad capped at 6.

---

## Outcome

All 7 issues completed. The full data and messaging foundation is in place:

- EventBus (Observer Pattern) decouples all game systems
- AlgoMonData / AlgoMonInstance implement the IV/EXP hardware-software split
- GameManager singleton manages Payload (warehouse) and Party (active squad, max 6)
- PriorityQueue (max-heap) drives ClockSpeed-based turn ordering, O(log N)
- TurnQueue wraps PriorityQueue with ASD counter override (ForceAfter)
- CombatResolver handles ASD triangle counter check and 6×6 element type chart
- Bug #7 caught and fixed: TurnQueue priority was inverted (used -ClockSpeed on a max-heap)

Design decisions made this sprint:
- Chose Universal 2D (URP) over Built-In pipeline for future visual flexibility
- Renamed Bandwidth → Throughput for clearer domain semantics
- Settled on 6 element types: Water / Fire / Grass / Ice / Electric / Ground
- ASD counter changes turn order (animation interrupt) but damage bonus is per-skill, not flat

---

## Carry-over

None. Sprint 2 will begin Arena scene UI and Grid DAG generation.
