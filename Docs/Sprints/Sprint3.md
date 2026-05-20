# Sprint 3 - May 18 to May 24

## Goal

Deliver the **first playable roguelite loop**: a run starts from MainTerminal,
the player navigates a procedural DAG node network on TheGrid, fights one
battle per node in TheArena, captures defeated AlgoMon back into the Payload,
and reaches a Boss node that ends the run with a Victory or Defeat resolution.

When Sprint 3 closes, the project should move from "battle prototype" to
"actual game loop you can sit down and play".

---

## Sprint Context

Sprint 2 left the battle layer feature-complete: turn loop, ASD counter, status
ticks, defense cooldown, scoped Subroutine triggers, and the generic battle
presentation template are all in place. The single biggest gap between the
project today and a real roguelite is that **battles do not chain into a run**.
Sprint 3 fills that gap.

Sprint 3 runs for one week (`May 18 - May 24`). TheGrid DAG generator is the
biggest unknown. If it overruns, scope cuts should remove stretch goals first,
then defer non-critical battle enhancement such as the remaining Subroutine
triggers, instead of extending the sprint window.

---

## Planned Issues

| # | Title | Status |
|---|---|---|
| #19 | [Grid] Implement DAG generator with layered topology and reachability validation | Done |
| #20 | [Grid] Create TheGrid scene and node selection UI | Done |
| #21 | [Flow] Wire scene transitions from MainTerminal to TheGrid to TheArena | Planned |
| #22 | [Menu] Create MainTerminal scene v1 with Start Run and party preview | Planned |
| #23 | [Battle] Add capture mechanic v1 with auto-extraction to Payload | Planned |
| #24 | [Flow] Add run end flow for Boss victory and party defeat | Planned |
| #25 | [Battle] Wire remaining Subroutine triggers | Planned |

### Stretch Goals

Only pick these up if the main Sprint 3 loop is already playable.

| # | Title | Status |
|---|---|---|
| #26 | [Polish] Add BattleAnimationProfile and first-pass Sortex / Cachelon profiles | Stretch |
| #27 | [Polish] Add SkillVfxProfile and first-pass Attack / Defense / Status profiles | Stretch |
| #28 | [Polish] Add object pooling for floating feedback text | Stretch |

Per `Docs/BattlePresentation.md`, the polish stretch goals plug into the
existing generic template (`BattleSpriteAnimator` / `BattlePresentationController`)
without a rewrite, so they can be picked up safely if time remains.

---

## Issue Briefs

### #19 - [Grid] Implement DAG generator with layered topology and reachability validation

**Objective:** Build the data-only procedural node network generator for
TheGrid. It should create a forward-only DAG route map for each run and
guarantee that the Boss node is reachable.

**Acceptance Criteria**

- [x] Generate a layered node graph with one Start node, several intermediate route layers, and one Boss node.
- [x] Assign node types using Sprint 3 defaults: Combat, Elite, Shop slot, Reboot, and Boss.
- [x] Create directed edges only from earlier layers to later layers; no backward edges or cycles.
- [x] Ensure every reachable non-final node has at least one outgoing edge.
- [x] Validate Boss reachability after generation.
- [x] Regenerate or repair the graph if validation fails.
- [x] Add a short `Docs/GridDesign.md` note covering graph schema, parameters, and validation rules.

**Scope Notes**

- This issue is data / algorithm only; no Unity scene UI is required here.
- Shop nodes only need to exist as a reserved node type. Shop behavior is out of scope.
- The generated graph should be deterministic when given the same seed if practical.

### #20 - [Grid] Create TheGrid scene and node selection UI

**Objective:** Create the playable TheGrid scene that visualizes the generated
DAG and lets the player choose valid next nodes during a run.

**Acceptance Criteria**

- [x] Create or complete the `TheGrid` scene.
- [x] Render generated DAG nodes as clickable UI elements.
- [x] Display distinct visual states for current, available, visited, locked, and Boss nodes.
- [x] Only allow selecting nodes connected from the current node.
- [x] Publish or handle `NodeSelectedEvent` when a valid node is selected.
- [x] Store the selected node as the current run node in `GameManager`.
- [x] Show basic node type labels or icons for Combat, Elite, Shop, Reboot, and Boss.

**Scope Notes**

- This issue depends on #19.
- Shop nodes may use placeholder behavior for Sprint 3.
- Rest nodes are intentionally removed from active generation; battles start at full per-encounter state.
- Visual polish is secondary; the priority is a clear playable route-selection flow.

### #21 - [Flow] Wire scene transitions from MainTerminal to TheGrid to TheArena

