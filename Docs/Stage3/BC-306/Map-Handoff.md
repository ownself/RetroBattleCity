# BC-306 独立地图组件交接

## 范围与结果

验证者：**subagent worker**；2026-10-25；Unity **6000.6.7f1**，CLI **1.0.0-beta.14**（父代理已检查最新，未升级）。HEAD main / `bdb20b0e509276be1d4818c09638e5f419e463e4`。原批次精确起点 `Temp/BC305306-Before`；本lane在305交接后另外记录 `Temp/BC306-Map-Before/{Hashes.json,Tracked.patch,Status.txt}`。

本轮仅交付 MapState 精确写入接口、BaseFortificationState 独立模型、EditMode 专项。**没有接线ArenaSession/Controller/PickupDefinition/PickupState，没有改资产/场景/ProjectSettings，没有实现整个BC306，没有开展307以后、提交/推送或勾选总计划/总看板。** BC304、BC305和.vsconfig未提交成果均保留。

实际编译成功；**49唯一EditMode Passed，0 Failed**，不是全量/PlayMode/自然通关/人工验收。首次46项成功后，仅新增3项并单独执行，不重复前46项。`Map-Audit.json`按FullName记录原始结果来源及保护hash。接线和独立review由父代理后续安排；本lane无未解决执行阻塞。

规则全部是当前项目默认，未经原版核实。

## 完整新增公开API

命名空间均为 `BattleCity`。

```csharp
// MapState：一次准确替换一个8px格；返回实际数据是否改变。
public bool SetTerrainCell(int x, int y, TerrainKind kind, byte brickMask = 0);

public sealed class BaseFortificationState
{
    public const int DefaultDurationTicks = 1200;
    public BaseFortificationState(MapState map, Vector2Int baseCenterPixels,
        int durationTicks = DefaultDurationTicks);
    public int DurationTicks { get; }
    public int RemainingTicks { get; }
    public bool IsActive { get; }
    public bool IsRestoring { get; }
    public int PendingCellCount { get; }
    public void Activate(IReadOnlyList<Rect> occupants);
    public void Step(IReadOnlyList<Rect> occupants);
    public void Cancel();
}
```

### MapState写入约定

- x/y为既有26×26、左下原点、每格8px的terrain坐标；越界抛 `ArgumentOutOfRangeException`。
- `Brick` 必须传1～15的残砖mask；位序沿用 `x + y*2`、Y向上，每bit代表4px象限。其他terrain必须传0；不合法mask抛 `ArgumentException`，未知terrain抛 `ArgumentOutOfRangeException`。不默默填满/清洗非法数据，不产生Brick+0或非砖残mask。
- 校验在写入前完成；实际terrain或mask变化时统一写两者，`Revision`递增一次，对应 `CellRevisionAt`取得该revision；相同内容返回false且不更新revision。其他格不变。
- 这是数据接口，**调用者**负责战斗伤害/扫掠之后的写入顺序和占用安全；本组件通过它写入，不绕过MapState，不增加Tilemap碰撞真相。
- 没有改动现有弹伤方法：补强仍是真实Steel，普通弹只消耗自身、最高级弹沿用既有Steel移除8px格规则。

### 构造及基地前置条件

- 构造持有MapState引用、持续时间数值快照和U环坐标，**此时不快照地形，也不改地图**。`durationTicks`必须正数，默认1200；null map拒绝。
- 经父代理明确批准：本能力要求基地中心在8px地形网格对齐，16px足迹完全在208px地图内；非法中心清晰抛 `ArgumentException`，不隐式四舍五入。**未改变LevelDefinition通用校验或既有无铁锹会话规则。** Stage01 `(104,8)`符合。
- 后续集成仅在有Base且配置Pickups（启用铁锹能力）的会话构造组件并应用这个前置条件；无基地仍消费铁锹收据而不创建隐形墙。
- 基地16px足迹外，左、上、右各一层8px格，无底边，越界格裁剪而非wrap/向内挪。Stage01坐标恰为：`(11,0),(14,0),(11,1),(14,1),(11,2),(12,2),(13,2),(14,2)`。基地本身四格 `(12/13,0/1)` 不受影响。一般非边界基地为8格，角落可少于8格。

### 调用者提供占用，不复制实体/时钟

- Activate/Step参数是**当前所有活坦克（玩家和敌人）+出生预留**的合并像素空间 `IReadOnlyList<Rect>`；每次调用使用当时已结算后的bounds，不传旧缓存。应含玩家复活预留（如将来有该机制）及当前wave reservation；无需把静态基地足迹当作环占用。
- 显式无占用传空list，null抛 `ArgumentNullException`。组件不持有、不修改该list或任何坦克、出生预留、位置、HP、武器、RNG或波次状态，不挤开/杀/传送占用者。
- 与既有MapState一致使用 `Epsilon` 的严格正面积重叠，贴边不算占用。延期粒度为整个8px格：恢复残砖时也保守地以整个8px格检查，不在残砖缺口里塞回同格阻挡。
- 目标为Brick/Steel/Water且该格需要改变时，若被占用就逐格延期。目标Empty/Grass/Ice无须等待；目标与现值已相同也无需写入或等待。

