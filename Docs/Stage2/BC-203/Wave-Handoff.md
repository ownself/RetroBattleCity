# BC-203 wave 组件交接

状态：**ready（组件行为/专项验证完成，待独立审查与后续 owner 集成；不代表 BC-203/M2 已验收）**。

## 起点、边界与保留

- 起点 `main / 7de290635ca8b097022b379e911fbef1234baefc`。首先保存 `Wave-Starting-State.json`：起点所有既存脏文件（含未跟踪源码、证据、`.vsconfig`）SHA256 与空暂存区。基于已验收但未提交的 BC-201/202 工作继续，没有回滚、暂存、提交或推送。
- 本组件只新增 `Scripts/AI/EnemyWaveDefinition.cs`、`EnemyWaveState.cs`、独立 `Tests/EditMode/EnemyWaveStateTests.cs`，以及 Unity 自动生成的三个 script meta、本目录 Wave-* 证据。
- 未编辑 ArenaSession、ArenaController、TankState、其他组件/测试、作者脚本、实际资产、场景、总计划或主看板。没有递归委派，没有开始 BC-204 以后。
- 最终起点哈希复核：除 `Docs/Stage2/Execution-Board.md` 被外部同步外，所有既存脏文件逐字节不变；该看板差异已通知父代理，本 worker 从未写入。详见 `Wave-File-Integrity.json`。暂存区仍为空。

## 公开 API

命名空间 `BattleCity`。

```csharp
[CreateAssetMenu(menuName = "Battle City/Enemy Wave Definition")]
public sealed class EnemyWaveDefinition : ScriptableObject
{
    public TankDefinition[] tankDefinitions; // definitionId -> direct reference
    public uint seed = 1;
    public int preparationTicks = 60;
    public int spawnIntervalTicks = 120;
    public bool requiresCalibration = true;
    public void Validate();
}

public readonly struct EnemySpawnRequest
{
    public int SequenceIndex { get; }      // 0-based sequence entry, stable across retries
    public string DefinitionId { get; }   // captured ID
    public TankDefinition Definition { get; } // direct validated author reference
    public Vector2Int PositionPixels { get; } // map-local center, X right/Y up
    public uint BrainSeed { get; }        // explicit nonzero seed for RegisterEnemyAI
}

public sealed class EnemyWaveState
{
    public EnemyWaveState(LevelDefinition level, EnemyWaveDefinition configuration);
    public bool Step(int aliveCount, Func<Vector2Int, bool> isOccupied,
                     Func<EnemySpawnRequest, bool> tryCreate);

    public int TotalCount { get; }
    public int SpawnedCount { get; }
    public int PendingCount { get; }
    public int ReservedCount { get; }
    public int UnreservedCount { get; }
    public int AliveCount { get; }
    public int MaxActiveEnemies { get; }
    public int PreparationTicks { get; }
    public int SpawnIntervalTicks { get; }
    public uint InitialSeed { get; }
    public uint RandomState { get; }
    public EnemySpawnRequest? Reservation { get; }
    public int PreparationRemainingTicks { get; }
    public int IntervalRemainingTicks { get; }
    public bool SequenceExhausted { get; }
}
```

配置仅承载定义映射、计时与显式种子，不再复制序列、总数、上限或出生点。实际配置资产由 integration owner 通过 Editor API 创建并绑定。本组件没有建任何实际资产。

构造时调用 `level.Validate()` / `configuration.Validate()`；空/重复定义 ID、缺失/null 映射、未知序列 ID、非法 TankDefinition、零种子、非正计时等立即抛出带说明的异常。种子类型是 uint，不能表达负数。重复定义采用 Ordinal ID 比较；同 ID 重复映射（即使同一引用）也拒绝。空序列有效且无需映射任何坦克，但 LevelDefinition 自身的结构校验仍生效。

## 计数与状态语义

