/*
Script Audit:
- Purpose: Owns cross-scene game state and controls the main run lifecycle.
- Attached GameObject: Auto-created persistent GameObject named GameManager through Bootstrap/EnsureInstance.
- Main responsibilities: Store payload and party, start/end runs, generate current run graph, track visited nodes, create encounters, grant rewards, and load scenes.
- Important variables: Instance, payload, party, currentRunGraph, currentNodeId, currentOpponent, currentOpponentParty, IsRunActive, selectedThreatTier, pendingRunOutcome, currentRunRewards.
- Inputs: MainTerminal start command, Grid node selections, BattleEndEvent, party data, and reward data.
- Outputs or effects: Changes scene, updates run state, creates opponents, saves rewards/captures, and publishes or responds to game events.
- AI/tutorial/template assistance: AI tools (Codex/Cursor/Claude/ChatGPT) assisted with parts of this script (implementation, refactoring, and/or documentation); the author reviewed, tested, and validated the logic. See AI_USE.md.
- Testing notes: Start a run, select combat nodes, win/lose battles, and confirm Grid/Arena/RunResult scene flow and rewards.
*/
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Singleton that owns all cross-scene game state.
/// Persists across scene loads via DontDestroyOnLoad.
///
/// Responsibilities:
///   - Payload: the full warehouse of all captured AlgoMons (no size cap,
///     sorted via QuickSort in the Lab)
///   - Party: the active squad taken into a run (max 4 slots)
///   - Track current run state (active node, current opponent)
///   - Drive scene transitions via EventBus
/// </summary>
// Defense note: GameManager coordinates the main state and flow for the game system.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private static readonly string[] BossSpeciesCodeNames =
    {
        "Cachelon",
        "Heapion",
        "Nullbyte",
        "Overflux",
        "Recursix",
        "Sortex"
    };
    private const string AlgoMonAssetSearchFolder = "Assets/_AlgoMon/ScriptableObjects/AlgoMons";
    private const string EncounterSpeciesCatalogResourcePath = "EncounterSpeciesCatalog";
    private const bool DefenseDemoInjectMaxNullbyte = true;
    private const string DefenseDemoNullbyteInstanceId = "DEFENSE_DEMO_NULLBYTE";
    private const string DefenseDemoNullbyteCodeName = "Nullbyte";
    private const int DefenseDemoMaxIv = 255;
    private const string ComputeBalancePrefsKey = "AlgoMon.Progress.Credits";

    [Header("Payload — Full Warehouse (all captured AlgoMons)")]
    public List<AlgoMonInstance> payload = new List<AlgoMonInstance>();

    [Header("Party — Active Squad (max 4 for current run)")]
    public List<AlgoMonInstance> party = new List<AlgoMonInstance>();
    public const int MaxPartySize = 4;

    // Evolution data and credits persist; run buffs and shop rolls reset per run.
    [Header("Player Progress")]
    // Legacy save field retained for older serialized data. User EXP is no longer awarded or displayed.
    public int playerExp;
    public int computeBalance;
    public List<string> evolutionDataSpeciesCodes = new List<string>();
    public List<RunBuffType> currentRunBuffs = new List<RunBuffType>();
    public List<RunBuffType> currentShopOfferTypes = new List<RunBuffType>();
    public string currentShopNodeId;
    public int currentShopRefreshCount;

    [Header("Run State")]
    public string currentNodeId;
    public int currentRunSeed;
    public GridGraph currentRunGraph;
    public List<string> visitedNodeIds = new List<string>();
    public AlgoMonInstance currentOpponent;
    public List<AlgoMonInstance> currentOpponentParty = new List<AlgoMonInstance>();
    public bool IsRunActive { get; private set; }

    [Header("Threat Tier")]
    // Serialized ints are Inspector/debug-facing; enum properties below are the clamped logic API.
    [Range(ThreatTierRules.MinTier, ThreatTierRules.MaxTier)]
    public int highestUnlockedThreatTier = ThreatTierRules.MaxTier;
    [Range(ThreatTierRules.MinTier, ThreatTierRules.MaxTier)]
    public int selectedThreatTier = ThreatTierRules.MinTier;
    public int currentThreatTier = ThreatTierRules.MinTier;
    // Applied by EncounterRewardCalculator when combat rewards are granted.
    public float currentRewardMultiplier = 1f;

    [Header("Boss Target")]
    public string selectedBossSpeciesCodeName = "Cachelon";
    public string currentBossSpeciesCodeName = "Cachelon";

    [Header("Run Result")]
    public RunOutcome pendingRunOutcome = RunOutcome.None;
    public int completedRunSeed;
    public string completedRunNodeId;
    public NodeType completedRunNodeType;
    public int completedRunVisitedCount;
    public int completedRunThreatTier = ThreatTierRules.MinTier;
    public float completedRunRewardMultiplier = 1f;
    public EncounterReward lastEncounterReward = new EncounterReward();
    public RunRewardSummary currentRunRewards = new RunRewardSummary();
    public RunRewardSummary completedRunRewards = new RunRewardSummary();

    // ----------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    // Defense note: Runs the bootstrap helper used by this script.
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    // Defense note: Ensures the instance dependency or state exists before use.
    public static GameManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject(nameof(GameManager));
        return managerObject.AddComponent<GameManager>();
    }

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureRewardContainers();
        LoadPersistentProgress();
        SubscribePersistentEvents();
    }

    // Defense note: Unity lifecycle hook that runs the on destroy step for this component.
    private void OnDestroy()
    {
        if (Instance == this)
            SavePersistentProgress();
        UnsubscribePersistentEvents();
    }

    // ----------------------------------------------------------------
    // Payload management (warehouse — no cap)

    // Defense note: Adds the to payload entry into the target collection or UI.
    public void AddToPayload(AlgoMonInstance mon)
    {
        if (mon == null)
            return;

        EnsureRewardContainers();
        mon.EnsurePersistentRuntimeState();
        if (IndexOfMon(payload, mon) < 0)
            payload.Add(mon);
    }

    // Defense note: Removes the from payload entry from the target collection or UI.
    public void RemoveFromPayload(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        RemoveMonFromList(payload, mon);
        RemoveMonFromList(party, mon);
    }

    // Defense note: Ensures the roster state dependency or state exists before use.
    public void EnsureRosterState()
    {
        EnsureRewardContainers();
    }

    // ----------------------------------------------------------------
    // Party management (active squad — max 4)

    // Defense note: Adds the to party entry into the target collection or UI.
    public bool AddToParty(AlgoMonInstance mon)
    {
        if (mon == null)
            return false;

        EnsureRewardContainers();
        mon.EnsurePersistentRuntimeState();

        AlgoMonInstance payloadMon = EnsurePayloadEntry(mon);
        if (payloadMon == null)
            return false;
        if (IsInParty(payloadMon))
            return true;
        if (party.Count >= MaxPartySize)
            return false;

        party.Add(payloadMon);
        return true;
    }

    // Defense note: Removes the from party entry from the target collection or UI.
    public void RemoveFromParty(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        RemoveMonFromList(party, mon);
    }

    // Defense note: Attempts to replace party member and reports success or failure.
    public bool TryReplacePartyMember(int index, AlgoMonInstance mon)
    {
        if (mon == null)
            return false;

        EnsureRewardContainers();
        if (party == null || index < 0 || index >= party.Count)
            return false;

        AlgoMonInstance payloadMon = EnsurePayloadEntry(mon);
        if (payloadMon == null)
            return false;

        int existingIndex = IndexOfMon(party, payloadMon);
        if (existingIndex >= 0 && existingIndex != index)
            return false;

        party[index] = payloadMon;
        return true;
    }

    // ----------------------------------------------------------------
    // Run lifecycle

    // Defense note: Begins the run flow and initializes its state.
    public void BeginRun()
    {
        int seed = (int)(DateTime.UtcNow.Ticks & int.MaxValue);
        BeginRun(seed);
    }

    // Defense note: Begins the run flow and initializes its state.
    public void BeginRun(int seed)
    {
        BeginRun(seed, null);
    }

    // Defense note: Begins the run flow and initializes its state.
    public void BeginRun(int seed, GridGenerationSettings gridSettings)
    {
        ClearRunResult();
        EnsureRewardContainers();
        currentRunRewards.Reset();
        if (currentRunBuffs != null)
            currentRunBuffs.Clear();
        ResetShopState();
        lastEncounterReward = new EncounterReward();

        ThreatTier runTier = SelectedThreatTier;
        selectedThreatTier = ThreatTierRules.ToInt(runTier);
        currentThreatTier = selectedThreatTier;
        currentRewardMultiplier = ThreatTierRules.RewardMultiplier(runTier, HighestUnlockedThreatTier);
        selectedBossSpeciesCodeName = SelectedBossSpeciesCodeName;
        currentBossSpeciesCodeName = selectedBossSpeciesCodeName;

        GridGenerationSettings runGridSettings = GridGenerationSettings.CloneForThreatTier(
            gridSettings,
            selectedThreatTier,
            seed);
        GridGraph graph = new GridGenerator(runGridSettings).Generate(seed);
        graph.threatTier = currentThreatTier;
        graph.rewardMultiplierPercent = ThreatTierRules.RewardMultiplierPercent(runTier, HighestUnlockedThreatTier);
        // Party level is sampled once so later growth does not turn this into full level matching.
        ThreatTierRules.ApplyDifficultyToGraph(graph, runTier, AveragePartyLevel());

        IsRunActive = true;
        currentRunSeed = seed;
        currentRunGraph = graph;
        currentNodeId = graph.startNodeId;
        visitedNodeIds.Clear();
        visitedNodeIds.Add(currentNodeId);
        currentOpponent = null;
        currentOpponentParty.Clear();
    }

    // Defense note: Ends the run flow and clears its runtime state.
    public void EndRun()
    {
        IsRunActive = false;
        currentRunSeed = 0;
        currentRunGraph = null;
        currentNodeId = string.Empty;
        visitedNodeIds.Clear();
        currentOpponent = null;
        currentOpponentParty.Clear();
        currentThreatTier = ThreatTierRules.MinTier;
        currentRewardMultiplier = 1f;
        currentBossSpeciesCodeName = SelectedBossSpeciesCodeName;
        if (currentRunBuffs != null)
            currentRunBuffs.Clear();
        ResetShopState();
        EnsureRewardContainers();
        currentRunRewards.Reset();
        lastEncounterReward = new EncounterReward();
    }

    // Defense note: Clears the run result state so it can be rebuilt safely.
    public void ClearRunResult()
    {
        pendingRunOutcome = RunOutcome.None;
        completedRunSeed = 0;
        completedRunNodeId = string.Empty;
        completedRunNodeType = NodeType.Start;
        completedRunVisitedCount = 0;
        completedRunThreatTier = ThreatTierRules.MinTier;
        completedRunRewardMultiplier = 1f;
        EnsureRewardContainers();
        completedRunRewards.Reset();
        lastEncounterReward = new EncounterReward();
    }

    public ThreatTier HighestUnlockedThreatTier
    {
        get { return ThreatTierRules.ClampTier(highestUnlockedThreatTier); }
    }

    public ThreatTier SelectedThreatTier
    {
        get { return ThreatTierRules.ClampSelectableTier(selectedThreatTier, highestUnlockedThreatTier); }
    }

    public int HighestUnlockedThreatTierNumber
    {
        get { return ThreatTierRules.ToInt(HighestUnlockedThreatTier); }
    }

    public int SelectedThreatTierNumber
    {
        get { return ThreatTierRules.ToInt(SelectedThreatTier); }
    }

    public string SelectedBossSpeciesCodeName
    {
        get { return NormalizeBossSpeciesCodeName(selectedBossSpeciesCodeName); }
    }

    public string CurrentBossSpeciesCodeName
    {
        get { return NormalizeBossSpeciesCodeName(currentBossSpeciesCodeName); }
    }

    // Defense note: Attempts to set selected threat tier and reports success or failure.
    public bool TrySetSelectedThreatTier(int tier)
    {
        if (!ThreatTierRules.CanEnterTier(tier, highestUnlockedThreatTier))
            return false;

        selectedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampTier(tier));
        return true;
    }

    // Defense note: Attempts to set selected boss species and reports success or failure.
    public bool TrySetSelectedBossSpecies(string speciesCodeName)
    {
        if (IsRunActive || !TryNormalizeBossSpeciesCodeName(speciesCodeName, out string normalized))
            return false;

        selectedBossSpeciesCodeName = normalized;
        currentBossSpeciesCodeName = normalized;
        return true;
    }

    // Defense note: Updates the highest unlocked threat tier state or visual value.
    public void SetHighestUnlockedThreatTier(int tier)
    {
        highestUnlockedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampTier(tier));
        selectedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampSelectableTier(selectedThreatTier, highestUnlockedThreatTier));
    }

    // Defense note: Attempts to select run node and reports success or failure.
    public bool TrySelectRunNode(string nodeId)
    {
        if (!IsNodeAvailable(nodeId))
            return false;

        if (!visitedNodeIds.Contains(nodeId))
            visitedNodeIds.Add(nodeId);

        currentNodeId = nodeId;
        return true;
    }

    // Defense note: Retrieves the available node ids value used by this system.
    public List<string> GetAvailableNodeIds()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return new List<string>();

        GridNode current = currentRunGraph.GetNode(currentNodeId);
        if (current == null || current.outgoingNodeIds == null)
            return new List<string>();

        var available = new List<string>(current.outgoingNodeIds);
        if (current.nodeType == NodeType.Reboot &&
            !string.IsNullOrEmpty(currentRunGraph.startNodeId) &&
            !available.Contains(currentRunGraph.startNodeId))
        {
            available.Add(currentRunGraph.startNodeId);
        }

        return available;
    }

    // Defense note: Returns whether this value is node available.
    public bool IsNodeAvailable(string nodeId)
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(nodeId))
            return false;
        if (currentRunGraph.GetNode(nodeId) == null)
            return false;

        List<string> available = GetAvailableNodeIds();
        return available.Contains(nodeId);
    }

    // Defense note: Returns whether this value is node visited.
    public bool IsNodeVisited(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && visitedNodeIds.Contains(nodeId);
    }

    // Defense note: Attempts to register capture and reports success or failure.
    public bool TryRegisterCapture(AlgoMonInstance mon, out AlgoMonInstance captured)
    {
        return TryRegisterCapture(mon, RewardDataQuality.Base, out captured);
    }

    // Defense note: Attempts to register capture and reports success or failure.
    public bool TryRegisterCapture(AlgoMonInstance mon, RewardDataQuality quality, out AlgoMonInstance captured)
    {
        captured = null;
        EnsureRewardContainers();
        if (!CanPersistCapture(mon))
            return false;

        captured = AlgoMonInstance.CreateRewardBase(mon.data, quality, RewardTalentSeed(null, mon.data));
        if (captured == null)
            return false;

        AddToPayload(captured);
        return true;
    }

    // Defense note: Registers the capture data so other systems can use it.
    public AlgoMonInstance RegisterCapture(AlgoMonInstance mon)
    {
        TryRegisterCapture(mon, out AlgoMonInstance captured);
        return captured;
    }

    // Defense note: Checks whether persist capture is currently allowed.
    private static bool CanPersistCapture(AlgoMonInstance mon)
    {
        return mon != null && CanPersistSpecies(mon.data, mon.usesTransientData);
    }

    // Defense note: Checks whether afford compute is currently allowed.
    public bool CanAffordCompute(int amount)
    {
        return amount <= 0 || computeBalance >= amount;
    }

    // Defense note: Attempts to spend compute and reports success or failure.
    public bool TrySpendCompute(int amount)
    {
        if (amount <= 0)
            return true;
        if (!CanAffordCompute(amount))
            return false;

        computeBalance -= amount;
        SavePersistentProgress();
        return true;
    }

    // Defense note: Returns whether run buff exists or is active.
    public bool HasRunBuff(RunBuffType buffType)
    {
        return currentRunBuffs != null && currentRunBuffs.Contains(buffType);
    }

    // Defense note: Checks whether purchase shop offer is currently allowed.
    public bool CanPurchaseShopOffer(RunShopOffer offer, out string reason)
    {
        reason = string.Empty;
        if (offer == null)
        {
            reason = "No offer selected.";
            return false;
        }

        if (!IsRunActive)
        {
            reason = "Shop buffs require an active run.";
            return false;
        }

        EnsureRewardContainers();
        if (HasRunBuff(offer.BuffType))
        {
            reason = "Already active this run.";
            return false;
        }

        if (currentShopOfferTypes != null &&
            currentShopOfferTypes.Count > 0 &&
            !currentShopOfferTypes.Contains(offer.BuffType))
        {
            reason = "Offer is not available in this shop roll.";
            return false;
        }

        if (!CanAffordCompute(offer.ComputeCost))
        {
            reason = $"Need {offer.ComputeCost - computeBalance} more credits.";
            return false;
        }

        return true;
    }

    // Defense note: Attempts to purchase shop offer and reports success or failure.
    public bool TryPurchaseShopOffer(RunShopOffer offer, out string message)
    {
        if (!CanPurchaseShopOffer(offer, out message))
            return false;

        if (!TrySpendCompute(offer.ComputeCost))
        {
            message = "Credit spend failed.";
            return false;
        }

        currentRunBuffs.Add(offer.BuffType);
        message = $"Purchased {offer.DisplayName}.";
        return true;
    }

    // Defense note: Ensures the shop offers for node dependency or state exists before use.
    public void EnsureShopOffersForNode(string nodeId)
    {
        EnsureRewardContainers();
        if (string.IsNullOrEmpty(nodeId))
            nodeId = "SHOP";

        if (currentShopOfferTypes.Count > 0 &&
            string.Equals(currentShopNodeId, nodeId, StringComparison.Ordinal))
        {
            return;
        }

        currentShopNodeId = nodeId;
        currentShopRefreshCount = 0;
        GenerateShopOffers();
    }

    // Defense note: Runs the current shop offers helper used by this script.
    public List<RunShopOffer> CurrentShopOffers()
    {
        EnsureRewardContainers();
        var offers = new List<RunShopOffer>(currentShopOfferTypes.Count);
        for (int i = 0; i < currentShopOfferTypes.Count; i++)
        {
            RunShopOffer offer = RunShopCatalog.Find(currentShopOfferTypes[i]);
            if (offer != null)
                offers.Add(offer);
        }

        return offers;
    }

    public int CurrentShopRefreshCost
    {
        get
        {
            int exponent = Mathf.Clamp(currentShopRefreshCount, 0, 10);
            return RunShopCatalog.BaseRefreshCost * (1 << exponent);
        }
    }

    // Defense note: Checks whether refresh shop offers is currently allowed.
    public bool CanRefreshShopOffers(out string reason)
    {
        reason = string.Empty;
        if (!IsRunActive)
        {
            reason = "Shop refresh requires an active run.";
            return false;
        }

        int cost = CurrentShopRefreshCost;
        if (!CanAffordCompute(cost))
        {
            reason = $"Need {cost - computeBalance} more credits.";
            return false;
        }

        return true;
    }

    // Defense note: Attempts to refresh shop offers and reports success or failure.
    public bool TryRefreshShopOffers(out string message)
    {
        if (!CanRefreshShopOffers(out message))
            return false;

        int cost = CurrentShopRefreshCost;
        if (!TrySpendCompute(cost))
        {
            message = "Credit spend failed.";
            return false;
        }

        currentShopRefreshCount++;
        GenerateShopOffers();
        message = $"Shop refreshed for {cost} credits. Next refresh costs {CurrentShopRefreshCost}.";
        return true;
    }

    public float PlayerRunOutgoingDamageMultiplier
    {
        get
        {
            EnsureRewardContainers();
            return RunShopCatalog.OutgoingDamageMultiplier(currentRunBuffs);
        }
    }

    public float PlayerRunIncomingDamageMultiplier
    {
        get
        {
            EnsureRewardContainers();
            return RunShopCatalog.IncomingDamageMultiplier(currentRunBuffs);
        }
    }

    public int PlayerRunSkillCostReduction
    {
        get
        {
            EnsureRewardContainers();
            return RunShopCatalog.SkillCostReduction(currentRunBuffs);
        }
    }

    public float PlayerRunClockSpeedMultiplier
    {
        get
        {
            EnsureRewardContainers();
            return RunShopCatalog.ClockSpeedMultiplier(currentRunBuffs);
        }
    }

    public float PlayerRunExpRewardMultiplier
    {
        get
        {
            EnsureRewardContainers();
            return RunShopCatalog.ExpRewardMultiplier(currentRunBuffs);
        }
    }

    // Defense note: Runs the current run buff summary helper used by this script.
    public string CurrentRunBuffSummary()
    {
        EnsureRewardContainers();
        return RunShopCatalog.BuildActiveSummary(currentRunBuffs);
    }

    // Defense note: Runs the evolution data count for helper used by this script.
    public int EvolutionDataCountFor(string speciesCodeName)
    {
        if (evolutionDataSpeciesCodes == null || string.IsNullOrWhiteSpace(speciesCodeName))
            return 0;

        int count = 0;
        for (int i = 0; i < evolutionDataSpeciesCodes.Count; i++)
        {
            if (string.Equals(evolutionDataSpeciesCodes[i], speciesCodeName, StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count;
    }

    // Defense note: Runs the evolvable payload count helper used by this script.
    public int EvolvablePayloadCount()
    {
        EnsureRewardContainers();
        int count = 0;
        for (int i = 0; i < payload.Count; i++)
        {
            AlgoMonInstance mon = payload[i];
            if (mon == null)
                continue;

            mon.EnsurePersistentRuntimeState();
            if (mon.CanEvolve)
                count++;
        }

        return count;
    }

    // Defense note: Runs the first fusion candidate index for helper used by this script.
    public int FirstFusionCandidateIndexFor(int targetIndex)
    {
        EnsureRewardContainers();
        for (int i = 0; i < payload.Count; i++)
        {
            if (i == targetIndex)
                continue;
            if (CanFusePayload(targetIndex, i, out _))
                return i;
        }

        return -1;
    }

    // Defense note: Checks whether fuse payload is currently allowed.
    public bool CanFusePayload(int targetIndex, int materialIndex, out string reason)
    {
        reason = string.Empty;
        EnsureRewardContainers();

        if (IsRunActive)
        {
            reason = "Gene Lab is locked during active runs.";
            return false;
        }

        if (!TryGetPayloadMon(targetIndex, out AlgoMonInstance target) ||
            !TryGetPayloadMon(materialIndex, out AlgoMonInstance material))
        {
            reason = "Select two valid payload records.";
            return false;
        }

        target.EnsurePersistentRuntimeState();
        material.EnsurePersistentRuntimeState();

        if (ReferenceEquals(target, material) ||
            string.Equals(target.instanceId, material.instanceId, StringComparison.Ordinal))
        {
            reason = "UNIT 1 and UNIT 2 must be different records.";
            return false;
        }

        if (!target.IsBaseForm || !material.IsBaseForm)
        {
            reason = "Only base-form bodies can be fused.";
            return false;
        }

        if (target.CanEvolve)
        {
            reason = "UNIT 1 is ready to evolve.";
            return false;
        }

        if (!SameSpecies(target, material))
        {
            reason = "Fusion requires the same species.";
            return false;
        }

        return true;
    }

    // Defense note: Attempts to fuse payload and reports success or failure.
    public bool TryFusePayload(int targetIndex, int materialIndex, out string message)
    {
        if (!CanFusePayload(targetIndex, materialIndex, out message))
            return false;

        AlgoMonInstance target = payload[targetIndex];
        AlgoMonInstance material = payload[materialIndex];
        int targetPartyIndex = IndexOfMon(party, target);
        int materialPartyIndex = IndexOfMon(party, material);
        string materialName = DisplayNameFor(material);
        target.FuseFrom(material);
        target.EnsureKnownSkillsFromLearnset();
        payload.RemoveAt(materialIndex);
        ReconcilePartyAfterFusion(target, material, targetPartyIndex, materialPartyIndex);

        message = $"{DisplayNameFor(target)} + {materialName} fused. UNIT 1 keeps the record. Fusion {target.FusionProgressText}. Level L{target.level:00}.";
        return true;
    }

    // Defense note: Runs the reconcile party after fusion helper used by this script.
    private void ReconcilePartyAfterFusion(
        AlgoMonInstance target,
        AlgoMonInstance material,
        int targetPartyIndex,
        int materialPartyIndex)
    {
        if (party == null || target == null || material == null)
            return;

        if (targetPartyIndex >= 0)
        {
            RemoveMonFromList(party, material);
            return;
        }

        if (materialPartyIndex >= 0)
        {
            if (materialPartyIndex < party.Count)
                party[materialPartyIndex] = target;
            else if (party.Count < MaxPartySize)
                party.Add(target);
        }
    }

    // Defense note: Checks whether evolve payload is currently allowed.
    public bool CanEvolvePayload(int targetIndex, out string reason)
    {
        reason = string.Empty;
        EnsureRewardContainers();

        if (IsRunActive)
        {
            reason = "Gene Lab is locked during active runs.";
            return false;
        }

        if (!TryGetPayloadMon(targetIndex, out AlgoMonInstance target))
        {
            reason = "Select a valid payload record.";
            return false;
        }

        target.EnsurePersistentRuntimeState();
        if (!target.IsBaseForm)
        {
            reason = "This record is already evolved.";
            return false;
        }

        if (!target.CanEvolve)
        {
            reason = $"Need {target.RemainingFusionCopies} more same-species base fusion(s).";
            return false;
        }

        return true;
    }

    // Defense note: Attempts to evolve payload and reports success or failure.
    public bool TryEvolvePayload(int targetIndex, out string message)
    {
        if (!CanEvolvePayload(targetIndex, out message))
            return false;

        AlgoMonInstance target = payload[targetIndex];
        if (!target.Evolve())
        {
            message = "Evolution failed.";
            return false;
        }

        message = $"{DisplayNameFor(target)} evolved to its evolved form.";
        return true;
    }

    // Defense note: Runs the grant current encounter reward helper used by this script.
    public EncounterReward GrantCurrentEncounterReward(AlgoMonInstance defeatedOpponent)
    {
        EnsureRewardContainers();

        GridNode completedNode = CurrentRunNode();
        ThreatTier tier = currentRunGraph != null
            ? ThreatTierRules.ClampTier(currentRunGraph.threatTier)
            : ThreatTierRules.ClampTier(currentThreatTier);

        EncounterReward reward = EncounterRewardCalculator.Build(
            completedNode,
            defeatedOpponent,
            tier,
            currentRewardMultiplier);

        ApplyRunRewardBuffs(reward);
        ApplyEncounterReward(reward, defeatedOpponent);
        currentRunRewards.Add(reward);
        lastEncounterReward = reward.Clone();
        return reward;
    }

    // Defense note: Applies the run reward buffs change to gameplay or UI state.
    private void ApplyRunRewardBuffs(EncounterReward reward)
    {
        if (reward == null)
            return;

        float expMultiplier = PlayerRunExpRewardMultiplier;
        if (Mathf.Approximately(expMultiplier, 1f))
            return;

        reward.algoMonExp = Mathf.Max(0, Mathf.RoundToInt(reward.algoMonExp * expMultiplier));
    }

    // Defense note: Applies the encounter reward change to gameplay or UI state.
    private void ApplyEncounterReward(EncounterReward reward, AlgoMonInstance defeatedOpponent)
    {
        if (reward == null)
            return;

        computeBalance += reward.compute;
        SavePersistentProgress();
        GrantPartyExp(reward.algoMonExp);

        if (reward.shouldGrantBaseData &&
            TryRegisterRewardBase(reward, defeatedOpponent, out _))
        {
            reward.baseDataGranted = true;
        }
    }

    // Defense note: Runs the grant party exp helper used by this script.
    private void GrantPartyExp(int amount)
    {
        if (amount <= 0 || party == null)
            return;

        for (int i = 0; i < party.Count; i++)
        {
            AlgoMonInstance mon = party[i];
            if (mon == null)
                continue;

            int beforeLevel = mon.level;
            mon.GainExp(amount);
            if (mon.level > beforeLevel)
            {
                // Backend auto-fills empty slots; prompt/replace UI can read learnset later.
                mon.EnsureKnownSkillsFromLearnset();
            }
        }
    }

    // Defense note: Runs the on node selected helper used by this script.
    private void OnNodeSelected(NodeSelectedEvent e)
    {
        if (!IsRunActive || e.Node == null)
            return;

        if (!IsEncounterNode(e.Type))
        {
            currentOpponent = null;
            currentOpponentParty.Clear();
            return;
        }

        if (BattleLinkTransition.IsActive)
            return;

        ThreatTier threatTier = currentRunGraph != null
            ? ThreatTierRules.ClampTier(currentRunGraph.threatTier)
            : ThreatTierRules.ClampTier(currentThreatTier);
        GridNode encounterNode = e.Node;
        string bossSpeciesCodeName = encounterNode.nodeType == NodeType.Boss ? CurrentBossSpeciesCodeName : null;
        string encounterLabel = BuildBattleTransitionEncounterLabel(encounterNode);
        string riskLabel = BuildBattleTransitionRiskLabel(encounterNode);

        // Silence the grid music first so the encounter-lock impact lands clean;
        // the arena's battle track fades in on scene load.
        AudioManager.Instance?.FadeOutMusic();
        AudioManager.Instance?.PlayUiSfx(UiSfx.Impact);
        BattleLinkTransition.Play(
            encounterLabel,
            riskLabel,
            () =>
            {
                currentOpponentParty.Clear();
                currentOpponentParty.AddRange(EncounterFactory.CreateParty(currentRunSeed, encounterNode, threatTier, bossSpeciesCodeName));
                currentOpponent = currentOpponentParty.Count > 0
                    ? currentOpponentParty[0]
                    : EncounterFactory.Create(currentRunSeed, encounterNode, threatTier, bossSpeciesCodeName);
            },
            () => GoTo(GameScene.TheArena));
    }

    // Defense note: Builds the battle transition encounter label data or UI structure.
    private static string BuildBattleTransitionEncounterLabel(GridNode node)
    {
        if (node == null)
            return "ENCOUNTER";

        return $"{BattleTransitionNodeTypeLabel(node.nodeType)} // {node.id.ToUpperInvariant()}";
    }

    // Defense note: Builds the battle transition risk label data or UI structure.
    private static string BuildBattleTransitionRiskLabel(GridNode node)
    {
        if (node == null)
            return "RISK UNKNOWN";

        int danger = Mathf.Clamp(node.dangerRating, 1, ThreatTierRules.MaxTier);
        int level = Mathf.Max(1, node.encounterLevel);
        return $"D{danger} // LV {level:00} // {BattleTransitionNodeTypeLabel(node.nodeType)}";
    }

    // Defense note: Runs the battle transition node type label helper used by this script.
    private static string BattleTransitionNodeTypeLabel(NodeType nodeType)
    {
        switch (nodeType)
        {
            case NodeType.Combat:
                return "WILD";
            case NodeType.Hacker:
                return "BREACH";
            case NodeType.Elite:
                return "ELITE";
            case NodeType.Boss:
                return "BOSS";
            default:
                return nodeType.ToString().ToUpperInvariant();
        }
    }

    // Defense note: Ensures the current run has early hacker dependency or state exists before use.
    public bool EnsureCurrentRunHasEarlyHacker()
    {
        if (!IsRunActive || currentRunGraph == null)
            return false;

        bool changed = GridGenerator.EnsureHackerNode(currentRunGraph, true);
        if (changed)
        {
            ThreatTier tier = ThreatTierRules.ClampTier(
                currentRunGraph.threatTier > 0 ? currentRunGraph.threatTier : currentThreatTier);
            ThreatTierRules.ApplyDifficultyToGraph(currentRunGraph, tier, AveragePartyLevel());
        }

        return changed;
    }

    // Defense note: Runs the on battle end helper used by this script.
    private void OnBattleEnd(BattleEndEvent e)
    {
        if (!IsRunActive)
            return;

        GridNode completedNode = CurrentRunNode();
        currentOpponent = null;
        currentOpponentParty.Clear();

        if (e.PlayerWon && completedNode != null && completedNode.nodeType != NodeType.Boss)
        {
            AudioManager.Instance?.PlayRewardSfx();
            GoTo(GameScene.TheGrid);
            return;
        }

        RecordRunResult(e.PlayerWon ? RunOutcome.Victory : RunOutcome.Defeat, completedNode);
        EndRun();
        GoTo(GameScene.RunResult);
    }

    // Defense note: Ends the active grid run from a node-screen flee action.
    public bool TryFleeCurrentRun()
    {
        if (!IsRunActive)
            return false;

        GridNode completedNode = CurrentRunNode();
        RecordRunResult(RunOutcome.Defeat, completedNode);
        EndRun();
        GoTo(GameScene.RunResult);
        return true;
    }

    // Defense note: Runs the current run node helper used by this script.
    private GridNode CurrentRunNode()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return null;

        return currentRunGraph.GetNode(currentNodeId);
    }

    // Defense note: Runs the record run result helper used by this script.
    private void RecordRunResult(RunOutcome outcome, GridNode completedNode)
    {
        pendingRunOutcome = outcome;
        completedRunSeed = currentRunSeed;
        completedRunNodeId = completedNode != null ? completedNode.id : currentNodeId;
        completedRunNodeType = completedNode != null ? completedNode.nodeType : NodeType.Start;
        completedRunVisitedCount = visitedNodeIds != null ? visitedNodeIds.Count : 0;
        completedRunThreatTier = currentThreatTier;
        completedRunRewardMultiplier = currentRewardMultiplier;
        EnsureRewardContainers();
        completedRunRewards = currentRunRewards.Clone();
    }

    // Defense note: Returns whether this value is encounter node.
    private static bool IsEncounterNode(NodeType type)
    {
        return ThreatTierRules.IsEncounterNode(type);
    }

    // Defense note: Runs the average party level helper used by this script.
    private int AveragePartyLevel()
    {
        if (party == null || party.Count == 0)
            return 0;

        int total = 0;
        int count = 0;
        for (int i = 0; i < party.Count; i++)
        {
            AlgoMonInstance mon = party[i];
            if (mon == null)
                continue;

            total += Mathf.Clamp(mon.level, 1, AlgoMonInstance.MAX_LEVEL);
            count++;
        }

        return count > 0 ? Mathf.RoundToInt(total / (float)count) : 0;
    }

    // Defense note: Generates the shop offers content from current settings.
    private void GenerateShopOffers()
    {
        EnsureRewardContainers();
        currentShopOfferTypes.Clear();

        RunShopOffer[] allOffers = RunShopCatalog.Offers;
        var candidates = new List<RunShopOffer>(allOffers.Length);
        var tradeoffCandidates = new List<RunShopOffer>(allOffers.Length);

        for (int i = 0; i < allOffers.Length; i++)
        {
            RunShopOffer offer = allOffers[i];
            if (offer == null || HasRunBuff(offer.BuffType))
                continue;

            candidates.Add(offer);
            if (offer.HasTradeoff)
                tradeoffCandidates.Add(offer);
        }

        System.Random random = new System.Random(ShopRollSeed());
        if (tradeoffCandidates.Count > 0)
            AddRandomOffer(tradeoffCandidates, candidates, random);

        while (currentShopOfferTypes.Count < RunShopCatalog.OfferSlots && candidates.Count > 0)
            AddRandomOffer(candidates, candidates, random);

        for (int i = 0; currentShopOfferTypes.Count < RunShopCatalog.OfferSlots && i < allOffers.Length; i++)
        {
            if (allOffers[i] != null && !currentShopOfferTypes.Contains(allOffers[i].BuffType))
                currentShopOfferTypes.Add(allOffers[i].BuffType);
        }
    }

    // Defense note: Adds the random offer entry into the target collection or UI.
    private void AddRandomOffer(
        List<RunShopOffer> source,
        List<RunShopOffer> sharedCandidatePool,
        System.Random random)
    {
        if (source == null || source.Count == 0 || random == null)
            return;

        int index = random.Next(source.Count);
        RunShopOffer offer = source[index];
        if (offer == null)
            return;

        currentShopOfferTypes.Add(offer.BuffType);
        RemoveOffer(source, offer.BuffType);
        if (!ReferenceEquals(source, sharedCandidatePool))
            RemoveOffer(sharedCandidatePool, offer.BuffType);
    }

    // Defense note: Removes the offer entry from the target collection or UI.
    private static void RemoveOffer(List<RunShopOffer> offers, RunBuffType type)
    {
        if (offers == null)
            return;

        for (int i = offers.Count - 1; i >= 0; i--)
        {
            if (offers[i] != null && offers[i].BuffType == type)
                offers.RemoveAt(i);
        }
    }

    // Defense note: Runs the shop roll seed helper used by this script.
    private int ShopRollSeed()
    {
        unchecked
        {
            int seed = currentRunSeed;
            seed = seed * 397 ^ StableHash(currentShopNodeId);
            seed = seed * 397 ^ currentShopRefreshCount;
            return seed & int.MaxValue;
        }
    }

    // Defense note: Runs the stable hash helper used by this script.
    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash;
        }
    }

    // Defense note: Runs the reset shop state helper used by this script.
    private void ResetShopState()
    {
        if (currentShopOfferTypes != null)
            currentShopOfferTypes.Clear();
        currentShopNodeId = string.Empty;
        currentShopRefreshCount = 0;
    }

    // Defense note: Ensures the reward containers dependency or state exists before use.
    private void EnsureRewardContainers()
    {
        if (payload == null)
            payload = new List<AlgoMonInstance>();
        if (party == null)
            party = new List<AlgoMonInstance>();
        if (evolutionDataSpeciesCodes == null)
            evolutionDataSpeciesCodes = new List<string>();
        if (currentRunBuffs == null)
            currentRunBuffs = new List<RunBuffType>();
        if (currentShopOfferTypes == null)
            currentShopOfferTypes = new List<RunBuffType>();
        if (lastEncounterReward == null)
            lastEncounterReward = new EncounterReward();
        if (currentRunRewards == null)
            currentRunRewards = new RunRewardSummary();
        if (completedRunRewards == null)
            completedRunRewards = new RunRewardSummary();

        EnsureMonState(payload);
        EnsureMonState(party);
        EnsurePartyPayloadLinks();
    }

    // Defense note: Loads permanent player progress kept outside the current run.
    private void LoadPersistentProgress()
    {
        int fallback = Mathf.Max(0, computeBalance);
        computeBalance = Mathf.Max(0, PlayerPrefs.GetInt(ComputeBalancePrefsKey, fallback));
    }

    // Defense note: Saves permanent credits after earning or spending them.
    private void SavePersistentProgress()
    {
        computeBalance = Mathf.Max(0, computeBalance);
        PlayerPrefs.SetInt(ComputeBalancePrefsKey, computeBalance);
        PlayerPrefs.Save();
    }

    // Defense note: Adds the temporary defense demo Nullbyte to payload after normal roster setup.
    public void EnsureDefenseDemoNullbyteInPayload()
    {
        EnsureRewardContainers();
        EnsureDefenseDemoNullbyte();
    }

    // Defense note: Returns whether a mon is the temporary defense demo Nullbyte.
    public static bool IsDefenseDemoNullbyteInstance(AlgoMonInstance mon)
    {
        return IsDefenseDemoNullbyte(mon);
    }

    // Defense note: Ensures the temporary defense demo Nullbyte exists in payload only.
    private void EnsureDefenseDemoNullbyte()
    {
        if (!DefenseDemoInjectMaxNullbyte || payload == null)
            return;

        AlgoMonInstance demoMon = FindDefenseDemoNullbyte(payload);
        bool createdDemoMon = false;
        if (demoMon == null)
        {
            AlgoMonData species = FindRewardSpeciesByCodeName(DefenseDemoNullbyteCodeName);
            if (species == null)
                return;

            demoMon = CreateDefenseDemoNullbyte(species);
            if (demoMon == null)
                return;

            payload.Add(demoMon);
            createdDemoMon = true;
        }

        MaxOutDefenseDemoNullbyte(demoMon, createdDemoMon);
    }

    // Defense note: Creates the temporary defense demo Nullbyte instance used for presentation testing.
    private static AlgoMonInstance CreateDefenseDemoNullbyte(AlgoMonData species)
    {
        AlgoMonInstance mon = AlgoMonInstance.CreateRewardBase(
            species,
            RewardDataQuality.HighQualityBase,
            int.MaxValue);
        MaxOutDefenseDemoNullbyte(mon, true);
        return mon;
    }

    // Defense note: Maxes the temporary defense demo Nullbyte's level and IVs while keeping normal learnset loading.
    private static void MaxOutDefenseDemoNullbyte(AlgoMonInstance mon, bool initializeLoadout)
    {
        if (mon == null)
            return;

        mon.nickname = DefenseDemoNullbyteCodeName;
        mon.instanceId = DefenseDemoNullbyteInstanceId;
        mon.dataQuality = RewardDataQuality.HighQualityBase;
        mon.battleFormName = "Base";
        mon.fusedBaseCopies = 0;
        mon.level = AlgoMonInstance.MAX_LEVEL;
        mon.exp = 0;
        mon.iv_Battery = DefenseDemoMaxIv;
        mon.iv_ClockSpeed = DefenseDemoMaxIv;
        mon.iv_ComputingPower = DefenseDemoMaxIv;
        mon.iv_Throughput = DefenseDemoMaxIv;
        mon.iv_Firewall = DefenseDemoMaxIv;
        mon.iv_Encryption = DefenseDemoMaxIv;
        mon.EnsurePersistentRuntimeState();
        if (mon.knownSkills == null)
            mon.knownSkills = new List<SkillData>();
        if (mon.fusionSourceInstanceIds == null)
            mon.fusionSourceInstanceIds = new List<string>();

        mon.knownSkills.RemoveAll(skill => skill == null);
        bool needsLoadoutInitialization = initializeLoadout || mon.knownSkills.Count == 0;

        if (needsLoadoutInitialization)
        {
            mon.knownSkills.Clear();
            mon.EnsureKnownSkillsFromLearnset();
        }
    }

    // Defense note: Finds the fixed temporary defense demo Nullbyte in a runtime list.
    private static AlgoMonInstance FindDefenseDemoNullbyte(List<AlgoMonInstance> mons)
    {
        if (mons == null)
            return null;

        for (int i = 0; i < mons.Count; i++)
        {
            AlgoMonInstance mon = mons[i];
            if (IsDefenseDemoNullbyte(mon))
                return mon;
        }

        return null;
    }

    // Defense note: Returns whether a mon is the temporary defense demo Nullbyte.
    private static bool IsDefenseDemoNullbyte(AlgoMonInstance mon)
    {
        return mon != null &&
               string.Equals(mon.instanceId, DefenseDemoNullbyteInstanceId, StringComparison.Ordinal);
    }

    // Defense note: Ensures the mon state dependency or state exists before use.
    private static void EnsureMonState(List<AlgoMonInstance> mons)
    {
        if (mons == null)
            return;

        for (int i = 0; i < mons.Count; i++)
        {
            if (mons[i] != null)
                mons[i].EnsurePersistentRuntimeState();
        }
    }

    // Defense note: Ensures the party payload links dependency or state exists before use.
    private void EnsurePartyPayloadLinks()
    {
        if (payload == null || party == null)
            return;

        for (int i = 0; i < party.Count; i++)
        {
            AlgoMonInstance mon = party[i];
            if (mon == null)
                continue;

            AlgoMonInstance payloadMon = EnsurePayloadEntry(mon);
            if (payloadMon != null)
                party[i] = payloadMon;
        }
    }

    // Defense note: Ensures the payload entry dependency or state exists before use.
    private AlgoMonInstance EnsurePayloadEntry(AlgoMonInstance mon)
    {
        if (mon == null || payload == null)
            return null;

        mon.EnsurePersistentRuntimeState();
        int existingIndex = IndexOfMon(payload, mon);
        if (existingIndex >= 0)
            return payload[existingIndex];

        payload.Add(mon);
        return mon;
    }

    // Defense note: Attempts to register reward base and reports success or failure.
    private bool TryRegisterRewardBase(
        EncounterReward reward,
        AlgoMonInstance defeatedOpponent,
        out AlgoMonInstance captured)
    {
        captured = null;
        if (!TryResolveRewardSpecies(reward, defeatedOpponent, out AlgoMonData species))
            return false;

        int seed = RewardTalentSeed(reward, species);
        captured = AlgoMonInstance.CreateRewardBase(species, reward.baseDataQuality, seed);
        if (captured == null)
            return false;

        if (reward != null && !string.IsNullOrWhiteSpace(species.codeName))
            reward.speciesCodeName = species.codeName.Trim();

        AddToPayload(captured);
        return true;
    }

    // Defense note: Attempts to resolve reward species and reports success or failure.
    private bool TryResolveRewardSpecies(
        EncounterReward reward,
        AlgoMonInstance defeatedOpponent,
        out AlgoMonData species)
    {
        species = null;
        if (defeatedOpponent != null && CanPersistCapture(defeatedOpponent))
        {
            species = defeatedOpponent.data;
            return true;
        }

        string rewardCode = reward != null ? reward.speciesCodeName : string.Empty;
        species = FindRewardSpeciesByCodeName(rewardCode);
        if (species != null)
            return true;

        species = FindRewardSpeciesByCodeName(CurrentBossSpeciesCodeName);
        return species != null;
    }

    // Defense note: Runs the reward talent seed helper used by this script.
    private int RewardTalentSeed(EncounterReward reward, AlgoMonData species)
    {
        unchecked
        {
            int seed = currentRunSeed;
            seed = seed * 397 ^ StableHash(currentNodeId);
            seed = seed * 397 ^ StableHash(species != null ? species.codeName : string.Empty);
            seed = seed * 397 ^ (payload != null ? payload.Count : 0);
            seed = seed * 397 ^ (reward != null ? reward.encounterLevel : 0);
            return seed & int.MaxValue;
        }
    }

    // Defense note: Checks whether persist species is currently allowed.
    private static bool CanPersistSpecies(AlgoMonData data, bool usesTransientData)
    {
        if (data == null || usesTransientData)
            return false;

#if UNITY_EDITOR
        return AssetDatabase.Contains(data);
#else
        return true;
#endif
    }

    // Defense note: Finds the reward species by code name reference used by this component.
    private static AlgoMonData FindRewardSpeciesByCodeName(string codeName)
    {
        string normalized = NormalizeSpeciesKey(codeName);
        if (string.IsNullOrEmpty(normalized))
            return null;

        EncounterSpeciesCatalog catalog = Resources.Load<EncounterSpeciesCatalog>(EncounterSpeciesCatalogResourcePath);
        if (catalog != null)
        {
            AlgoMonData found = FindSpeciesInPool(catalog.GetSpecies(), normalized);
            if (found != null)
                return found;
        }

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:AlgoMonData", new[] { AlgoMonAssetSearchFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AlgoMonData data = AssetDatabase.LoadAssetAtPath<AlgoMonData>(path);
            if (data != null &&
                string.Equals(NormalizeSpeciesKey(data.codeName), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }
#endif

        return null;
    }

    // Defense note: Finds the species in pool reference used by this component.
    private static AlgoMonData FindSpeciesInPool(AlgoMonData[] pool, string normalizedCodeName)
    {
        if (pool == null)
            return null;

        for (int i = 0; i < pool.Length; i++)
        {
            AlgoMonData data = pool[i];
            if (data != null &&
                string.Equals(NormalizeSpeciesKey(data.codeName), normalizedCodeName, StringComparison.OrdinalIgnoreCase))
            {
                return data;
            }
        }

        return null;
    }

    // Defense note: Attempts to get payload mon and reports success or failure.
    private bool TryGetPayloadMon(int index, out AlgoMonInstance mon)
    {
        mon = null;
        if (payload == null || index < 0 || index >= payload.Count)
            return false;

        mon = payload[index];
        return mon != null;
    }

    // Defense note: Returns whether this value is in party.
    public bool IsInParty(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        return IndexOfMon(party, mon) >= 0;
    }

    // Defense note: Runs the index of mon helper used by this script.
    private static int IndexOfMon(List<AlgoMonInstance> mons, AlgoMonInstance mon)
    {
        if (mons == null || mon == null)
            return -1;

        mon.EnsurePersistentRuntimeState();
        for (int i = 0; i < mons.Count; i++)
        {
            AlgoMonInstance candidate = mons[i];
            if (candidate == null)
                continue;

            candidate.EnsurePersistentRuntimeState();
            if (ReferenceEquals(mon, candidate) ||
                string.Equals(mon.instanceId, candidate.instanceId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // Defense note: Removes the mon from list entry from the target collection or UI.
    private static bool RemoveMonFromList(List<AlgoMonInstance> mons, AlgoMonInstance mon)
    {
        int index = IndexOfMon(mons, mon);
        if (index < 0)
            return false;

        mons.RemoveAt(index);
        return true;
    }

    // Defense note: Runs the same species helper used by this script.
    private static bool SameSpecies(AlgoMonInstance a, AlgoMonInstance b)
    {
        return a != null &&
               b != null &&
               !string.IsNullOrWhiteSpace(a.SpeciesCodeName) &&
               string.Equals(a.SpeciesCodeName, b.SpeciesCodeName, StringComparison.OrdinalIgnoreCase);
    }

    // Defense note: Runs the display name for helper used by this script.
    private static string DisplayNameFor(AlgoMonInstance mon)
    {
        if (mon == null)
            return "AlgoMon";
        if (!string.IsNullOrWhiteSpace(mon.nickname))
            return mon.nickname.Trim();
        return !string.IsNullOrWhiteSpace(mon.SpeciesCodeName) ? mon.SpeciesCodeName : "AlgoMon";
    }

    // Defense note: Runs the normalize species key helper used by this script.
    private static string NormalizeSpeciesKey(string codeName)
    {
        return string.IsNullOrWhiteSpace(codeName) ? string.Empty : codeName.Trim();
    }

    // Defense note: Runs the normalize boss species code name helper used by this script.
    private static string NormalizeBossSpeciesCodeName(string speciesCodeName)
    {
        return TryNormalizeBossSpeciesCodeName(speciesCodeName, out string normalized)
            ? normalized
            : BossSpeciesCodeNames[0];
    }

    // Defense note: Attempts to normalize boss species code name and reports success or failure.
    private static bool TryNormalizeBossSpeciesCodeName(string speciesCodeName, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(speciesCodeName))
            return false;

        string trimmed = speciesCodeName.Trim();
        for (int i = 0; i < BossSpeciesCodeNames.Length; i++)
        {
            if (string.Equals(trimmed, BossSpeciesCodeNames[i], StringComparison.OrdinalIgnoreCase))
            {
                normalized = BossSpeciesCodeNames[i];
                return true;
            }
        }

        return false;
    }

    // ----------------------------------------------------------------
    // Scene transitions

    // Defense note: Subscribes to the persistent events events used by this object.
    private void SubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);

        EventBus.Subscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Subscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Subscribe<BattleEndEvent>(OnBattleEnd);
    }

    // Defense note: Unsubscribes from the persistent events events to avoid stale callbacks.
    private void UnsubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);
    }

    // Defense note: Runs the on scene transition helper used by this script.
    private void OnSceneTransition(SceneTransitionEvent e)
    {
        EventBus.Clear();
        SubscribePersistentEvents();
        SceneManager.LoadScene(e.Destination.ToString());
    }

    /// <summary>Convenience wrapper so other systems don't need to know scene names.</summary>
    // Defense note: Runs the go to helper used by this script.
    public static void GoTo(GameScene destination)
    {
        EventBus.Publish(new SceneTransitionEvent { Destination = destination });
    }
}