### Activate、持续时间端点与Step位置

- **只在新的成功拾取收据**调用Activate，不能每帧/每tick重放LastCollected。首次Activate快照**当时**U环全部terrain和mask，包括Empty；重复Activate不覆盖首次快照，刷新到DurationTicks而不累加，并请求重新补强所有环格（包括被最高级弹击毁者）。
- Activate立即尝试安全补Steel；被活坦克或预留占用的格保留待办，后续Step逐格重试。已成功补强的格后来被弹毁，**不会每tick自动重建**；只有再次Activate请求补钢。
- 本组件没有LogicClock、elapsed time、deltaTime或Update；只有现有session的单一战斗tick调用Step来计数。
- **Step恰好每个完整未来战斗tick末一次：伤害已结算，生命/出生和wave reservation已结算，收据Activate之前。** Ready/暂停/失焦暂停/终局不调用Step。不要在激活后同一tick再Step；也不要为了占用重试额外Step。
- 具体端点：tick T末先Step旧状态，再领取铁锹Activate，返回Remaining=1200；T+1的完整移动/弹道阶段钢墙有效，其末Step变1199；T+1199末变1；T+1200完整战斗阶段仍有效，末Step变0并立刻只尝试恢复。这样恰好保护1200个未来完整战斗tick，且拾取tick不扣时。
- 对照305现有Step：放在 `EnemyWave.Step` 之后、拾取收据之前，使用最新占用。若先行 `SettleOutcome()` 已终局，停止正常地形Step而走Cancel；若手雷拾取后才终局，共用终局清理也必须Cancel。不再Step波次、不增加时间源。

### 到期、再次激活与Cancel

- 剩余递减到0时，所有旧补钢待办被替换成首次快照的恢复待办，**不再补钢**；移除墙/恢复非阻挡格无需等待，阻挡恢复按格延期。
- `IsActive`仅表示Remaining>0；激活期间 `PendingCellCount`为尚待补钢格数。到期仍有待恢复时 `IsRestoring=true`、Remaining=0，PendingCellCount为恢复待办数。每个安全tick可只恢复一部分格，其他格保持待办。
- 一轮全部恢复后释放快照，两个bool均false、PendingCellCount=0；后续新Activate会重新快照当前布局。恢复还未完成时重新Activate则保留同一首次快照、刷新计时，并重新请求补钢。
- **Cancel仅用于终局/退休/重开旧会话清理**：调用者先保证不再推进战斗移动；Cancel无占用等待强制精确恢复首次快照、Remaining归0、取消补钢和恢复待办。幂等，不推进tick。不能拿它当战斗中的安全提前到期，否则可能恢复到活坦克脚下。
- 后续接线需覆盖胜利/失败的共用清理、Stop/Release、Controller重开退休；新session持有新组件，不复用旧实例。这里未擅自编辑那些文件。

## 自动证据与覆盖边界

| 原始结果 | 实际唯一通过 | 说明 |
| --- | ---: | --- |
| `Edit-Request.json` / `Edit-Result.json` | 46 | BaseFortificationStateTests初版全类，全部Passed |
| `Bullet-Mask-Request.json` / `Bullet-Mask-Final-Result.json` | 1 | 新增真实BulletWorld弹损残砖后快照/精确mask和revision恢复；`Bullet-Mask-Result.json`保留首次running状态 |
| `Nonblocking-Request.json` / `Nonblocking-Result.json` | 2 | 新增Grass/Ice恢复有占用仍不等待 |

测试覆盖：全部15种非零残砖mask、首次Activate而非构造时快照、重复不覆盖；Empty/Brick/Steel/Water/Grass/Ice布局及环外不变；Stage01 U环全图坐标、无底、四角和上边界裁剪；1200精确未来tick与重复刷新不累加；激活和重复补强受活坦克/预留阻挡并分别释放；到期残砖/水阻挡恢复与空格立即移除，逐格解除待办，到期未完成激活不再补钢；恢复中再激活、完整恢复后新快照；Cancel从active/待恢复清全部工作、幂等和再激活；普通/最高级真实弹Steel规则；真实残砖弹伤；MapState写入原子校验、只dirty变格、不变写入不增revision；ArenaMapView真实Tilemap增量Steel/完整Brick/Empty投影及环外tile身份保持；非法中心/持续时间/null拒绝。