**Objective:** Connect the Sprint 3 scenes into the first playable run flow
using the existing `GameManager.GoTo(GameScene)` transition path.

**Acceptance Criteria**

- [ ] Starting a run from MainTerminal transitions to `TheGrid`.
- [ ] Selecting a Combat, Elite, or Boss node transitions to `TheArena`.
- [ ] Battle victory returns the player to `TheGrid` unless the defeated node is the Boss.
- [ ] Battle defeat routes to the run defeat flow.
- [ ] Current node and current opponent state are stored through `GameManager`.
- [ ] Existing `SceneTransitionEvent` / `GameManager.GoTo` infrastructure is reused.

**Scope Notes**

- Do not build a new scene loading framework.
- Transition animations are out of scope.
- This issue is the glue between Menu, Grid, Battle, and Run End work.

### #22 - [Menu] Create MainTerminal scene v1 with Start Run and party preview

**Objective:** Create the first functional MainTerminal scene so the player has
a clear entry point into a Sprint 3 run.

**Acceptance Criteria**

- [ ] Create or complete the `MainTerminal` scene.
- [ ] Add a clear Start Run button.
- [ ] Start Run initializes fresh run state and transitions to `TheGrid`.
- [ ] Show a read-only party preview row using `GameManager.party`.
- [ ] Provide a placeholder starter party if no party exists yet.
- [ ] Optionally show simple stats such as Payload size or runs completed.

**Scope Notes**

- Save / load, settings, AlgoMon detail view, and TheLab are out of scope.
- The scene only needs to support starting a run for Sprint 3.
- Keep the UI consistent with the cyber terminal style already defined in the project.

### #23 - [Battle] Add capture mechanic v1 with auto-extraction to Payload

**Objective:** Add the first capture / extraction mechanic: when the player
defeats an enemy AlgoMon, its data is automatically added to the player's
Payload.

**Acceptance Criteria**

- [ ] Detect player victory when the enemy AlgoMon reaches 0 Battery.
- [ ] Create a persistent `AlgoMonInstance` copy for the defeated enemy.
- [ ] Add the captured AlgoMon to `GameManager.payload`.
- [ ] Avoid storing transient battle-only ScriptableObject instances in Payload.
- [ ] Show a simple battle log or UI message confirming extraction.
- [ ] Ensure capture happens once per defeated enemy, not multiple times from repeated battle-end events.

**Scope Notes**

- No capture probability, capture item, or capture button in v1.
- Boss capture rules can be simple for Sprint 3, but should be documented in code or notes.
- This issue should not change the core damage / status battle rules.

### #24 - [Flow] Add run end flow for Boss victory and party defeat

**Objective:** Complete the Sprint 3 run loop by adding simple Victory and
Defeat outcomes, then returning the player to MainTerminal.

**Acceptance Criteria**

- [ ] Detect when the player wins a Boss battle.
- [ ] Show a simple Victory result screen or result panel.
- [ ] Detect player defeat and show a simple Defeat result screen or result panel.
- [ ] Clear or reset active run state after the result is confirmed.
- [ ] Return to `MainTerminal` from the result state.
- [ ] Keep captured Payload entries from completed battles.

**Scope Notes**

- Detailed rewards, score breakdowns, save data, and run history are out of scope.
- The result screen can be minimal; correctness of flow is the priority.
- This issue depends on scene transition wiring and Boss node handling.

### #25 - [Battle] Wire remaining Subroutine triggers

**Objective:** Finish the remaining Subroutine trigger call sites in
BattleManager so species passives can activate beyond `OnBattleStart` and
`OnCounterWin`.

**Acceptance Criteria**

- [ ] Trigger `OnTurnStart` at the start of the relevant unit's turn.
- [ ] Trigger `OnCounterLose` after a unit loses an ASD counter.
- [ ] Trigger `OnDamageTaken` after a unit takes direct damage.
- [ ] Trigger `OnAllyFainted` when a party ally is shut down.
- [ ] Trigger `OnLowBattery` when a unit drops below 25% Battery.
- [ ] Reuse the existing `BattleEffectBundle` path for Subroutine effects.
- [ ] Update `Docs/BattleDesign.md` with concrete timing notes for these triggers.

**Scope Notes**

- This issue is independent of TheGrid and can be scheduled flexibly.
- Party-wide behavior may be minimal until Sprint 4 party switching exists.
- If Sprint 3 scope slips, this is the safest main issue to defer.

---

## Issue Dependency Order

