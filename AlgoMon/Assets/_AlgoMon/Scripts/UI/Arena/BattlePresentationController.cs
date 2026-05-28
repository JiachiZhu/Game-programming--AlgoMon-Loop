/*
Script Audit:
- Purpose: Listens to battle events and drives TheArena visual feedback.
- Attached GameObject: TheArena presentation/controller object with references to player and enemy BattleSpriteAnimator components.
- Main responsibilities: Register combatants, play attack/defense/status/hit/faint animations, show damage/status/CP floating feedback, handle counter clash visuals, and load bitmap feedback fonts.
- Important variables: playerId, enemyId, playerAnimator, enemyAnimator, feedback settings, bitmap font references, feedbackSlots, counterActionSuppressUntil.
- Inputs: DamageEvent, BattleActionEvent, BattleFeedbackEvent, StatusAppliedEvent, UnitFaintedEvent, CounterEvent, and animation profile data.
- Outputs or effects: Starts sprite animations, spawns floating feedback text/sprites, and suppresses duplicate counter/hit visuals when needed.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
- Testing notes: Use attacks, counters, status skills, heals, CP changes, and fainting to confirm the correct visual feedback appears.
*/
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

    [Header("Animation Profiles")]
    [SerializeField] private BattleAnimationProfile playerAnimationProfileOverride;
    [SerializeField] private BattleAnimationProfile enemyAnimationProfileOverride;
    [SerializeField] private string defaultAnimationForm = "Base";
    [SerializeField] private bool autoLoadAnimationProfilesInEditor = true;

    [Header("Floating Feedback")]
    [SerializeField, Min(0f)] private float feedbackLifetime = 0.75f;
    [SerializeField, Min(0f)] private float feedbackRise = 0.75f;
    [SerializeField] private int feedbackSortingOrder = 80;
    [SerializeField] private Font feedbackFont;
    [SerializeField, Min(0.01f)] private float bitmapFeedbackScale = 1.25f;
    [SerializeField, Min(0.01f)] private float textFeedbackCharacterSize = 0.21f;
    [SerializeField, Min(1)] private int textFeedbackFontSize = 64;
    [SerializeField] private bool moveEnemyDamageFeedbackRight;
    [SerializeField, Min(0f)] private float enemyDamageFeedbackRightPadding = 0.55f;
    [SerializeField] private float enemyDamageFeedbackVerticalOffset = 0.15f;
    [SerializeField] private bool movePlayerFeedbackLeft = true;
    [SerializeField, Min(0f)] private float playerFeedbackLeftPadding = 0.45f;
    [SerializeField] private float playerFeedbackVerticalOffset = -0.28f;

    [Header("Element Feedback Bitmap Fonts")]
    [SerializeField] private bool useElementBitmapFeedbackFonts = true;
    [SerializeField] private bool autoLoadBitmapFeedbackFontsInEditor = true;
    [SerializeField] private string bitmapFeedbackFontAssetRoot = "Assets/_AlgoMon/Fonts/NicoBitmap";
    [SerializeField] private NicoBitmapFontReference normalBitmapFont =
        new NicoBitmapFontReference("BoldBasic", Color.white);
    [SerializeField] private NicoBitmapFontReference waterBitmapFont =
        new NicoBitmapFontReference("DigitalPup", Color.white);
    [SerializeField] private NicoBitmapFontReference fireBitmapFont =
        new NicoBitmapFontReference("CakeIcing", Color.white);
    [SerializeField] private NicoBitmapFontReference grassBitmapFont =
        new NicoBitmapFontReference("PaintBasic", new Color(0.45f, 1f, 0.58f));
    [SerializeField] private NicoBitmapFontReference iceBitmapFont =
        new NicoBitmapFontReference("PoolParty", Color.white);
    [SerializeField] private NicoBitmapFontReference electricBitmapFont =
        new NicoBitmapFontReference("BoldCheese", Color.white);
    [SerializeField] private NicoBitmapFontReference groundBitmapFont =
        new NicoBitmapFontReference("IceCream", Color.white);
    [SerializeField] private NicoBitmapFontReference utilityBitmapFont =
        new NicoBitmapFontReference("BoldTwilight", Color.white);

    [Header("Counter Clash")]
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

    private static readonly Vector3[] RightSideDamageFeedbackOffsets =
    {
        new Vector3( 0.00f,  0.00f, 0f),
        new Vector3( 0.18f,  0.16f, 0f),
        new Vector3(-0.08f,  0.30f, 0f),
        new Vector3( 0.14f, -0.14f, 0f),
        new Vector3(-0.12f,  0.08f, 0f),
    };

    private static readonly Vector3[] LeftSidePlayerFeedbackOffsets =
    {
        new Vector3( 0.00f,  0.00f, 0f),
        new Vector3(-0.16f,  0.12f, 0f),
        new Vector3( 0.08f,  0.24f, 0f),
        new Vector3(-0.12f, -0.10f, 0f),
        new Vector3( 0.10f,  0.06f, 0f),
    };

    private readonly Dictionary<BattleSpriteAnimator, int> feedbackSlots =
        new Dictionary<BattleSpriteAnimator, int>();
    private readonly Dictionary<BattleSpriteAnimator, float> feedbackTimes =
        new Dictionary<BattleSpriteAnimator, float>();
    private readonly Dictionary<string, float> counterActionSuppressUntil =
        new Dictionary<string, float>();
    private readonly Dictionary<string, int> counterActionSuppressCounts =
        new Dictionary<string, int>();
    private readonly Dictionary<string, float> actionMarkerFeedbackTimes =
        new Dictionary<string, float>();
    private readonly Dictionary<string, float> hitReactionSuppressUntil =
        new Dictionary<string, float>();

    private void Awake()
    {
        EnsureBitmapFontDefaults();
        AutoBind();
    }

    private void OnValidate()
    {
        EnsureBitmapFontDefaults();
    }

    private void OnEnable()
    {
        feedbackSlots.Clear();
        feedbackTimes.Clear();
        counterActionSuppressUntil.Clear();
        counterActionSuppressCounts.Clear();
        actionMarkerFeedbackTimes.Clear();
        hitReactionSuppressUntil.Clear();
        EventBus.Subscribe<BattleActionEvent>(OnBattleAction);
        EventBus.Subscribe<DamageEvent>(OnDamage);
        EventBus.Subscribe<BattleFeedbackEvent>(OnFeedback);
        EventBus.Subscribe<StatusAppliedEvent>(OnStatusApplied);
        EventBus.Subscribe<CounterEvent>(OnCounter);
        EventBus.Subscribe<UnitFaintedEvent>(OnUnitFainted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleActionEvent>(OnBattleAction);
        EventBus.Unsubscribe<DamageEvent>(OnDamage);
        EventBus.Unsubscribe<BattleFeedbackEvent>(OnFeedback);
        EventBus.Unsubscribe<StatusAppliedEvent>(OnStatusApplied);
        EventBus.Unsubscribe<CounterEvent>(OnCounter);
        EventBus.Unsubscribe<UnitFaintedEvent>(OnUnitFainted);
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

    private void EnsureBitmapFontDefaults()
    {
        if (normalBitmapFont == null || (!normalBitmapFont.HasFontName && !normalBitmapFont.HasAssignedAssets))
            normalBitmapFont = new NicoBitmapFontReference("BoldBasic", Color.white);
        if (waterBitmapFont == null || (!waterBitmapFont.HasFontName && !waterBitmapFont.HasAssignedAssets))
            waterBitmapFont = new NicoBitmapFontReference("DigitalPup", Color.white);
        if (fireBitmapFont == null || (!fireBitmapFont.HasFontName && !fireBitmapFont.HasAssignedAssets))
            fireBitmapFont = new NicoBitmapFontReference("CakeIcing", Color.white);
        if (grassBitmapFont == null || (!grassBitmapFont.HasFontName && !grassBitmapFont.HasAssignedAssets))
            grassBitmapFont = new NicoBitmapFontReference("PaintBasic", new Color(0.45f, 1f, 0.58f));
        if (iceBitmapFont == null || (!iceBitmapFont.HasFontName && !iceBitmapFont.HasAssignedAssets))
            iceBitmapFont = new NicoBitmapFontReference("PoolParty", Color.white);

        bool electricUsesIceDefault =
            electricBitmapFont != null &&
            !electricBitmapFont.HasAssignedAssets &&
            string.Equals(electricBitmapFont.FontName, "PoolParty", System.StringComparison.OrdinalIgnoreCase);
        if (electricBitmapFont == null ||
            electricUsesIceDefault ||
            (!electricBitmapFont.HasFontName && !electricBitmapFont.HasAssignedAssets))
        {
            electricBitmapFont = new NicoBitmapFontReference("BoldCheese", Color.white);
        }

        if (groundBitmapFont == null || (!groundBitmapFont.HasFontName && !groundBitmapFont.HasAssignedAssets))
            groundBitmapFont = new NicoBitmapFontReference("IceCream", Color.white);
        if (utilityBitmapFont == null || (!utilityBitmapFont.HasFontName && !utilityBitmapFont.HasAssignedAssets))
            utilityBitmapFont = new NicoBitmapFontReference("BoldTwilight", Color.white);

        if (bitmapFeedbackScale <= 0f)
            bitmapFeedbackScale = 1.25f;
        if (textFeedbackCharacterSize <= 0f)
            textFeedbackCharacterSize = 0.21f;
        if (textFeedbackFontSize <= 0)
            textFeedbackFontSize = 64;
    }

    public void RegisterCombatants(
        string playerCombatantId,
        string enemyCombatantId,
        BattleAnimationProfile playerProfile = null,
        BattleAnimationProfile enemyProfile = null,
        string playerCodeName = null,
        string enemyCodeName = null,
        string playerFormName = null,
        string enemyFormName = null)
    {
        if (!string.IsNullOrWhiteSpace(playerCombatantId))
            playerId = playerCombatantId;
        if (!string.IsNullOrWhiteSpace(enemyCombatantId))
            enemyId = enemyCombatantId;

        if (playerAnimator != null)
            playerAnimator.SetAnimationProfile(ResolveProfile(playerAnimationProfileOverride, playerProfile, playerCodeName, playerId, playerFormName));
        if (enemyAnimator != null)
            enemyAnimator.SetAnimationProfile(ResolveProfile(enemyAnimationProfileOverride, enemyProfile, enemyCodeName, enemyId, enemyFormName));
    }

    private BattleAnimationProfile ResolveProfile(
        BattleAnimationProfile overrideProfile,
        BattleAnimationProfile dataProfile,
        string codeName,
        string fallbackId,
        string formName = null)
    {
        if (overrideProfile != null)
            return overrideProfile;
        if (dataProfile != null)
            return dataProfile;
        if (!autoLoadAnimationProfilesInEditor)
            return null;

        string resolvedCodeName = !string.IsNullOrWhiteSpace(codeName) ? codeName : fallbackId;
        string resolvedFormName = !string.IsNullOrWhiteSpace(formName) ? formName : defaultAnimationForm;
        BattleAnimationProfile profile = BattleAnimationProfileLoader.TryLoadEditorProfile(resolvedCodeName, resolvedFormName);
        if (profile == null &&
            !string.Equals(resolvedFormName, defaultAnimationForm, System.StringComparison.OrdinalIgnoreCase))
        {
            profile = BattleAnimationProfileLoader.TryLoadEditorProfile(resolvedCodeName, defaultAnimationForm);
        }

        return profile;
    }

    private void OnBattleAction(BattleActionEvent evt)
    {
        BattleSpriteAnimator actor = AnimatorFor(evt.ActorId);
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        if (actor == null)
            return;
        if (ConsumeSuppressedCounterAction(evt.ActorId))
        {
            actionMarkerFeedbackTimes.Remove(evt.ActorId);
            return;
        }

        Vector3 targetPosition = target != null ? target.ContactWorldPosition : actor.ContactWorldPosition;
        switch (evt.InstructionType)
        {
            case InstructionType.Attack:
                RecordActionMarkerTime(evt.ActorId, actor, BattleAnimationState.Attack);
                actor.PlayAttackToward(targetPosition, target);
                break;
            case InstructionType.Defense:
                RecordActionMarkerTime(evt.ActorId, actor, BattleAnimationState.Defense);
                actor.PlayDefense();
                break;
            case InstructionType.Status:
                RecordActionMarkerTime(evt.ActorId, actor, BattleAnimationState.Status);
                actor.PlayStatusAction(new Color(0.64f, 0.82f, 1f));
                break;
        }
    }

    private void OnDamage(DamageEvent evt)
    {
        float delay = DelayUntilRecordedMarker(evt.AttackerId);
        if (delay > 0f)
            StartCoroutine(PlayDamageFeedbackAfterDelay(evt, delay));
        else
            PlayDamageFeedback(evt);
    }

    private void PlayDamageFeedback(DamageEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        if (target != null && !ShouldSuppressHitReaction(evt.TargetId))
            target.PlayHit();

        SpawnFeedback(
            target,
            DamageLabel(evt),
            DamageColor(evt.ElementMultiplier),
            evt.SkillElement,
            false,
            ShouldUseEnemyDamageSidePosition(target));
    }

    public float ExpectedDamageFeedbackRemaining(string attackerId, string targetId)
    {
        float markerDelay = PeekDelayUntilRecordedMarker(attackerId);
        BattleSpriteAnimator target = AnimatorFor(targetId);
        if (target == null || ShouldSuppressHitReaction(targetId))
            return markerDelay;

        return markerDelay + target.HitPlaybackDurationSeconds;
    }

    private IEnumerator PlayDamageFeedbackAfterDelay(DamageEvent evt, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayDamageFeedback(evt);
    }

    private void RecordActionMarkerTime(string actorId, BattleSpriteAnimator actor, BattleAnimationState state)
    {
        if (string.IsNullOrEmpty(actorId) || actor == null)
            return;

        if (actor.TryGetActionMarkerDelay(state, out float delay))
            actionMarkerFeedbackTimes[actorId] = Time.time + delay;
        else
            actionMarkerFeedbackTimes.Remove(actorId);
    }

    private float DelayUntilRecordedMarker(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return 0f;
        if (!actionMarkerFeedbackTimes.TryGetValue(actorId, out float markerTime))
            return 0f;
        float delay = markerTime - Time.time;
        if (delay <= 0f)
        {
            actionMarkerFeedbackTimes.Remove(actorId);
            return 0f;
        }

        return delay;
    }

    private float PeekDelayUntilRecordedMarker(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return 0f;
        if (!actionMarkerFeedbackTimes.TryGetValue(actorId, out float markerTime))
            return 0f;

        return Mathf.Max(0f, markerTime - Time.time);
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
                SpawnFeedback(
                    target,
                    evt.Label,
                    new Color(1f, 0.36f, 0.32f),
                    null,
                    true,
                    ShouldUseEnemyDamageSidePosition(target));
                break;
            case BattleFeedbackType.Heal:
                SpawnUtilityFeedback(target, evt.Label, new Color(0.45f, 1f, 0.58f));
                break;
            case BattleFeedbackType.CPGain:
                SpawnUtilityFeedback(target, evt.Label, new Color(0.42f, 0.78f, 1f));
                break;
            case BattleFeedbackType.CPDrain:
                SpawnUtilityFeedback(target, evt.Label, new Color(1f, 0.76f, 0.28f));
                break;
            case BattleFeedbackType.Counter:
                SpawnUtilityFeedback(target, evt.Label, new Color(1f, 0.92f, 0.45f));
                break;
            case BattleFeedbackType.Status:
                SpawnUtilityFeedback(target, evt.Label, StatusColor(StatusType.Corrupted));
                break;
        }
    }

    private void OnStatusApplied(StatusAppliedEvent evt)
    {
        float delay = DelayUntilRecordedMarker(evt.SourceId);
        if (delay > 0f)
            StartCoroutine(PlayStatusAppliedFeedbackAfterDelay(evt, delay));
        else
            PlayStatusAppliedFeedback(evt);
    }

    private void PlayStatusAppliedFeedback(StatusAppliedEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.TargetId);
        Color color = StatusColor(evt.Status);
        if (target != null)
            target.PlayStatus(color);

        SpawnUtilityFeedback(target, $"{evt.Status} +{evt.Stacks}", color);
    }

    private IEnumerator PlayStatusAppliedFeedbackAfterDelay(StatusAppliedEvent evt, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlayStatusAppliedFeedback(evt);
    }

    private void OnUnitFainted(UnitFaintedEvent evt)
    {
        BattleSpriteAnimator target = AnimatorFor(evt.UnitId);
        if (target != null)
            target.PlayFaint();
    }

    private void OnCounter(CounterEvent evt)
    {
        BattleSpriteAnimator counter = AnimatorFor(evt.CounterId);
        BattleSpriteAnimator countered = AnimatorFor(evt.CounteredId);
        if (counter != null && countered != null)
        {
            SuppressNextCounterAction(evt.CounterId);
            SuppressNextCounterAction(evt.CounteredId);
            actionMarkerFeedbackTimes.Remove(evt.CounterId);
            actionMarkerFeedbackTimes.Remove(evt.CounteredId);

            if (evt.CounterInstructionType == InstructionType.Defense &&
                evt.CounteredInstructionType == InstructionType.Attack)
            {
                StartCoroutine(PlayDefenseBlocksAttackCounter(evt, counter, countered));
            }
            else
            {
                StartCoroutine(PlayProfileCounterSequence(evt, counter, countered));
            }
            return;
        }

        SpawnUtilityFeedback(counter, "COUNTER", new Color(1f, 0.92f, 0.45f));
    }

    private void SuppressNextCounterAction(string combatantId)
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
        SpawnFeedback(target, label, color, null, false);
    }

    private void SpawnUtilityFeedback(BattleSpriteAnimator target, string label, Color color)
    {
        SpawnFeedback(target, label, color, null, true);
    }

    private void SpawnFeedback(BattleSpriteAnimator target, string label, Color color, ElementType? skillElement)
    {
        SpawnFeedback(target, label, color, skillElement, false);
    }

    private void SpawnFeedback(
        BattleSpriteAnimator target,
        string label,
        Color color,
        ElementType? skillElement,
        bool useUtilityBitmapFont)
    {
        SpawnFeedback(target, label, color, skillElement, useUtilityBitmapFont, false);
    }

    private void SpawnFeedback(
        BattleSpriteAnimator target,
        string label,
        Color color,
        ElementType? skillElement,
        bool useUtilityBitmapFont,
        bool useEnemyDamageSidePosition)
    {
        if (target == null || string.IsNullOrEmpty(label))
            return;

        GameObject go = new GameObject($"Feedback_{label}");
        go.transform.SetParent(transform, false);
        bool usePlayerLeftPosition = ShouldUsePlayerLeftPosition(target);
        Vector3 feedbackPosition = FeedbackPositionFor(target, useEnemyDamageSidePosition, usePlayerLeftPosition);
        go.transform.position = feedbackPosition + NextFeedbackOffset(
            target,
            useEnemyDamageSidePosition,
            usePlayerLeftPosition);

        NicoBitmapFontReference bitmapSource = null;
        if (skillElement.HasValue)
            bitmapSource = BitmapFontForElement(skillElement.Value);
        else if (useUtilityBitmapFont)
            bitmapSource = utilityBitmapFont;

        if (bitmapSource != null &&
            TryCreateBitmapFeedback(go.transform, label, bitmapSource, out List<SpriteRenderer> glyphRenderers))
        {
            StartCoroutine(FloatAndFade(go, glyphRenderers));
            return;
        }

        TextMesh text = go.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = textFeedbackCharacterSize;
        text.fontSize = textFeedbackFontSize;
        text.color = color;
        if (feedbackFont != null)
            text.font = feedbackFont;

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sortingOrder = feedbackSortingOrder;

        StartCoroutine(FloatAndFade(go, text, color));
    }

    private bool ShouldUseEnemyDamageSidePosition(BattleSpriteAnimator target)
    {
        return moveEnemyDamageFeedbackRight && target != null && target == enemyAnimator;
    }

    private bool ShouldUsePlayerLeftPosition(BattleSpriteAnimator target)
    {
        return movePlayerFeedbackLeft && target != null && target == playerAnimator;
    }

    private Vector3 FeedbackPositionFor(
        BattleSpriteAnimator target,
        bool useEnemyDamageSidePosition,
        bool usePlayerLeftPosition)
    {
        if (useEnemyDamageSidePosition)
            return target.SideFeedbackWorldPosition(1f, enemyDamageFeedbackRightPadding, enemyDamageFeedbackVerticalOffset);
        if (usePlayerLeftPosition)
            return target.SideFeedbackWorldPosition(-1f, playerFeedbackLeftPadding, playerFeedbackVerticalOffset);
        return target.FeedbackWorldPosition;
    }

    private bool TryCreateBitmapFeedback(
        Transform root,
        string label,
        NicoBitmapFontReference source,
        out List<SpriteRenderer> glyphRenderers)
    {
        glyphRenderers = null;
        if (!useElementBitmapFeedbackFonts || source == null)
            return false;

        if (!TryGetBitmapFont(source, out NicoBitmapFont font))
            return false;

        glyphRenderers = font.CreateRenderers(root, label, feedbackSortingOrder);
        if (glyphRenderers.Count > 0)
            root.localScale = Vector3.one * bitmapFeedbackScale;
        return glyphRenderers.Count > 0;
    }

    private bool TryGetBitmapFont(NicoBitmapFontReference source, out NicoBitmapFont font)
    {
        if (source.TryGetAssignedFont(out font))
            return true;

#if UNITY_EDITOR
        if (autoLoadBitmapFeedbackFontsInEditor)
            return source.TryGetEditorAutoFont(bitmapFeedbackFontAssetRoot, out font);
#endif

        font = null;
        return false;
    }

    private NicoBitmapFontReference BitmapFontForElement(ElementType element)
    {
        switch (element)
        {
            case ElementType.Water:
                return waterBitmapFont;
            case ElementType.Fire:
                return fireBitmapFont;
            case ElementType.Grass:
                return grassBitmapFont;
            case ElementType.Ice:
                return iceBitmapFont;
            case ElementType.Electric:
                return electricBitmapFont;
            case ElementType.Ground:
                return groundBitmapFont;
            case ElementType.Normal:
            default:
                return normalBitmapFont;
        }
    }

    private IEnumerator PlayDefenseBlocksAttackCounter(
        CounterEvent evt,
        BattleSpriteAnimator defender,
        BattleSpriteAnimator attacker)
    {
        if (defender == null || attacker == null)
            yield break;

        attacker.TryGetClipTiming(BattleAnimationState.Attack, out float attackMarkerDelay, out float attackDuration);
        float attackRemainingFromMarker = Mathf.Max(0f, attackDuration - attackMarkerDelay);
        float totalShieldSequenceDuration = Mathf.Max(attackDuration, attackMarkerDelay + attackRemainingFromMarker);
        if (totalShieldSequenceDuration > 0f)
            hitReactionSuppressUntil[evt.CounterId] = Time.time + totalShieldSequenceDuration;

        if (attackMarkerDelay > 0f)
            actionMarkerFeedbackTimes[evt.CounteredId] = Time.time + attackMarkerDelay;

        bool heldAttack = attacker.PlayStateToActionMarkerAndHold(
            BattleAnimationState.Attack,
            defender.ContactWorldPosition,
            true,
            defender);
        if (!heldAttack)
            attacker.PlayAttackToward(defender.ContactWorldPosition, defender);

        if (attackMarkerDelay > 0f)
            yield return new WaitForSeconds(attackMarkerDelay);

        if (!defender.PlayActionMarkerWindowLoop(BattleAnimationState.Defense, attackRemainingFromMarker))
            defender.PlayDefense();
        SpawnUtilityFeedback(defender, "COUNTER", new Color(1f, 0.92f, 0.45f));

        if (attackRemainingFromMarker > 0f)
            yield return new WaitForSeconds(attackRemainingFromMarker);

        if (heldAttack)
            attacker.ContinueHeldProfileClip();
    }

    private IEnumerator PlayProfileCounterSequence(
        CounterEvent evt,
        BattleSpriteAnimator counter,
        BattleSpriteAnimator countered)
    {
        if (counter == null || countered == null)
            yield break;

        BattleAnimationState counteredState = StateForInstruction(evt.CounteredInstructionType);
        countered.TryGetActionMarkerDelay(counteredState, out float counteredMarkerDelay);
        PlayCounterInstruction(countered, evt.CounteredInstructionType, counter);

        if (counteredMarkerDelay > 0f)
            yield return new WaitForSeconds(counteredMarkerDelay);

        PlayCounterInstruction(counter, evt.CounterInstructionType, countered);
        SpawnUtilityFeedback(counter, "COUNTER", new Color(1f, 0.92f, 0.45f));
    }

    private void PlayCounterInstruction(
        BattleSpriteAnimator actor,
        InstructionType instruction,
        BattleSpriteAnimator target)
    {
        if (actor == null)
            return;

        switch (instruction)
        {
            case InstructionType.Attack:
                Vector3 targetPosition = target != null ? target.ContactWorldPosition : actor.ContactWorldPosition;
                actor.PlayAttackToward(targetPosition, target);
                break;
            case InstructionType.Defense:
                actor.PlayDefense();
                break;
            case InstructionType.Status:
                actor.PlayStatusAction(new Color(0.64f, 0.82f, 1f));
                break;
        }
    }

    private static BattleAnimationState StateForInstruction(InstructionType instruction)
    {
        switch (instruction)
        {
            case InstructionType.Attack:
                return BattleAnimationState.Attack;
            case InstructionType.Defense:
                return BattleAnimationState.Defense;
            case InstructionType.Status:
            default:
                return BattleAnimationState.Status;
        }
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

    private bool ShouldSuppressHitReaction(string combatantId)
    {
        if (string.IsNullOrEmpty(combatantId))
            return false;
        if (!hitReactionSuppressUntil.TryGetValue(combatantId, out float until))
            return false;
        if (Time.time <= until)
            return true;

        hitReactionSuppressUntil.Remove(combatantId);
        return false;
    }

    private static string DamageLabel(DamageEvent evt)
    {
        if (evt.ElementMultiplier > 1.01f)
            return $"WEAK -{evt.Amount}";
        if (evt.ElementMultiplier < 0.99f)
            return $"RESIST -{evt.Amount}";
        return $"-{evt.Amount}";
    }

    private static Color DamageColor(float elementMultiplier)
    {
        if (elementMultiplier > 1.01f)
            return new Color(1f, 0.82f, 0.24f);
        if (elementMultiplier < 0.99f)
            return new Color(0.42f, 0.78f, 1f);
        return new Color(1f, 0.36f, 0.32f);
    }

    private Vector3 NextFeedbackOffset(
        BattleSpriteAnimator target,
        bool useRightSideDamageOffsets = false,
        bool useLeftSidePlayerOffsets = false)
    {
        if (!feedbackTimes.TryGetValue(target, out float lastTime) || Time.time - lastTime > 0.3f)
            feedbackSlots[target] = 0;

        feedbackTimes[target] = Time.time;
        feedbackSlots.TryGetValue(target, out int slot);
        Vector3[] offsets = useRightSideDamageOffsets
            ? RightSideDamageFeedbackOffsets
            : useLeftSidePlayerOffsets
                ? LeftSidePlayerFeedbackOffsets
                : FeedbackOffsets;
        feedbackSlots[target] = (slot + 1) % offsets.Length;
        return offsets[slot % offsets.Length];
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

    private IEnumerator FloatAndFade(GameObject go, List<SpriteRenderer> renderers)
    {
        Vector3 start = go.transform.position;
        Vector3 end = start + Vector3.up * feedbackRise;
        float elapsed = 0f;
        float startAlpha = renderers.Count > 0 && renderers[0] != null ? renderers[0].color.a : 1f;

        while (elapsed < feedbackLifetime && go != null)
        {
            elapsed += Time.deltaTime;
            float p = feedbackLifetime <= 0f ? 1f : Mathf.Clamp01(elapsed / feedbackLifetime);
            go.transform.position = Vector3.Lerp(start, end, EaseOutCubic(p));

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] == null)
                    continue;

                Color color = renderers[i].color;
                color.a = startAlpha * (1f - p);
                renderers[i].color = color;
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
