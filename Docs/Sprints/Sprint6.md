# Sprint 6 - June 13 to June 18 final submission

## Goal

Sprint 6 is the final hardening sprint before the June 16 presentation and the
June 18 final report / game submission. The game already has a playable vertical
slice, so the goal is not to add another large system. The goal is to make the
game clearer to a first-time player, safer to demo from a packaged build, and
ready to explain with evidence in the final report.

The active priorities are:

- make subroutines visible enough that players understand passive effects;
- re-test the Windows build after the latest UI and battle changes;
- get feedback from someone who has not worked on the project and record what
  changed because of that feedback;
- prepare the June 16 presentation evidence separately from the June 18 report;
- close #45 as the final sprint submission-readiness QA gate after the packaged
  player pass.

## Sprint Context

Sprint 5 finished most of the presentation pass and produced the first Windows
build. That build passed boot and smoke checks, but Sprint 6 changes touch
combat UI, scene data, and player-facing explanation. Because of that, the
presentation and final submission build need a fresh packaged-player check
instead of relying only on editor play mode.

The external playtest is deliberately small. The tester does not need to judge
the code or the algorithms. They only need to show whether a first-time player
can understand the loop, choose actions, read feedback, and explain what they
think happened after a battle. The presentation evidence and final report are
tracked separately because the presentation is due on June 16, while the report
and final game submission are due on June 18.

## Kanban Snapshot

| Rank | Issue | Title | Labels | Status |
|---:|---|---|---|---|
| 1 | #54 | [Battle HUD] Surface subroutine information across inspection and battle feedback | `sprint:6`, `priority:P0`, `area:ui`, `area:battle` | Closed |
| 2 | #45 | [QA] Run readability, playability, and submission smoke tests | `sprint:6`, `priority:P0`, `area:qa` | Closed |
| 3 | #51 | [Build] Re-test packaged Windows build after Sprint 6 changes | `sprint:6`, `priority:P0`, `area:qa`, `area:flow` | Closed |
| 4 | #52 | [Playtest] Run first-time player feedback session | `sprint:6`, `priority:P0`, `area:qa`, `area:ui` | Next |
| 5 | #53 | [Presentation] Prepare June 16 demo evidence | `sprint:6`, `priority:P1`, `documentation`, `area:qa` | Ready |
| 6 | #55 | [Report] Finish final report and June 18 submission package | `sprint:6`, `priority:P0`, `documentation`, `area:qa` | Ready |

## Closed / Deferred

| Issue | Title | Decision |
|---|---|---|
| #28 | [Polish] Add object pooling for floating feedback text | Deferred outside Sprint 6. Keep as future optimization if heavier battle feedback causes visible slowdown. |

## Working Rules

- Keep Sprint 6 small. If a change does not improve understanding, build safety,
  presentation evidence, report evidence, or #45 final QA, it waits.
- Do not add a new combat rule unless a test shows that the existing rule is
  confusing or broken.
- Treat player confusion as data. Record what the tester misunderstood before
  deciding whether to fix UI, wording, pacing, or the report explanation.
- Do not mark the build card done until the packaged `.exe` has been tested,
  not just the Unity editor.

## Issue Briefs

### #54 - [Battle HUD] Surface subroutine information across inspection and battle feedback

**Objective:** Make each AlgoMon's passive subroutine visible before and during
battle, without adding a permanent wall of text to the arena.

**Acceptance Criteria**

- [ ] Payload / Gene Lab inspection shows the subroutine name, trigger, and a
  short description.
- [ ] TheArena lets the player inspect player and enemy subroutines from the
  combatant cards.
- [ ] When a subroutine activates, the battle log and presentation feedback make
  the activation clear.
- [ ] Subroutine wording is consistent between data, payload inspection, battle
  hover, and activation feedback.
- [ ] The added text does not cover HP, CP, skill buttons, or the center
  battlefield.
- [ ] One editor play session confirms the most visible trigger cases are
  readable when they fire.

**Current Notes**

- Implementation PR: #50.
- Closed after PR #50 merged. Final packaged-player verification remains tracked
  by #45 and #51.
- `SubroutineData.TriggerLabel` provides shared trigger wording.
- `BattleHudController.SetSubroutine` stores per-side hover cards for the arena.
- `BattleManager` pushes subroutine data into the HUD and logs activations.
- `BattlePresentationController` has a distinct subroutine feedback color.

