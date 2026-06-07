/*
Script Audit:
- Purpose: Owns cross-scene game state and controls the main run lifecycle.
- Attached GameObject: Auto-created persistent GameObject named GameManager through Bootstrap/EnsureInstance.
- Main responsibilities: Store payload and party, start/end runs, generate current run graph, track visited nodes, create encounters, grant rewards, and load scenes.
- Important variables: Instance, payload, party, currentRunGraph, currentNodeId, currentOpponent, currentOpponentParty, IsRunActive, selectedThreatTier, pendingRunOutcome, currentRunRewards.
- Inputs: MainTerminal start command, Grid node selections, BattleEndEvent, party data, and reward data.
- Outputs or effects: Changes scene, updates run state, creates opponents, saves rewards/captures, and publishes or responds to game events.
- AI/tutorial/template assistance: AI was used to help audit and document this script; final meaning was checked against the project.
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

    [Header("Payload — Full Warehouse (all captured AlgoMons)")]
    public List<AlgoMonInstance> payload = new List<AlgoMonInstance>();

    [Header("Party — Active Squad (max 4 for current run)")]
    public List<AlgoMonInstance> party = new List<AlgoMonInstance>();
    public const int MaxPartySize = 4;

    // Player EXP and evolution data persist; compute is run-scoped shop currency.
    [Header("Player Progress")]
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
    public int highestUnlockedThreatTier = ThreatTierRules.MinTier;
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
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static GameManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject(nameof(GameManager));
        return managerObject.AddComponent<GameManager>();
    }

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
        SubscribePersistentEvents();
    }

    private void OnDestroy()
    {
        UnsubscribePersistentEvents();
    }

    // ----------------------------------------------------------------
    // Payload management (warehouse — no cap)

    public void AddToPayload(AlgoMonInstance mon)
    {
        if (mon == null)
            return;

        EnsureRewardContainers();
        mon.EnsurePersistentRuntimeState();
        if (IndexOfMon(payload, mon) < 0)
            payload.Add(mon);
    }

    public void RemoveFromPayload(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        RemoveMonFromList(payload, mon);
        RemoveMonFromList(party, mon);
    }

    public void EnsureRosterState()
    {
        EnsureRewardContainers();
    }

    // ----------------------------------------------------------------
    // Party management (active squad — max 4)

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

    public void RemoveFromParty(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        RemoveMonFromList(party, mon);
    }

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

    public void BeginRun()
    {
        int seed = (int)(DateTime.UtcNow.Ticks & int.MaxValue);
        BeginRun(seed);
    }

    public void BeginRun(int seed)
    {
        BeginRun(seed, null);
    }

    public void BeginRun(int seed, GridGenerationSettings gridSettings)
    {
        ClearRunResult();
        EnsureRewardContainers();
        currentRunRewards.Reset();
        computeBalance = 0;
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

        GridGraph graph = new GridGenerator(gridSettings).Generate(seed);
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
        computeBalance = 0;
        if (currentRunBuffs != null)
            currentRunBuffs.Clear();
        ResetShopState();
        EnsureRewardContainers();
        currentRunRewards.Reset();
        lastEncounterReward = new EncounterReward();
    }

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

    public bool TrySetSelectedThreatTier(int tier)
    {
        if (!ThreatTierRules.CanEnterTier(tier, highestUnlockedThreatTier))
            return false;

        selectedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampTier(tier));
        return true;
    }

    public bool TrySetSelectedBossSpecies(string speciesCodeName)
    {
        if (IsRunActive || !TryNormalizeBossSpeciesCodeName(speciesCodeName, out string normalized))
            return false;

        selectedBossSpeciesCodeName = normalized;
        currentBossSpeciesCodeName = normalized;
        return true;
    }

    public void SetHighestUnlockedThreatTier(int tier)
    {
        highestUnlockedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampTier(tier));
        selectedThreatTier = ThreatTierRules.ToInt(ThreatTierRules.ClampSelectableTier(selectedThreatTier, highestUnlockedThreatTier));
    }

    public bool TrySelectRunNode(string nodeId)
    {
        if (!IsNodeAvailable(nodeId))
            return false;

        if (!visitedNodeIds.Contains(nodeId))
            visitedNodeIds.Add(nodeId);

        currentNodeId = nodeId;
        return true;
    }

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

    public bool IsNodeAvailable(string nodeId)
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(nodeId))
            return false;
        if (currentRunGraph.GetNode(nodeId) == null)
            return false;

        List<string> available = GetAvailableNodeIds();
        return available.Contains(nodeId);
    }

    public bool IsNodeVisited(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && visitedNodeIds.Contains(nodeId);
    }

    public bool TryRegisterCapture(AlgoMonInstance mon, out AlgoMonInstance captured)
    {
        return TryRegisterCapture(mon, RewardDataQuality.Base, out captured);
    }

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

    public AlgoMonInstance RegisterCapture(AlgoMonInstance mon)
    {
        TryRegisterCapture(mon, out AlgoMonInstance captured);
        return captured;
    }

    private static bool CanPersistCapture(AlgoMonInstance mon)
    {
        return mon != null && CanPersistSpecies(mon.data, mon.usesTransientData);
    }

    public bool CanAffordCompute(int amount)
    {
        return amount <= 0 || computeBalance >= amount;
    }

    public bool TrySpendCompute(int amount)
    {
        if (amount <= 0)
            return true;
        if (!CanAffordCompute(amount))
            return false;

        computeBalance -= amount;
        return true;
    }

    public bool HasRunBuff(RunBuffType buffType)
    {
        return currentRunBuffs != null && currentRunBuffs.Contains(buffType);
    }

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
            reason = $"Need {offer.ComputeCost - computeBalance} more compute.";
            return false;
        }

        return true;
    }

    public bool TryPurchaseShopOffer(RunShopOffer offer, out string message)
    {
        if (!CanPurchaseShopOffer(offer, out message))
            return false;

        if (!TrySpendCompute(offer.ComputeCost))
        {
            message = "Compute spend failed.";
            return false;
        }

        currentRunBuffs.Add(offer.BuffType);
        message = $"Purchased {offer.DisplayName}.";
        return true;
    }

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
            reason = $"Need {cost - computeBalance} more compute.";
            return false;
        }

        return true;
    }

    public bool TryRefreshShopOffers(out string message)
    {
        if (!CanRefreshShopOffers(out message))
            return false;

        int cost = CurrentShopRefreshCost;
        if (!TrySpendCompute(cost))
        {
            message = "Compute spend failed.";
            return false;
        }

        currentShopRefreshCount++;
        GenerateShopOffers();
        message = $"Shop refreshed for {cost} compute. Next refresh costs {CurrentShopRefreshCost}.";
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

    public string CurrentRunBuffSummary()
    {
        EnsureRewardContainers();
        return RunShopCatalog.BuildActiveSummary(currentRunBuffs);
    }

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

    private void ApplyRunRewardBuffs(EncounterReward reward)
    {
        if (reward == null)
            return;

        float expMultiplier = PlayerRunExpRewardMultiplier;
        if (Mathf.Approximately(expMultiplier, 1f))
            return;

        reward.playerExp = Mathf.Max(0, Mathf.RoundToInt(reward.playerExp * expMultiplier));
        reward.algoMonExp = Mathf.Max(0, Mathf.RoundToInt(reward.algoMonExp * expMultiplier));
    }

    private void ApplyEncounterReward(EncounterReward reward, AlgoMonInstance defeatedOpponent)
    {
        if (reward == null)
            return;

        playerExp += reward.playerExp;
        computeBalance += reward.compute;
        GrantPartyExp(reward.algoMonExp);

        if (reward.shouldGrantBaseData &&
            TryRegisterRewardBase(reward, defeatedOpponent, out _))
        {
            reward.baseDataGranted = true;
        }
    }

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

        ThreatTier threatTier = currentRunGraph != null
            ? ThreatTierRules.ClampTier(currentRunGraph.threatTier)
            : ThreatTierRules.ClampTier(currentThreatTier);
        currentOpponentParty.Clear();
        string bossSpeciesCodeName = e.Node.nodeType == NodeType.Boss ? CurrentBossSpeciesCodeName : null;
        currentOpponentParty.AddRange(EncounterFactory.CreateParty(currentRunSeed, e.Node, threatTier, bossSpeciesCodeName));
        currentOpponent = currentOpponentParty.Count > 0
            ? currentOpponentParty[0]
            : EncounterFactory.Create(currentRunSeed, e.Node, threatTier, bossSpeciesCodeName);
        GoTo(GameScene.TheArena);
    }

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

    private void OnBattleEnd(BattleEndEvent e)
    {
        if (!IsRunActive)
            return;

        GridNode completedNode = CurrentRunNode();
        currentOpponent = null;
        currentOpponentParty.Clear();

        if (e.PlayerWon && completedNode != null && completedNode.nodeType != NodeType.Boss)
        {
            GoTo(GameScene.TheGrid);
            return;
        }

        RecordRunResult(e.PlayerWon ? RunOutcome.Victory : RunOutcome.Defeat, completedNode);
        EndRun();
        GoTo(GameScene.RunResult);
    }

    private GridNode CurrentRunNode()
    {
        if (currentRunGraph == null || string.IsNullOrEmpty(currentNodeId))
            return null;

        return currentRunGraph.GetNode(currentNodeId);
    }

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

    private static bool IsEncounterNode(NodeType type)
    {
        return ThreatTierRules.IsEncounterNode(type);
    }

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

    private void ResetShopState()
    {
        if (currentShopOfferTypes != null)
            currentShopOfferTypes.Clear();
        currentShopNodeId = string.Empty;
        currentShopRefreshCount = 0;
    }

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

        AddToPayload(captured);
        return true;
    }

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

    private bool TryGetPayloadMon(int index, out AlgoMonInstance mon)
    {
        mon = null;
        if (payload == null || index < 0 || index >= payload.Count)
            return false;

        mon = payload[index];
        return mon != null;
    }

    public bool IsInParty(AlgoMonInstance mon)
    {
        EnsureRewardContainers();
        return IndexOfMon(party, mon) >= 0;
    }

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

    private static bool RemoveMonFromList(List<AlgoMonInstance> mons, AlgoMonInstance mon)
    {
        int index = IndexOfMon(mons, mon);
        if (index < 0)
            return false;

        mons.RemoveAt(index);
        return true;
    }

    private static bool SameSpecies(AlgoMonInstance a, AlgoMonInstance b)
    {
        return a != null &&
               b != null &&
               !string.IsNullOrWhiteSpace(a.SpeciesCodeName) &&
               string.Equals(a.SpeciesCodeName, b.SpeciesCodeName, StringComparison.OrdinalIgnoreCase);
    }

    private static string DisplayNameFor(AlgoMonInstance mon)
    {
        if (mon == null)
            return "AlgoMon";
        if (!string.IsNullOrWhiteSpace(mon.nickname))
            return mon.nickname.Trim();
        return !string.IsNullOrWhiteSpace(mon.SpeciesCodeName) ? mon.SpeciesCodeName : "AlgoMon";
    }

    private static string NormalizeSpeciesKey(string codeName)
    {
        return string.IsNullOrWhiteSpace(codeName) ? string.Empty : codeName.Trim();
    }

    private static string NormalizeBossSpeciesCodeName(string speciesCodeName)
    {
        return TryNormalizeBossSpeciesCodeName(speciesCodeName, out string normalized)
            ? normalized
            : BossSpeciesCodeNames[0];
    }

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

    private void SubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);

        EventBus.Subscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Subscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Subscribe<BattleEndEvent>(OnBattleEnd);
    }

    private void UnsubscribePersistentEvents()
    {
        EventBus.Unsubscribe<SceneTransitionEvent>(OnSceneTransition);
        EventBus.Unsubscribe<NodeSelectedEvent>(OnNodeSelected);
        EventBus.Unsubscribe<BattleEndEvent>(OnBattleEnd);
    }

    private void OnSceneTransition(SceneTransitionEvent e)
    {
        EventBus.Clear();
        SubscribePersistentEvents();
        SceneManager.LoadScene(e.Destination.ToString());
    }

    /// <summary>Convenience wrapper so other systems don't need to know scene names.</summary>
    public static void GoTo(GameScene destination)
    {
        EventBus.Publish(new SceneTransitionEvent { Destination = destination });
    }
}