- `TotalCount` 唯一来源为构造时 `level.enemySequence.Length`。
- `SpawnedCount` 是累计**成功物化**数，死亡不扣减；`SequenceIndex` 等于本次将提交的 SpawnedCount。
- `PendingCount = TotalCount - SpawnedCount`，包含已预留但未物化者；不因为等待、预备、占用或失败回调而减少。
- 首版最多一个在途预留，`ReservedCount` 只能为 0/1；`UnreservedCount = PendingCount - ReservedCount`。
- `AliveCount` 是最近一次 Step 宿主提供的伤害死亡结算后存活敌人数，若本次创建成功则再加 1。不是独立死亡账本，暂停不调用 Step 时也不更新该快照。
- 不变量：`Total = Spawned + Pending`、`Pending = Unreserved + Reserved`、`Alive + Reserved <= MaxActive`，计数非负。多次传入同一死亡后人数不会被当成重复死亡事件。
- 宿主不能无视此组件的预留而另占容量。输入负存活数，或 `aliveCount + ReservedCount > MaxActive`，Step 在任何状态变更前抛出 `ArgumentOutOfRangeException`；这是宿主违约，不是取消/吞掉预留。有效输入下物化前仍复查容量。
- `SequenceExhausted` 只表示 Pending=0，不表示胜利。例如最后一敌已成功创建时它为 true，但 AliveCount 可以是 1。以后 BC-206 等读取耗尽/活体/预留，自己决定胜负；本组件不实现生命周期或胜利逻辑。

## 父代理确认的计时口径（未校准原型规则）

父代理在本组件开始前明确批准以下端点。不是原版事实：

1. 首次战斗 Step 可选点并建立预留，预备年龄 0，Remaining=60；建立预留的这个 Step 不消耗预备时间。
2. 之后满 60 次战斗 Step 尝试物化。默认无阻时第一次成功在 **Step 调用序号 61**。
3. **仅成功物化**后开始 120tick 间隔，Remaining=120；经过满 120 次战斗 Step 可建立下一预留。
4. 因而默认第一次成功 #61，第二次建立预留 #181，第二次成功 **#241**。无阻时成功物化间距 180tick（120间隔 + 60预备）。专项用绝对调用号锁定，无隐含 off-by-one。
5. 预备已结束但出生点受阻，Remaining 保持 0，每个战斗 Step 复查原预留点。不重置预备，不换种子，不吞序列，不挤开已有对象；解除占用后本次 Step 即可再次尝试物化。
6. 回调返回 false 同样保留原请求，每战斗tick重试。失败不开始间隔；成功才提交序列和 AliveCount。回调异常传播且不提交计数，预留可重试。禁止 Step 重入。
7. 间隔满但容量满/全部出生点受阻则等待。没有预留时每tick扫描全部点；只在可用点中确定性选择，不会随机命中受阻点而漏掉可用点。

`preparationTicks` / `spawnIntervalTicks` 都必须至少为 1。宿主沿用现有60Hz逻辑时钟。本组件没有 Time、壁钟、Update、独立 clock 或异步调度；暂停门禁是**不调用 Step**，恢复不补算暂停时间。

## 宿主接入顺序与责任

1. 从作者 LevelDefinition 与波次配置构造一个会话独占 `EnemyWaveState`。Stage01 由 integration owner 填 20 个基本敌 ID、cap=4；CombatArena 保持无自然波次。本组件不写这两者。
2. 每战斗tick先执行已有 AI 决策/移动/武器/扫掠/伤害/死亡结算。**之后**调用一次 Step，传入当前真实存活敌人数；不用再给 wave 发死亡通知。
3. `isOccupied(center)` 必须是只读查询，按坦克16×16盒/中心像素/半开边界规则合并：地图与基地、玩家、活敌、其他系统预留；不能只查可见 Tile。应忽略 wave 自己当前预留，否则会永久自阻。宿主也应把 `Reservation` 的16×16占地向其他预留/生成系统公开，避免双方分配重叠。
4. 建立预留前扫描所有出生点；物化前再次调用占用查询，即使这点开始预备时是空的。持有中的点被阻塞会等待该点，不自动搬迁到另一出生点。
5. `tryCreate(request)` 必须是原子操作：成功创建/注册共享 TankState 与对应敌方 AI（`request.BrainSeed`），才能返回 true；失败必须无副作用（宿主负责不留下半注册对象）。回调执行时 SpawnedCount/PendingCount 尚未提交；true 返回后才提交。
6. Step 放在本tick伤害结算后，不要再返回同tick移动/武器阶段执行新敌。这样新敌**下一tick**才能移动/射击。波次本身不引用 ArenaSession，也无法替宿主强制这个顺序；后续真实会话专项必须覆盖它。
7. 回调不能重入 Step。共享模型只保证单线程宿主协议，不引入锁/线程调度框架。

