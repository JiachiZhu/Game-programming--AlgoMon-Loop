# Battle Presentation

This document records the current battle presentation layer and the planned
extension points for future animation / VFX work. Sprint 2 issue #18 builds the
generic template only; per-species animation profiles and per-skill VFX profiles
are intentionally deferred.

## Current Runtime Pieces

| Piece | File | Responsibility |
|---|---|---|
| Sprite motion template | `Assets/_AlgoMon/Scripts/UI/Arena/BattleSpriteAnimator.cs` | Idle bob, generic action lunge, counter clash, hit flash/shake, status pulse |
| Event presentation bridge | `Assets/_AlgoMon/Scripts/UI/Arena/BattlePresentationController.cs` | Listens to battle events and triggers sprite motion / floating feedback |
| Background fitting | `Assets/_AlgoMon/Scripts/UI/Arena/BattleBackgroundFitter.cs` | Scales the world-space arena background to cover the camera view |
| HUD smoothing | `Assets/_AlgoMon/Scripts/UI/Arena/BattleHudController.cs` | Smooth Battery / CP visual interpolation |
| Feedback events | `Assets/_AlgoMon/Scripts/Core/GameEvents.cs` | Damage, CP, heal, status, and counter presentation events |

## Current Generic Templates

These are intentionally species-neutral. They are the fallback behavior when no
future species or skill profile exists.

| Template | Current behavior |
|---|---|
| Idle | Vertical bob, breathing scale, slight tilt, shadow pulse |
| Action / attack | Move toward target, then return |
| Counter clash | Counter-loser moves halfway into the attack, freezes, counter-winner completes action, then loser returns |
| Hit | Short shake and white flash |
| Status | Color pulse based on status type |
| Floating feedback | Damage, heal, CP gain/drain, status text, COUNTER text |
| Background | SpriteRenderer background auto-fits the active camera |

## Future Species Animation Profile

Create one `BattleAnimationProfile` ScriptableObject per AlgoMon species or per
form. The animator should stay generic; profiles provide style and tuning.

Suggested fields:

```csharp
public class BattleAnimationProfile : ScriptableObject
{
    public string profileId;

    [Header("Idle")]
    public float idleBobAmplitude;
    public float idleBobSpeed;
    public float idleScaleAmplitude;
    public float idleTiltDegrees;

    [Header("Motion")]
    public float attackLungeDistance;
    public float attackDuration;
    public float castMotionDistance;
    public float defenseMotionDistance;
    public float hitShakeAmplitude;
    public float hitDuration;

    [Header("Counter")]
    public float counterInterruptedDistance;
    public float counterInterruptedStartup;
    public float counterInterruptedHoldDuration;
    public float counterRecoverDuration;

    [Header("Style")]
    public Color hitFlashColor;
    public Color counterFreezeColor;
    public Color statusTintStrength;
}
```

Example direction:

| AlgoMon | Motion style |
|---|---|
| Sortex | Quick bounce, larger lunges, electric snap |
| Cachelon | Floating drift, slow recovery, jelly-like hit response |
| Heapion | Heavy idle, small lunge, strong guard pose |
| Nullbyte | Glitch jitter, short teleport-like action |
| Overflux | Liquid sway, longer smear/recovery |
| Recursix | Rhythmic loop, mirrored afterimages |

## Future Skill VFX Profile

Create one `SkillVfxProfile` ScriptableObject per skill or reusable VFX family.
The same skill VFX can be used by many species, while the species animation
profile controls how each caster moves.

Suggested fields:

```csharp
public enum PresentationMotion
{
    Strike,
    Cast,
    Guard,
    Charge,
    Status,
}

public class SkillVfxProfile : ScriptableObject
{
    public string profileId;
    public PresentationMotion motion;

    [Header("Timing")]
    public float windupSeconds;
    public float releaseSeconds;
    public float impactSeconds;
    public float recoverySeconds;

    [Header("Color")]
    public Color castColor;
    public Color impactColor;

    [Header("Prefabs")]
    public GameObject castVfxPrefab;
    public GameObject projectilePrefab;
    public GameObject impactVfxPrefab;
    public GameObject statusVfxPrefab;

    [Header("Camera")]
    public float hitStopSeconds;
    public float cameraShakeAmplitude;
    public float cameraShakeDuration;
}
```

Playback rule:

```text
Presentation = caster BattleAnimationProfile motion + skill SkillVfxProfile effect
```

Examples:

| Case | Result |
|---|---|
| Sortex uses Thermal Throttling | Sortex-specific cast motion + Thermal Throttling heat / burn VFX |
| Cachelon uses Thermal Throttling | Cachelon-specific cast motion + same Thermal Throttling VFX |
| Any species uses Safe Mode | Species-specific guard motion + Safe Mode shield/heal VFX |

## Suggested Sprint 3 Issue

Title: `[Polish] Add species animation profiles and skill VFX profiles`

Checklist:

- [ ] Add `BattleAnimationProfile` asset type.
- [ ] Add `SkillVfxProfile` asset type.
- [ ] Add optional animation / VFX references to `AlgoMonData` and `SkillData`.
- [ ] Teach `BattleSpriteAnimator` to read species profile values.
- [ ] Teach `BattlePresentationController` to combine species motion with skill VFX.
- [ ] Create first-pass profiles for Sortex and Cachelon.
- [ ] Create first-pass VFX profiles for one Attack, one Defense, and one Status skill.
- [ ] Add object pooling for floating feedback and short-lived VFX prefabs.
- [ ] Add Play Mode smoke checks for non-null profiles and fallback behavior.

## Non-Goals For Issue #18

- No per-skill particle library yet.
- No per-species animation assets yet.
- No camera shake / hit-stop system yet beyond local sprite motion.
- No sound effects yet.
- No custom timeline sequencing per individual skill yet.
