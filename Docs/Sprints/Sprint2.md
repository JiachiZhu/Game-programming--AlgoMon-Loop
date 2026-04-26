# Sprint 2 — Apr 26 to May 2

## Goal
Make the game playable for the first time: complete the skill pool data layer, build TheArena scene, and implement a functional BattleManager so that a full battle — from turn ordering through damage resolution and status effects — can run end-to-end in the Unity editor.

---

## Planned Issues

| # | Title | Status |
|---|---|---|
| #10 | [Data] Complete skill pool — LearnsetEntry, knownSkills, 33 assets, learnsets, SkillPool.md | 🔄 In Progress |
| #13 | [Data] Create 6 SubroutineData assets (one per species) | ⬜ Pending |
| #14 | [Arena] Create TheArena scene and battle UI layout | ⬜ Pending |
| #15 | [Battle] Implement BattleManager — turn loop, ASD check, CP management, damage | ⬜ Pending |
| #16 | [Battle] Implement status effect tick system | ⬜ Pending |
| #17 | [Battle] Implement defense cooldown and Subroutine basic triggers | ⬜ Pending |

---

## Scope Notes

- **TheGrid DAG generator** is deliberately deferred to Sprint 3. Arena battle loop is the critical path; Grid depends on a working battle to be meaningful.
- **Full animation system** (coroutine-based sprite tweens) is a stretch goal for this sprint — basic functional battle takes priority over visual polish.
- **SubroutineData logic** (BattleManager reading and applying subroutine effects) is included in #17 for `OnBattleStart` and `OnCounterWin` triggers only; remaining triggers deferred.

## Issue Dependency Order

```
#10 (skill assets) ──┐
#13 (subroutines)  ──┼──▶  #15 (BattleManager)  ──▶  #16 (status ticks)
#14 (Arena scene)  ──┘                            ──▶  #17 (cooldown + subroutine)
```

---

## Decisions & Notes

*(To be filled during the sprint)*

---

## Outcome

*(To be filled at sprint close)*

---

## Carry-over

*(To be filled at sprint close)*
