# 2026-06-13 — Packaged-build bug triage (old build findings vs current main)

- Build tested: standalone Windows — the 2026-06-12 evening package (pre runtime-asset-catalog
  and pre-subroutine work) for the original report, then a fresh `StandaloneWindows64` build
  from current `main` for the retest.
- Goal: take the issues found while playtesting the old `.exe`, reproduce them on a fresh
  build, and separate genuine bugs from things already fixed on `main` after the old package
  was made — then fix the genuine ones.

## What was tested

Five issues recorded against the old packaged build:

1. Terminal shell is offset from the background art when terminal zoom is **off**.
2. The Settings panel shows the word "SETTINGS" twice.
3. TheGrid node icons in the packaged build do not match the editor.
4. Overflux (Evolved) attack SFX lands late — the explosion sound trails the VFX.
5. On the main terminal the floor labels (`2F`–`5F`) sit right of the button centre, and the
   `DEPTH` line above each button also looked shifted right.

## Results

| # | Issue | Verdict | How confirmed |
|---|---|---|---|
| 1 | Terminal shell offset (zoom off) | Already fixed on main | Non-zoom path restores the captured authored rect; fresh packaged build frames the shell correctly. No code change. |
| 2 | "SETTINGS" shown twice | Genuine bug — fixed | Section header `SectionTitle` and in-panel `SettingsPanelTitle` were both visible. Removed the in-panel duplicate. |
| 3 | Grid icons don't match build | Genuine bug — fixed | Build-only; the runtime asset catalog never covered grid icons. Fixed by baking them and resolving through the catalog. |
| 4 | Overflux Evolved SFX late | Genuine bug — fixed | Clip had a long lead-in; explosion transient was ~1.5 s behind the impact marker. Re-sliced the clip. |
| 5 | Floor labels shifted right | Genuine bug — fixed | Floor label rect sat ~14 px right of centre with `UpperLeft` alignment. Centred it. The `DEPTH` row already measured centred on main (old-build artifact). |

## Bugs found

| Bug | Severity | Repro steps | Root cause | Fix |
|---|---|---|---|---|
| Settings panel title duplicated | Low | Main terminal → SETTINGS; "SETTINGS" appears as both the section header and a panel title | `EnsureSettingsPanel` drew its own `SettingsPanelTitle` on top of the section header set by `EnterSectionView("SETTINGS")` | Removed the in-panel title; the section header is the single source of the screen name (`MainTerminalController.EnsureSettingsPanel`) |
| Grid node icons wrong in standalone build | High | Build a standalone player, enter TheGrid; nodes show cyberpunk HUD icons instead of the editor's lucide icons | `GridMapController.LoadGridVisualSprites` loaded the icons via `AssetDatabase` inside `#if UNITY_EDITOR` only, so builds fell back to the stale serialized HUD icons. The runtime asset catalog (added 2026-06-12) never included grid icons. | Added the six grid icon paths to `RuntimeAssetCatalogBuilder` and resolve them through `RuntimeUiAssetCatalog` in both editor and build (`ResolveGridSprite`). Rebuilt the catalog. |
| Overflux Evolved attack SFX late | Medium | Battle as/against Overflux Evolved; the explosion sound arrives well after the on-screen blast | `Atk_Overflux_Evolved.wav` (3.0 s) was near-silent for ~0.8 s then ramped slowly; the loud transient began ~1.23 s in (peak ~1.68 s). The attack SFX fires on the impact marker alongside the VFX, so the bang trailed by ~1.5 s. Other species' clips peak ~0.18–0.30 s in. | Re-sliced the clip: trimmed ~1.10 s of lead-in, added a 10 ms fade-in, kept the decay tail. Bang onset now ~0.12 s, peak ~0.58 s. |
| Floor labels `2F`–`5F` off-centre | Low | Main terminal depth selector; floor numbers sit right of the button centre | The authored floor-number rect sat ~14 px right of the button centre and used `UpperLeft` alignment | `NormalizeSourceLayoutDepthButtons` now spans the label full-width and uses `UpperCenter`; measured centre offset is 0 px. |

## Notes on the runtime asset catalog

The grid-icon bug shows the catalog's coverage is path-driven and must be kept in sync with any
controller that resolves sprites by `AssetDatabase` path. When adding a new editor-loaded sprite,
add its path to `RuntimeAssetCatalogBuilder` and resolve it through `RuntimeUiAssetCatalog` so the
standalone build matches the editor.

## Follow-ups

- After fixes are committed, the standalone player and final zip should pass the
  `2026-06-13_build-regression-checklist.md` once more.
- Overflux **Base** attack clip is fine (bang onset ~0.30 s); only the Evolved clip needed slicing.
- The original `Atk_Overflux_Evolved.wav` remains recoverable from git history if the slice needs
  retuning.
