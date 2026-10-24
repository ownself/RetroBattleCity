# BC205 fresh 只读审查

## Review

- **审查范围**：先读 `Contract.md`、`Model-Handoff.md`、`Recovery-Decision.md`、`Verification.md`，并读取恢复交接 `integration.md`。随后核对工作树 Assets diff、全部新增生命模型/测试、相关既有实现，以及最终原始测试、运行观察、Console、设置恢复报告和三张截图。
- **执行约束**：本审查仅使用文件读取、搜索及只读 diff；未修改项目文件或 Unity 状态，未启动编译、测试或新的运行观察，未写 Acceptance、勾选计划、提交或推送。

### Correct：实现与契约一致

| 检查项 | 证据与结论 |
|---|---|
| **D+60、R+120、R+121** | `Assets/BattleCity/Scripts/Core/PlayerLifeState.cs:66-84`：死亡分支建立等待后立即返回；后续 dead Step 才递减并尝试出生；成功出生建立完整保护，不在 R 当 tick 递减。`ArenaSession.cs:235-240` 在伤害阶段读取保护、伤害后调用模型。`PlayerLifeIntegrationTests.cs:55-79` 通过真实共享敌武器建立 D=37，断言 D+59=96、R=97，以及连续120次保护命中被消费、218=R+121再次死亡。没有发现 off-by-one。 |
| **3→2→1→0、多弹幂等、一次耗尽通知** | `PlayerLifeState.cs:66-81` 每代只在 alive→dead 时扣命，耗尽后不再出生；`ArenaSession.cs:225-249` 完成全部伤害和出生阶段后才通知，不提前中断伤害循环或冻结世界。`PlayerLifeIntegrationTests.cs:81-94,227-240` 覆盖同 tick 多弹、其他坦克/基地同 tick 死亡、三代扣命、一次事件和耗尽后继续推进时钟。 |
| **保护实际消费敌弹** | 玩家仍进入正常 `BulletTarget` 集合；保护只跳过 HP 处理（`ArenaSession.cs:214-236`），没有过滤扫掠目标。`BulletWorld.cs:61,84-89` 正常消费并释放命中弹。专项逐 tick 检查敌弹 lease 归零；独立运行原始记录还保存了 tick68、owner2/Bullet3→target1 的真实命中。 |
| **统一出生阻挡、玩家先于波次** | `ArenaSession.cs:142-148,175-183` 使用统一完整16px `Map.CanOccupy`，加入活坦克、基地（含废墟）及敌出生预留；死亡玩家不占位。地图边界、残砖、钢、水由 `MapState.cs:81-90,102-119` 统一处理。玩家生命 Step 明确早于 `EnemyWave.Step`（`ArenaSession.cs:240-247`）。新增测试覆盖16px边缘、受阻后保持 ready、释放后出生、敌预留/活敌阻挡及玩家先占出生点（`PlayerLifeIntegrationTests.cs:138-199`）。 |
| **稳定身份、快照及复位** | `ArenaSession.cs:55,182` 使用会话创建时出生坐标快照，并复活原玩家。`TankState.cs:35-45` 恢复初始HP、清命令和通用保护，不替换 Motor/Weapon；`TankMotor.cs:74-80` 恢复出生位置、朝上、履带0、静止。`PlayerLifeIntegrationTests.cs:96-126` 检查 PlayerTank/Motor/Weapon 引用、ID1、其他坦克引用与后续ID分配，并修改作者配置验证快照仍生效。 |
| **冷却、旧弹、lease与敌弹保留** | `WeaponState.cs:48-53` 先按 Owner 真实释放旧弹，再检查实际计数为0及清冷却；没有直接把 ActiveBullets 清零。`BulletWorld.cs:183-186,195-204` 每发均走既有 owner lease 释放路径。该操作仅发生在成功复活回调，不在死亡伤害循环发生。`PlayerLifeIntegrationTests.cs:96-136` 覆盖玩家旧弹回收、敌弹及敌武器计数保留、冷却就绪、参数快照，以及最后一命旧弹继续存在并正常释放。 |
| **死亡输入、多 tick、暂停/失焦** | `ArenaSession.cs:152,188-190,240-245` 同时限制死亡采样、死亡 tick 命令及复活后的 held/pressed 残留；现有 `PlayerInputBuffer.cs:29-45,61-67` 保持释放后再按的重装口径，Pause 边沿独立返回。新增测试覆盖死亡期间零 tick 新按、多 tick 帧跨死亡/复活、持续按住不重放、释放再按和 Esc（`PlayerLifeIntegrationTests.cs:201-225`）；暂停/失焦冻结与手动恢复见 `:242-253`。 |
| **Stage01实际启用、旧构造兼容** | `Stage01Authoring.cs:57` 与 `Scenes/Stage01.unity:158` 均显式启用；`ArenaSession.cs:49-56` 末尾参数 null 默认禁用。新增 Edit 测试覆盖旧2/3/4参数构造；Play 测试直接加载实际 Stage01，另加载 CombatArena 验证生命周期禁用且无盾对象（`PlayerLifeIntegrationPlayModeTests.cs:31-81`）。未见通过全局关闭生命周期绕过集成门禁。 |
| **盾只读投影与清理** | `ArenaController.cs:42-48,189-206` 使用既有盾资源，动画只由保护剩余 tick 派生，位置跟随玩家显示；无独立计时或物理组件。`ArenaController.cs:81-109` 在禁用/销毁时清显示并销毁所属盾对象。Play 测试检查位置、资源、无 Collider/Rigidbody、暂停时 sprite 不变、到期清空及仅销毁 Controller 后盾不残留。 |

