# 2026-06-13 - Sprint 6 packaged build regression checklist

- Build tested: fresh `StandaloneWindows64` from current `main`, built 2026-06-13 after the
  old-build bug fixes (catalogs rebuilt first). See `2026-06-13_old-build-bug-triage.md`.
- Focus: verify the packaged build after subroutine display and final UI changes, plus the
  four old-build bug fixes.
- Final manual packaged-player pass completed on 2026-06-13 after the Sprint 6
  subroutine UI changes. No submission-blocking issue was reported in this pass.

## What to test

Use the packaged player, not the Unity editor. The previous Sprint 5 build smoke
passed, but Sprint 6 changes touch battle UI and scene data, so this needs a
fresh pass before submission.

## Results

| Check | Result | Notes |
|---|---|---|
| `AlgoMon.exe` boots from the packaged folder | Pass | Boots windowed and fullscreen; MainTerminal renders. |
| MainTerminal loads with music, settings, and readable controls | Pass | Renders correctly; floor labels centred; terminal shell framed (no offset, zoom OFF // SCALE 100%). Settings panel shows a single "SETTINGS" title — verified in the packaged build. |
| Payload / Gene Lab inspection shows subroutine information | Pass | Verified during the final packaged-player pass. |
| Depth selection starts a run and opens TheGrid | Pass | In the packaged build: select a depth (e.g. 1F), then ENTER GRID → run begins and the route graph populates. (ENTER GRID without first selecting a depth leaves the grid "waiting for BeginRun" — pick a depth first.) |
| TheGrid route nodes remain readable and enterable | Pass | Verified in the packaged build: nodes render the editor's lucide icons — sword (combat/WILD), square-terminal (BREACH), crossed swords (ELITE), shopping-bag (SHOP), loop arrow (REBOOT), cpu (BOSS), chevron (START). |
| Battle opens from a route node without player-log errors | Pass | Verified during the final packaged-player pass. |
| Player and enemy combatant cards expose subroutine details | Pass | Verified during the final packaged-player pass. |
| A subroutine activation is visible in battle feedback/log text | Pass | Verified during the final packaged-player pass. |
| Skills, Recharge, Switch, and Flee controls still respond | Pass | Verified during the final packaged-player pass. |
| Battle animations, VFX, SFX, and BGM work in the player | Pass | Overflux Evolved attack SFX now lands with the blast (bang onset ~0.12 s); full battle A/V pass completed. |
| Victory or defeat reaches RunResult cleanly | Pass | Verified during the final packaged-player pass. |
| Final zip excludes `AlgoMon_BurstDebugInformation_DoNotShip` | Pass | Keep excluding this folder when sharing the Windows package. |

## Bugs found

These were the old-build findings; all four genuine ones are fixed on `main` (working tree).
See `2026-06-13_old-build-bug-triage.md` for full detail.

| Bug | Severity | Repro steps | Fix |
|---|---|---|---|
| Settings panel showed "SETTINGS" twice | Low | Main terminal → SETTINGS | Removed the in-panel duplicate title |
| Grid node icons wrong in standalone build | High | Build a player, enter TheGrid | Resolve grid icons through the runtime catalog |
| Overflux Evolved attack SFX late | Medium | Battle with Overflux Evolved | Re-sliced the clip's silent lead-in |
| Floor labels `2F`–`5F` off-centre | Low | Main terminal depth selector | Centred the floor-number labels |
| Terminal shell offset (zoom off) | — | (old build only) | Already fixed on main; no change needed |
| Skill loadout / squad popups closed on a click in empty space | Medium | Payload → open the skill loadout (or squad) popup → click any empty area | Removed the backdrop click-to-close; only the CLOSE button dismisses now (found during this round, not in the old build) |

## Follow-ups

- Packaged playthrough completed for Sprint 6 QA. Keep any new late issues as
  separate follow-ups instead of reopening the closed build regression card.
- After committing the fixes, swap the "working tree" refs in `FixedBugsLog.md` for the commit hashes.
- When zipping the final build, exclude `AlgoMon_BurstDebugInformation_DoNotShip`.
