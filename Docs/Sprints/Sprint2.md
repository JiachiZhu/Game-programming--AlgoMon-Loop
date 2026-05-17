# Sprint 2 — Apr 26 to May 18

## Goal
Make the game playable for the first time: complete the skill pool data layer, build TheArena scene, and implement a functional BattleManager so that a full battle — from turn ordering through damage resolution and status effects — can run end-to-end in the Unity editor.

---

## Sprint Context — Why this sprint runs 23 days

The originally planned window was Apr 26 – May 2 (one week). Two factors stretched it to three weeks:

1. **Vacation block May 1 – May 5.** Following the regular game-dev principle of *no work on vacation*, those five days produced minimal to zero progress.
2. **Computer theory exam in the same period.** Game-programming time was further compressed by exam preparation.
3. **Scope weight.** The remaining issues (#15 BattleManager, #16 status ticks, #17 cooldown + subroutine triggers) are the central battle loop; rushing them would compromise every system that depends on them. A longer sprint was preferred over a partial battle implementation.

---

## Planned Issues

| # | Title | Status |
|---|---|---|
| #10 | [Data] Complete skill pool — LearnsetEntry, knownSkills, 34 + 1 assets, learnsets, SkillPool.md | ✅ Done |
| #13 | [Data] Create 6 SubroutineData assets (one per species) | ✅ Done |
| #14 | [Arena] Create TheArena scene and battle UI layout | ✅ Done |
| #15 | [Battle] Implement BattleManager — turn loop, ASD check, CP management, damage | ✅ Done |
| #16 | [Battle] Implement status effect tick system | ✅ Done |
| #17 | [Battle] Implement defense cooldown and Subroutine basic triggers | ✅ Done |
| #18 | [Polish] Add basic battle presentation feedback | ✅ Done |

### #14 — Acceptance criteria (from issue tracker)

> Objective: *"Set up the foundational Unity scene for battles and construct the basic UI layout required for the BattleManager to interface with. The priority is functional UI hooks over visual polish."*

| AC | Status | Evidence |
|---|---|---|
| 1. Create a new Unity scene named `TheArena` | ✅ | `Assets/_AlgoMon/Scenes/TheArena.unity` |
| 2. Build the base Canvas layout including placeholder UI elements for Player/Enemy HP, CP, and basic combat status | ✅ | `BattleHud.prefab` provides both combatant panels with Battery bar, 10-dot CP row, status text + 4 skill buttons + Recharge / Bag / Switch / Flee + Skill Details panel |
| 3. Ensure UI elements are correctly exposed for BattleManager script references | ✅ | `BattleHudController.cs` — MonoBehaviour on `Canvas_Arena` exposing `SetCombatant / SetBattery / SetCP / SetStatus / SetSkillSlot / SetRound / SetBattleState / SetSkillDetail` plus `SkillSlotClicked` and `ActionClicked` events |

---

## Unplanned Issues (added mid-sprint — data layer refactor wave)

Resulted from implementing #10 / #13 and discovering that `SkillData`'s targeting model
could not cleanly express several SkillPool entries. Documented in `BattleDesign.md`.

| # | Title | Status |
|---|---|---|
| — | [Data] Add `StatusDurationType` enum (Permanent / WhileOnField / Turns) | ✅ Done |
| — | [Data] Apply `StatusDurationType` to `onHitFirewallShred` + `onHitStatus` durations | ✅ Done |
| — | [Data] Add `DamageType.None` for Defense / Status; guard `ResolveDamage` | ✅ Done |
| — | [Data] Add `StatusTarget` enum + base skill effect fields for Status skills | ✅ Done |
| — | [Data] Full SkillData restructure — explicit Self / Opponent targeting on every effect | ✅ Done |
| — | [Data] Add `isUniversal` + `baseHealCPAmount` to SkillData (Recharge support) | ✅ Done |
| — | [Data] Add `elementType` field to AlgoMonData (CombatResolver reference fix) | ✅ Done |
| — | [Battle] Update CombatResolver to use `counterSelfDamageMultiplier` after refactor | ✅ Done |
| — | [Docs] Author `Docs/BattleDesign.md` — full battle system reference for #15 | ✅ Done |
| — | [Assets] Rename `Sleep Thread？.asset` → `Sleep Thread.asset` (invalid char in filename) | ✅ Done |

---

## Scope Notes

- **TheGrid DAG generator** is deliberately deferred to Sprint 3. Arena battle loop is the critical path; Grid depends on a working battle to be meaningful.
- **Full animation system** (coroutine-based sprite tweens) is a stretch goal for this sprint — basic functional battle takes priority over visual polish.
- **SubroutineData logic** (BattleManager reading and applying subroutine effects) is included in #17 for `OnBattleStart` and `OnCounterWin` triggers only; remaining triggers deferred.
- **Battle presentation polish** is included as #18 after #17: background, floating feedback numbers, hit flash/shake, Battery / CP interpolation, and lightweight status feedback. Full skill-specific VFX remains deferred.
- Former "special case" skills now resolve through generic `SkillData` fields read by BattleManager. No skill-name-specific BattleManager branches are expected for Ignite Loop, Short Circuit, Absolute Zero Crash, Safe Mode, Sleep Thread, or Spore Script.

## Issue Dependency Order

```
#10 (skill assets) ──┐
#13 (subroutines)  ──┼──▶  #15 (BattleManager)  ──▶  #16 (status ticks)
#14 (Arena scene)  ──┘                            ──▶  #17 (cooldown + subroutine)
```

`#14` and `#15` are partially parallelizable: the scene scaffold (already done) needs no battle code, but skill button wiring and resource bars must wait for `#15`'s public surface.

---

## Decisions & Notes

### Data layer

- **SkillData targeting model finalised:** every effect field now explicitly declares Self vs Opponent. Removed implicit "counterSelfStatus is always self-buff" assumption — the field name is kept for back-compat but `StatusTarget` enum is the source of truth.
- **`StatusDurationType` replaces magic-number duration:** `0` no longer overloads as "permanent". Choices are now `Permanent`, `WhileOnField`, `Turns` (with `counterStatusDuration` int), removing two ambiguity bugs found while creating Hardcode Armor and Circuit Breaker assets.
- **Recharge as universal skill:** added `isUniversal` flag and `baseHealCPAmount` field rather than coding a special case in BattleManager. Any future "all species learn this" skill follows the same pattern.
- **`DamageType.None`:** Defense and Status skills now have a real "no damage path" type rather than relying on `basePower = 0`. `CombatResolver.ResolveDamage()` early-returns on `None`, removing a class of divide-by-zero / negative-damage edge cases.

### Arena scene

- **Layout target:** 1920×1080 reference; `CanvasScaler.MatchWidthOrHeight = 0.5` for balanced scaling.
- **Three-zone UI:** TopBar (round + state) / CombatLayer (both combatant panels + center message) / CommandPanel (4-skill grid + Recharge / Bag / Switch / Flee + Skill Details panel). Matches BattleDesign §1 flow.
- **HUD source-of-truth: prefab (migration complete).** Originally the HUD was generated by builder scripts; that path has been retired. The HUD now lives in `Assets/_AlgoMon/Prefabs/UI/Arena/BattleHud.prefab`, and `Canvas_Arena` in `TheArena.unity` is a connected prefab instance at scene root. All visual edits should happen in prefab edit mode - they persist across plays.
- **Stable scripting surface: `BattleHudController.cs`.** Lives on the HUD prefab root. Self-binds at runtime by walking the canvas hierarchy by name. BattleManager (#15) drives the HUD through its API (`SetCombatant / SetBattery / SetCP / SetStatus / SetSkillSlot / SetSkillSlotAvailable / SetActionButtonAvailable / SetRound / SetBattleState / SetSkillDetail`) and events (`SkillSlotClicked`, `ActionClicked`). The Find-by-name binding will tolerate cosmetic prefab edits but breaks silently if a designer renames a node — node-name list documented in `BattleHudController.Bind()`.
- **HUD lifecycle decision for #15-#17:** keep the connected scene instance `Canvas_Arena` in `TheArena.unity` as the runtime HUD. This keeps the BattleManager / status tick / subroutine work deterministic while the battle loop is still being built. Dynamic HUD instantiation per match remains the long-term direction for multi-battle, PvP, or online flows, but it is explicitly deferred and is not a blocker for #16 / #17.
- **Skill tag placeholders:** Every skill slot has CP / PWR / Counter roots. `SetSkillSlot` fills CP, toggles PWR when `basePower > 0`, and toggles Counter for Defense counter skills, so #15 can place any skill in any slot without prefab edits.

### Documentation

- **`Docs/BattleDesign.md` is now the canonical reference** for BattleManager implementation. Section 3 (counter system) and Section 6 (status persistence on swap) are the two most likely places where #15/#16 implementation will need to verify against the doc rather than guess.
- **`Docs/BattlePresentation.md` is the handoff for animation / VFX work.** Issue #18 establishes the generic presentation template; future per-species animation profiles and per-skill VFX profiles should start from that document instead of guessing from runtime code.

---

## Outcome

- `BattleManager` now runs the core battle loop in `TheArena`: player action selection, simple enemy action selection, ASD counter ordering, skill priority / ClockSpeed ordering, CP spend / Recharge recovery, damage resolution, rolling battle log, and battle end events.
- `BattleHudController` is the stable bridge between the prefab HUD and battle runtime. It supports live Battery / CP / status updates, skill tags, skill availability, action-button availability, and persistent battle-log text with hover previews.
- Status ticking landed in #16. Defense cooldowns, the scoped Subroutine triggers, and the former special counter hooks landed in #17.

### Issue #15 closure note

- Implementation landed in commit `6d0bb61` (`Implement core BattleManager loop`).
- Verified in the Unity editor on 2026-05-16: compile clean, a smoke battle advances from Round 1 to Round 2, both Sortex and Cachelon act, CP / Battery values change, and the HUD remains bound through the scene-resident `Canvas_Arena` prefab instance.
- #15 is considered complete and ready to close. Status ticks are handled by #16; defense cooldowns, scoped Subroutine triggers, and data-driven former special counter hooks are handled by #17.

### Issue #16 closure note

- Runtime status state now tracks stacks, source/caster, duration type, and temporary stat / CP modifiers through `BattleStatusSet`.
- BattleManager applies base, counter-win, and on-hit statuses; Burn and Leech tick after both queued actions resolve; timed statuses decrement after round-end ticks.
- Freeze immediately affects future stat / CP calculations (`-15% ClockSpeed`, `+1 CP cost` per layer), but the current action queue is not re-sorted mid-round.
- Verified in the Unity editor on 2026-05-17: compile clean, Play Mode smoke advanced TheArena from Round 1 to Round 2 with CP / Battery changes and no runtime console errors.

### Issue #17 completion note

- Defense skills now enter a one-round cooldown only after successful CP spend and execution; failed or unpaid Defense attempts do not start cooldown.
- BattleManager applies `SubroutineData` for the scoped `OnBattleStart` and `OnCounterWin` triggers. `OnCounterWin` Subroutine rewards are applied after the winning action resolves, so they do not retroactively buff that hit.
- Counter-win and Subroutine-trigger effects share `BattleEffectBundle`, so future Subroutine triggers can reuse one effect application path instead of copying field-by-field logic.
- Former special counter skills are data-driven: `counterRecast`, `counterPermanentCPReduce`, `counterNextPriorityBonus`, `counterNextBasePowerBonus`, `counterForceOpponentLast`, heal, cleanse, and status-apply hooks are all read through generic fields.
- Verified in the Unity editor on 2026-05-17: compile clean, 20/20 issue #17 runtime checks passed, with #16 smoke coverage for Burn, Freeze, BufferLoad, and Concurrent.

### Issue #18 completion note

- TheArena now has a generic battle presentation template: idle sprite motion, action lunge, counter clash timing, hit flash/shake, status pulse, floating feedback text, Battery / CP interpolation, and camera-cover background fitting.
- `BattlePresentationController` consumes battle events without owning battle logic; `BattleSpriteAnimator` owns reusable sprite motion; `BattleBackgroundFitter` keeps the arena background covering the camera.
- The current animation is intentionally a fallback template. Per-species `BattleAnimationProfile` assets and per-skill `SkillVfxProfile` assets are deferred to a future polish issue and sketched in `Docs/BattlePresentation.md`.
- Verified in the Unity editor on 2026-05-17: compile clean, Play Mode smoke covered background fit and a real Defense counter path with action suppression consumed after resolution.

---

## Carry-over

*(To be filled at sprint close. Likely candidates:
TheGrid DAG generator -> Sprint 3;
remaining 5 Subroutine triggers beyond `OnBattleStart` / `OnCounterWin` -> Sprint 3.)*

### Pre-#15 cleanup / decisions

- **HUD builder cleanup:** Resolved. The runtime HUD path is the connected `BattleHud.prefab` instance in `TheArena.unity`.
- **#15 BattleManager wiring decision:** Resolved. Keep the scene-resident connected `Canvas_Arena` HUD instance for #15-#17. Revisit `BattleManager.Instantiate(hudPrefab)` later when the project needs multiple battle sessions, PvP, or online match lifecycle isolation.
