/*
Script Audit:
- Purpose: Stores frame animation clips for each battle animation state.
- Attached GameObject: None; this is a ScriptableObject asset referenced by AlgoMonData and BattleSpriteAnimator.
- Main responsibilities: Keep entry, idle, attack, defense, status, hit, and faint clip data and return the correct clip by state.
- Important variables: profileId, mirrorX, entry, idle, attack, defense, status, hit, faint, frames, fps, loop, startFrame, actionFrame, contactFrame, returnFrame.
- Inputs: Sprite frames and timing values assigned in the Inspector or loaded by BattleAnimationProfileLoader.
- Outputs or effects: BattleSpriteAnimator uses this data to play species-specific animations.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Assign a profile to an AlgoMon and verify the correct animation plays for idle, attack, hit, and faint.
*/
using System;
using UnityEngine;

public enum BattleAnimationState
{
    Idle,
    Attack,
    Defense,
    Status,
    Hit,
    Faint,
    Entry
}

[Serializable]
public class BattleAnimationClipData
{
    [Tooltip("Animation frames in playback order.")]
    public Sprite[] frames = new Sprite[0];

    [Min(1f)]
    public float fps = 12f;

    public bool loop;

    [Tooltip("1-based authoring frame where playback starts. Defaults to the first frame.")]
    public int startFrame = 1;

    [Tooltip("1-based authoring frame for impact / guard / effect. -1 means unused.")]
    public int actionFrame = -1;

    [Tooltip("1-based authoring frame where the sprite moves to the target. -1 means no contact movement.")]
    public int contactFrame = -1;

    [Tooltip("1-based authoring frame where the sprite returns to its home position. -1 means return at clip end.")]
    public int returnFrame = -1;

    [Tooltip("If true, movement eases from home to contact across frames 1..contactFrame. If false, movement snaps on contactFrame.")]
    public bool smoothContactMovement;

    [Tooltip("If true, movement eases from contact back home across returnFrame..clip end. If false, movement snaps back on returnFrame.")]
    public bool smoothReturnMovement;

    [Tooltip("How far to stop from the target when contact movement is used.")]
    [Min(0f)]
    public float contactDistanceFromTarget = 0.65f;

    [Tooltip("Additional contact offset. X is applied in the attack direction; Y is vertical.")]
    public Vector2 contactOffset;

    [Tooltip("If true, a non-looping clip keeps its final frame after playback. Used by faint clips.")]
    public bool holdLastFrame;

    public bool HasFrames => frames != null && frames.Length > 0;
    public int FrameCount => frames != null ? frames.Length : 0;
    public float SecondsPerFrame => 1f / Mathf.Max(1f, fps);

    public int ActionFrameIndex => ToZeroBasedIndex(actionFrame);
    public int ContactFrameIndex => ToZeroBasedIndex(contactFrame);
    public int ReturnFrameIndex => ToZeroBasedIndex(returnFrame);
    public int StartFrameIndex => ToZeroBasedIndex(startFrame);

    private int ToZeroBasedIndex(int oneBasedFrame)
    {
        if (oneBasedFrame <= 0 || FrameCount <= 0)
            return -1;

        return Mathf.Clamp(oneBasedFrame - 1, 0, FrameCount - 1);
    }
}

[CreateAssetMenu(fileName = "BattleAnimationProfile", menuName = "AlgoMon/Battle Animation Profile")]
public class BattleAnimationProfile : ScriptableObject
{
    public string profileId;

    [Tooltip("Flips this profile horizontally before side-facing scale is applied. Use for species/forms authored opposite to the normal battle-facing convention.")]
    public bool mirrorX;

    [Tooltip("Species/form-specific visual scale applied after automatic height normalization.")]
    [Min(0.1f)]
    public float visualScaleMultiplier = 1f;

    [Header("Core States")]
    public BattleAnimationClipData entry = new BattleAnimationClipData { fps = 8f };
    public BattleAnimationClipData idle = new BattleAnimationClipData { loop = true, fps = 8f };
    public BattleAnimationClipData attack = new BattleAnimationClipData { fps = 12f };
    public BattleAnimationClipData defense = new BattleAnimationClipData { fps = 12f };
    public BattleAnimationClipData status = new BattleAnimationClipData { fps = 12f };
    public BattleAnimationClipData hit = new BattleAnimationClipData { fps = 12f };
    public BattleAnimationClipData faint = new BattleAnimationClipData { fps = 10f, holdLastFrame = true };

    public BattleAnimationClipData ClipFor(BattleAnimationState state)
    {
        switch (state)
        {
            case BattleAnimationState.Entry:
                return entry;
            case BattleAnimationState.Idle:
                return idle;
            case BattleAnimationState.Attack:
                return attack;
            case BattleAnimationState.Defense:
                return defense;
            case BattleAnimationState.Status:
                return status;
            case BattleAnimationState.Hit:
                return hit;
            case BattleAnimationState.Faint:
                return faint;
            default:
                return null;
        }
    }
}
