# MainTerminal Component Sheet

Issue #39 now has a Unity-side component sheet for the MainTerminal overlay.
It is intentionally a prefab language pass, not the final interaction wiring.

## Assets

Imported third-party sprite sources:

- `Assets/_AlgoMon/Sprites/UI/MainTerminal/CyberpunkHUD`
- `Assets/_AlgoMon/Sprites/UI/MainTerminal/PixelUIHUD`

Generated/tinted derivative sprites:

- `Assets/_AlgoMon/Sprites/UI/MainTerminal/Components/CyberpunkHUD/*_tint.png`
- `Assets/_AlgoMon/Sprites/UI/MainTerminal/Components/MainTerminal_ScanlineTile.png`

Generated prefabs:

- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_ComponentSheet.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_TerminalPanel.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_CommandButton.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_TierCard.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_PseudoSpriteWindow.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_ScanlineStrip.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_AccentRail.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_StatusChip.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_DagNode.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_DagPreview.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_ValueBar.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_ModuleSlot.prefab`
- `Assets/_AlgoMon/Prefabs/UI/MainTerminal/MainTerminal_SourceLayout.prefab`

Preview render:

- `Assets/Screenshots/MainTerminal_component_sheet_assetpack_preview.png`
- `Assets/Screenshots/MainTerminal_source_layout_preview.png`
- `Assets/Screenshots/CyberpunkHUD_asset_contact_sheet.png`
- `Assets/Screenshots/PixelUI_contact_*.png`

## Component Intent

- Source layout: first-pass reconstruction of the Cyberpunk HUD pack's own layout rhythm. Use this as the baseline before AlgoMon-specific optimization.
- Terminal panel: large readable dark screen surface assembled from Cyberpunk HUD frames, cyan rails, status chips, and scanline texture.
- Command button: main-menu entry button shell using dark cyber HUD buttons plus tintable icon slots for Enter Grid, Payload Box, Gene Lab, Settings, and related terminal commands.
- DAG node / DAG preview: run-only route graph language using Pixel UI skill-tree slots, connectors, hover/selected reticles, and AlgoMon-colored node interiors.
- Value bar: Pixel UI value bar pieces tinted for battery, CP, integrity, reward, and enemy status.
- Module slot: payload/module slot combining Cyberpunk HUD slot frames with Pixel UI selector focus states.
- Tier card: compact threat-depth selector, now treated as a node-state variant rather than main-menu navigation.
- Pseudo sprite window: focused avatar display panel for the selected depth/tier threat.
- Scanline strip: terminal log rail and low-contrast texture piece.
- Accent rail: small cyan/magenta/purple edge detail for making the terminal screen feel assembled from parts.
- Status chip: compact online/session/status marker.
- Bitmap text: component labels use `NicoBitmap/BoldBasic` through `CyberBitmapTextGraphic` instead of the default Unity UI font.

## Third-party Credits

See `Docs/UIStyle/ThirdPartyAssetCredits.md`.

## Boundaries

- The main-menu entry buttons belong in MainTerminal.
- DAG/node graph UI belongs after the player enters a run, in TheGrid.
- Existing transparent legacy buttons should remain as hitboxes until the new prefab buttons are wired to the controller.
- The typing character and purple cyber room background stay decorative; no extra character shadow or backing panel is required.

## Regeneration

Use Unity menu item:

`Tools/AlgoMon/UI/Rebuild MainTerminal Component Sheet`

Export the preview PNG with:

`Tools/AlgoMon/UI/Export MainTerminal Component Sheet Preview PNG`

Export the source-layout baseline PNG with:

`Tools/AlgoMon/UI/Export MainTerminal Source Layout Preview PNG`
