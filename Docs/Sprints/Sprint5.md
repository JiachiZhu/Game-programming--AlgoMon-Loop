# Sprint 5 - June 1 to June 12

## Goal

Deliver the **UI, feedback, and presentation polish pass** for AlgoMon. Sprint 4
made the vertical slice playable and structurally clearer; Sprint 5 should make
that slice feel like one coherent game. The main focus is visual consistency,
readable interfaces, satisfying audio feedback, and lightweight VFX that support
player decisions without hiding the gameplay.

When Sprint 5 closes, a player should be able to move from MainTerminal to
TheGrid to TheArena to RunResult and feel that every screen belongs to the same
cyber-pixel interface language. Buttons, panels, icons, text, sounds, battle
feedback, node feedback, and transitions should all feel intentional and
consistent.

---

## Sprint Context

Sprint 4 delivered the run difficulty framework, route readability, encounter
reward identities, party switching, hacker encounters, shop choices, and the
first pass of asset attribution. The current gap is not the core playable loop;
it is presentation quality. Some screens work mechanically but still feel like
separate pieces made at different times.

Sprint 5 ran from `June 1 - June 12`. The planned polish scope expanded because
the first Windows build exposed player-only asset loading and compile problems.
The final sprint work therefore covered both presentation polish and the build
readiness pass needed to make the vertical slice run outside the editor.

---

## Design Direction

AlgoMon's UI should feel like a compact cyber-pixel operating system:

- Pixel readable, not blurry.
- High-contrast text over quiet, controlled panels.
- Consistent button states across all screens.
- Clear visual hierarchy for primary action, secondary action, danger, reward,
  and status information.
- Small animation and sound responses when the player clicks, selects, wins,
  loses, switches, takes damage, earns rewards, or changes screens.
- Effects should support readability instead of covering important numbers,
  pieces, or battlefield sprites.

---

## Planned Issues

Sprint 5 issues live under the `Sprint5(6.1-6.12)` milestone. This is the final
local snapshot for the sprint.

| Rank | # | Title | Priority | Kanban |
|---:|---|---|---|---|
| 1 | #38 | [UI] Define a shared visual style guide for Sprint 5 screens | P0 | Done |
| 2 | #39 | [MainTerminal] Polish start flow, tier selector, and terminal feedback | P0 | Done |
| 3 | #40 | [Grid] Add consistent node feedback, route preview, and danger accents | P0 | Done |
| 4 | #41 | [Battle HUD] Unify action panels, status display, HP/CP bars, and switch UI | P0 | Done |
| 5 | #42 | [Animation] Produce entry animations for all 12 base/evolved AlgoMon forms | P1 | Done |
| 6 | #43 | [Audio] Add first-pass UI, grid, and battle sound effects | P1 | Done |
| 7 | #44 | [Flow] Polish scene transitions, restart, continue, victory, and defeat states | P1 | Done |
| 8 | #45 | [QA] Run readability, playability, and submission smoke tests | P0 final gate | Carried to Sprint 6 |
| 9 | #46 | [Gene Lab] Implement payload, fusion, and evolution loop | P0 | Done |
| 10 | #28 | [Polish] Add object pooling for floating feedback text | P2 | Closed as deferred |

### Kanban Notes

- Sprint 5 is functionally closed for feature polish. The playable route now
  covers MainTerminal, TheGrid, TheArena, RunResult, and the first Gene Lab loop.
- #45 moves into Sprint 6 as the final QA / submission evidence gate.
- #28 was closed as deferred because testing has not shown visible slowdown from
  the current floating feedback implementation, and the final sprint needs to
  stay focused on clarity, build safety, playtesting, and presentation evidence.
- The standalone build readiness work became part of the Sprint 5 final gate
  because the first player build exposed editor-only asset paths.

### Stretch Goals

Only pick these up if the core presentation polish pass is stable.

| # | Title | Status |
|---|---|---|
| Backlog | [Accessibility] Add optional colorblind/readability checks for node colors | Carry to Sprint 6 only if feedback needs it |
| #45 | [QA] Run readability, playability, and submission smoke tests | Sprint 6 carry-over |
| #28 | [Polish] Add object pooling for floating feedback text | Closed as future optimization |

---

## Issue Briefs

### #28 - [Polish] Add object pooling for floating feedback text

**Objective:** Replace repeated floating feedback text instantiation with a
small reusable object pool to reduce runtime allocation during battle
presentation.

**Acceptance Criteria**

- [ ] Add a simple pool for floating feedback text objects.
- [ ] Reuse pooled objects for damage, healing, CP gain, CP drain, status, and
  counter feedback.
- [ ] Return feedback objects to the pool after their animation completes.
- [ ] Preserve the current visual behavior of floating feedback.
- [ ] Ensure missing or exhausted pool entries fail gracefully by expanding the
  pool or using a safe fallback.
- [ ] Verify TheArena can show repeated feedback over several rounds without
  console errors.

**Scope Notes**

- Keep the pool focused on short-lived floating feedback text; broader VFX
  pooling can happen later.
- Do not redesign the battle feedback event system.

### #38 - [UI] Define a shared visual style guide for Sprint 5 screens

**Objective:** Create a small set of visual rules so every screen uses the same
UI language.

**Acceptance Criteria**

- [ ] Define the shared color roles for background, panel, accent, danger,
  reward, disabled state, and selected state.
- [ ] Define shared text sizes and font import settings for pixel readability.
- [ ] Define consistent button states: normal, hover/focus, selected, disabled,
  and pressed.
