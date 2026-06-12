# Asset Attribution & Source Log

This file tracks third-party, generated, and externally assisted assets used by
AlgoMon. It is meant as the citation checklist for reports, presentations, and
final submission packaging.

Last reviewed: 2026-06-09

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
| Custom battle/grid UI support pieces | `GroundDisc.png`, `CPSegmentFill.png`, `CPZapFill.png`, `TerminalNodeDisc.png`, `TerminalNodeRing.png`, `Sandclock/Sandclock_01.png` through `Sandclock_05.png` | Custom project-generated UI art | Project-owned UI pieces; no external image pack used directly. `CPSegmentFill.png`, `CPZapFill.png`, and the sandclock frames are serialized into `BattleHud.prefab`/`TheArena.unity`. | Confirmed by project author and serialized-reference audit on 2026-05-29. Sandclock frames were replaced with deterministic project-owned pixel art on 2026-05-29. |
| CP battery frame reference | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/CPBatteryFrame.png` | Custom project-generated UI art, visually referenced from [Complete UI Essential Pack by Crusenho](https://crusenho.itch.io/complete-ui-essential-pack) | Crusenho pack is CC BY 4.0. Credit Crusenho and link the license if describing the reference. No exact source-pack file was detected in the repo. | Confirmed by project author on 2026-05-29. |
| Nico Font Pack | `AlgoMon/Assets/_AlgoMon/Resources/Fonts/NicoBold-Regular.ttf`, `AlgoMon/Assets/_AlgoMon/Fonts/NicoBitmap/` | [Nico Font Pack by Emily Huo](https://emhuo.itch.io/nico-pixel-fonts-pack) | SIL Open Font License 1.1. Local README says free for commercial and non-commercial projects, no attribution necessary. | Confirmed from local `README.txt`, `OFL.txt`, and hash matches to local pack files. |
| Free SciFi Inventory UI pack | `AlgoMon/Assets/_AlgoMon/Resources/UI/SkillFrame/` | [Free Inventory Sci-Fi by ELV Games](https://elvgames.itch.io/free-sci-fi-inventory) | Page/local README permit personal/commercial use and modification, require credits to ELV Games, and forbid resale/claiming as own. The page also forbids AI training and crypto/NFT use. | Direct matches found for original pack files; `inventory_example_02_four_rows*` are project-derived variants from `Inventory_Example_02.png`. |
| Pixel UI Pack 3 health bars | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/PixelUIPack3_Bars.png`, `BatteryBar_Pack3BlueFill.png`, `BatteryBar_Pack3RedOrangeFill.png`, `BatteryBar_Pack3Track.png` | [Basic Pixel Health bar and Scroll bar by BDragon1727](https://bdragon1727.itch.io/basic-pixel-health-bar-and-scroll-bar) | Page permits free non-commercial use, asks commercial projects to contribute any value, allows modification, and forbids resale/redistribution of the asset. A creator comment asks paid-game users to introduce/credit BDragon1727. | `PixelUIPack3_Bars.png` is an exact copy of local `Pixel UI pack 3/06.png`; blue/red-orange fills are exact crops, while `BatteryBar_Pack3Track.png` is a project-made piece based on that sheet. |
| Battle announcer green panel | `AlgoMon/Assets/_AlgoMon/Resources/UI/BattleAnnouncer_GreenPanel.png` | [Pixel ui asset art by DuxDevGames](https://dux-dev-games.itch.io/pixel-ui-asset-art) | Page permits use but forbids selling the pixel art itself or claiming it as yours; copyright Dux Dev Games. | Exact crop at `x=92, y=23, w=33, h=16` from local `Pixel ui/pixil-frame-0 (7).png`. |
| Lucide SVG icons (`cpu`, `refresh-cw`, `shopping-bag`, `square-chevron-right`, `square-terminal`, `sword`, `swords`, `zap`) | `AlgoMon/Assets/_AlgoMon/Sprites/UI/Grid/SVGSource/`, `AlgoMon/Assets/_AlgoMon/Sprites/UI/Arena/SVGSource/`, plus the battle-banner copy `AlgoMon/Assets/_AlgoMon/Resources/UI/Icons/zap.png` (lightning glyph) | [Lucide Icons](https://lucide.dev/) | [ISC License](https://lucide.dev/license); keep copyright/license notice when distributing copies. `square-chevron-right` (`AlgoMon/Assets/_AlgoMon/Sprites/UI/Grid/Icons/square-chevron-right.png`) is additionally reused horizontally-flipped as the MainTerminal main-menu section back/return arrow, wired through `MainTerminalController.backArrowSprite` (added 2026-06-06). | Confirmed by SVG class names and official Lucide license. |
| Pixel UI & HUD MainTerminal UI sprites | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD/**`, including `Buttons/Blue/ButtonE_Unpressed.png`, `ButtonStone_Highlighted.png`, and `ButtonF_Pressed.png`; battle announcement banner `AlgoMon/Assets/_AlgoMon/Resources/UI/Banners/TitleBanner.png` (from `Sprites/Banners/Black/TitleBanner.png`) | [Pixel UI & HUD Pack](https://deadrevolver.itch.io/pixel-ui-hud-pack) by Dead Revolver, obtained from an itch.io purchase for this project; local source `C:\Users\rog\OneDrive\桌面\游戏编程\Pixel UI & HUD` | Imported UI sprites used for MainTerminal panel frames, selectors, grid/select states, skill-tree pieces, value bars, tooltips, the Payload/Squad command-button states, and the in-battle skill/counter announcement banner. Keep Dead Revolver credit, purchase/license evidence, and purchaser permission evidence if bought via another account. | Source confirmed by project author on 2026-06-06; local readme names Dead Revolver. Battle banner added 2026-06-09. |
| PRO Cyberpunk HUD System PNG assets | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD/**`, `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/InventorySlots/cyber_slot_*.png` (generator: `Docs/tools/pro_cyberpunk_slots_export.py`) | "PRO Cyberpunk HUD System - Godot 4 Animated UI" by DJY66 / GameSupply; local source `Cyberpunk_HUD_PNG_Assets_Only` | Local license says assets may be used and modified in personal/commercial projects, but may not be resold, redistributed, repackaged, or uploaded as a competing asset pack. Used for MainTerminal cyber HUD panels, frames, icons, deco, progress pieces, the Payload talent-bar fill, and PRO-derived Payload storage-grid slot states. | Confirmed from local `CyberpunkHUD_License.txt` and `CyberpunkHUD_README_PNG_ONLY.txt`; keep purchase/download evidence. Payload slot states were regenerated from `CyberpunkHUD/slot_item_bg.png` on 2026-06-06 to avoid relying on unclear free-pack commercial terms. |
| Monster base oval pedestal | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/Inspector/UI_MonsterBase_Oval.png` | PixelLab generation by the project author | Project-specific generated output. Used as the Payload inspector pedestal beneath the selected AlgoMon idle sprite. Keep AI-generation disclosure in report. | Confirmed by project author on 2026-06-06. |
| Super Pixel Effects Pack 2 battle effects | `AlgoMon/Assets/_AlgoMon/Resources/Effects/{SortexBaseClawLargeBlue,SortexBaseElectricBurstLargeBlue,OverfluxBaseFireBurstSmallOrange,OverfluxEvolvedExplosionLargeRed,OverfluxSplatterLargeRed,NullbyteBaseSplatterLargeBlue,RecursixBaseFireBurstLargeGreen,RecursixEvolvedMagicSwirlLargeGreen}/**` | [Super Pixel Effects Pack 2 by unTied Games](https://untiedgames.itch.io/super-pixel-effects-pack-2); local source `C:\Users\rog\OneDrive\桌面\游戏编程\Super Pixel Effects Pack 2` | Animated pixel effects used as AlgoMon attack/guard action effects. License: attribution required, no reselling the asset itself, commercial and non-commercial use OK; pack page states no generative AI was used. Frames imported as individual sprite sequences; the `RecursixEvolvedMagicSwirlLargeGreen` swirl is subsampled to every other source frame (26 of 52). | Confirmed from local `readme.txt` and the itch.io page on 2026-06-09. Keep purchase/download evidence. |
| Unity packages from registry | `AlgoMon/Packages/manifest.json`, `AlgoMon/Packages/packages-lock.json` | Unity Package Manager | Unity package terms/licenses apply. | Confirmed by package manifest/lock file. |
| Git package: UOS Launcher | `AlgoMon/Packages/manifest.json`, `AlgoMon/Assets/UOSLauncherEncrypt/` | `https://cnb.cool/unity/uos/UOSLauncher.git` | Check upstream package license before redistribution. | Source URL recorded in manifest. |
| Git package: Unity MCP plugin | `AlgoMon/Packages/manifest.json` | `https://github.com/AnkleBreaker-Studio/unity-mcp-plugin.git` | Check upstream package license before redistribution. | Source URL recorded in manifest. |

## Needs Source Confirmation

| Asset or package | Project paths | Current source note | Required confirmation |
|---|---|---|---|
| Payload panel frame exports | `AlgoMon/Assets/_AlgoMon/Sprites/UI/MainTerminal/Inspector/PanelFrame01.png`, `PanelFrame03.png` | Exported from frame 1 and frame 3 of local `C:\Users\rog\OneDrive\桌面\游戏编程\panel.aseprite`, then cropped to its visible panel bounds for Unity UI stretching. | Confirm whether `panel.aseprite` is project-created, AI-generated, or derived from a licensed third-party pack. Once confirmed, move this row into the confirmed table with the correct license/source note. |
| Numbered battle effects | `AlgoMon/Assets/_AlgoMon/Resources/Effects/{SortexEvolvedEffect31,NullbyteBaseEffect26,CachelonBaseEffect29,SortexGuardStatusEffect16}/**` | Folder naming (`EffectNN`) does not match Super Pixel Effects Pack 2's descriptive scheme; likely sourced from unTied Games' earlier "Super Pixel Effects" pack, but not yet confirmed. | Confirm the exact source pack and license for these numbered effects before final submission, then move into the confirmed table. |
| Pozac Combat Effects 6 battle effects | `AlgoMon/Assets/_AlgoMon/Resources/Effects/{HeapionBaseCombatEffect1,OverfluxBaseCombatEffect3,NullbyteEvolvedCombatEffect8,OverfluxDefenseEffect13,NullbyteDefenseEffect19,CachelonGuardStatusEffect30}/**` | [Combat Effects 6 - 2D Pixel Art VFX Pack by Pozac](https://pozac.itch.io/combat-effects-6-2d-pixel-art-vfx-pack); local source `C:\Users\rog\OneDrive\桌面\游戏编程\Beat 'em Up Combat Effects - 2D Pixel Art VFX Pack 6`. Effect 1 (Heapion base attack), Effect 3 (Overflux base attack), Effect 8 (Nullbyte evolved attack), Effect 13 (Overflux defense), Effect 19 (Nullbyte defense), and Effect 30 (Cachelon defense/status) imported as sprite sequences. | Paid itch.io pack with no explicit license file shipped in the download. Confirm the exact usage license with Pozac (page/EULA) and keep purchase evidence before final submission. |

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
> cover, and battle background were generated with ChatGPT Image 2. MainTerminal
> UI sprites also use PRO Cyberpunk HUD System PNG assets by DJY66/GameSupply
> and Pixel UI & HUD by Dead Revolver. Battle action effect animations use
> Super Pixel Effects Pack 2 by unTied Games (attribution required, no reselling
> the asset itself).

## Before Final Submission

- Keep itch purchase/download evidence for Pixel UI & HUD and PRO Cyberpunk HUD
  System. The PRO Cyberpunk HUD System local license is already copied into the
  project folder, but keep purchase/download evidence outside the repo too.
- If any pack was bought via another person's itch.io account, keep a short
  written note that the purchaser bought it for this project and permits its
  use in this submission.
- Keep copies of license text or screenshots/receipts for any downloaded asset
  packs outside the repo if the license does not allow committing them.
- If a source cannot be verified, replace the asset with original/generated
  project-owned art or remove it from the build.
