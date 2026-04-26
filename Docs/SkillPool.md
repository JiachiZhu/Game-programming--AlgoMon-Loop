# AlgoMon Skill Pool — Full Design Reference

> **Total: 34 species skills + 1 universal (Recharge)**
> Status values confirmed: Burn 5%/layer (max 4), Leech 5%/layer (max 3), Freeze −15% ClockSpeed/layer (max 3).
> Stat-buff layers use ComputingUp / ThroughputUp / FirewallUp / EncryptionUp (each layer = +10%).
> ⚠️ Normal-type skills have no ElementType in current enum — recommend adding `Normal` to ElementType before asset creation.

---

## Universal Skill

| Name | Element | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|---------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| Recharge 充能 | — | Status | — | 0 | 0 | 0 | true | None | Restore 5 CP to self. Universal — all species can use. |

---

## Attack Skills (18 total)

### 🔥 Fire — Computing (Physical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 热能探测 Thermal Ping | Attack | Computing | 30 | 1 | +1 | false | None | — |
| 点火循环 Ignite Loop | Attack | Computing | 35 | 3 | 0 | true | None | **Special**: counter success auto-triggers a second Ignite Loop at 1 CP cost this turn. Needs custom BattleManager handling. |
| 熔毁指令 Meltdown Override | Attack | Computing | 80 | 6 | −1 | false | None | — |

