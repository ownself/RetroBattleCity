# BC-300 验证与验收

## 结论

**BC-300 已完成并通过本项验收。** 四类敌人的移动/子弹速度、血量、分值、射击冷却与在场弹上限已数据化；所有交付值明确为未核实原版的默认值。没有推进 BC-301 或改动 M2 场景。M3 仍未通过。

实施/验证者：主代理直接执行并做源码自查；本轮没有 subagent 或独立审查，不将自查称为 fresh 独立审查。

基线：`main / a8daced9ef7f71708cd642deaf873dd21497eeb5`；无提交/推送。Unity 6000.6.7f1，Unity CLI 1.0.0-beta.14（检查为最新），现有 Pipeline 包未升级。

## 交付

- `Scripts/AI/EnemyBalanceDefinition.cs`：`EnemyKind` 四类型、显式目录、共享参数校验、来源完整性校验、32帧语义引用校验。
- `Editor/EnemyBalanceAuthoring.cs`：只创建缺失默认资产、保留既有调参，不切换/保存场景，不改旧绑定。
- `Tests/EditMode/EnemyBalanceDefinitionTests.cs`：34项专项。
- `Data/EnemyBalance.asset` 与 Basic/Fast/FastBullet/Armor 四个 TankDefinition，均经 Unity 编辑器 API 创建和保存，meta 由 Unity 生成；没有手写序列化 YAML。
- 参数值、单位、来源策略、与 M2 的边界及后续接入位置见 [Enemy-Balance.md](Enemy-Balance.md)。

## 实际执行结果

| 检查 | 实际结果 | 原始证据 |
| --- | --- | --- |
| Editor 编译 | completed、failed=false、compilationFailed=false | `Compile-Final.json`、`Compile-TestFix-Result.json` |
| 实际资产创建 | 四类 + 目录成功，32个 Sprite 帧引用有效 | `Authoring-Final.json`，专项资产断言 |
| BC300 专项首轮 | 34项：33 Passed、1 Failed（测试夹具错误） | `Tests-Start.json`、`Tests-Result.json` |
| 单项修复后复测 | 1/1 Passed；不重复已通过33项 | `Retest-Start.json`、`Retest-Result.json` |
| 相邻波次回归 | 30/30 Passed | `WaveRegression-Start.json`、`WaveRegression-Result.json` |
| 第一关数据回归 | 6/6 Passed，包括676格PNG比对/出生位置/保存场景绑定 | `StageRegression-Start.json`、`StageRegression-Result.json` |
| 有效证据并集 | **70个唯一用例最终状态 Passed**，不是一次70项全绿 | `Audit.json`（按原始结果顺序合并同名用例） |
| Console 增量 | 同session，cursor800→836，36条均log；新增warn/error=0；Editor Console error/warn=0/0 | `Console-Before.json`、`Console-After.json`、`Console-Increment.json` |
| 既有资源保护 | 原有Assets/ProjectSettings/Packages没有tracked修改；6项关键旧资源与HEAD比对一致（忽略CRLF） | `Audit.json` |
| 最终编辑器状态 | Stage01、stopped、not paused、scene clean | `Editor-Final.json` |

专项覆盖：类型齐全/乱序查找、空或重复类型/ID/引用、非法枚举、速度零/负/NaN/Infinity、冷却/弹数/HP/分值、无来源不得标为已核实、四类实际资产数值、32帧绑定、缺失帧、现有波次映射兼容、重复作者调用逐字节不修改资产、旧Stage01原型序列和score=0保持。

### 失败与一次有界纠正

1. 首轮缺失帧测试错误地使用 `TankDefinition` 默认玩家前缀（实际存在），因此预期异常未发生。仅将此夹具改为 `tank.missing.enemy`；没有修改生产实现或放宽断言。该项1/1复测通过，原失败结果保留。
2. 首次 `script eval` 在编译仍进行时遇到域重载 `socket hang up`，保留 `Authoring.json`。一次有界重连先确认同一Editor ready和编译completed，再调用同一API创建资产成功；不是反复重启/换工具，无持续阻塞。
3. 读取命令发现JSON时Python本机默认GBK解码失败，改为显式UTF-8；发生在解析本地输出阶段，没有Unity修改。

Console历史缓冲区自带累计error5/warn1，前后不变；不将其说成全历史0错误。测试框架把首轮失败写在测试结果中，本轮没有新增Console Error。

## 范围与未覆盖

- 没有新的场景运行行为，故没有重复 PlayMode/通关/截图/持续tick验证，也没有独立构建或人工平衡体验。M2既有运行证据不冒称本轮重测。
- Stage01仍使用20个 `enemy.basic.prototype`，没有把四类加入自然波次；新分值未累计，装甲颜色/受击/计分未实现。这些属 BC-301/308。
- `requiresCalibration=false` + 非空来源只表示元数据完整性，来源真实性仍需审核。本轮没有任何已核实原版数值。
- 未改变后台运行、输入设备、GameView、场景或项目设置；没有临时观察器/测试对象残留，也无需用户补测本项。
- 主代理源码自查及 `git diff --check` 通过；`Audit.json` 保存16个新增实现/资产/meta的SHA256，供后续接入时对账。

## 后续

下一项为 **BC-301：实现不同敌人及装甲反馈**，等待用户授权。本项完成不表示M3完成，不自动进入后续任务。
