# AlgoMon 🧬

> A Data-Driven PVE Roguelite emphasizing complex data structures and algorithmic combat.

AlgoMon is a strategic game developed with Unity (2022.3 LTS) for a Game Programming coursework project. Rather than focusing on graphic-intensive 3D environments, this project highlights computer science principles: **procedural graph generation, dynamic resource management, and algorithmic sorting**. Players navigate a node-based network to capture algorithmic creatures and manage their data payloads.

---

## 📐 System Architecture & UI Prototypes

The following prototypes outline the core game loop and the underlying algorithms driving the UI and gameplay.

### 1. The Terminal Dashboard (Main Hub)
The game adopts a "cyber-hacker" aesthetic, treating the main menu as a data terminal. 

![Main Menu](./Game%20main%20menu.png)

* **Algorithmic Focus:** Implements UI State Management and adheres to the Open-Closed Principle (OCP). Different modules (Grid, Payload) are decoupled, allowing the dashboard to route visual states dynamically without physical player avatars.

### 2. Procedural Node Network (The Grid)
The exploration phase ditches traditional maps in favor of a mathematical node network.

![Grid Start](./exploration_grid_start.png)
*Initial generation of the data grid.*

![Grid Active](./exploration_grid_active.png)
*Active pathing showing highlighted routes and dynamic log updates.*

* **Algorithmic Focus:** The map is procedurally generated using a **Directed Acyclic Graph (DAG)** algorithm. Constraint logic ensures continuous path connectivity and validates that the Boss node is always reachable from the starting point.

### 3. Tactical Battle Engine (The Arena)
The core combat is a rock-paper-scissors (A-S-D) tactical simulator driven by dynamic resources.

![Battle Scene](./Battle%20scene.jpg)

* **Algorithmic Focus:**
    * **Resource Constraints:** Combat relies on strictly managed values: **Battery (HP)** and **Computing Power (CP)**. 
    * **Priority Queue:** Turn orders and action executions are sorted using a dynamic priority queue, decoupled via an Event Bus pattern for clean damage/buff calculations.

### 4. Payload Box (Data Matrix)
Instead of a simple inventory, captured AlgoMons are managed in a structured database view.

![Display Panel](./AlgoMon%20display%20panel.png)

* **Algorithmic Focus:** Showcases efficient data structure management. Includes custom implementation of sorting algorithms (e.g., QuickSort) to filter instances based on `Battery`, `Computing Power`, and `IV Scores` in $O(N \log N)$ time.

---
*Status: Pre-production & System Prototyping Complete. Proceeding to Unity Engine initialization.*
