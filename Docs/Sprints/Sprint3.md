# Sprint 3 — May 18 to TBD

## Goal
Deliver the **first playable roguelite loop**: a run starts from MainTerminal,
the player navigates a procedural DAG node network on TheGrid, fights one
battle per node in TheArena, captures defeated AlgoMon back into the Payload,
and reaches a Boss node that ends the run with a Victory or Defeat resolution.

When Sprint 3 closes the project goes from "battle prototype" to "actual game
loop you can sit down and play".

---

## Sprint Context

Sprint 2 left the battle layer feature-complete (turn loop, ASD counter, status
ticks, defense cooldown, scoped Subroutine triggers, generic presentation
template). The single biggest gap between the project today and a real
roguelite is that **battles do not chain into a run**. Sprint 3 fills that gap.

End date is provisional. Target window is 1 weeks (`May 18 – May 24`) but
TheGrid DAG generator is unknown-territory work, so the window will be
confirmed after issue-level scoping on Day 1.

---

## Planned Issues

> Issue numbers will be assigned in the GitHub tracker on Day 1 (May 18).
> Titles below are the intended scope; full acceptance criteria are written
> per-issue at that point.

| # | Title | Status |
|---|---|---|
| #? | [Grid] DAG generator — layered topology + reachability validation | ⬜ Planned |
| #? | [Grid] TheGrid scene + node selection UI (current position, valid next nodes, visited states) | ⬜ Planned |
| #? | [Flow] Scene transition wiring — MainTerminal ↔ TheGrid ↔ TheArena via existing `GameManager.GoTo` | ⬜ Planned |
| #? | [Menu] MainTerminal scene v1 — Start Run + party preview, minimal pass | ⬜ Planned |
| #? | [Battle] Capture mechanic v1 — defeated AlgoMon auto-extracted to Payload on KO (no probability roll, no item) | ⬜ Planned |
| #? | [Flow] Run end flow — Boss victory screen + party wipe defeat screen + return to MainTerminal | ⬜ Planned |
| #? | [Battle] Wire remaining 5 Subroutine triggers (OnTurnStart / OnCounterLose / OnDamageTaken / OnAllyFainted / OnLowBattery) | ⬜ Planned |

### Stretch Goals (only if main scope finishes ahead of target)

| # | Title | Status |
|---|---|---|
| #? | [Polish] `BattleAnimationProfile` ScriptableObject + first-pass Sortex / Cachelon profiles | ⬜ Stretch |
| #? | [Polish] `SkillVfxProfile` ScriptableObject + first-pass profile for one Attack / one Defense / one Status skill | ⬜ Stretch |
| #? | [Polish] Object pooling for floating feedback text | ⬜ Stretch |

Per `Docs/BattlePresentation.md`, the polish stretch goals plug into the
existing generic template (`BattleSpriteAnimator` / `BattlePresentationController`)
without any rewrite, so they can be picked up safely if time remains.

---

## Scope Notes — Explicit Non-Goals

- **TheLab (gene merge + Payload QuickSort UI)** — deliberately Sprint 4. It depends on the player having captured several AlgoMon across multiple runs, which only becomes interesting once the loop in this sprint actually runs.
- **Party switch + Bag items** — deferred to Sprint 4. Implementing these in Sprint 3 inflates scope and the current single-AlgoMon flow is enough to validate the roguelite loop end-to-end.
- **Dynamic HUD instantiation per battle** — kept scene-resident `Canvas_Arena` for Sprint 3, same as Sprint 2 closure note.
- **`BattlePresentationController` dynamic combatant routing** — bundled with Sprint 4 party-switch work, not done in isolation here.
- **Sound effects, camera shake, hit-stop** — explicit Non-Goals per `Docs/BattlePresentation.md`.
- **Save / load system, settings menu** — out of scope for Sprint 3; one-button "Start Run" only.
- **Shop nodes** — DAG generator should reserve a node-type slot but Shop logic is deferred.

