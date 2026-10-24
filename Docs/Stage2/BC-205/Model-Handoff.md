# BC205 life-model handoff（组件交付，不代表集成验收）

状态：**ready for independent review / integration consumption**。仅 BC205-life-model；BC206、胜负/重开/HUD/阶段三未进入。起点 HEAD 为 `d010f12f1a814d3595f8c065eedd7732475cddc7`。未暂存、提交、推送；既存 `.vsconfig` 保留。

## 文件与公开 API（本交接冻结）

只新增以下源码及由现有 Editor 导入生成的 `.meta`：

- `Assets/BattleCity/Scripts/Core/PlayerLifeParameters.cs`
- `Assets/BattleCity/Scripts/Core/PlayerLifeState.cs`
- `Assets/BattleCity/Tests/EditMode/PlayerLifeParametersTests.cs`
- `Assets/BattleCity/Tests/EditMode/PlayerLifeStateTests.cs`

运行时命名空间 `BattleCity`，仅依赖 `System`，无 Unity/Tank/Map/Weapon 引用，无独立时钟。

```csharp
new PlayerLifeParameters(int initialLives, int respawnDelayTicks, int protectionTicks)
PlayerLifeParameters.Prototype // immutable 3 / 60 / 120, RequiresCalibration == true
new PlayerLifeState(PlayerLifeParameters parameters)
PlayerLifeStepResult PlayerLifeState.Step(bool playerAliveAfterDamage, Func<bool> tryRespawn)
```

参数类 sealed、全部 get-only；三个参数必须 >=1，0/负数分别抛 `ArgumentOutOfRangeException`（带对应参数名），允许 `int.MaxValue`。模型构造时重新复制为自身不可变 `Parameters` 快照；null 参数拒绝。无可变配置引用或运行期参数 setter。

`PlayerLifeState` 只读属性：

| 属性 | 含义 |
| --- | --- |
| `Parameters` | 本状态创建时不可变快照 |
| `RemainingLives` | 仍可使用的总生命，活着时包含当前；初始3，死亡扣1，复活不加/扣 |
| `IsAlive` | 当前代是否存活；初始true，死亡后只有成功复活回调才能变回true |
| `RespawnRemainingTicks` | 死亡建立的等待；ready受阻保持0；耗尽固定0 |
| `ProtectionRemainingTicks` | 尚受保护的完整后续伤害阶段数；初始0，成功复活建立120 |
| `IsProtected` | `IsAlive && ProtectionRemainingTicks > 0`；宿主必须在伤害前读取 |
| `IsExhausted` | `RemainingLives == 0`，永久锁存的BC206请求；不冻结宿主/整局 |
| `IsWaitingForRespawn` | 已死且尚有生命（包含ready受阻） |
| `IsRespawnReady` | 已死、尚有生命且等待为0 |

`PlayerLifeStepResult` 是 readonly struct；`DeathRecorded`、`Respawned`、`LivesExhaustedThisTick` 是**本次Step边沿**，不是累计计数或可重复消费事件。普通Step返回全false。最后一次死亡同时返回DeathRecorded=true、LivesExhaustedThisTick=true；之后一直dead的Step只返回全false，IsExhausted继续true。没有胜负事件系统或清除锁存接口。

## 宿主调用顺序与绝对端点

宿主沿用现有战斗时钟。每个真正战斗tick：

1. 在扫掠/伤害阶段前读取 `IsProtected`；保护坦克的敌弹仍由宿主正常消费，只免扣HP。
2. 宿主完成所有该tick伤害及死亡结算，得到玩家当前代最终alive快照。
3. **恰好调用一次** `Step(snapshot, tryRespawn)`，在敌波次出生前。不能每发子弹调用、不能把只读观察当Step、不能同tick重试Step。
4. 成功复活只进入本tick出生/显示阶段；不倒流执行移动/发弹/伤害。下一tick正常继续。

| 绝对tick | 伤害前 / Step后状态 |
| --- | --- |
| 初始 | alive，生命3，无保护 |
| 死亡D | Step识别true→false一次，生命3→2，等待60；D不递减、不尝试复活 |
| D+1 | Step后等待59 |
| D+59 | Step后等待1，不调用回调 |
| D+60 = R | 等待减到0，首次调用回调；成功后alive、生命仍2、保护120 |
| R+1…R+120 | 每tick完整扫掠/伤害阶段前均IsProtected=true；之后Step才减1 |
| R+120末 | 保护减到0 |
| R+121 | 首个可受伤的伤害阶段 |

专项使用D=37、R=97，所以D+59=96、R+120=217、R+121=218，均以绝对tick断言。1tick参数边界同样遵循D不减、R不减，0tick明确拒绝而不另定语义。

暂停/失焦时不调用Step；没有deltaTime、Time.time或补算入口。专项只证明模型在不Step时不变化，**不冒称验证了真实Controller暂停路径**。

## 回调原子性、重试与计数