### #45 - [QA] Run readability, playability, and submission smoke tests

**Objective:** Carry the Sprint 5 final QA card into Sprint 6 and use it as the
final QA and submission-readiness gate.

**Acceptance Criteria**

- [x] Test a full run from MainTerminal through RunResult.
- [x] Test all depth options from 1F to 5F.
- [x] Test wild, hacker, elite, boss, and shop nodes.
- [x] Test forced switch after the active player AlgoMon goes offline.
- [x] Test full-party defeat.
- [x] Test victory rewards and compute spending.
- [x] Check UI readability at the target screen resolution.
- [x] Re-check the newest subroutine display in battle and payload inspection.
- [x] Update the smoke checklist with what was tested and what still needs work.

**Current Notes**

- Closed after the Sprint 6 final QA pass on 2026-06-13.
- Evidence: `Docs/TestLogs/2026-06-12_smoke-checklist.md` and
  `Docs/TestLogs/2026-06-13_build-regression-checklist.md`.

### #51 - [Build] Re-test packaged Windows build after Sprint 6 changes

**Objective:** Confirm the final Windows package still works after the latest
subroutine display and UI scene changes.

**Acceptance Criteria**

- [x] Rebuild or refresh the Windows package after Sprint 6 changes are saved.
- [x] Remove the `AlgoMon_BurstDebugInformation_DoNotShip` folder before making
  the final share zip.
- [x] Boot `AlgoMon.exe` from the packaged folder.
- [x] Run MainTerminal -> TheGrid -> TheArena -> RunResult in the packaged
  player.
- [x] Check that battle animations, subroutine display, SFX/BGM, settings
  sliders, and scene transitions still work outside the editor.
- [x] Record the result in `Docs/TestLogs/2026-06-13_build-regression-checklist.md`.

**Current Notes**

- Closed after the final packaged-player regression pass on 2026-06-13.
- Evidence: `Docs/TestLogs/2026-06-13_build-regression-checklist.md`.

### #52 - [Playtest] Run first-time player feedback session

**Objective:** Get one useful playtest from a player who has not worked on the
project and is not expected to understand the code.

**Acceptance Criteria**

- [ ] Give the tester the packaged Windows build, not the Unity editor.
- [ ] Do not explain every rule before play. Give only the basic controls and
  observe where the UI succeeds or fails.
- [ ] Ask the tester what they think the goal is after the main menu.
- [ ] Ask what they think HP/Battery, CP, skills, route nodes, and subroutines
  mean after they encounter them.
- [ ] Record at least three observations and at least three direct player
  comments.
- [ ] Convert the feedback into a short action list: fixed now, explained in
  report, or left as future work.
- [ ] Record the session in
  `Docs/TestLogs/2026-06-13_external-playtest-plan.md`.

### #53 - [Presentation] Prepare June 16 demo evidence

**Objective:** Prepare clear evidence for the June 16 presentation without
turning the presentation task into the final report task.

**Acceptance Criteria**

- [ ] Capture a short video or screenshots showing MainTerminal, TheGrid,
  TheArena, RunResult, and the Gene Lab / payload view.
- [ ] Prepare the demo route for the presentation build.
- [ ] Note which features should be shown live and which can be shown with
  screenshots if the live route takes too long.
- [ ] Keep the presentation evidence separate from the final report and final
  build submission work.
- [ ] Store any notes or evidence links where they can be referenced again for
  the June 18 report.

### #55 - [Report] Finish final report and June 18 submission package

**Objective:** Finish the June 18 hand-in work separately from the June 16
presentation.

**Acceptance Criteria**

- [ ] Keep the report structure aligned with the module criteria and the final
  submitted build.
- [ ] Link or reference the Sprint 5 smoke checklist, Sprint 6 build check, and
  external playtest notes.
- [ ] Explain at least one design change caused by testing.
- [ ] Explain at least one technical problem from the packaged build process and
  how it was fixed.
- [ ] Run a final packaged-player check before submitting the game build.
- [ ] Prepare the final game package without the
  `AlgoMon_BurstDebugInformation_DoNotShip` folder.
- [ ] Keep asset credits and generated-asset disclosure aligned with the final
  submitted build.

## Definition of Done

Sprint 6 is done when the June 16 presentation build runs, the June 18 report
and final game package are ready to submit, the subroutine UI can be explained
without guessing, one external playtest has been recorded, and the final report
can point to concrete evidence rather than only describing features.
