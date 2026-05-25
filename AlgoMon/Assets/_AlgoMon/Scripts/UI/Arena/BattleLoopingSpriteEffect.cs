using UnityEngine;

/// <summary>
/// Plays a looping sprite-sheet effect for lightweight battle idles.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class BattleLoopingSpriteEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1f)] private float framesPerSecond = 18f;
    [SerializeField] private bool randomizeStartFrame = true;
    [SerializeField, Range(0f, 1f)] private float alpha = 0.75f;

    private int frameIndex;
    private float frameCursor;

    private void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Awake()
    {
        EnsureRenderer();
    }

    private void OnEnable()
    {
        EnsureRenderer();
        frameCursor = 0f;
        frameIndex = randomizeStartFrame && HasFrames()
            ? Random.Range(0, frames.Length)
            : 0;
        ApplyFrame();
    }

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

    private void OnValidate()
    {
        EnsureRenderer();
        ApplyFrame();
    }

    private bool HasFrames()
    {
        return frames != null && frames.Length > 0;
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

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