---

## Issue Dependency Order

```
[Grid] DAG generator ──┐
                       ├─▶ [Grid] TheGrid scene + node selection UI ──┐
                       │                                              │
                       ▼                                              ▼
              [Flow] Scene transition wiring  ────▶  [Flow] Run end flow
                       ▲                                              ▲
                       │                                              │
[Menu] MainTerminal scene v1 ─────────────────────────────────────────┘
                                                                      ▲
                                                                      │
                                              [Battle] Capture mechanic v1

[Battle] 5 remaining Subroutine triggers  (independent — schedule anywhere)

(stretch) Animation / VFX profiles  (independent — schedule if time)
```

---

## Decisions & Notes

### DAG Generation (parameters to lock on Day 1)

Proposed defaults — all open to revision before the issue is written:

| Parameter | Proposed | Rationale |
|---|---|---|
| Layers (depth) | 6–8 | Long enough to feel like a run, short enough to playtest end-to-end in <10 minutes |
| Nodes per layer | 1–4 | Wider middle (3–4), narrow start/end (1–2) |
| Node-type weights | Combat 70% / Elite 15% / Rest 10% / Shop slot 5% (Shop logic deferred) / Boss 1 (fixed last) | Mirror genre conventions; reserve Shop slot now to avoid DAG regen later |
| Edge density | Each non-leaf node has 1–3 outgoing edges | Provides meaningful choice without spaghetti |
| Reachability rule | Every layer-0 entry point must reach the Boss; topological sort validates after generation; regenerate on failure | The README §1 algorithmic spec |

### Capture / Data Extraction v1

Sprint 3 uses KO-bound capture.

When an enemy AlgoMon is defeated, its data is automatically extracted and added
to the player's Payload. There is no capture button, no probability roll, and no
capture item in v1.

Design rationale:
- Fits the setting: defeated algorithmic creatures can have their data recovered.
- Keeps Sprint 3 focused on the first playable roguelite loop.
- Leaves probability-based capture, opt-out rules, and capture-related items for
  a later sprint if needed.

### MainTerminal Scope (v1)

Minimum viable:
- Title text
- "Start Run" button (creates fresh run, transitions to TheGrid)
- Party preview row (read-only view of current `GameManager.party` — placeholder Sortex for now)
- Stats footer (Payload size, runs completed) — optional

Out of v1: save/load slots, settings menu, AlgoMon detail view (those flow
naturally into Sprint 4 alongside TheLab).

### Scene Lifecycle Reuse

`GameManager.GoTo(GameScene)` and `EventBus.SceneTransitionEvent` already
exist from Sprint 1. Sprint 3 wiring just needs to publish the right enum
values at the right transitions; no new infrastructure required.

### Carry-Over Awareness

The remaining 5 Subroutine triggers (`OnTurnStart`, `OnCounterLose`,
`OnDamageTaken`, `OnAllyFainted`, `OnLowBattery`) are still pure-data on
`SubroutineData` assets. They piggyback on the existing `BattleEffectBundle`
path created in Sprint 2 issue #17, so each one is a small additive call site
in `BattleManager`. Estimated effort: half a day for all five.

### Documentation Targets

- `Docs/BattleDesign.md` § 8 — extend with concrete trigger timing for the 5 new Subroutine entry points
- New `Docs/GridDesign.md` — DAG schema, generation algorithm pseudocode, validation rules (write alongside the first DAG issue)
- `Docs/Sprints/Sprint2.md` Carry-over section — close out the "remaining 5 Subroutine triggers → Sprint 3" line once they land

---

## Outcome

*(To be filled at sprint close.)*

---

## Carry-over

*(To be filled at sprint close. Likely candidates if scope slips:
TheLab → Sprint 4 regardless;
animation / VFX stretch goals → Sprint 4 polish pass;
Shop node logic → Sprint 4 or later;
Party switch + Bag + dynamic HUD → Sprint 4.)*
