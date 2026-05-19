using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// First-pass presentation layer for TheArena. It listens to battle events and
/// drives generic sprite motion plus floating world-space feedback text.
/// </summary>
[DisallowMultipleComponent]
public class BattlePresentationController : MonoBehaviour
{
    [Header("Combatants")]
    // Sprint 2 uses the fixed TheArena matchup. Party switching should replace
    // these static ids with runtime combatant registration.
    [SerializeField] private string playerId = "Sortex";
    [SerializeField] private string enemyId = "Cachelon";
    [SerializeField] private BattleSpriteAnimator playerAnimator;
    [SerializeField] private BattleSpriteAnimator enemyAnimator;

    [Header("Floating Feedback")]
    [SerializeField, Min(0f)] private float feedbackLifetime = 0.75f;
    [SerializeField, Min(0f)] private float feedbackRise = 0.75f;
    [SerializeField] private int feedbackSortingOrder = 80;
    [SerializeField] private Font feedbackFont;

    [Header("Counter Clash")]
    [SerializeField, Min(0f)] private float counterResponseDelay = 0.24f;
    [SerializeField, Min(0f)] private float counterInterruptedHoldDuration = 1.15f;
    [Tooltip("Safety window for suppressing the real damage event's lunge after the counter clash already played it.")]
    [SerializeField, Min(0f)] private float counterActionSuppressSeconds = 12f;

    private static readonly Vector3[] FeedbackOffsets =
    {
        new Vector3(-0.28f, 0.10f, 0f),
        new Vector3( 0.28f, 0.24f, 0f),
        new Vector3( 0.00f, 0.42f, 0f),
        new Vector3(-0.18f, 0.58f, 0f),
        new Vector3( 0.18f, 0.72f, 0f),
    };

    private readonly Dictionary<BattleSpriteAnimator, int> feedbackSlots =
        new Dictionary<BattleSpriteAnimator, int>();
    private readonly Dictionary<BattleSpriteAnimator, float> feedbackTimes =
        new Dictionary<BattleSpriteAnimator, float>();
    private readonly Dictionary<string, float> counterActionSuppressUntil =
        new Dictionary<string, float>();
    private readonly Dictionary<string, int> counterActionSuppressCounts =
        new Dictionary<string, int>();

    private void Awake()
    {
        AutoBind();
    }

    private void OnEnable()
    {
        feedbackSlots.Clear();
        feedbackTimes.Clear();
        counterActionSuppressUntil.Clear();
        counterActionSuppressCounts.Clear();
        EventBus.Subscribe<DamageEvent>(OnDamage);
        EventBus.Subscribe<BattleFeedbackEvent>(OnFeedback);
        EventBus.Subscribe<StatusAppliedEvent>(OnStatusApplied);
        EventBus.Subscribe<CounterEvent>(OnCounter);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<DamageEvent>(OnDamage);
        EventBus.Unsubscribe<BattleFeedbackEvent>(OnFeedback);
        EventBus.Unsubscribe<StatusAppliedEvent>(OnStatusApplied);
        EventBus.Unsubscribe<CounterEvent>(OnCounter);
    }

    private void AutoBind()
    {
        if (playerAnimator == null)
        {
            GameObject go = GameObject.Find("PlayerSpriteAnchor");
            if (go != null)
                playerAnimator = go.GetComponent<BattleSpriteAnimator>();
        }

        if (enemyAnimator == null)
        {
            GameObject go = GameObject.Find("EnemySpriteAnchor");
            if (go != null)
                enemyAnimator = go.GetComponent<BattleSpriteAnimator>();
        }
    }

    public void RegisterCombatants(string playerCombatantId, string enemyCombatantId)
    {
        if (!string.IsNullOrWhiteSpace(playerCombatantId))
            playerId = playerCombatantId;
        if (!string.IsNullOrWhiteSpace(enemyCombatantId))
            enemyId = enemyCombatantId;
    }

    private void OnDamage(DamageEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        BattleSpriteAnimator attacker = AnimatorFor(evt.AttackerId);
        if (attacker != null && target != null && !ConsumeSuppressedCounterAction(evt.AttackerId))
            attacker.PlayActionToward(target.FeedbackWorldPosition);
        if (target != null)
            target.PlayHit();

        SpawnFeedback(target, $"-{evt.Amount}", new Color(1f, 0.36f, 0.32f));
    }

    private void OnFeedback(BattleFeedbackEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        if (target == null)
            return;

        switch (evt.Type)
        {
            case BattleFeedbackType.Damage:
                target.PlayHit();
                SpawnFeedback(target, evt.Label, new Color(1f, 0.36f, 0.32f));
                break;
            case BattleFeedbackType.Heal:
                SpawnFeedback(target, evt.Label, new Color(0.45f, 1f, 0.58f));
                break;
            case BattleFeedbackType.CPGain:
                SpawnFeedback(target, evt.Label, new Color(0.42f, 0.78f, 1f));
                break;
            case BattleFeedbackType.CPDrain:
                SpawnFeedback(target, evt.Label, new Color(1f, 0.76f, 0.28f));
                break;
            case BattleFeedbackType.Counter:
                SpawnFeedback(target, evt.Label, new Color(1f, 0.92f, 0.45f));
                break;
            case BattleFeedbackType.Status:
                SpawnFeedback(target, evt.Label, StatusColor(StatusType.Corrupted));
                break;
        }
    }