```text
#19 DAG generator
  -> #20 TheGrid scene + node selection UI
      -> #21 Scene transition wiring
          -> #24 Run end flow

#22 MainTerminal scene v1
  -> #21 Scene transition wiring

#23 Capture mechanic v1
  -> #24 Run end flow

#25 Remaining Subroutine triggers
  Independent; schedule anywhere, defer first if Grid work overruns.

Stretch:
#26 BattleAnimationProfile
  -> #27 SkillVfxProfile
  -> #28 Floating feedback object pooling
```

Recommended priority path:

```text
#19 -> #20 -> #21 -> #23 -> #24
```

#22 can be built in parallel with #19 / #20 because it is mostly independent.
#25 is a battle-depth issue and should not block the first playable loop.

---

## Scope Notes - Explicit Non-Goals

- **TheLab (gene merge + Payload QuickSort UI)** - deliberately Sprint 4. It depends on the player having captured several AlgoMon across multiple runs, which only becomes interesting once the loop in this sprint actually runs.
- **Party switch + Bag items** - deferred to Sprint 4. The current single-AlgoMon flow is enough to validate the roguelite loop end-to-end.
- **Dynamic HUD instantiation per battle** - keep the scene-resident `Canvas_Arena` for Sprint 3, same as the Sprint 2 closure note.
- **`BattlePresentationController` dynamic combatant routing** - bundled with Sprint 4 party-switch work, not done in isolation here.
- **Sound effects, camera shake, hit-stop** - explicit non-goals per `Docs/BattlePresentation.md`.
- **Save / load system, settings menu** - out of scope for Sprint 3; one-button Start Run only.
- **Shop nodes** - DAG generator should reserve a node-type slot, but Shop logic is deferred.

---

## Decisions & Notes

### DAG Generation Defaults

| Parameter | Proposed | Rationale |
|---|---|---|
| Layers (depth) | 6-7 | Long enough to feel like a run, short enough to playtest end-to-end in under 10 minutes. |
| Nodes per layer | 1-4 | Wider middle layers, narrow start/end. |
| Node-type weights | Combat 70% / Elite 15% / Shop slot 10% / Reboot 5% / Boss fixed last | Keeps the run focused on encounter choices instead of attrition/rest pacing, while reserving Shop now for later logic and giving players a rare optional route reset. |
| Edge density | 1-3 outgoing edges per non-leaf node | Provides meaningful choice without visual spaghetti. |
| Reachability rule | Boss must be reachable from Start | Validate after generation; regenerate or repair if invalid. |

### Capture / Data Extraction v1

Sprint 3 uses KO-bound capture. When an enemy AlgoMon is defeated, its data is
automatically extracted and added to the player's Payload. There is no capture
button, no probability roll, and no capture item in v1.

Design rationale:

- Fits the setting: defeated algorithmic creatures can have their data recovered.
- Keeps Sprint 3 focused on the first playable roguelite loop.
- Leaves probability-based capture, opt-out rules, and capture-related items for a later sprint if needed.

### MainTerminal Scope v1

Minimum viable:

- Title text.
- Start Run button that creates fresh run state and transitions to TheGrid.
- Party preview row, read-only from `GameManager.party`.
- Placeholder starter party if no party exists yet.
- Optional stats footer for Payload size or runs completed.

Out of v1: save/load slots, settings menu, AlgoMon detail view. Those flow
naturally into Sprint 4 alongside TheLab.

### Scene Lifecycle Reuse

`GameManager.GoTo(GameScene)` and `EventBus.SceneTransitionEvent` already exist
from Sprint 1. Sprint 3 wiring should publish the right enum values at the
right transitions; no new infrastructure is required.

### Carry-Over Awareness

The remaining 5 Subroutine triggers (`OnTurnStart`, `OnCounterLose`,
`OnDamageTaken`, `OnAllyFainted`, `OnLowBattery`) are still pure data on
`SubroutineData` assets. They piggyback on the existing `BattleEffectBundle`
path created in Sprint 2 issue #17, so each one should be a small additive call
site in `BattleManager`.

### Documentation Targets

- `Docs/BattleDesign.md` section 8 - extend with concrete trigger timing for the 5 new Subroutine entry points.
- New `Docs/GridDesign.md` - DAG schema, generation algorithm pseudocode, validation rules. Write alongside #19.
- `Docs/Sprints/Sprint2.md` Carry-over section - close out the "remaining 5 Subroutine triggers -> Sprint 3" line once they land.

---

## Outcome

*(To be filled at sprint close.)*

---

## Carry-over

*(To be filled at sprint close. Likely candidates if scope slips: TheLab to
Sprint 4 regardless; animation / VFX stretch goals to Sprint 4 polish pass;
Shop node logic to Sprint 4 or later; Party switch + Bag + dynamic HUD to
Sprint 4.)*
