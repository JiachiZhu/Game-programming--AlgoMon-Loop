# 2026-06-12 — Sprint 5 smoke checklist (issue #45)

- Build tested: editor (through merge commit 1d93883) and standalone Windows build (v0.5 candidate)
- Acceptance criteria mirrored from GitHub issue #45.
- Editor column reflects the cumulative Sprint 5 play sessions up to 2026-06-12: full runs played repeatedly, no open bugs found. Standalone column pending a gameplay pass on the packaged exe (boot smoke already clean, see build readiness audit).

## Results

| # | Check | Editor | Standalone | Notes |
|---|---|---|---|---|
| 1 | Full run from MainTerminal through RunResult | ☑ | ☐ | |
| 2 | All depth options 1F–5F selectable and scale correctly | ☑ | ☐ | |
| 3 | Wild, hacker, elite, boss, and shop nodes all enterable | ☑ | ☐ | |
| 4 | Forced switch after active AlgoMon goes offline | ☑ | ☐ | |
| 5 | Full-party defeat flows to the defeat result screen | ☑ | ☐ | |
| 6 | Victory rewards granted and compute spending works in shop | ☑ | ☐ | |
| 7 | UI readability at target resolution (1920×1080 fullscreen) | ☑ | ☐ | |
| 8 | Battle animations play (entry/attack/hit/faint, both forms) | ☑ | ☐ | build-fix regression check |
| 9 | Settings panel: music/SFX sliders styled and functional, track switch, zoom toggle | ☑ | ☐ | build-fix regression check |
| 10 | ENTER GRID and battle transitions show styled chrome + progress bar | ☑ | ☐ | build-fix regression check |
| 11 | Floating damage/status text uses the element bitmap fonts | ☑ | ☐ | build-fix regression check |
| 12 | Asset attribution up to date for new assets | ☑ | — | no new assets this pass |
| 13 | Screenshots/video captured for submission evidence | ☐ | ☐ | demo video planned before the presentation |

## Bugs found

| Bug | Severity | Repro steps | Fix (commit / issue) |
|---|---|---|---|
|  |  |  |  |

## Follow-ups

- Close issue #45 with a link to this log once both columns are green.