    private void OnStatusApplied(StatusAppliedEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        Color color = StatusColor(evt.Status);
        if (target != null)
            target.PlayStatus(color);

        SpawnFeedback(target, $"{evt.Status} +{evt.Stacks}", color);
    }

    private void OnCounter(CounterEvent evt)
    {
        BattleSpriteAnimator counter = AnimatorFor(evt.CounterId);
        BattleSpriteAnimator countered = AnimatorFor(evt.CounteredId);
        if (counter != null && countered != null)
        {
            if (evt.CounterHasDamage)
                SuppressNextCounterAttack(evt.CounterId);
            if (evt.CounteredHasDamage)
                SuppressNextCounterAttack(evt.CounteredId);
            countered.PlayCounterInterruptedToward(counter.FeedbackWorldPosition, counterInterruptedHoldDuration);
            StartCoroutine(PlayCounterResponse(counter, countered));
            return;
        }

        SpawnFeedback(counter, "COUNTER", new Color(1f, 0.92f, 0.45f));
    }

    private void SuppressNextCounterAttack(string combatantId)
    {
        if (string.IsNullOrEmpty(combatantId))
            return;

        counterActionSuppressCounts.TryGetValue(combatantId, out int count);
        counterActionSuppressCounts[combatantId] = count + 1;
        counterActionSuppressUntil[combatantId] = Time.time + counterActionSuppressSeconds;
    }

    private BattleSpriteAnimator AnimatorFor(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (id == playerId || id == "Player")
            return playerAnimator;
        if (id == enemyId || id == "Enemy")
            return enemyAnimator;
        return null;
    }

    private void SpawnFeedback(BattleSpriteAnimator target, string label, Color color)
    {
        if (target == null || string.IsNullOrEmpty(label))
            return;

        GameObject go = new GameObject($"Feedback_{label}");
        go.transform.SetParent(transform, false);
        go.transform.position = target.FeedbackWorldPosition + NextFeedbackOffset(target);

        TextMesh text = go.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.14f;
        text.fontSize = 48;
        text.color = color;
        if (feedbackFont != null)
            text.font = feedbackFont;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = feedbackSortingOrder;

        StartCoroutine(FloatAndFade(go, text, color));
    }

    private IEnumerator PlayCounterResponse(BattleSpriteAnimator counter, BattleSpriteAnimator countered)
    {
        if (counterResponseDelay > 0f)
            yield return new WaitForSeconds(counterResponseDelay);

        if (counter != null && countered != null)
            counter.PlayActionToward(countered.FeedbackWorldPosition);
        SpawnFeedback(counter, "COUNTER", new Color(1f, 0.92f, 0.45f));
    }

    private bool ConsumeSuppressedCounterAction(string attackerId)
    {
        if (string.IsNullOrEmpty(attackerId))
            return false;

        if (!counterActionSuppressCounts.TryGetValue(attackerId, out int count) || count <= 0)
            return false;

        if (counterActionSuppressUntil.TryGetValue(attackerId, out float until) && Time.time > until)
        {
            counterActionSuppressCounts.Remove(attackerId);
            counterActionSuppressUntil.Remove(attackerId);
            return false;
        }

        count--;
        if (count <= 0)
        {
            counterActionSuppressCounts.Remove(attackerId);
            counterActionSuppressUntil.Remove(attackerId);
        }
        else
        {
            counterActionSuppressCounts[attackerId] = count;
        }

        return true;
    }

    private Vector3 NextFeedbackOffset(BattleSpriteAnimator target)
    {
        if (!feedbackTimes.TryGetValue(target, out float lastTime) || Time.time - lastTime > 0.3f)
            feedbackSlots[target] = 0;

        feedbackTimes[target] = Time.time;
        feedbackSlots.TryGetValue(target, out int slot);
        feedbackSlots[target] = (slot + 1) % FeedbackOffsets.Length;
        return FeedbackOffsets[slot % FeedbackOffsets.Length];
    }

    private IEnumerator FloatAndFade(GameObject go, TextMesh text, Color startColor)
    {
        Vector3 start = go.transform.position;
        Vector3 end = start + Vector3.up * feedbackRise;
        float elapsed = 0f;

        while (elapsed < feedbackLifetime && go != null)
        {
            elapsed += Time.deltaTime;
            float p = feedbackLifetime <= 0f ? 1f : Mathf.Clamp01(elapsed / feedbackLifetime);
            go.transform.position = Vector3.Lerp(start, end, EaseOutCubic(p));
            if (text != null)
            {
                Color color = startColor;
                color.a = 1f - p;
                text.color = color;
            }
            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    private static Color StatusColor(StatusType status)
    {
        switch (status)
        {
            case StatusType.Burn:
                return new Color(1f, 0.38f, 0.18f);
            case StatusType.Freeze:
                return new Color(0.45f, 0.9f, 1f);
            case StatusType.Leech:
                return new Color(0.45f, 1f, 0.58f);
            case StatusType.Concurrent:
            case StatusType.BufferLoad:
            case StatusType.ComputingUp:
            case StatusType.ThroughputUp:
            case StatusType.FirewallUp:
            case StatusType.EncryptionUp:
            case StatusType.Overclock:
                return new Color(0.64f, 0.82f, 1f);
            default:
                return new Color(0.86f, 0.72f, 1f);
        }
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }
}
