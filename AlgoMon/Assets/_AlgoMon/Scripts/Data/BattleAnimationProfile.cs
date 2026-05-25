using System;
using UnityEngine;

public enum BattleAnimationState
{
    Idle,
    Attack,
    Defense,
    Status,
    Hit,
    Faint
}

[Serializable]
public class BattleAnimationClipData
{
    [Tooltip("Animation frames in playback order.")]
    public Sprite[] frames = new Sprite[0];

    [Min(1f)]
    public float fps = 12f;

    public bool loop;

    [Tooltip("1-based authoring frame for impact / guard / effect. -1 means unused.")]
    public int actionFrame = -1;

    [Tooltip("1-based authoring frame where the sprite moves to the target. -1 means no contact movement.")]
    public int contactFrame = -1;

    [Tooltip("1-based authoring frame where the sprite returns to its home position. -1 means return at clip end.")]
    public int returnFrame = -1;

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

    [Header("Core States")]
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
