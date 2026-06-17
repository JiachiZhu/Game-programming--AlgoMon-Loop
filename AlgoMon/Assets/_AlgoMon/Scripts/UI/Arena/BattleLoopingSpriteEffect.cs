using UnityEngine;

/// <summary>
/// Plays a looping sprite-sheet effect for lightweight battle idles.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
// Defense note: BattleLoopingSpriteEffect is a Unity component attached to a scene object for this feature.
public class BattleLoopingSpriteEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField] private bool randomizeStartFrame = true;
    [SerializeField, Range(0f, 1f)] private float alpha = 0.75f;

    private int frameIndex;
    private float frameCursor;

    // Defense note: Runs the reset helper used by this script.
    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        EnsureRenderer();
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        EnsureRenderer();
        frameCursor = 0f;
        frameIndex = randomizeStartFrame && HasFrames()
            ? Random.Range(0, frames.Length)
            : 0;
        ApplyFrame();
    }

    // Defense note: Unity lifecycle hook that runs the update step for this component.
    private void Update()
    {
        if (!HasFrames())
        {
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;
            return;
        }

        EnsureRenderer();
        frameCursor += Time.deltaTime * framesPerSecond;
        int steps = Mathf.FloorToInt(frameCursor);
        if (steps <= 0)
            return;

        frameCursor -= steps;
        frameIndex = (frameIndex + steps) % frames.Length;
        ApplyFrame();
    }

    // Defense note: Unity lifecycle hook that runs the on validate step for this component.
    private void OnValidate()
    {
        EnsureRenderer();
        ApplyFrame();
    }

    // Defense note: Returns whether frames exists or is active.
    private bool HasFrames()
    {
        return frames != null && frames.Length > 0;
    }

    // Defense note: Ensures the renderer dependency or state exists before use.
    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Defense note: Applies the frame change to gameplay or UI state.
    private void ApplyFrame()
    {
        if (spriteRenderer == null || !HasFrames())
            return;

        frameIndex = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        spriteRenderer.enabled = true;
        spriteRenderer.sprite = frames[frameIndex];
        Color color = spriteRenderer.color;
        color.a = alpha;
        spriteRenderer.color = color;
    }
}
