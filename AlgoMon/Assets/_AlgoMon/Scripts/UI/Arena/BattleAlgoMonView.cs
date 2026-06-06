/*
Script Audit:
- Purpose: Defines one reusable battle AlgoMon view GameObject that owns sprite renderer bindings and animation profile selection.
- Attached GameObject: BattleAlgoMonView prefab, PlayerSpriteAnchor, or EnemySpriteAnchor in TheArena.
- Main responsibilities: Store combatant identity, bind Body/GroundDisc renderers to BattleSpriteAnimator, resolve profile data, and apply runtime combatant swaps.
- Important variables: combatantId, codeName, formName, animationProfileOverride, body, primaryRenderer, bodyRenderers, shadowRenderer, animator.
- Inputs: BattleAnimationProfile data, AlgoMonData, runtime combatant registration, and editor sprite folders.
- Outputs or effects: Configures BattleSpriteAnimator so battle animation plays on a concrete GameObject instead of a loose frame player.
- Testing notes: Put this component on PlayerSpriteAnchor/EnemySpriteAnchor, assign Body and GroundDisc, then confirm entry/idle/attack/hit/faint still play.
*/
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-facing view model for one battle combatant. It keeps the GameObject,
/// renderers, and profile data together, while BattleSpriteAnimator remains the
/// lower-level playback component.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleSpriteAnimator))]
public class BattleAlgoMonView : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string combatantId = "AlgoMon";
    [SerializeField] private AlgoMonData data;
    [SerializeField] private string codeName;
    [SerializeField] private string formName = "Base";

    [Header("Renderer Bindings")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer primaryRenderer;
    [SerializeField] private SpriteRenderer[] bodyRenderers;
    [SerializeField] private SpriteRenderer shadowRenderer;
    [SerializeField] private BattleSpriteAnimator animator;

    [Header("Animation")]
    [SerializeField] private BattleAnimationProfile animationProfileOverride;
    [SerializeField] private bool applyProfileOnAwake = true;
    [SerializeField] private bool autoLoadEditorProfile = true;
    [SerializeField] private bool usePortraitAsFallbackSprite = true;

    public string CombatantId => combatantId;

    public string CodeName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(codeName))
                return codeName.Trim();
            return data != null ? data.codeName : string.Empty;
        }
    }

    public string FormName => string.IsNullOrWhiteSpace(formName) ? "Base" : formName.Trim();

    public BattleSpriteAnimator Animator
    {
        get
        {
            BindAnimator();
            return animator;
        }
    }

    private void Awake()
    {
        BindAnimator();
        if (applyProfileOnAwake)
            ApplyResolvedProfile(ResolveInspectorProfile());
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(formName))
            formName = "Base";

        CacheMissingBindings();
    }

    public void ApplyCombatant(
        string nextCombatantId,
        BattleAnimationProfile resolvedProfile,
        string nextCodeName = null,
        string nextFormName = null,
        bool useInspectorFallback = true)
    {
        if (!string.IsNullOrWhiteSpace(nextCombatantId))
            combatantId = nextCombatantId.Trim();
        if (!string.IsNullOrWhiteSpace(nextCodeName))
            codeName = nextCodeName.Trim();
        if (!string.IsNullOrWhiteSpace(nextFormName))
            formName = nextFormName.Trim();

        BindAnimator();
        ApplyResolvedProfile(resolvedProfile != null || !useInspectorFallback ? resolvedProfile : ResolveInspectorProfile());
    }

    public void SetData(AlgoMonData nextData, string nextFormName = null)
    {
        data = nextData;
        if (data != null && !string.IsNullOrWhiteSpace(data.codeName))
            codeName = data.codeName.Trim();
        if (!string.IsNullOrWhiteSpace(nextFormName))
            formName = nextFormName.Trim();

        BindAnimator();
        ApplyResolvedProfile(ResolveInspectorProfile());
    }

    private void BindAnimator()
    {
        CacheMissingBindings();
        if (animator == null)
            return;

        animator.ConfigureSpriteBindings(body, primaryRenderer, bodyRenderers, shadowRenderer);
    }

    private void CacheMissingBindings()
    {
        if (animator == null)
            animator = GetComponent<BattleSpriteAnimator>();

        if (body == null)
            body = FindChildTransform("Body", "Sprite", "SpritePreview");
        if (shadowRenderer == null)
            shadowRenderer = FindChildRenderer("GroundDisc", "Shadow");
        if (primaryRenderer == null)
            primaryRenderer = ResolvePrimaryRenderer();
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = ResolveBodyRenderers();
    }

    private BattleAnimationProfile ResolveInspectorProfile()
    {
        if (animationProfileOverride != null)
            return animationProfileOverride;
        if (data != null && data.battleAnimationProfile != null)
            return data.battleAnimationProfile;
        if (!autoLoadEditorProfile)
            return null;

        string resolvedCodeName = CodeName;
        if (string.IsNullOrWhiteSpace(resolvedCodeName))
            resolvedCodeName = combatantId;

        return BattleAnimationProfileLoader.TryLoadEditorProfile(resolvedCodeName, FormName);
    }

    private void ApplyResolvedProfile(BattleAnimationProfile profile)
    {
        if (primaryRenderer != null && profile == null && usePortraitAsFallbackSprite && data != null && data.portrait != null)
        {
            primaryRenderer.sprite = data.portrait;
            if (animator != null)
                animator.ConfigureSpriteBindings(body, primaryRenderer, bodyRenderers, shadowRenderer);
        }

        if (animator != null)
            animator.SetAnimationProfile(profile);
    }

    private Transform FindChildTransform(params string[] nameFragments)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
                continue;

            for (int n = 0; n < nameFragments.Length; n++)
            {
                if (child.name.IndexOf(nameFragments[n], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            }
        }

        return transform.childCount > 0 ? transform.GetChild(0) : null;
    }

    private SpriteRenderer FindChildRenderer(params string[] nameFragments)
    {
        Transform child = FindChildTransform(nameFragments);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private SpriteRenderer ResolvePrimaryRenderer()
    {
        if (body != null)
        {
            SpriteRenderer bodyRenderer = body.GetComponent<SpriteRenderer>();
            if (bodyRenderer != null && bodyRenderer != shadowRenderer)
                return bodyRenderer;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i] != shadowRenderer)
                return renderers[i];
        }

        return null;
    }

    private SpriteRenderer[] ResolveBodyRenderers()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        List<SpriteRenderer> filtered = new List<SpriteRenderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate != null && candidate != shadowRenderer)
                filtered.Add(candidate);
        }

        return filtered.ToArray();
    }
}
