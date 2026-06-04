# Sprint 5 Visual Style Guide

This guide defines the shared cyber-pixel UI language for Sprint 5 polish work.
It should guide MainTerminal, TheGrid, TheArena, Compute Shop, and RunResult so
they feel like one operating system instead of separate mockups.

## Direction

AlgoMon UI should feel like a compact hacker terminal inside a purple cyber
room. The world mood can stay purple and neon, but interactive UI should use a
clear blue terminal language.

- Purple is the room mood and brand atmosphere.
- Cyan and electric blue are the primary system UI colors.
- Magenta is warning, danger, capture tension, and active alerts.
- Amber is resource cost, compute, CP, and reward energy.
- Green is stable, online, battery, success, and confirmed state.

Use the current large-terminal MainTerminal direction as the main-menu
reference: purple room mood, clean terminal screen, and interactive UI rendered
as separate Unity overlay elements.

## Color Roles

| Role | Hex | Use |
|---|---|---|
| Background | `#050812` | Full-screen base, dark gaps, inactive depth. |
| Room purple | `#7B2CFF` | Ambient glow, neon edges, background-only mood. |
| Panel dark | `#07111F` | Terminal panels, HUD cards, list surfaces. |
| Panel border | `#174B74` | Default panel outline and quiet separators. |
| Primary accent | `#18D9FF` | Main UI strokes, reachable nodes, normal focus. |
| Selected | `#8DFFF0` | Selected tier, selected node, active menu item. |
| Danger | `#FF3B86` | Boss, critical warning, blocked capture risk. |
| Reward / CP | `#FF9B35` | Compute, CP, shop value, reward emphasis. |
| Success / Battery | `#78F28A` | Battery, online status, confirmed outcomes. |
| Disabled | `#39414F` | Locked, unavailable, spent, offline secondary text. |
| Text primary | `#F3F7FF` | Main labels and high priority information. |
| Text secondary | `#A8B6CE` | Descriptions, metadata, footer status. |

Do not let a screen become one-note purple or one-note blue. Purple should sit
mostly in the background; blue/cyan should carry the interaction layer.

## Typography

Use the Nico bitmap font assets already in the project. Keep text crisp and
avoid heavy resizing at runtime.

| Text type | Suggested size | Use |
|---|---:|---|
| Screen title | 22-28 | Main screen identity, result title. |
| Section title | 14-18 | Panel headers, module names, node preview titles. |
| Button label | 13-16 | Primary navigation and battle commands. |
| Body / detail | 10-13 | Descriptions, payload stats, route preview text. |
| Micro status | 8-10 | Footer logs, debug-like metadata, compact counters. |

Rules:

- Use uppercase for terminal labels and commands.
- Keep long paragraphs out of live gameplay surfaces.
- Prefer two short lines over one long line inside buttons or compact panels.
- Pixel text should use point sizes that render cleanly at the target canvas
  resolution.
- Important numbers should be readable without relying on color alone.

## Buttons

Buttons should feel like terminal commands, not generic app controls.

| State | Treatment |
|---|---|
| Normal | Dark panel fill, cyan border at low alpha, white label. |
| Hover / focus | Brighter cyan border, faint cyan fill, small glow or scanline. |
| Selected | Cyan/teal border, stable selected fill, optional small status LED. |
| Pressed | Short magenta or cyan flash, slight inset or brightness dip. |
| Disabled | Dark gray fill, dim label, border near `#39414F`, no glow. |
| Danger action | Magenta border and warning icon/accent, not full red fill. |
| Reward action | Amber accent, especially for shop and CP/compute decisions. |

Use consistent feedback timing:

- Hover/focus: immediate, subtle.
- Press: short 0.08-0.14 second flash.
- Confirmed action: one stronger pulse or sound.
- Error/locked: quick magenta flicker, then return to disabled/locked state.

## Panels And Borders

Use panels to group decisions, not to decorate every empty area.

- Main panel fill: dark blue-black with 80-95% opacity.
- Border: 1-2 px cyan/blue line, with cut corners when possible.
- Selected panels may use cyan plus a small magenta or amber accent.
- Avoid nested cards unless the inner card is an actual repeated item.
- Keep at least 12 px internal padding on compact panels and 20-32 px on main
  terminal panels.
- Keep center gameplay or map areas clear; put deep details in side panels,
  drawers, or hover previews.
- Use scanlines, dots, and tiny code blocks only as low-contrast texture.

## Screen Rules

### MainTerminal

MainTerminal keeps the purple hacker-room atmosphere. The background can be a
full image, but all interactive buttons, text, tier selection, and feedback
should be Unity UI overlays.

- Use the large empty terminal screen as the UI canvas area.
- The animated typing character stays decorative and must not cover controls.
- Primary action: Enter Grid / Continue / Restart.
- Selected depth tier must be obvious through selected state, not explanation.
- Keep the left character area atmospheric; keep the right terminal readable.

### TheGrid

TheGrid should be the pure blue data-network screen.

- Nodes use cyan for reachable, teal/white for current, gray for locked, and
  magenta for boss/danger.
- Route lines should be thinner than nodes.
- Hover/focus preview should explain risk and reward in a compact side or
  bottom panel.
- Avoid large permanent menu panels over the graph.

### TheArena

TheArena prioritizes battle readability over decoration.

- Battery is green, CP is amber, danger/offline states are magenta.
- Skill buttons use the same button state rules as MainTerminal.
- Player and enemy panels should mirror each other where possible.
- Damage, healing, CP, status, and counter feedback should not hide Battery/CP.
- Reserve large motion for hits, counters, victory, defeat, and capture.

### Compute Shop

Shop UI should feel like a terminal transaction panel.

- Costs and compute use amber.
- Affordable items use normal cyan focus states.
- Unaffordable or spent items use disabled gray.
- Purchase confirmation should use a short amber pulse plus status text.

### RunResult

RunResult should feel like a system report.

- Victory uses cyan/green success accents with amber rewards.
- Defeat uses controlled magenta danger accents.
- Rewards should be grouped into readable rows: payload, compute, EXP, unlocks.
- Continue/restart actions follow the shared button states.

## Motion

Use a few purposeful animations:

- Button hover glow.
- Selected tier/node pulse.
- Terminal cursor blink.
- Small scanline over progress or loading elements.
- Short confirmation/error flashes.

Avoid constant full-screen motion, heavy bloom, or decorative animation over
important text. Respect the visual hierarchy first.

## Asset Notes

Current Sprint 5 references:

- Main menu character animation:
  `Assets/_AlgoMon/Sprites/UI/MainTerminal/CharacterTypingDisplayFrames_Fixed/`
- MainTerminal backgrounds should remain atmosphere-only where possible; live
  buttons, labels, tier selectors, progress, and terminal status belong in
  Unity UI overlays.
- Style exploration images are language references, not literal single-screen
  layouts. Do not combine MainTerminal menus, Grid DAG, and Arena HUD into one
  in-game surface.

## Done Checklist

Use this checklist when polishing each Sprint 5 screen:

- The screen uses the shared color roles.
- Main actions are visually stronger than secondary details.
- Button states are visible and consistent.
- Text remains crisp at gameplay resolution.
- Disabled, selected, danger, and reward states are distinct.
- Decorative animation does not cover controls, numbers, routes, or sprites.
- The screen feels like part of AlgoMon's cyber-pixel operating system.
