# Grid Design

TheGrid uses a data-only generated DAG. The generator creates the route map
for a run; the scene UI visualizes route state, previews node risk/rewards, and
selects from this graph.

## Schema

`GridGraph`

| Field | Meaning |
|---|---|
| `seed` | Seed used for deterministic generation. |
| `startNodeId` | The single start anchor node. |
| `bossNodeId` | The single final Boss node. |
| `nodes` | Flat list of all `GridNode` records. |

`GridNode`

| Field | Meaning |
|---|---|
| `id` | Stable string id. Current format is `start`, `L{layer}N{index}`, `boss`. |
| `layer` | Integer depth. Edges must point to a higher layer. |
| `indexInLayer` | Stable sort position for future UI layout. |
| `nodeType` | Active values are `Start`, `Combat`, `Hacker`, `Elite`, `Shop`, `Reboot`, and `Boss`. `Rest` is kept only as a legacy enum value for serialized compatibility. |
| `depthBand` | Sprint 4 difficulty band: `Early`, `Middle`, `Late`, `Boss`, or `None` for utility nodes. |
| `encounterLevel` | Resolved encounter level after Threat Tier and node-depth scaling. |
| `dangerRating` | Compact D1-D5 risk rating used by TheGrid readability UI. |
| `outgoingNodeIds` | Forward outgoing edges by node id. No reverse edges are stored. |

Reverse links are intentionally omitted. Reachability checks use temporary BFS
from `startNodeId`, which keeps the saved graph small and easy to serialize.

## Generation Parameters

Defaults live in `GridGenerationSettings`.

| Parameter | Default | Notes |
|---|---:|---|
| `totalLayers` | 7 | Includes Start and Boss. |
| `minIntermediateNodes` | 1 | Applies only to non-start, non-boss layers. |
| `maxIntermediateNodes` | 4 | Clamped by previous-layer outgoing capacity. |
| `minOutgoingEdges` | 1 | Every non-final node gets at least one outgoing edge. |
| `maxOutgoingEdges` | 3 | Used as the edge-density target and capacity limit. |
| `maxGenerationAttempts` | 10 | Regenerate retry cap if validation fails. |
| `combatWeight` | 70 | Intermediate node type weight. |
| `eliteWeight` | 15 | Intermediate node type weight. |
| `shopWeight` | 10 | Intermediate node type weight for Compute Shop nodes. |
| `rebootWeight` | 5 | Route-control node; from Reboot, Start becomes an optional target while visited nodes are preserved. |

The generator uses `System.Random(seed)`, not `UnityEngine.Random`, so the same
seed and settings produce the same graph.

## Algorithm

```text
Generate(seed):
  rng = System.Random(seed)
  repeat up to maxGenerationAttempts:
    create layer sizes
      layer 0 = 1 Start
      final layer = 1 Boss
      intermediate layers = random 1..4, capped by previous capacity

    create nodes
      start node type = Start
      intermediate node type = weighted Combat / Hacker / Elite / Shop / Reboot
      final node type = Boss
      guarantee at least one early Hacker node for Sprint 4 route pressure

    for each adjacent layer pair:
      first connect every child to one parent with remaining capacity
      then ensure every parent has at least one outgoing edge
      then add random extra edges up to the target density

    validate graph
    return if valid

  throw a validation error summary
```

Issue #19 uses regeneration rather than graph repair. Repair logic is deferred
because the current layered generation strategy is already constrained to
produce valid graphs under the Sprint 3 defaults.

## Validation Rules

`GridValidator.Validate(graph)` checks:

- Graph has nodes.
- Node ids are non-empty and unique.
- `startNodeId` exists and points to the only `NodeType.Start` node.
- `bossNodeId` exists and points to the only `NodeType.Boss` node.
- Start is in layer 0.
- Boss is in the final layer and has no outgoing edges.
- Every outgoing edge points to an existing node.
- Every outgoing edge points to a strictly later layer.
- Boss is reachable from Start.
- Every non-start node is reachable from Start.
- Every reachable non-final node has at least one outgoing edge.

These rules guarantee a forward-only DAG with no cycles, no dead-end route
before the Boss, and no unreachable UI nodes.

## Run Integration Notes

- #19 owns only the data layer.
- #20 should draw nodes by `(layer, indexInLayer)` and use
  `outgoingNodeIds` to decide which next nodes are selectable.
- `GameManager.BeginRun()` creates and stores `currentRunGraph`, sets
  `currentNodeId` to the Start node, and initializes `visitedNodeIds`.
- #20 should call `GameManager.TrySelectRunNode(nodeId)` for clicks instead of
  writing `currentNodeId` directly. The method rejects locked nodes.
- TheGrid should not silently create production run state. Any direct-scene
  fallback run or New Run button is editor-debug only and disabled by default;
  #22 owns normal run creation from MainTerminal.
- Rest nodes are intentionally not generated. Battle encounters start from
  full per-battle Battery/CP runtime state, keeping the route map focused on
  encounter choice rather than attrition management.
- `NodeType.Hacker` nodes field multi-AlgoMon parties and reward higher EXP /
  compute without adding defeated AlgoMon to Payload.
- `NodeType.Shop` nodes open the Compute Shop and spend run-scoped compute on
  current-run buffs instead of starting a battle.
- `NodeType.Reboot` is not a stored backward graph edge. Selecting a Reboot
  node moves the cursor there normally. While the cursor is on Reboot,
  `GameManager.GetAvailableNodeIds()` adds `startNodeId` as an extra optional
  target alongside the node's normal outgoing edges. Previously visited nodes
  should not repeat rewards or battles when routed through again.
- Boss victory and run-end behavior are handled by #21/#24, not by the graph.

## Sprint 4 Readability Notes

- Nodes show compact type labels: `WILD`, `HACK`, `ELITE`, `SHOP`, `REBOOT`,
  and `BOSS`.
- Encounter nodes append `D1-D5` danger to the node label. Higher-risk nodes
  also use stronger accent colors.
- Route state labels use `HERE`, `NEXT`, `DONE`, dimmed locked nodes, and
  `TARGET` for the Boss.
- Hover/focus preview text shows the expected encounter type, level, danger
  band, and broad reward identity before the player commits to the route.