- [ ] Define panel spacing and border usage for terminal, grid, battle, shop,
  and result screens.
- [ ] Document the style rules in a short file that can guide future UI work.

### #39 - [MainTerminal] Polish start flow, tier selector, and terminal feedback

**Objective:** Make the first screen clearly communicate how to start, what
difficulty is selected, and what will happen next.

**Acceptance Criteria**

- [ ] The 1F-5F selector is visually aligned with the MainTerminal layout.
- [ ] The selected tier/depth state is obvious without needing extra text.
- [ ] Start, continue, and restart actions have consistent feedback.
- [ ] Hover/click states feel responsive.
- [ ] Any decorative animation does not cover important text or controls.

### #40 - [Grid] Add consistent node feedback, route preview, and danger accents

**Objective:** Make route planning easier and more visually consistent.

**Acceptance Criteria**

- [ ] Wild, hacker, elite, boss, and shop nodes have consistent icons or visual
  accents.
- [ ] Selected and reachable nodes are clearly different from locked nodes.
- [ ] Route preview text communicates expected risk and reward.
- [ ] Node labels remain sharp and readable at gameplay scale.
- [ ] Grid feedback does not overlap important node information.

### #41 - [Battle HUD] Unify action panels, status display, HP/CP bars, and switch UI

**Objective:** Make battle information easier to scan and make player choices
feel polished.

**Acceptance Criteria**

- [ ] Skill buttons, switch choices, recharge, and flee use consistent button
  styling.
- [ ] Active, disabled, and forced-switch states are visually clear.
- [ ] HP/Battery, CP, status effects, and element information are readable.
- [ ] Damage, healing, switching, and offline states have clear UI feedback.
- [ ] Important battle text is not hidden by announcer panels or VFX.

### #42 - [Animation] Produce entry animations for all 12 base/evolved AlgoMon forms

**Objective:** Turn the new battle `Entry` animation hook into complete content
coverage for all six AlgoMon species in both Base and Evolved forms.

**Acceptance Criteria**

- [ ] Confirm the `BattleAnimationProfile.entry` clip works for Sortex Base
  using `Assets/_AlgoMon/Sprites/SORTEX/Base/Entry`.
- [ ] Create or select entry animation frames for all 12 forms: Cachelon
  Base/Evolved, Heapion Base/Evolved, Nullbyte Base/Evolved, Overflux
  Base/Evolved, Recursix Base/Evolved, and Sortex Base/Evolved.
- [ ] Store each entry animation under
  `Assets/_AlgoMon/Sprites/{SPECIES}/{Form}/Entry` with
  `{Species}_{Form}_Entry_XX.png` naming.
- [ ] Add `entry` manifest timing for each form, including `startFrame` when
  an entry should begin on a later pose.
- [ ] Verify each form enters battle cleanly, then returns to idle without
  console errors.
- [ ] Keep the animation readable and short enough not to slow the battle loop.
- [ ] Update asset attribution if any new generated or external entry frames
  are added.

**Scope Notes**

- This issue is content coverage, not a rewrite of the battle animation system.
- Sortex Base and Evolved are the reference implementations: both entry
  sequences are packaged in their `Entry` folders and start from frame 6.

### #43 - [Audio] Add first-pass UI, grid, and battle sound effects

**Objective:** Add sound feedback for the main playable loop.

**Acceptance Criteria**

- [ ] Add click, hover/select, confirm, cancel, and error UI sounds.
- [ ] Add grid node select and enter-node sounds.
- [ ] Add battle attack, hit, switch, recharge, victory, and defeat sounds.
- [ ] Add shop purchase and reward sounds.
- [ ] Keep sound volume balanced and avoid repeated sounds becoming annoying.
- [ ] Record all external or generated audio sources in the asset attribution
  log.

### #44 - [Flow] Polish scene transitions, restart, continue, victory, and defeat states

**Objective:** Make moving between screens feel deliberate instead of abrupt.

**Acceptance Criteria**

- [ ] Add or improve transitions between MainTerminal, TheGrid, TheArena, and
  RunResult.
- [ ] Victory and defeat states have distinct audio/visual feedback.
- [ ] Restart and continue are clear and reliable.
- [ ] Loading or waiting states are communicated when needed.
- [ ] The player can always tell what screen they are on and what the next
  action is.

### #45 - [QA] Run readability, playability, and submission smoke tests

**Objective:** Confirm the polished vertical slice can be played and submitted.

**Acceptance Criteria**

- [ ] Test a full run from MainTerminal through RunResult.
- [ ] Test all depth options from 1F to 5F.
- [ ] Test wild, hacker, elite, boss, and shop nodes.
- [ ] Test forced switch after the active player AlgoMon goes offline.
- [ ] Test full-party defeat.
- [ ] Test victory rewards and compute spending.
- [ ] Check UI readability at the target screen resolution.
- [ ] Update asset attribution for any new UI, audio, or VFX assets.
- [ ] Capture final screenshots or video evidence if required for submission.

---

## Non-Goals

- Do not build the full Lab / gene merge system in this sprint.
- Do not redesign the core battle rules unless a bug blocks playability.
- Do not add a large set of new AlgoMon species.
- Do not replace working UI with a completely new theme; unify and polish the
  existing cyber-pixel direction.
- Do not add effects that make text, health, CP, or route choices harder to see.

---

## Definition of Done

Sprint 5 is done when the existing playable slice feels visually and
aurally coherent, the main screens share one style language, the player receives
clear feedback for important actions, and the project has a final smoke-test
record for submission.
