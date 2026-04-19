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
| #5 | [Battle] Implement PriorityQueue (min-heap) | 🔲 In progress |
| #6 | [Battle] Implement CombatResolver (ASD matrix) | 🔲 Planned |

---

## Decisions & Notes

- **Unity template:** Chose Universal 2D (URP) over Built-In pipeline. Rationale: URP supports Shader Graph and post-processing effects suited to the cyberpunk terminal aesthetic; migrating from Built-In mid-project would be costly.
- **Stat naming:** Renamed `Bandwidth` → `Throughput` for clearer domain semantics. Throughput (network output volume) better represents the magic-attack equivalent than Data Bandwidth, which already carries a broader infrastructure meaning elsewhere in the design.
- **Payload vs Party:** Clarified two-tier roster system — Payload is the unlimited warehouse (sorted via QuickSort in the Lab); Party is the active run squad capped at 6.

---

## Outcome
*(To be filled at end of sprint)*

---

## Carry-over
*(To be filled at end of sprint)*
