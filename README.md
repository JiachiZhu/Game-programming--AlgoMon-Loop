# AlgoMon — Algorithmic Monster Roguelite

> **A data-driven PVE Roguelite built on real Computer Science.**  
> Navigate procedural node networks, capture algorithmic creatures, and optimize their hardware limits through genetic merging — all powered by the data structures you know from class.

**Engine:** Unity 2022.3 LTS &nbsp;|&nbsp; **Language:** C# &nbsp;|&nbsp; **Genre:** Roguelite / Tactical Simulator  
**Status:** `Pre-production — System Prototyping Complete`

---

## 🧬 Concept

AlgoMon rejects the traditional fantasy dungeon aesthetic. Instead, the player is a **cyber-hacker** operating inside a data network, capturing and reprogramming **algorithmic creatures** — beings whose stats, abilities, and genetic makeup are pure data structures. Player will constantly engage in attacks, fixes, and management of the network (that is, constantly interacting with various bug algomons), incorporate them into one's own use, gradually become stronger, and eventually become the strongest hacker in the entire DIICSU.

Every mechanic in this game is a direct expression of a computer science concept. The algorithms are not hidden in the engine; they *are* the gameplay.

---

## ⚙️ Core Systems & Algorithm Index

### 1. The Grid — Procedural Node Network
The exploration layer is a pure UI route-selection graph, with no walking or real-time movement.

| Component | Algorithm / Pattern | Complexity |
|---|---|---|
| Map generation | **Directed Acyclic Graph (DAG)** with layered topology | O(V + E) |
| Path connectivity | Topological sort + reachability validation | O(V + E) |
| State separation | Tactical Chips (session) vs. Payload (persistent) | — |

A DAG guarantees the player always has a valid route to the Boss node, while preventing backward loops that would break roguelite progression.

### 2. The Arena — Priority-Based Combat Engine
A 2D side-view tactical battle system. Skills are packaged as executable data instructions, not magic spells.

| Component | Algorithm / Pattern | Complexity |
|---|---|---|
| Turn ordering | **Max-Heap Priority Queue** keyed on Clock Speed | O(log N) per insert/extract |
| ASD counter system | A-S-D **RPS triangle** — counter overrides turn order | O(1) |
| Element type chart | **6×6 static matrix** lookup (Water/Fire/Grass/Ice/Electric/Ground) | O(1) |
| Buff/Debuff system | **Observer Pattern (Event Bus)** — fully decoupled | O(1) dispatch |
| Dual resource model | Battery (HP) + Computing Power (CP) constraints | — |

The Priority Queue ensures faster AlgoMons act first. However, the **ASD counter system** can override this: both players simultaneously choose Attack / Status / Defense (A > S > D > A). If a counter occurs, the countered unit's animation is interrupted mid-play, and the countering unit's skill animation is inserted — regardless of Clock Speed. The counter also triggers any bonus effect defined on the skill (e.g. ×3 damage).

#### AlgoMon Stat Design — Six Dimensions

Each AlgoMon has six base stats that map directly to classic RPG archetypes, re-skinned as hardware specifications:

| Stat | RPG Equivalent | Role in Combat |
|---|---|---|
| **Battery** | HP | Reaches zero → unit is shut down |
| **Clock Speed** | Speed | Key for the Priority Queue — higher clock acts first |
| **Computing Power** | Physical Attack | Damage output via A-type (Attack) instructions |
| **Throughput** | Magic Attack | Damage output via S-type (Special) instructions |
| **Firewall** | Physical Defence | Damage reduction against Computing Power attacks |
| **Encryption** | Magic Defence | Damage reduction against Throughput attacks |

The dual-damage-route design creates a meaningful strategic layer: if the opponent's Firewall is high, swap to a high-Throughput AlgoMon to exploit their Encryption instead — and vice versa. This mirrors the physical/magical split found in classic monster-battler games, grounded here in networking and hardware terminology.

### 3. The Terminal — Gene Lab & Payload Vault
The meta-progression layer, styled as a backend admin dashboard.

| Component | Algorithm / Pattern | Complexity |
|---|---|---|
| IV inheritance (gene merge) | **Greedy Algorithm** — `IV_child = Math.Max(IV_A, IV_B)` per stat | O(S) where S = stat dimensions |
| Payload sorting & search | **QuickSort** with multi-key comparator | O(N log N) average |
| Stat model | Hard-cap IV (hardware) / soft-cap EXP (software) separation | — |

The IV/EXP split is the game's core design pillar: grinding only raises software progress. To break the hardware ceiling, players must invest in genetic merging — a deliberate resource sink.

#### Payload vs. Party — Two-Tier Roster System

| | Payload (Warehouse) | Party (Active Squad) |
|---|---|---|
| **What it is** | Every AlgoMon the player has ever captured | The squad selected for the current run |
| **Size limit** | Unlimited | Max 6 |
| **Where managed** | The Lab — sorted via QuickSort | Pre-run selection screen |
| **Algorithmic focus** | O(N log N) retrieval and sorting | — |

This separation means the player must make deliberate squad-building decisions before each run — they cannot bring everything.

---

## 🗺️ UI Prototypes

Detailed UI wireframes and interaction flows are archived in the [`/Prototype`](./Prototype/) directory.

| Screen | Preview |
|---|---|
| Main Terminal Dashboard | ![Main Menu](./Prototype/Game%20main%20menu.png) |
| The Grid — Initial State | ![Grid Start](./Prototype/exploration_grid_start.png) |
| The Grid — Active Pathing | ![Grid Active](./Prototype/exploration_grid_active.png) |
| The Arena — Battle View | ![Battle Scene](./Prototype/Battle%20scene.jpg) |
| Payload Vault — Data Matrix | ![Display Panel](./Prototype/AlgoMon%20display%20panel.png) |

---

## 📂 Project Structure

```
AlgoMon-Loop/
├── Assets/_AlgoMon/
│   ├── Scripts/
│   │   ├── Core/          # EventBus, GameManager, StateMachine
│   │   ├── Data/          # ScriptableObjects — AlgoMonSO, SkillSO
│   │   ├── Grid/          # DAGGenerator, NodeGraph, PathValidator
│   │   ├── Battle/        # PriorityQueue, CombatResolver, ASDMatrix
│   │   ├── Lab/           # GeneticMerger, PayloadSorter (QuickSort)
│   │   └── UI/            # Controllers for Terminal, Grid, Arena, Lab
│   ├── Scenes/
│   │   ├── MainTerminal.unity
│   │   ├── TheGrid.unity
│   │   ├── TheArena.unity
│   │   └── TheLab.unity
│   └── ScriptableObjects/
├── Prototype/             # UI wireframes & design archive
└── README.md
```

---

## 🚀 Getting Started

1. Clone the repository
2. Open in **Unity Hub** with Unity **2022.3 LTS**
3. Open scene `Assets/_AlgoMon/Scenes/MainTerminal.unity`
4. Press Play

---

## 👥 Team

| Role | Contributor |
|---|---|
| Design & Engineering | Jiachi Zhu |

---

## 🎨 Credits

| Asset | Tool | Notes |
|---|---|---|
| AlgoMon sprite artwork (12 images) | Google Gemini 3.1 Pro (image generation) | All portraits generated specifically for this project |