## 确定性、快照与作者数据

- 构造时复制 LevelDefinition 的序列数组、出生点数组、场上上限，以及配置映射解析结果、计时、种子。之后修改这些作者数组/计时/上限不影响当前会话；运行不写回作者数据。
- `EnemySpawnRequest.Definition` 保留原 TankDefinition 直接引用用于集成，不深拷贝/创建新 SO；其模型参数按现有作者资产只读契约使用。捕获的 DefinitionId 不随引用后续字段修改而重新解析。本组件不会修改 TankDefinition。
- 出生点顺序来自快照数组，只在成功建立预留时推进独立 xorshift32，一次取模选择当次可用点。无可用点、预备等待、占用重试、创建失败不消耗随机数。
- BrainSeed 由 `(InitialSeed, SequenceIndex)` 经固定 uint 混合派生，0映射为1，与占用等待时长/回调重试次数无关。算法在源码中固定，seed=1 的首个 PRNG 状态270369、首个 BrainSeed2527132011被精确断言锁定。
- 不读取 Unity 全局 Random 或壁钟。相同种子、序列、出生点及宿主逻辑输入轨迹可重放；不同占用轨迹自然可能改变选择的可用点。

## 本轮实际验证与原始证据

- 同一既存 Unity Editor：6000.6.7f1，PID26924、port7800，项目显式锁定 `H:/Projects/Unities/BattleCity`。CLI `1.0.0-beta.14`，self-update --check 无更新。Pipeline0.9.1提示沿用记录，不升级。没有断连/重启/协议替换，没有进入 Play，没有更改项目设置/实际资产/场景。
- 实际 `script recompile` / `script recompile_status`：completed、failed=false、errors=[]、compilationFailed=false，见 `Wave-Compile-Request.json` / `Wave-Compile-Result.json`。
- 实际 `test run --mode editor --filter EnemyWaveStateTests --async_tests true` 后查询完成：**30/30 Passed，failed/skipped/inconclusive均0**，逐条结果在 `Wave-Tests-Result.json`；Unity原始NUnit `Wave-EditMode-Results.xml` 同样30例全部通过。本轮没有执行历史256/38全量，不能把它们算成本轮通过。
- 覆盖计数守恒、cap1/4、死亡后补位/重复同人数快照、多个出生点/全阻/解除、预备结束再占用、创建失败/异常/重入、固定种子/全局随机独立、作者数组与计时快照/不写回、非法配置、空序列、序列耗尽仍有活体、无 Step 暂停冻结、默认绝对端点61/241。
- Console 基线 cursor286 同session至294，**8条全部log，无新增warn/error**，无丢弃/重置。历史buffer有1条error，不冒称历史不存在；当前groundTruth error/warn为0。见 `Wave-Console-Baseline.json` / `Wave-Console-Final-Since286.json`。
- 最终 Editor 非Play、非编译、compilationFailed=false，见 `Wave-Editor-Final-State.json`。
- `git diff --check` 通过；`git diff --cached --name-only` 空。三个新增源码/测试meta由 Unity 导入生成，不手写。

所有 `unity command` 均带 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity`；实际命令/schema先从既存 Editor 发现。探索schema保留于忽略目录 `Temp/BC-203-wave`，不纳入证据目录。

## 未覆盖 / 下一门禁

未执行全量EditMode/PlayMode、真实Stage01自然出生/显示、移动射击下一tick集成断言、截图、人工体验、独立构建、长期压力或胜负流程。占用查询与原子注册由宿主提供；组件专项使用纯回调，不能证明会话未来实现正确。

下一步由父代理安排只读审查、view组件与integration owner按边界交接；集成创建默认资产、实现上述占用/注册/调用顺序并跑真实场景和受影响回归。本 worker 到组件行为完成为止，不替其他owner接入。
