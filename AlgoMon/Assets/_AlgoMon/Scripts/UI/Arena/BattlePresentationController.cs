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
    [System.Serializable]
    private class BattleActionEffectBinding
    {
        public string codeName = "Sortex";
        public string formName = "Base";
        public InstructionType instructionType = InstructionType.Attack;
        public string resourcePath;
        [Min(1f)] public float framesPerSecond = 28f;
        [Min(0.01f)] public float scale = 2f;
        // Extra vertical stretch on top of `scale` (1 = square). >1 makes the
        // effect taller than wide for a punchier hit.
        [Min(0.01f)] public float verticalScale = 1f;
        public Vector3 offset;
        public Color color = Color.white;
        public int sortingBoost = 16;
        [Min(0f)] public float startDelay = 0f;

        [System.NonSerialized] public Sprite[] cachedFrames;
    }

    [Header("Combatants")]
    // Sprint 2 uses the fixed TheArena matchup. Party switching should replace
    // these static ids with runtime combatant registration.
    [SerializeField] private string playerId = "Sortex";
    [SerializeField] private string enemyId = "Cachelon";
    [SerializeField] private BattleAlgoMonView playerView;
    [SerializeField] private BattleAlgoMonView enemyView;
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
    // The enemy status card sits in the top-right corner (Screen Space - Overlay,
    // so it always draws over world-space feedback). Erupting the enemy's damage
    // numbers straight up — or outward to the right — pushes them under that card.
    // Instead nudge them inward (toward arena centre) and slightly up: up-left of
    // the sprite stays clear of the card and of melee contact at the sprite's
    // lower-left.
    [SerializeField] private bool offsetEnemyDamageInward = true;
    [SerializeField, Min(0f)] private float enemyDamageFeedbackInwardPadding = 0.45f;
    [SerializeField] private float enemyDamageFeedbackYOffset = 0.35f;
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
    [Tooltip("How long the winner holds its status flourish during the counter cut-in before the clash.")]
    [SerializeField, Min(0f)] private float counterCutInSeconds = 0.55f;
    [Tooltip("Minimum time the defender's block (and the attacker's frozen contact frame) hold when a Defense counters an Attack — fast attacks otherwise flash the defense frames by too quickly to read.")]
    [SerializeField, Min(0f)] private float minCounterClashHold = 0.6f;
    [Tooltip("Sprite-frame resource spawned at the contact point the instant a Defense blocks an Attack, so the deflection reads instead of the suppressed hit VFX being invisible. Empty = no burst.")]
    [SerializeField] private string counterClashBurstResource = "Effects/SortexGuardStatusEffect16";
    [SerializeField, Min(0.01f)] private float counterClashBurstScale = 3.4f;
    [SerializeField, Min(1f)] private float counterClashBurstFps = 30f;
    [SerializeField] private Color counterClashBurstColor = new Color(0.55f, 0.85f, 1f, 1f);

    [Header("Switch Reveal")]
    [SerializeField, Min(0f)] private float switchRevealDuration = 0.48f;
    [SerializeField] private Color switchRevealFeedbackColor = new Color(0.62f, 0.95f, 1f);

    [Header("Battle Action Effects")]
    [SerializeField] private BattleActionEffectBinding[] actionEffectBindings;

    // Floating damage / status text must always sit above the combatant body
    // sprites. Scene sorting on those sprites can exceed the flat
    // feedbackSortingOrder, which let the enemy sprite's corner cover the number;
    // deriving the order from the target body clears the sprite every time.
    private const int FeedbackSortingBoostAboveBody = 20;

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
    private string registeredPlayerCodeName = "Sortex";
    private string registeredEnemyCodeName = "Cachelon";
    private string registeredPlayerFormName = "Base";
    private string registeredEnemyFormName = "Base";

    private void Awake()
    {
        EnsureActionEffectDefaults();
        EnsureBitmapFontDefaults();
        AutoBind();
    }

    private void OnValidate()
    {
        // NOTE: deliberately NOT calling EnsureActionEffectDefaults() here. In edit
        // mode OnValidate would populate the list and a scene save would serialize
        // it, which then overrides (and goes stale vs) the code defaults. The list
        // is built at runtime in Awake / ActionEffectsForCombatant instead, so the
        // code stays the single source of truth for action-effect bindings.
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
        AutoBindCombatant("PlayerSpriteAnchor", ref playerView, ref playerAnimator);
        AutoBindCombatant("EnemySpriteAnchor", ref enemyView, ref enemyAnimator);
    }

    private static void AutoBindCombatant(
        string anchorName,
        ref BattleAlgoMonView view,
        ref BattleSpriteAnimator animator)
    {
        if (view == null && animator != null)
            view = animator.GetComponent<BattleAlgoMonView>();

        if (view == null || animator == null)
        {
            GameObject go = GameObject.Find(anchorName);
            if (go != null)
            {
                if (view == null)
                    view = go.GetComponent<BattleAlgoMonView>();
                if (animator == null)
                    animator = go.GetComponent<BattleSpriteAnimator>();
            }
        }

        if (view != null)
            animator = view.Animator;
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

    private void EnsureActionEffectDefaults()
    {
        if (actionEffectBindings != null && actionEffectBindings.Length > 0)
            return;

        actionEffectBindings = new[]
        {
            CreateActionEffect(
                "Sortex",
                "Base",
                InstructionType.Attack,
                "Effects/SortexBaseClawLargeBlue",
                34f,
                3.35f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Sortex",
                "Base",
                InstructionType.Attack,
                "Effects/SortexBaseElectricBurstLargeBlue",
                38f,
                3.6f,
                Vector3.zero,
                Color.white,
                20),
            CreateActionEffect(
                "Sortex",
                "Evolved",
                InstructionType.Attack,
                "Effects/SortexEvolvedEffect31",
                30f,
                0.95f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Overflux",
                "Base",
                InstructionType.Attack,
                "Effects/OverfluxBaseCombatEffect3",
                24f,
                2.6f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Overflux",
                "Evolved",
                InstructionType.Attack,
                "Effects/OverfluxEvolvedExplosionLargeRed",
                30f,
                2.4f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Overflux",
                "Base",
                InstructionType.Attack,
                "Effects/OverfluxSplatterLargeRed",
                28f,
                3.0f,
                Vector3.zero,
                Color.white,
                21,
                0f),
            CreateActionEffect(
                "Overflux",
                "Evolved",
                InstructionType.Attack,
                "Effects/OverfluxSplatterLargeRed",
                28f,
                3.7f,
                Vector3.zero,
                Color.white,
                21,
                0.28f),
            CreateActionEffect(
                "Overflux",
                "Base",
                InstructionType.Status,
                "Effects/OverfluxStatusEffect25",
                24f,
                1.9f,
                Vector3.zero,
                Color.white,
                22,
                0f,
                1.3f),
            CreateActionEffect(
                "Overflux",
                "Evolved",
                InstructionType.Status,
                "Effects/OverfluxStatusEffect25",
                24f,
                2.2f,
                Vector3.zero,
                Color.white,
                22,
                0f,
                1.3f),
            CreateActionEffect(
                "Nullbyte",
                "Base",
                InstructionType.Attack,
                "Effects/NullbyteBaseEffect26",
                32f,
                2.55f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Nullbyte",
                "Evolved",
                InstructionType.Attack,
                "Effects/NullbyteEvolvedCombatEffect8",
                20f,
                3.6f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Cachelon",
                "Base",
                InstructionType.Attack,
                "Effects/CachelonBaseEffect29",
                18f,
                3.1f,
                Vector3.zero,
                Color.white,
                22,
                0f,
                1.5f),
            CreateActionEffect(
                "Cachelon",
                "Evolved",
                InstructionType.Attack,
                "Effects/CachelonBaseEffect29",
                18f,
                3.45f,
                Vector3.zero,
                Color.white,
                22,
                0f,
                1.5f),
            CreateActionEffect(
                "Recursix",
                "Base",
                InstructionType.Attack,
                "Effects/RecursixBaseFireBurstLargeGreen",
                32f,
                3.3f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Recursix",
                "Evolved",
                InstructionType.Attack,
                "Effects/RecursixEvolvedMagicSwirlLargeGreen",
                40f,
                2.4f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Heapion",
                "Base",
                InstructionType.Attack,
                "Effects/HeapionBaseCombatEffect1",
                18f,
                2.6f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Sortex",
                "Base",
                InstructionType.Defense,
                "Effects/SortexGuardStatusEffect16",
                30f,
                1.35f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Sortex",
                "Base",
                InstructionType.Status,
                "Effects/SortexGuardStatusEffect16",
                30f,
                1.35f,
                Vector3.zero,
                new Color(0.72f, 0.88f, 1f, 1f),
                22),
            CreateActionEffect(
                "Sortex",
                "Evolved",
                InstructionType.Defense,
                "Effects/SortexGuardStatusEffect16",
                30f,
                1.55f,
                Vector3.zero,
                Color.white,
                22),
            CreateActionEffect(
                "Sortex",
                "Evolved",
                InstructionType.Status,
                "Effects/SortexGuardStatusEffect16",
                30f,
                1.55f,
                Vector3.zero,
                new Color(0.72f, 0.88f, 1f, 1f),
                22),
        };
    }

    private static BattleActionEffectBinding CreateActionEffect(
        string codeName,
        string formName,
        InstructionType instructionType,
        string resourcePath,
        float framesPerSecond,
        float scale,
        Vector3 offset,
        Color color,
        int sortingBoost,
        float startDelay = 0f,
        float verticalScale = 1f)
    {
        return new BattleActionEffectBinding
        {
            codeName = codeName,
            formName = formName,
            instructionType = instructionType,
            resourcePath = resourcePath,
            framesPerSecond = framesPerSecond,
            scale = scale,
            verticalScale = verticalScale,
            offset = offset,
            color = color,
            sortingBoost = sortingBoost,
            startDelay = startDelay,
        };
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
        registeredPlayerCodeName = !string.IsNullOrWhiteSpace(playerCodeName) ? playerCodeName.Trim() : playerId;
        registeredEnemyCodeName = !string.IsNullOrWhiteSpace(enemyCodeName) ? enemyCodeName.Trim() : enemyId;
        registeredPlayerFormName = !string.IsNullOrWhiteSpace(playerFormName) ? playerFormName.Trim() : defaultAnimationForm;
        registeredEnemyFormName = !string.IsNullOrWhiteSpace(enemyFormName) ? enemyFormName.Trim() : defaultAnimationForm;

        BattleAnimationProfile resolvedPlayerProfile =
            ResolveProfile(playerAnimationProfileOverride, playerProfile, playerCodeName, playerId, playerFormName);
        BattleAnimationProfile resolvedEnemyProfile =
            ResolveProfile(enemyAnimationProfileOverride, enemyProfile, enemyCodeName, enemyId, enemyFormName);

        ApplyCombatantProfile(
            ref playerView,
            ref playerAnimator,
            playerId,
            resolvedPlayerProfile,
            playerCodeName,
            playerFormName,
            autoLoadAnimationProfilesInEditor);
        ApplyCombatantProfile(
            ref enemyView,
            ref enemyAnimator,
            enemyId,
            resolvedEnemyProfile,
            enemyCodeName,
            enemyFormName,
            autoLoadAnimationProfilesInEditor);
    }

    /// <summary>
    /// Re-applies the profile for ONE side only. Used on a switch / send-next so
    /// the side that actually changed re-binds (and replays its entry), while the
    /// other side is left untouched — re-registering both made the bystander
    /// replay its entry animation every time the opponent switched.
    /// </summary>
    public void RegisterCombatantSide(
        bool playerSide,
        string combatantId,
        BattleAnimationProfile profile,
        string codeName,
        string formName)
    {
        if (playerSide)
        {
            if (!string.IsNullOrWhiteSpace(combatantId))
                playerId = combatantId;
            registeredPlayerCodeName = !string.IsNullOrWhiteSpace(codeName) ? codeName.Trim() : playerId;
            registeredPlayerFormName = !string.IsNullOrWhiteSpace(formName) ? formName.Trim() : defaultAnimationForm;
            BattleAnimationProfile resolved =
                ResolveProfile(playerAnimationProfileOverride, profile, codeName, playerId, formName);
            ApplyCombatantProfile(
                ref playerView, ref playerAnimator, playerId, resolved, codeName, formName, autoLoadAnimationProfilesInEditor);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(combatantId))
                enemyId = combatantId;
            registeredEnemyCodeName = !string.IsNullOrWhiteSpace(codeName) ? codeName.Trim() : enemyId;
            registeredEnemyFormName = !string.IsNullOrWhiteSpace(formName) ? formName.Trim() : defaultAnimationForm;
            BattleAnimationProfile resolved =
                ResolveProfile(enemyAnimationProfileOverride, profile, codeName, enemyId, formName);
            ApplyCombatantProfile(
                ref enemyView, ref enemyAnimator, enemyId, resolved, codeName, formName, autoLoadAnimationProfilesInEditor);
        }
    }

    private static void ApplyCombatantProfile(
        ref BattleAlgoMonView view,
        ref BattleSpriteAnimator animator,
        string combatantId,
        BattleAnimationProfile profile,
        string codeName,
        string formName,
        bool allowViewFallback)
    {
        if (view == null && animator != null)
            view = animator.GetComponent<BattleAlgoMonView>();

        if (view != null)
        {
            view.ApplyCombatant(combatantId, profile, codeName, formName, allowViewFallback);
            animator = view.Animator;
            return;
        }

        if (animator != null)
            animator.SetAnimationProfile(profile);
    }

    public IEnumerator PlaySwitchReveal(string combatantId)
    {
        BattleSpriteAnimator animator = AnimatorFor(combatantId);
        if (animator == null)
            yield break;

        SpawnUtilityFeedback(animator, "SWITCH", switchRevealFeedbackColor);
        yield return animator.PlaySwitchReveal(switchRevealDuration);
    }

    private IEnumerator PlayActionEffectAtMarker(
        string actorId,
        InstructionType instructionType,
        BattleSpriteAnimator actor,
        BattleSpriteAnimator target)
    {
        List<BattleActionEffectBinding> bindings = ActionEffectsForCombatant(actorId, instructionType);
        if (bindings == null || bindings.Count == 0 || actor == null)
            yield break;

        float delay = 0f;
        BattleAnimationState state = StateForInstruction(instructionType);
        if (actor.TryGetActionMarkerDelay(state, out float markerDelay))
            delay = markerDelay;
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        for (int i = 0; i < bindings.Count; i++)
            StartCoroutine(SpawnAndPlayActionEffect(bindings[i], instructionType, actor, target));
    }

    private IEnumerator SpawnAndPlayActionEffect(
        BattleActionEffectBinding binding,
        InstructionType instructionType,
        BattleSpriteAnimator actor,
        BattleSpriteAnimator target)
    {
        if (binding == null || actor == null)
            yield break;

        Sprite[] frames = ActionEffectFrames(binding);
        if (frames == null || frames.Length == 0)
            yield break;

        if (binding.startDelay > 0f)
            yield return new WaitForSeconds(binding.startDelay);

        BattleSpriteAnimator effectTarget =
            instructionType == InstructionType.Attack && target != null ? target : actor;
        Vector3 position = effectTarget.VisualCenterWorldPosition + binding.offset;

        GameObject go = new GameObject(
            $"{binding.codeName}{binding.formName}{instructionType}Effect");
        go.transform.position = position;
        go.transform.localScale = new Vector3(binding.scale, binding.scale * binding.verticalScale, binding.scale);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = binding.color;
        renderer.sortingOrder = effectTarget.MaxBodySortingOrder + binding.sortingBoost;
        if (instructionType == InstructionType.Attack && target != null)
            renderer.flipX = target.ContactWorldPosition.x < actor.ContactWorldPosition.x;

        yield return PlayOneShotSpriteEffect(go, renderer, frames, binding.framesPerSecond, binding.color);
    }

    private BattleActionEffectBinding clashBurstBinding;

    private IEnumerator SpawnCounterClashBurst(Vector3 position, int sortingOrder)
    {
        if (string.IsNullOrWhiteSpace(counterClashBurstResource))
            yield break;

        if (clashBurstBinding == null || clashBurstBinding.resourcePath != counterClashBurstResource)
        {
            clashBurstBinding = new BattleActionEffectBinding
            {
                codeName = "CounterClash",
                resourcePath = counterClashBurstResource,
                framesPerSecond = counterClashBurstFps,
                scale = counterClashBurstScale,
                color = counterClashBurstColor,
            };
        }

        Sprite[] frames = ActionEffectFrames(clashBurstBinding);
        if (frames == null || frames.Length == 0)
            yield break;

        GameObject go = new GameObject("CounterClashBurst");
        go.transform.position = position;
        go.transform.localScale = new Vector3(counterClashBurstScale, counterClashBurstScale, counterClashBurstScale);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = frames[0];
        renderer.color = counterClashBurstColor;
        renderer.sortingOrder = sortingOrder;

        yield return PlayOneShotSpriteEffect(go, renderer, frames, counterClashBurstFps, counterClashBurstColor);
    }

    private IEnumerator PlayOneShotSpriteEffect(
        GameObject go,
        SpriteRenderer renderer,
        Sprite[] frames,
        float framesPerSecond,
        Color baseColor)
    {
        if (go == null || renderer == null || frames == null || frames.Length == 0)
            yield break;

        float frameSeconds = 1f / Mathf.Max(1f, framesPerSecond);
        for (int i = 0; i < frames.Length; i++)
        {
            renderer.sprite = frames[i];
            Color color = baseColor;
            if (i >= frames.Length - 2)
                color.a *= Mathf.Lerp(1f, 0.35f, (i - (frames.Length - 2)) / 1f);
            renderer.color = color;

            float elapsed = 0f;
            while (elapsed < frameSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (go != null)
            Destroy(go);
    }

    private Sprite[] ActionEffectFrames(BattleActionEffectBinding binding)
    {
        if (binding == null)
            return null;
        if (binding.cachedFrames != null)
            return binding.cachedFrames;
        if (string.IsNullOrWhiteSpace(binding.resourcePath))
            return null;

        binding.cachedFrames = Resources.LoadAll<Sprite>(binding.resourcePath);
        if (binding.cachedFrames != null && binding.cachedFrames.Length > 1)
        {
            System.Array.Sort(
                binding.cachedFrames,
                (a, b) => string.Compare(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty, System.StringComparison.OrdinalIgnoreCase));
        }

        return binding.cachedFrames;
    }

    private List<BattleActionEffectBinding> ActionEffectsForCombatant(string id, InstructionType instructionType)
    {
        EnsureActionEffectDefaults();
        if (actionEffectBindings == null || actionEffectBindings.Length == 0)
            return null;

        List<BattleActionEffectBinding> matches = null;
        for (int i = 0; i < actionEffectBindings.Length; i++)
        {
            BattleActionEffectBinding binding = actionEffectBindings[i];
            if (binding == null || binding.instructionType != instructionType)
                continue;
            if (!MatchesCombatantProfile(id, binding.codeName, binding.formName))
                continue;
            if (matches == null)
                matches = new List<BattleActionEffectBinding>();
            matches.Add(binding);
        }

        return matches;
    }

    private bool MatchesCombatantProfile(string id, string codeName, string formName)
    {
        if (string.IsNullOrEmpty(id))
            return false;
        if (string.Equals(id, playerId, System.StringComparison.Ordinal) ||
            string.Equals(id, "Player", System.StringComparison.Ordinal))
        {
            return MatchesRegisteredProfile(registeredPlayerCodeName, registeredPlayerFormName, codeName, formName);
        }
        if (string.Equals(id, enemyId, System.StringComparison.Ordinal) ||
            string.Equals(id, "Enemy", System.StringComparison.Ordinal))
        {
            return MatchesRegisteredProfile(registeredEnemyCodeName, registeredEnemyFormName, codeName, formName);
        }

        return MatchesRegisteredProfile(id, defaultAnimationForm, codeName, formName);
    }

    private static bool MatchesRegisteredProfile(
        string registeredCodeName,
        string registeredFormName,
        string expectedCodeName,
        string expectedFormName)
    {
        if (string.IsNullOrWhiteSpace(registeredCodeName) ||
            string.IsNullOrWhiteSpace(expectedCodeName))
        {
            return false;
        }

        return string.Equals(
                   registeredCodeName.Trim(),
                   expectedCodeName.Trim(),
                   System.StringComparison.OrdinalIgnoreCase) &&
               FormsMatch(registeredFormName, expectedFormName);
    }

    private static bool FormsMatch(string left, string right)
    {
        string leftForm = string.IsNullOrWhiteSpace(left) ? "Base" : left.Trim();
        string rightForm = string.IsNullOrWhiteSpace(right) ? "Base" : right.Trim();
        if (string.Equals(leftForm, rightForm, System.StringComparison.OrdinalIgnoreCase))
            return true;

        return IsEvolvedAlias(leftForm) && IsEvolvedAlias(rightForm);
    }

    private static bool IsEvolvedAlias(string formName)
    {
        return string.Equals(formName, "Evolved", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(formName, "Evolve", System.StringComparison.OrdinalIgnoreCase);
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
        StartCoroutine(PlayActionEffectAtMarker(evt.ActorId, evt.InstructionType, actor, target));
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

    public float ExpectedFaintRemaining(string unitId)
    {
        BattleSpriteAnimator target = AnimatorFor(unitId);
        return target != null ? target.FaintPlaybackDurationSeconds : 0f;
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

        int effectiveSortingOrder = feedbackSortingOrder;
        if (target != null)
            effectiveSortingOrder = Mathf.Max(
                effectiveSortingOrder,
                target.MaxBodySortingOrder + FeedbackSortingBoostAboveBody);

        if (bitmapSource != null &&
            TryCreateBitmapFeedback(go.transform, label, bitmapSource, effectiveSortingOrder, out List<SpriteRenderer> glyphRenderers))
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
            renderer.sortingOrder = effectiveSortingOrder;

        StartCoroutine(FloatAndFade(go, text, color));
    }

    /// <summary>
    /// Quick status flourish the counter winner plays during the counter cut-in,
    /// while the HUD shows its flash + letterbox banner. Yields for the hold so
    /// the manager can chain straight into the normal clash animation afterward.
    /// </summary>
    public IEnumerator PlayCounterCutInFlourish(string winnerId)
    {
        BattleSpriteAnimator winner = AnimatorFor(winnerId);
        if (winner == null)
            yield break;

        winner.PlayStatusAction(new Color(1f, 0.92f, 0.45f));
        if (counterCutInSeconds > 0f)
            yield return new WaitForSeconds(counterCutInSeconds);
    }

    /// <summary>
    /// Exposes a combatant's Status animation frames + fps so the HUD can replay
    /// the winner's status pose inside the counter banner.
    /// </summary>
    public bool TryGetStatusFrames(string id, out Sprite[] frames, out float fps)
    {
        frames = null;
        fps = 12f;

        BattleAnimationClipData clip = AnimatorFor(id)?.AnimationProfile?.ClipFor(BattleAnimationState.Status);
        if (clip == null || !clip.HasFrames)
            return false;

        frames = clip.frames;
        fps = clip.fps;
        return true;
    }

    private bool ShouldUseEnemyDamageSidePosition(BattleSpriteAnimator target)
    {
        return offsetEnemyDamageInward && target != null && target == enemyAnimator;
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
            return target.SideFeedbackWorldPosition(-1f, enemyDamageFeedbackInwardPadding, enemyDamageFeedbackYOffset);
        if (usePlayerLeftPosition)
            return target.SideFeedbackWorldPosition(-1f, playerFeedbackLeftPadding, playerFeedbackVerticalOffset);
        return target.FeedbackWorldPosition;
    }

    private bool TryCreateBitmapFeedback(
        Transform root,
        string label,
        NicoBitmapFontReference source,
        int sortingOrder,
        out List<SpriteRenderer> glyphRenderers)
    {
        glyphRenderers = null;
        if (!useElementBitmapFeedbackFonts || source == null)
            return false;

        if (!TryGetBitmapFont(source, out NicoBitmapFont font))
            return false;

        glyphRenderers = font.CreateRenderers(root, label, sortingOrder);
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
        // The block window is normally just the attack's follow-through (often only
        // a frame or two), so a fast attack flashes the defence by. Hold the clash
        // — defender's block + attacker's frozen contact frame — for at least
        // minCounterClashHold so it actually reads.
        float clashWindow = Mathf.Max(attackRemainingFromMarker, minCounterClashHold);
        float totalShieldSequenceDuration = Mathf.Max(attackDuration, attackMarkerDelay + clashWindow);
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

        if (!defender.PlayActionMarkerWindowLoop(BattleAnimationState.Defense, clashWindow))
            defender.PlayDefense();
        SpawnUtilityFeedback(defender, "COUNTER", new Color(1f, 0.92f, 0.45f));

        // The blocked attack's own hit VFX is suppressed, so the deflection used to
        // read as nothing but the defender's tiny block frame. Punch a cold guard
        // burst at the contact point so the clash actually lands visually.
        Vector3 clashPosition = Vector3.Lerp(defender.ContactWorldPosition, attacker.ContactWorldPosition, 0.35f);
        int clashSorting = Mathf.Max(defender.MaxBodySortingOrder, attacker.MaxBodySortingOrder) + 24;
        StartCoroutine(SpawnCounterClashBurst(clashPosition, clashSorting));

        if (clashWindow > 0f)
            yield return new WaitForSeconds(clashWindow);

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