### Correct：旧断言没有被削弱

Assets diff 中唯一既有测试修改是：

- `Assets/BattleCity/Tests/PlayMode/Stage01PlayModeTests.cs:123` 为基地 renderer 查询增加 `r.sprite != null`。
- 基地唯一性仍由 `Single` 保证；出生坐标、完整砖、地图 revision、基地位置、真实砖破坏及重载断言均保留。
- 原失败报告 `Integration-Full-Play-Status-01.json:425-429` 确为此查询对新增空 sprite 盾 renderer 解引用。该修改是最小空值保护，不是删除断言、忽略测试或改用隔离场景掩盖失败。

### Correct：原始运行、截图与 Console 相互吻合

直接核对 `Recovery-Runtime-Observed.json` 的关键原始段，而不只采用 summary：

- `:922-971`：frame44、D4死亡，生命2、等待60，玩家/盾隐藏；到frame149仍停在tick4、等待60。
- `:12518-12567`：tick63仍死亡、等待1；frame594、R64出生，保护120，玩家与盾可见。
- `:12570-12593`：焦点 handler 返回后，到frame685仍tick64、保护120、同一盾 sprite，保持暂停。
- `:37998-38073`：tick183保护1；tick184保护0、盾 disabled 且 sprite=null，玩家继续存活。
- `:38076-38091`：记录致死弹及保护期间命中弹的 owner、BulletId、target。
- `:38150-38177`：三张截图分别对应 frame46/tick4、frame596/tick64、frame1665/tick184。

已实际读取三张 PNG：出生点依次表现为死亡特效且无玩家、黄色玩家带盾、黄色玩家无盾，与记录一致。另核对本地安装包 `CaptureCommands.cs:63-68,239-253`，观察 recipe 调用的确为 screen 编码实现，不是另用场景相机伪造画面。

`RecoveryRuntimeProbe.cs` 只安排共享敌坦克及真实武器命令，由生产 `Controller.Update` 推进；未见手动调用 `AdvanceFrame/Step` 或写 HP/生命/时钟冒充运行。

Console 原始增量也与报告一致：

- `Recovery-Final-Console-All-Integration-Delta.json:700-714`：480→556共76条，dropped/reset均false。
- 新增 error481是已修 NUnit 编译错误；warning556是停止时 Already in edit mode。
- 同一增量中保留了原59/60失败和最终60/60通过记录（`:549,675`），未用最终摘要覆盖失败历史。

### Correct：测试与恢复证据

- 原始发现报告为419；原始 Edit 结果为359 Passed，最终异步 Play完成结果为60 Passed。已交叉核对原始 FullName 列表，包括全部新增专项及名单尾部，未见名单矛盾；原始结果搜索未发现 Failed/Skipped/Inconclusive/NotRun/Ignored。
- `Recovery-Test-Reconciliation.json:2-16` 保存419执行、419唯一 FullName，missing/unexpected/duplicates/notPassed均为空，并明确恢复未重跑。
- 最终 Play 启动报告中的 Total0 是异步“已启动”，没有被当成通过；通过依据来自完成状态报告。
- `Integration-Diagnostic2-Log.txt:205-229` 有修补后实际 PlayModeTests 构建成功及程序集重载，另有 completed/failed=false 状态；HTTP400请求没有被冒称编译成功。
- 原集成入口、恢复入口和最终设置报告相符：background=false、recent键不存在、autoTick=true/16ms、持久键不存在，Stage01 clean/stopped，无编译失败、observer或临时配置。`Recovery-Runtime-Cleanup.json` 记录 observer/keyboard 清理；`Recovery-Protected-Final-Comparison.json` 保存259文件无变化/丢失。
- 原4533 DroppedTicks仍见 `Integration-Runtime-Death.json:29` 和新观察的 `priorRunDroppedTicks`；新短路径0没有被写成性能达标。45分钟 timeout、未知HTTP400及停止前已退出Play原因均有明确保留，未据此推导功能通过或失败。

### 能力与覆盖边界（非缺陷）

1. **本会话未独立重算 SHA-256 或用程序重新执行 JSON 集合运算。**当前审查工具只有读取/搜索/diff，没有可用的只读计算执行工具，且审查规则禁止 shell。本次核对了64文件 manifest、模型交接 hash、结果关联说明和恢复比较报告，但不能将其描述为“reviewer 新鲜重算64/64通过”。父代理此前独立重算的记录与恢复报告属于已有证据。
2. 本次没有再向 Unity 请求实时状态；设置恢复结论限定在已保存的原始 API 报告时点。审查本身未改变现场。
3. 精确 R+121再次受伤由自动专项覆盖；恢复独立画面流程止于R+120结束，没有冒称又观察了一次R+121致死。
4. 地图阻挡结论包含复活接缝调用统一查询的源码证明及既有地图回归，不是新增复活测试逐一构造所有残砖/钢/水组合。
5. 仍未替代用户人工BC205体验、实际OS焦点切换、自然敌军长期运行或性能验收；BC206胜负/重开/HUD不在本项范围。

- **Fixed**：无；本次为严格只读审查。
- **Finding**：

No issues found.

未发现可由当前BC205 diff证明引入或变得可达的具体P0/P1/P2问题。上述能力和体验覆盖边界保留为审查说明，不提升为无证据缺陷。

**Merge verdict: OK with notes**