- `tryRespawn` 每个ready战斗tick最多一次；等待期间/活着/耗尽时不调用。参数不能为null（即使当前不会调用）。
- 回调必须自行原子完成出生占用复查和**同一玩家**重置/注册；占用则返回false且无副作用。模型没有地图/坐标/对象知识，不能代替宿主占用检测。
- 回调返回true之前，模型仍为dead、ready、无保护；true返回后才提交alive及完整保护120。生命不会再次扣减。
- false时保持dead、ready、等待0、保护0；后续每战斗tick继续重试，不吞生命/重设倒计时。
- 抛异常时模型不提交复活，已完成的等待递减不会回滚，ready仍可在**下一tick**重试；宿主必须保证抛异常前也无外部副作用。不能在同一tick重跑Step。
- 禁止回调重入Step，抛InvalidOperationException；finally释放重入保护。模型不声称跨线程安全。
- dead模型若收到外部alive=true会抛InvalidOperationException且不推进计时，防止宿主绕过成功回调擅自复活。
- 多弹同tick由宿主汇总成一次最终dead快照；该代死亡只扣1。后续dead快照推进等待但不重复扣命。成功回调后的下一代才能再记录一次死亡。

## 新鲜验证与证据入口

全部报告位于 `Docs/Stage2/BC-205/Model-*`：

- `Model-Baseline-Files.txt`：起点UTC/git/index、既存工作区变化和248项保护文件SHA-256（含.vsconfig/Contract及所有已跟踪Assets、ProjectSettings、Packages）。开始时另有父代理文档改动/BC204 Manual-Check，未编辑或回退；不是误称干净工作区。
- `Model-CLI-Version.txt`、`Model-Baseline-Status.json`：CLI1.0.0-beta.14，Unity6000.6.7f1，既有PID26924/端口7800 ready。
- `Model-Baseline-Console.json`、`Model-Baseline-Editor.json`、`Model-Baseline-Settings.json`：Stage01 clean/stopped，runInBackground=false。Console历史计数error=2；groundTruth errors=0、warnings=0，未清Console，未把历史结果当新验证。
- `Model-Command-Catalog.json`、`Model-Discovery-Full.json`、`Model-Selected-Schemas.json`及help：实际命令/schema发现；`Model-Commands.txt`列实际调用。
- **`Model-Compile.json`**：现有Editor实际recompile completed，0 errors / 0 warnings，CLI exit0；刷新/导入由Editor执行，没有手写.meta/YAML。
- **`Model-Test-Discovery.json`、`Model-Test-Run.json`、`Model-Test-Reconciliation.json`**：本轮EditMode发现344项，模型专项24项发现=执行，**24 passed / 0 failed / 0 skipped / 0 inconclusive**，FullName集合无缺失/额外。只执行模型lane；旧320项及PlayMode留给集成owner，不覆盖旧证据。
- `Model-Final-Settings-Imports.json`：Editor API确认4个新脚本的GUID与实际已加载类型，Stage01仍clean/stopped，runInBackground仍false。
- `Model-Final-Console.json`：同session cursor474→480，历史error计数仍2，groundTruth errors=0 / warnings=0 / compilationFailed=false；本轮只新增6条log。
- `Model-Final-Editor.json`：ready、未编译/未reload、stopped。
- **`Model-Protected-Files-Comparison.json`**：248项逐一复核，changed=[]；现有资源、场景、实现、其他测试、设置、Contract及.vsconfig字节不变。
- `Model-Partial.diff`：本lane四个新增源码的完整可审阅diff；`Model-Final-Git-Status.txt`：index空。`git diff --check`通过。
- `Model-Checkpoint.md`：验证开始前及重要阶段记录。

## 已验证源码 SHA-256

| 文件 | SHA-256 |
| --- | --- |
| Core/PlayerLifeParameters.cs | `823aa0d1674c0ad1744d359a018b3dec6a4452e56cda32b6e5d11cd6af0c9c7d` |
| Core/PlayerLifeState.cs | `2f8cd906cf1f3679ab36bf0f4a9f996df04ec6b969cdae855cc1a4624056142e` |
| EditMode/PlayerLifeParametersTests.cs | `f520e2b1337ec21dbf482bb7acdaaa3661123f8320c3a51a80af74cd8648e62e` |
| EditMode/PlayerLifeStateTests.cs | `630ef42d6a775b2434453bcb4567d44799faddd19579d05c6138744bc0fc5083` |

完整路径及Editor生成meta哈希见 `Model-Implementation-Test-Hashes.txt`。报告产生后没有修改这些源码。

## 剩余事项 / 集成owner约束

本模型lane无已知未解决缺陷，尚须父代理安排独立只读review。接下来仅由集成owner消费以上API；不能再造生命状态机/时钟。稳定对象/ID、出生及HP/武器参数快照、完整占用查询、旧弹lease回收、武器/命令/SuppressHeld重置、保护敌弹消费、Controller盾投影、Stage01显式启用/CombatArena默认禁用、实际Update与全量回归均**尚未实现或在此lane验证**，须按Contract执行。不勾选总计划，不把组件ready等同BC205整体完成。
