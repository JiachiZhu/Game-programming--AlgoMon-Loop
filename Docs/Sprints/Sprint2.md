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
| #15 | [Battle] Implement BattleManager — turn loop, ASD check, CP management, damage | ⬜ Pending |
| #16 | [Battle] Implement status effect tick system | ⬜ Pending |
| #17 | [Battle] Implement defense cooldown and Subroutine basic triggers | ⬜ Pending |

### #14 — Acceptance criteria (from issue tracker)

> Objective: *"Set up the foundational Unity scene for battles and construct the basic UI layout required for the BattleManager to interface with. The priority is functional UI hooks over visual polish."*

| AC | Status | Evidence |
|---|---|---|
| 1. Create a new Unity scene named `TheArena` | ✅ | `Assets/_AlgoMon/Scenes/TheArena.unity` |
| 2. Build the base Canvas layout including placeholder UI elements for Player/Enemy HP, CP, and basic combat status | ✅ | `BattleHudPreviewBuilder.cs` produces both combatant panels with Battery bar, 10-dot CP row, status text + 4 skill buttons + Recharge / Bag / Switch / Flee + Skill Details panel |
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
- **Six "special case" skills** (Ignite Loop, Short Circuit, Spore Script, Absolute Zero Crash, Safe Mode, Sleep Thread) listed in `BattleDesign.md §3` require custom BattleManager branches; they are part of #15 scope but may slip to a Sprint 2.5 hotfix if time-boxed.

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
- **HUD source-of-truth: prefab (migration complete).** Originally the HUD was generated at runtime by two coexisting builders (`ArenaSceneScaffoldBuilder.cs` and `BattleHudPreviewBuilder.cs`). The scaffold was deleted; `BattleHudPreviewBuilder.cs` survives only as a one-shot generator. The HUD has been migrated to `Assets/_AlgoMon/Prefabs/UI/Arena/BattleHud.prefab` (8 buttons, 31 text components, 94 transforms), and `Canvas_Arena` in `TheArena.unity` is now a prefab instance at scene root. All visual edits should happen in prefab edit mode — they persist across plays. `RebuildHud` now refuses to silently overwrite a prefab instance.
- **Stable scripting surface: `BattleHudController.cs`.** Lives on the HUD prefab root. Self-binds at runtime by walking the canvas hierarchy by name. BattleManager (#15) drives the HUD through its API (`SetCombatant / SetBattery / SetCP / SetStatus / SetSkillSlot / SetRound / SetBattleState / SetSkillDetail`) and events (`SkillSlotClicked`, `ActionClicked`). The Find-by-name binding will tolerate cosmetic prefab edits but breaks silently if a designer renames a node — node-name list documented in `BattleHudController.Bind()`.
- **HUD lifecycle plan (PvP / online ready):** Because future PvP / online battles need fresh HUD state per match, the long-term plan is **BattleManager owns the HUD prefab via `[SerializeField]` and instantiates it on battle start, destroys on battle end** (rather than relying on a scene-resident instance). The current scene-resident instance in `TheArena.unity` is convenient for #14 verification but will be replaced by Instantiate semantics in #15.
- **Known limitation:** `SetSkillSlot` only updates tags that already exist on a given button (the preview builder creates PWR / Counter tags conditionally). Adding new tags requires either editing the prefab or extending the controller.

### Documentation

- **`Docs/BattleDesign.md` is now the canonical reference** for BattleManager implementation. Section 3 (counter system) and Section 6 (status persistence on swap) are the two most likely places where #15/#16 implementation will need to verify against the doc rather than guess.

---

## Outcome

*(To be filled at sprint close — target May 18)*

---

## Carry-over

*(To be filled at sprint close. Likely candidates if #15–#17 partially slip:
TheGrid DAG generator → Sprint 3 regardless;
remaining 5 Subroutine triggers beyond `OnBattleStart` / `OnCounterWin` → Sprint 3;
6 special-case skill branches → Sprint 2.5 hotfix.)*

### Confirmed Sprint 3 cleanup items

- **Delete `BattleHudPreviewBuilder.cs`** once the prefab migration is verified working end-to-end in play mode (controller binds, click events fire, no missing children). The migration itself is already done — the prefab exists on disk and the scene instance is connected. The preview builder script is retained only as the "regenerate the prefab from scratch" escape hatch.
- **Delete the empty `ArenaHUDPreviewBuilder` GameObject** from `TheArena.unity`. After migration its only child (`Canvas_Arena`) was unparented to scene root, leaving the builder as an empty container holding only the `BattleHudPreviewBuilder` MonoBehaviour. If kept, its `[ExecuteAlways] OnEnable` will still fire `EnsurePreviewExtras` each scene load, which can quietly override the HUD prefab instance (it calls `EnsureVoltArrayPowerTag` on `SkillButton_1`). Either delete the GameObject or strip `[ExecuteAlways]` from the builder before #15.
- **#15 BattleManager wiring decision:** revisit whether to keep the scene-resident HUD instance or switch to `BattleManager.Instantiate(hudPrefab)` per match. The PvP / online roadmap pushes toward instantiation; #14's scene-resident instance is a verification convenience, not a final architectural choice.