### 💧 Water — Throughput (Magical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 液冷飞溅 Coolant Splash | Attack | Throughput | 40 | 2 | +1 | false | None | — |
| 洪泛攻击 Flood Attack | Attack | Throughput | 55 | 4 | 0 | true | Nullify | Counter win: opponent's skill cancelled, CP not consumed. |
| 深网海啸 Deep Web Tsunami | Attack | Throughput | 60 | 7 | 0 | true | None | Counter win: `counterPermanentCPCostReduce = 2` (this skill's cpCost permanently −2, min 0). |

### 🌿 Grass — Throughput (Magical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 孢子脚本 Spore Script | Attack | Throughput | 35 | 2 | +1 | true | None | Counter win: apply Leech (3 stacks, 3 turns) to opponent. `counterBonusValue = 3`, `counterStatusDuration = 3`, `counterSelfStatus = Leech` — wait, this is an opponent debuff. **Special**: counter applies opponent Leech, not a self-buff. Custom handling. |
| 根须权限 Root Access | Attack | Throughput | 45 | 3 | 0 | false | None | — |
| 木马丛林 Trojan Forest | Attack | Throughput | 75 | 6 | −1 | false | None | — |

### ⚡ Electric — Computing (Physical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 短路火花 Short Circuit | Attack | Computing | 20 | 2 | +1 | true | None | Counter win: self gains "next attack priority +1 AND basePower +10". **Special**: compound self-buff. Custom BattleManager handling. |
| 伏特阵列 Volt Array | Attack | Computing | 50 | 4 | 0 | false | None | — |
| 千兆瓦释放 Gigawatt Discharge | Attack | Computing | 80 | 7 | 0 | false | None | — |

### ❄️ Ice — Throughput (Magical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 冰霜字节 Frost Byte | Attack | Throughput | 30 | 1 | +1 | false | None | — |
| 系统冻结 System Freeze | Attack | Throughput | 40 | 3 | 0 | true | None | Counter win: apply 1 stack Freeze to opponent. `onHitStatusStacks = 1` on counter. |
| 绝对零度宕机 Absolute Zero Crash | Attack | Throughput | 70 | 6 | −1 | true | None | Counter win: opponent must act last next turn (forced priority −2). **Special**: custom BattleManager handling. |

### 🪨 Ground — Computing (Physical)

| Name | Type | DmgType | basePower | cpCost | priority | canCounter | counterSuccessType | Effects |
|------|------|---------|-----------|--------|----------|------------|--------------------|---------|
| 碎石比特 Gravel Bit | Attack | Computing | 40 | 2 | +1 | false | None | — |
| 硬件震颤 Hardware Quake | Attack | Computing | 55 | 4 | 0 | true | None | Counter win: `onHitFirewallShred = 0.20` (reduce opponent Firewall by 20%). |
| 坏道崩塌 Bad Sector Collapse | Attack | Computing | 65 | 5 | 0 | false | None | — |

---

## Defense Skills (8 total)
> All Defense skills: `canCounter = true`, `instructionType = Defense`, `damageType = —`, `basePower = 0`.

### ⚪ Normal (Universal)

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 空值拒绝 Null Reject | Normal | 1 | Block | 0.80 | — |
| 蜜罐协议 Honeypot Protocol | Normal | 2 | Block | 0.60 | `counterCPDrain = 2` (drain 2 CP from attacker on counter win) |

### 🔥 Fire

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 熔断机制 Circuit Breaker | Fire | 2 | Block | 0.70 | `counterSelfStatus = ComputingUp`, `counterBonusValue = 4`, `counterStatusDuration = 0` (permanent) |

### 💧 Water

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 冗余备份 Redundant Backup | Water | 2 | Block | 0.70 | `counterCPDiscount = 1`, `counterStatusDuration = 2` (own all skill CP −1 for 2 turns) |

### 🌿 Grass

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 安全模式 Safe Mode | Grass | 3 | Block | 0.70 | On counter win: heal self 8% max Battery + clear all negative statuses. **Special**: custom BattleManager handling. |

### ⚡ Electric

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 法拉第笼 Faraday Cage | Electric | 2 | Block | 0.70 | `counterSelfStatus = Overclock` (priority +1 next turn), `counterBonusValue = 1`, `counterStatusDuration = 1` |

### ❄️ Ice

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 休眠线程 Sleep Thread | Ice | 2 | Block | 0.80 | On counter win: apply 1 stack Freeze to attacker. (`counterSelfStatus` repurposed as opponent debuff — special handling.) |

### 🪨 Ground

| Name | Element | cpCost | counterSuccessType | counterBlockPercent | Other Counter Effects |
|------|---------|--------|--------------------|--------------------|----------------------|
| 硬核装甲 Hardcode Armor | Ground | 3 | Block | 0.80 | `counterSelfStatus = FirewallUp`, `counterBonusValue = 3`, `counterStatusDuration = 0` (permanent for battle) |

---

## Status Skills (8 total)
> Status skills: `damageType = —`, `basePower = 0`, `priority = 0` unless noted.

### ⚪ Normal (Universal)

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 自动化调优 Auto-Tuning | Normal | 2 | false | None | Apply `ComputingUp × 10 stacks` to self. |
| 向量化计算 Vectorized Computation | Normal | 2 | false | None | Apply `ThroughputUp × 10 stacks` to self. |

### 🔥 Fire

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 热阻尼降频 Thermal Throttling | Fire | 3 | true | None | Apply Burn (2 stacks, ongoing) to opponent. Counter win: apply 2 additional Burn stacks (total 4 = max). |

### 💧 Water

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 缓冲池预载 Buffer Pool Preload | Water | 2 | true | SelfBuff | Apply `BufferLoad` to self (next skill CP −4). Counter win: also gain priority +1 next turn. |

### 🌿 Grass

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 跨站脚本植入 XSS Injection | Grass | 3 | true | None | Apply Leech (3 stacks, 3 turns) to opponent. Counter win: extend duration by 1 extra turn. |

### ⚡ Electric

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 超频双线程 Hyper-Threading | Electric | 2 | false | None | Apply `Concurrent` to self (next skill fires twice, costs 2× CP). |

### ❄️ Ice

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 数据库死锁 Database Deadlock | Ice | 4 | true | None | Apply Freeze (3 stacks) to opponent. Counter win: apply 1 additional Freeze stack (but max is 3, so capped). **Alternative**: opponent speed −30% next turn via custom handling. |

### 🪨 Ground

| Name | Element | cpCost | canCounter | counterSuccessType | Effects |
|------|---------|--------|------------|--------------------|---------|
| 扇区塌陷 Sector Sinkhole | Ground | 3 | false | None | Apply `Ensnare` (3 turns) to opponent — cannot swap out. |

---

## Recommended Learnsets (Unlock Levels)

> Lv1 starts with 3 skills (standard attack + elemental defense + matching stat buff).
> Lv10 fills the 4th slot with the elemental status skill.
> Lv20 and Lv30 unlock new attacks that require replacing an existing skill.
> Computing species get Auto-Tuning; Throughput species get Vectorized Computation as starter buff.

| Species | Element | DmgType | Lv1 Slot 1 | Lv1 Slot 2 | Lv1 Slot 3 | Lv10 | Lv20 | Lv30 |
|---------|---------|---------|-----------|-----------|-----------|------|------|------|
| Sortex | Electric | Computing | Volt Array | Faraday Cage | Auto-Tuning | Hyper-Threading | Short Circuit | Gigawatt Discharge |
| Overflux | Fire | Computing | Ignite Loop | Circuit Breaker | Auto-Tuning | Thermal Throttling | Thermal Ping | Meltdown Override |
| Nullbyte | Water | Throughput | Flood Attack | Redundant Backup | Vectorized Computation | Buffer Pool Preload | Coolant Splash | Deep Web Tsunami |
| Recursix | Grass | Throughput | Root Access | Safe Mode | Vectorized Computation | XSS Injection | Spore Script | Trojan Forest |
| Cachelon | Ice | Throughput | System Freeze | Sleep Thread | Vectorized Computation | Database Deadlock | Frost Byte | Absolute Zero Crash |
| Heapion | Ground | Computing | Hardware Quake | Hardcode Armor | Auto-Tuning | Sector Sinkhole | Gravel Bit | Bad Sector Collapse |

---

## Special Cases — Require Custom BattleManager Handling

| Skill | Issue |
|-------|-------|
| 点火循环 Ignite Loop | Counter success auto-triggers a second cast. Not representable by current fields. |
| 短路火花 Short Circuit | Counter success grants self "next attack priority+1 AND basePower+10". Compound buff. |
| 孢子脚本 Spore Script | Counter success applies opponent Leech — current `counterSelfStatus` only models self-buffs. |
| 绝对零度宕机 Absolute Zero Crash | Counter success forces opponent to act last next turn (priority −2 injection). |
| 安全模式 Safe Mode | Counter success heals 8% HP AND clears all debuffs simultaneously. |
| 休眠线程 Sleep Thread | Counter success applies Freeze to the attacker, not self. Reversed target. |

---

## Asset Folder Structure

```
Assets/_AlgoMon/Skills/
├── Attack/
│   ├── Fire/        (Thermal Ping, Ignite Loop, Meltdown Override)
│   ├── Water/       (Coolant Splash, Flood Attack, Deep Web Tsunami)
│   ├── Grass/       (Spore Script, Root Access, Trojan Forest)
│   ├── Electric/    (Short Circuit, Volt Array, Gigawatt Discharge)
│   ├── Ice/         (Frost Byte, System Freeze, Absolute Zero Crash)
│   └── Ground/      (Gravel Bit, Hardware Quake, Bad Sector Collapse)
├── Defense/
│   ├── Normal/      (Null Reject, Honeypot Protocol)
│   ├── Fire/        (Circuit Breaker)
│   ├── Water/       (Redundant Backup)
│   ├── Grass/       (Safe Mode)
│   ├── Electric/    (Faraday Cage)
│   ├── Ice/         (Sleep Thread)
│   └── Ground/      (Hardcode Armor)
└── Status/
    ├── Normal/      (Auto-Tuning, Vectorized Computation)
    ├── Fire/        (Thermal Throttling)
    ├── Water/       (Buffer Pool Preload)
    ├── Grass/       (XSS Injection)
    ├── Electric/    (Hyper-Threading)
    ├── Ice/         (Database Deadlock)
    └── Ground/      (Sector Sinkhole)
```
