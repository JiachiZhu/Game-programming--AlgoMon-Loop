# Asset Attribution & Source Log

This file tracks third-party, generated, and externally assisted assets used by
AlgoMon. It is meant as the citation checklist for reports, presentations, and
final submission packaging.

Last reviewed: 2026-05-29

For the generated list of assets that are currently referenced, loaded, or
packaged by the project, see `Docs/UsedAssetInventory.md` and
`Docs/UsedAssetFiles.csv`.

## Summary

The project asset list is now split into confirmed generated art, confirmed
third-party packs, custom project-made UI pieces, and any repo files that are
present but not detected in the current playable flow. Treat rows marked as
needing confirmation as follow-up work before final submission.

## Confirmed / Traceable

| Asset or package | Project paths | Source / tool | License / usage note | Trace status |
|---|---|---|---|---|
| AlgoMon animation frames and species battle sprites | `AlgoMon/Assets/_AlgoMon/Sprites/{CACHELON,HEAPION,NULLBYTE,OVERFLUX,RECURSIX,SORTEX}/**` | [PixelLab](https://www.pixellab.ai/) generation, then project cleanup/integration | Project-specific generated output. Keep AI-generation disclosure in report. | Confirmed by project author on 2026-05-29; exact prompts/job IDs are not stored in repo. |
| Main menu typing character loop | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/CharacterTypingDisplayFrames_Fixed/*.png` | ChatGPT Image 2 | Six generated frames looped in the MainTerminal scene. Keep AI-generation disclosure in report. | Confirmed by project author on 2026-05-29. |
| Main menu cover and battle background | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/MainMenuCover16x9.png`, `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/BattleBackground_CyberArena.png` | ChatGPT Image 2 | Project-specific generated output. Keep AI-generation disclosure in report. | Confirmed by project author on 2026-05-29. |
| MainTerminal support masks | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/CharacterTypingBackdrop.png`, `CharacterTypingGhostShadow.png` | Custom project-generated support art | Created to cover/mask the generated menu character image in the MainTerminal scene. | Confirmed by project author on 2026-05-29. |
| Element icons | `AlgoMon/Assets/_AlgoMon/Resources/UI/Elements/Element_*.png` | Gemini 3.1 Pro / Nano Banana generation | Project-specific generated output. Keep AI-generation disclosure in report. | Confirmed by project author on 2026-05-29. |
| Custom battle/grid UI support pieces | `GroundDisc.png`, `CPSegmentFill.png`, `CPZapFill.png`, `TerminalNodeDisc.png`, `TerminalNodeRing.png`, `Sandclock/Sandclock_01.png` through `Sandclock_05.png` | Custom project-generated / Codex-assisted UI art | Project-owned UI pieces; no external image pack used directly. `CPSegmentFill.png`, `CPZapFill.png`, and the sandclock frames are serialized into `BattleHud.prefab`/`TheArena.unity`. | Confirmed by project author and serialized-reference audit on 2026-05-29. Sandclock frames were replaced with deterministic project-owned pixel art on 2026-05-29. |
| CP battery frame reference | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/CPBatteryFrame.png` | Custom project-generated / Codex-assisted UI art, visually referenced from [Complete UI Essential Pack by Crusenho](https://crusenho.itch.io/complete-ui-essential-pack) | Crusenho pack is CC BY 4.0. Credit Crusenho and link the license if describing the reference. No exact source-pack file was detected in the repo. | Confirmed by project author on 2026-05-29. |
| Nico Font Pack | `AlgoMon/Assets/_AlgoMon/Resources/Fonts/NicoBold-Regular.ttf`, `AlgoMon/Assets/_AlgoMon/Fonts/NicoBitmap/` | [Nico Font Pack by Emily Huo](https://emhuo.itch.io/nico-pixel-fonts-pack) | SIL Open Font License 1.1. Local README says free for commercial and non-commercial projects, no attribution necessary. | Confirmed from local `README.txt`, `OFL.txt`, and hash matches to local pack files. |
| Free SciFi Inventory UI pack | `AlgoMon/Assets/_AlgoMon/Resources/UI/SkillFrame/` | [Free Inventory Sci-Fi by ELV Games](https://elvgames.itch.io/free-sci-fi-inventory) | Page/local README permit personal/commercial use and modification, require credits to ELV Games, and forbid resale/claiming as own. The page also forbids AI training and crypto/NFT use. | Direct matches found for original pack files; `inventory_example_02_four_rows*` are project-derived variants from `Inventory_Example_02.png`. |
| Pixel UI Pack 3 health bars | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/PixelUIPack3_Bars.png`, `BatteryBar_Pack3BlueFill.png`, `BatteryBar_Pack3RedOrangeFill.png`, `BatteryBar_Pack3Track.png` | [Basic Pixel Health bar and Scroll bar by BDragon1727](https://bdragon1727.itch.io/basic-pixel-health-bar-and-scroll-bar) | Page permits free non-commercial use, asks commercial projects to contribute any value, allows modification, and forbids resale/redistribution of the asset. A creator comment asks paid-game users to introduce/credit BDragon1727. | `PixelUIPack3_Bars.png` is an exact copy of local `Pixel UI pack 3/06.png`; blue/red-orange fills are exact crops, while `BatteryBar_Pack3Track.png` is a project-made piece based on that sheet. |
| Battle announcer green panel | `AlgoMon/Assets/_AlgoMon/Resources/UI/BattleAnnouncer_GreenPanel.png` | [Pixel ui asset art by DuxDevGames](https://dux-dev-games.itch.io/pixel-ui-asset-art) | Page permits use but forbids selling the pixel art itself or claiming it as yours; copyright Dux Dev Games. | Exact crop at `x=92, y=23, w=33, h=16` from local `Pixel ui/pixil-frame-0 (7).png`. |
| Lucide SVG icons (`cpu`, `refresh-cw`, `shopping-bag`, `square-chevron-right`, `square-terminal`, `sword`, `swords`, `zap`) | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Grid/SVGSource/`, `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/SVGSource/` | [Lucide Icons](https://lucide.dev/) | [ISC License](https://lucide.dev/license); keep copyright/license notice when distributing copies. | Confirmed by SVG class names and official Lucide license. |
| Unity packages from registry | `AlgoMon/Packages/manifest.json`, `AlgoMon/Packages/packages-lock.json` | Unity Package Manager | Unity package terms/licenses apply. | Confirmed by package manifest/lock file. |
| Git package: UOS Launcher | `AlgoMon/Packages/manifest.json`, `AlgoMon/Assets/UOSLauncherEncrypt/` | `https://cnb.cool/unity/uos/UOSLauncher.git` | Check upstream package license before redistribution. | Source URL recorded in manifest. |
| Git package: Unity MCP plugin | `AlgoMon/Packages/manifest.json` | `https://github.com/AnkleBreaker-Studio/unity-mcp-plugin.git` | Check upstream package license before redistribution. | Source URL recorded in manifest. |

## Needs Source Confirmation

| Asset group | Project paths | Current clue | What to add |
|---|---|---|---|
| Present but not currently detected as used | `AlgoMon/Assets/_AlgoMon/Sprites/Effects/FreePixelEffectsPack/19_freezing_spritesheet.png` | Local `Free Pixel Effects Pack/README.txt` says public domain, no credit required. | Keep only if needed, or remove from repo if unused; if kept, add source URL. |

## Recommended Citation Text

Use this as a starting point in a report, then add the confirmed missing
sources above:

> AlgoMon uses Unity 2022.3 LTS and Unity Package Manager dependencies listed in
> `Packages/manifest.json`. Several UI icons are from Lucide Icons under the ISC
> License. The Nico Font Pack by Emily Huo is used under the SIL Open Font
> License 1.1. Free Inventory Sci-Fi UI assets by ELV Games are used with
> credit to ELV Games. Pixel UI Pack 3 health-bar pieces by BDragon1727 are
> used in the battle HUD. A battle announcer panel crop uses Pixel ui asset art
> by DuxDevGames. The CP battery frame was custom-made with visual reference to
> Crusenho's Complete UI Essential Pack under CC BY 4.0. AlgoMon battle sprite
> animation frames were generated for this project with PixelLab and integrated
> into Unity by the project author. Element icons were generated with Gemini
> 3.1 Pro / Nano Banana. The MainTerminal typing-character loop, main menu
> cover, and battle background were generated with ChatGPT Image 2.

## Before Final Submission

- Add a source URL for the currently unused Free Pixel Effects Pack file if it
  remains in the repo.
- Keep copies of license text or screenshots/receipts for any downloaded asset
  packs outside the repo if the license does not allow committing them.
- If a source cannot be verified, replace the asset with original/generated
  project-owned art or remove it from the build.