**未覆盖/未声称：**整个BC306收据分派、会话配置资产快照接线、暂停/终局/退休/重开集成、真实Controller.Update拾取、PlayMode、截图或自然游玩。这些属于后续integration lane。本lane EditMode投影专项只直接Render完整砖/钢/空；残砖15mask与revision准确性已在数据层断言，但本轮未重跑依赖PlayMode的OverrideGeometry部分砖mesh测试。Water/Grass/Ice只测试数据恢复与既有阻挡语义，ArenaMapView仍保留既有仅Empty/Brick/Steel支持限制；没有偷做307地形显示。

## 编译与首次工具失败（原件保留）

- 首轮误将catalog的空格命令名整体作为一个CLI参数，三个请求被Editor命令查找拒绝：`Console-Before.json`、`Compile-Request.json`、`Compile-Status.json`，产生Console seq1848～1850三条命令分发error。一次明确低风险纠正为分开的CLI token后正常。没有C#错误，不清Console，不声称实施过程0 error。
- 首轮成功编译：`Compile-Corrected-Request.json` / `Compile-Corrected-Status.json`，completed / failed=false / compilationFailed=false。
- 新增3项测试的最终编译：`Compile-Final-Request.json`已触发；`Ready-Final.json`碰到Reloading Domain短暂对话框，`Compile-Final.json`真实PIPELINE_UNREACHABLE。一轮有界8秒等待后 `Ready-After-Final-Reload.json` / `Compile-Final-After-Reload.json`恢复ready与completed / failed=false / compilationFailed=false。没有关对话框/重启/升级或持续恢复循环。
- 一次本地Python读取catalog用系统GBK导致UnicodeDecodeError，显式UTF-8读取纠正；不是Unity编译/测试失败。catalog元数据见 `Compile-Catalog.json`、`Console-Catalog.json`、`Eval-Catalog.json`。
- 原始全Console增量 `Console-Final-Delta.json`：seq1847→1875，28条，3 error为上面已纠正的命令名失败、0 warn、25 log；之后无新增C#或runtime error/warn。`Console-Final-Status.json`保留当前consoleErrors=3/consoleWarnings=0、compiling=false/compilationFailed=false和历史buffer error13/warn1，不伪称清零。

## 文件hash与现场

相对本lane起点，1714项既有保护hash中 **仅MapState.cs改变，其余1713项一致，无缺失**。保护清单含全部Assets/BattleCity既有文件、ProjectSettings、既有Docs与.vsconfig；BC304/305实现与测试/meta、场景/PNG/地图/波次/升级数据、相机/HUD均保持本lane起点hash。详细before/after及路径在 `Map-Audit.json`。

| 改动文件（相对工程根） | SHA-256 |
| --- | --- |
| Assets/BattleCity/Scripts/Map/MapState.cs | `479d16da5b6a582bfcedbb3e1b4a2a542a447102d2303018772800c06572c044` |
| Assets/BattleCity/Scripts/Map/BaseFortificationState.cs | `8a8448314eccc14d1a5725db8dd4655f506965ee5880484c5ac1e09a4389c434` |
| Assets/BattleCity/Scripts/Map/BaseFortificationState.cs.meta | `4a8e780249f73e327f74560ec346c29d3a195ab0234242efcb7a2b877682f0bb` |
| Assets/BattleCity/Tests/EditMode/BaseFortificationStateTests.cs | `6237d66ae90aff067d3a301b00b60295b59c9d2d8f7664c370352dda652c8360` |
| Assets/BattleCity/Tests/EditMode/BaseFortificationStateTests.cs.meta | `1fbb7349ab9933ce6037ec3467f7b55807c6b3268ab1497b73bca97f36ae08a8` |

两个meta由Unity Editor导入生成，未手写YAML。`MapState.patch`仅16行新增精确接口，既有弹伤/碰撞逻辑未改。`git diff --check`通过，`Diff-Check.txt`为空（终端LF/CRLF策略提示不算Unity告警）；`Staged-Files.txt`为空，未暂存。Git检查中UserSettings/HierarchyWindow.settings曾瞬时出现行尾策略提示，紧接只读diff/status均无差异；没有手工覆盖或恢复用户设置。

`Final-State-After-Tests.json`：Stage01 stopped / not paused / clean，runInBackground=false，compiling=false。未进Play、无临时观察器/输入设备/后台设置覆盖；投影测试的临时GameObject/Tile/Sprite/Texture在finally中销毁。原历史Console保留。

后续owner先读本API与305的Verification tick接缝再接线，不重写本地图算法。CLI skill精确路径 `H:/Projects/Unities/unity-agent-plugin/skills/unity-cli/SKILL.md`，本轮CLI使用前完整读取skill与 `references/integration-advanced.md`；不需要本lane重跑不相关全量或既有成功项。
