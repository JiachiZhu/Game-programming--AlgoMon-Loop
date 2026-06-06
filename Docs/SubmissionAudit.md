# Submission Audit

Last updated: 2026-06-01  
Current submitted GitHub commit: `7a98aae` - `Finish tier selection and party defeat flow`

This audit answers the classroom checklist for the current AlgoMon Sprint 4 vertical slice.

## What is ready?

- The Sprint 4 vertical slice is playable as a connected flow: MainTerminal -> TheGrid -> TheArena -> RunResult.
- The player can start a run, select a difficulty depth from 1F to 5F, enter TheGrid, choose route nodes, enter battles, win rewards, and return to the result flow.
- Threat Tier 1-5 progression and Lv1-Lv50 encounter bands are implemented.
- Node depth now affects encounter pressure inside each Threat Tier.
- Wild, hacker, elite, boss, and shop nodes have distinct reward roles.
- The player can switch party AlgoMons during battle, and switching resolves before normal skill actions.
- Hacker encounters can use multi-AlgoMon pressure.
- The battle defeat rule has been corrected: one fainted player AlgoMon does not end the battle if another party member can still fight.
- TheGrid readability work from issue #35 is complete.
- Main asset citation files are present in `Docs/AssetAttribution.md`, `Docs/UsedAssetInventory.md`, `Docs/UsedAssetFiles.csv`, and `Docs/UsedAssetFiles.tsv`.

## What is missing?

- A final packaged build has not been confirmed from this machine yet.
- A final classroom submission package, such as a short gameplay video or screenshots, still needs to be prepared if the teacher requires it.
- Unity Play Mode test notes for the latest pushed commit should be added after one final smoke test.
- The unused `FreePixelEffectsPack` file still needs either a confirmed source URL or removal before final submission if it remains in the repository.
- Sprint 5 systems such as the full Lab, gene merge UI, and final evolution workflow are not part of this Sprint 4 playable slice yet.

## What is unclear?

- The exact required submission format is still unclear: GitHub link only, playable build, video proof, screenshots, or all of them.
- It is unclear whether unused assets must be removed from the repository or whether it is enough to document them as unused.
- It is unclear whether the teacher expects only Sprint 4 scope, or whether future Sprint 5 features should be mentioned as planned work.

## What is not credited?

- No known actively used major asset group is currently uncredited.
- AI-assisted and generated assets still need to be disclosed clearly in the final report or presentation. This includes PixelLab-generated AlgoMon sprite frames, ChatGPT Image 2 menu and battle images, and Gemini / Nano Banana element icons.
- The main remaining citation risk is the currently unused Free Pixel Effects Pack file. If it stays in the repo, its source URL should be added to the attribution log.
- External packages from Unity Package Manager, UOS Launcher, and Unity MCP are listed, but their upstream license terms should be checked before redistribution.

## What is not tested?

- `dotnet build AlgoMon\AlgoMon.sln` cannot complete on this machine because the .NET Framework 4.7.1 reference assemblies are missing.
- The latest pushed gameplay changes still need one Unity Play Mode smoke test:
  - Start the game from MainTerminal.
  - Select each depth option from 1F to 5F.
  - Confirm TheGrid uses the selected difficulty.
  - Confirm grid labels and node information are readable.
  - Enter wild, hacker, elite, boss, and shop nodes.
  - In battle, faint the active player AlgoMon and confirm the game asks the player to switch.
  - Confirm the battle only ends in defeat when all player party AlgoMons are offline.
  - Confirm victory rewards, RunResult, restart, and continue paths still work.
- Automated gameplay tests are not currently available for the Unity battle and grid flow.

## What cannot be played?

- The main Sprint 4 vertical slice can be played in Unity, but a standalone exported build has not been confirmed yet.
- The full Lab / gene merge system cannot be played yet because it is planned as later work.
- The final evolution UI cannot be played yet; Sprint 4 only defines and grants evolution data as part of the reward contract.
- Some polish goals, such as broader VFX and final animation polish, are still outside the current playable slice.

## What must be fixed first?

1. Run a final Unity Play Mode smoke test on commit `7a98aae`.
2. If the smoke test finds a broken flow, fix that before packaging anything else.
3. Confirm or remove the unused Free Pixel Effects Pack file.
4. Prepare the final submission evidence required by the teacher, such as a build, screenshots, or gameplay video.
5. Keep this audit, asset attribution, and Sprint 4 notes together with the final GitHub submission link.

