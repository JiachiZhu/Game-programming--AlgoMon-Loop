# 2026-06-12 — Build readiness audit

- Build tested: editor (branch `build/standalone-asset-loading`, commit c44a8d6)
- Goal: prepare the first standalone Windows build for the v0.5 release and find anything that only works inside the editor.

## What was tested

Audited every runtime script for `#if UNITY_EDITOR` / `AssetDatabase` usage, classified each call site by whether it has a build-safe primary path, then fixed the ones that did not. Verified the fix in the editor before packaging.

## Findings

| Area | Editor behaviour | Standalone behaviour before fix | Verdict |
|---|---|---|---|
| Battle animation profiles | Built live from sprite folders via AssetDatabase | No profile assets existed anywhere — all combat/menu/boss-preview animations would be lost | Broken, fixed |
| Settings sliders, zoom toggle, EXP bars, panel buttons, boss route panel | Loaded via AssetDatabase paths | Unstyled flat-colour rectangles | Broken, fixed |
| ENTER GRID transition visuals | Loaded via AssetDatabase paths (runtime-created overlay, no scene serialization) | Lost all chrome and the progress bar styling | Broken, fixed |
| Battle feedback bitmap fonts | Auto-loaded from font folders in editor; scene atlas/metrics refs were empty | Floating damage text lost its element fonts | Broken, fixed |
| Payload roster form stills | Loaded via AssetDatabase | Fell back to base portraits only | Degraded, fixed |
| Encounter species data | EncounterSpeciesCatalog in Resources (AssetDatabase only as editor fallback) | Works | Safe |
| TheGrid map sprites and node icons | Serialized into the scene by the editor preview tooling | Works | Safe |
| Battle VFX, music, SFX, TMP/legacy fonts, skill popup chrome | Resources or generated at runtime | Works | Safe |

## Fix

Added two Resources catalogs (`BattleAnimationProfileCatalog`, `RuntimeUiAssetCatalog`) plus an editor command **AlgoMon > Build > Rebuild Runtime Asset Catalogs** that bakes 12 species/form animation profile assets and 57 UI assets. Loaders now resolve editor-first in the editor (so the live sprite-folder workflow is unchanged) and catalog-first in builds. Also set PlayerSettings company/product name (was `DefaultCompany/My project`).

## Verification

| Check | Result | Notes |
|---|---|---|
| Script compilation | Pass | 0 errors after refactor |
| Catalog lookups at runtime API level | Pass | slider sprite, panel button texture, font metrics, Heapion Evolved profile (idle+attack frames), `Evolve` alias, payload still — all resolve |
| Editor play soak, MainTerminal boot | Pass | 12 s in play mode, 0 console errors |
| Player compile (StandaloneWindows64) | Fail, then pass | First attempt: 4 × CS0115 — `OnValidate` overrides in the three Cyber*Graphic components only compile in the editor assembly. Wrapped in `UNITY_EDITOR`; rebuild succeeded in 28.7 s with 0 errors |
| Standalone boot smoke | Pass | AlgoMon.exe ran windowed for 20 s, process stable at ~284 MB, Player.log contains no exceptions |

## Follow-ups

- Run the full issue #45 smoke checklist (see `2026-06-12_smoke-checklist.md`) in the editor and on the packaged exe.
- Verify in the standalone build specifically: battle animations, settings slider styling, ENTER GRID transition, floating damage text fonts.
