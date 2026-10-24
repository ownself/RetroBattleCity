# BC-205 integration verification（worker验证历史，不是 Acceptance）

> 最新主代理状态：**BC205已验收，阶段二6/10，M2未通过。** fresh审查OK with notes，无具体缺陷；主代理419原始集合/当前hash/运行端点/截图/Console核对完成，见`Parent-Check.md`与`Parent-Final-Audit.json`。用户明确总共出场3次及复活护盾有效，人工计数疑问已解决，见`Manual-Check.md`。正式结论见`Acceptance.md`；按用户要求不启动BC206。下文保留worker交付时记录，“待审查/未人工体验”等描述以本提示和验收文档为准。

## 状态 / 范围

**ready for independent review**。BC205 集成实现及本轮剩余自动验证已完成；父代理保留验收/总计划勾选权。没有进入 BC206 胜负、重开、HUD 或阶段三。未暂存、提交、推送、发布，也未创建 PR。用户既存 `.vsconfig` 保留。

基线 HEAD：`d010f12f1a814d3595f8c065eedd7732475cddc7`。模型 owner 的四份源码/测试及 Editor 生成 meta 原样消费，未重写生命策略。原集成 run 在45分钟硬期限结束前未写完报告，**不能将该 timeout 当通过**；本次仅按 `Recovery-Decision.md` 补剩余观察与证据，无 Assets 改动、无重复运行24/419成功测试。原失败报告、原截图/观察和父代理 Temp/BC205-Recovery 快照均保留。

## 具体实现变更（original integration lane）

| 文件 | 内容 |
| --- | --- |
| `Assets/BattleCity/Scripts/Core/ArenaSession.cs` | 新末尾可选 `PlayerLifeParameters`，null默认禁用；快照出生位置；全部伤害后、敌波次出生前恰好一次消费既定 `PlayerLifeState.Step`；完整16px Map.CanOccupy + 活坦克/基地废墟/敌预留查询；玩家同对象原子复活；耗尽只锁存请求/一次事件，不冻结整局；死亡/复活SuppressHeld且死亡采样不缓存输入。 |
| `Assets/BattleCity/Scripts/Tanks/TankState.cs` | 快照初始HP，最小internal复活接缝，复用同Motor/Weapon和稳定ID1，清命令/通用保护。 |
| `Assets/BattleCity/Scripts/Tanks/TankMotor.cs` | internal复位出生位置、朝上、履带0、静止。 |
| `Assets/BattleCity/Scripts/Combat/WeaponState.cs` | 仅成功复活时释放该武器旧弹lease，再断言真实计数0并冷却0；不直接把计数清零。原武器配置快照不变。 |
| `Assets/BattleCity/Scripts/Combat/BulletWorld.cs` | 仅按Weapon引用释放其活弹，每发都经过既有ReleaseAt/Owner.ReleaseBullet；不清敌弹/全世界，不在伤害循环截断旧弹。 |
| `Assets/BattleCity/Scripts/Core/ArenaController.cs` | 显式enablePlayerLifecycle；死亡隐藏玩家；同一显示投影附属只读盾SpriteRenderer，使用effect.shield.0/.1，动画仅派生逻辑保护剩余；死亡/结束/禁用/销毁无残影，无新Collider/Rigidbody/UI/独立计时。 |
| `Assets/BattleCity/Editor/Stage01Authoring.cs` | Stage01 recipe显式启用生命周期。 |
| `Assets/BattleCity/Scenes/Stage01.unity` | 由现有Editor API设置并保存enablePlayerLifecycle=true，仅一行序列化变更；没有手写YAML/meta。CombatArena不变、默认禁用。 |
| `Assets/BattleCity/Tests/EditMode/PlayerLifeIntegrationTests.cs` + meta | 新15个用例（含参数化），见下。meta由Editor导入。 |
| `Assets/BattleCity/Tests/PlayMode/PlayerLifeIntegrationPlayModeTests.cs` + meta | 新2个真实Update/默认场景兼容用例。meta由Editor导入。 |
| `Assets/BattleCity/Tests/PlayMode/Stage01PlayModeTests.cs` | 旧基地renderer选择predicate增加sprite!=null，避免对新inactive shield空sprite解引用；旧基地唯一性、位置、砖与重载断言全部保留。 |

接手前父代理已有总计划、Continuation-Handoff、Execution-Board、BC204 Manual-Check文档变化；不是本worker修改，不回退、不勾选。模型新增8文件也是上游，不归本lane冒领。恢复只新增Docs证据和未导入的 `RecoveryRuntimeProbe.cs`，保留原 `IntegrationRuntimeProbe.cs`，不在Assets放临时probe。

## 编译与专项 / 全量执行集合

### 真实编译

- `Integration-Compile-Final.json`：实际Editor recompile completed，failed=false、compilationFailed=false、errors0/warnings0；`Integration-Compile-Final-Settled.json`再次确认完成/无错误。
- 后续唯一源码变化是旧Play测试sprite空值predicate保护。其显式compile请求收到HTTP400（见失败历史，不能把该请求当成功）。经父代理批准的只读诊断，`Integration-Diagnostic2-Log.txt`记录该修补后的Tundra build success及successful assembly reload；`Integration-Diagnostic2-Compile-Status.json`为completed/failed=false/compilationFailed=false；`Integration-Compile-Loaded-Final.json`确认已加载真实Runtime/PlayModeTests程序集和当前源码。
- `Integration-Validated-Source-Hashes.json`在最终Play重跑前记录64份源码/asmdef；父代理独立重算一致。本次 `Recovery-Source-Baseline.json`、`Recovery-Source-Final-Comparison.json`再次64/64一致，无漂移。恢复没有修改Assets，不重复机械编译。
- 本次Docs observer仅通过既有 `script run` ephemeral编译：305ms、diagnostics=[]；这不是生产程序集编译的替代品。

### 专项

- 模型24项原证据保留，最终Edit全量也包含这些用例，本次不重跑。
- `Integration-Edit-Special-Recovered.json`原13/13通过，之后新增2个更强Edit用例包含在全量15项集成用例中。
- Edit覆盖：真实共享敌武器死亡；绝对D37/D+59=96/R97/R+120=217/R+121=218；保护弹消费；同tick多弹仅扣1且其他坦克/基地伤害继续；三代3→2→1→0和耗尽一次事件/不冻结；占用ready等待/释放、16px边缘与尸体不占位、敌出生预留及玩家出生先于新波次预留；旧引用/ID/HP/朝向/履带/武器快照；只释放玩家旧lease且敌弹保留/最后一命旧弹继续；死亡新按、零tick渲染采样、多tick帧、释放再按与Esc；暂停/失焦冻结/恢复无补算；旧2/3/4参数默认不启用。
- `Integration-Play-Special-Status-01.json` 2/2通过：默认Stage01真实Controller.Update经共享敌Weapon致死、玩家隐藏、复活盾/保护过期与pause/focus handler冻结、引用稳定与销毁清理；CombatArena明确不启用。

### 全量，不拿摘要代替FullName集合

| 执行证据 | 发现/执行 | 结果 |
| --- | --- | --- |
| `Integration-Test-Discovery.json` | Edit359 + Play60 =419 | 全部发现，非历史发现冒充新执行 |
| `Integration-Full-Edit.json` | 359 | 359 Passed，0 Failed/Skipped/Inconclusive |
| `Integration-Final-Play-Start.json` + `Integration-Final-Play-Status-01.json` | 60 | 60 Passed，0 Failed/Skipped/Inconclusive |

`Integration-Test-Reconciliation.json`和本次重新计算的 **`Recovery-Test-Reconciliation.json`**：419发现=419执行=419唯一FullName；missing/unexpected/duplicates/notPassed均[]。原Play59/60失败结果保留、不覆盖。

`Recovery-Test-Source-Association.json`给出各执行对应源码hash：最终Play前已保存64份manifest；Edit359成功后到该manifest仅Play测试guard改变，Runtime/模型/Editor/Edit测试均未改变。因此保留原Edit成功结果，不谎称manifest在Edit运行瞬间产生。当前64份hash全部匹配，模型owner原hash亦未变。

## 独立真实Update画面观察（本次新鲜证据）

入口：**`Recovery-Runtime-Observed.json`**（1464条原始frame/tick采样）、`Recovery-Runtime-Summary.json`、`Recovery-Runtime-Probe-Request.json`、`RecoveryRuntimeProbe.cs`。

这是受控自动观察，**不是人工体验/自然无安排游玩**。使用实际Stage01及生产Controller.Update，不手动AdvanceFrame/Step，不写HP/生命/LogicClock/Time.maximumDeltaTime；只加入一辆受控共享敌坦克（ID2，参数clone后销毁作者对象），其真实Weapon产生敌弹；虚拟键盘为空，默认波次照常继续（后续ID3也有真实开火）。手工加入的敌人没有冒充自然波次视图。

先在Editor暂停状态进入新鲜Play，观察recipe安装取消hook、90s deadline/finally；解除Editor暂停但保持**会话暂停**，让真实Update从frame1到11温机，tick仍0，再开始战斗，避免启动前CLI等待成为首次巨大delta。整个流程单次自动运行，不每阶段CLI往返、不重试。

| 生产路径事实 | 原始观测 |
| --- | --- |
| warmup | frame1→11，tick0、会话paused，生命3 |
| 真实敌弹致死 | enemy owner2 / BulletId1 / target1，frame44、D=4；生命3→2，wait60；玩家/盾均隐藏 |
| 暂停冻结 | frame44→149、tick4/wait60均不变；没有补算 |
| 等待与复活 | 后续真实Update推进到R=64，恰好D+60；同PlayerTank/Motor/Weapon，生命仍2，玩家可见、盾可见、保护120 |
| 失焦/聚焦冻结 | 调生产OnApplicationFocus(false/true) handler，frame594→685仍tick64/保护120/同盾sprite且保持暂停；需手动SetPaused(false)恢复。**没有冒称真的切换OS焦点。** |
| 保护实际消费敌弹 | owner2 / BulletId3 / target1于tick68命中，敌武器lease释放，玩家HP1且生命2保持；本次确实观察到敌弹飞行，不只观察最终HP |
| 保护结束 | tick183仍保护1；tick184=R+120后保护0、盾disabled且sprite=null、玩家存活可见；与专项R+121首次可伤语义一致，不声称本次独立流程又射了R+121致死弹 |
| actual frames/ticks | frame1→1665，战斗tick0→184，真实Update持续推进，不是只有Play状态或静态截图 |

### 三张新截图（已读取图像检查）

直接调用同一安装包 `CaptureCommands.EncodeScreenToPng`（即发现的capture game_view/source=screen编码实现）读取GameView实际render target；在会话暂停至少2个真实渲染帧后写PNG到Docs，避免capture命令的Assets相对路径/AssetDatabase导入副作用。没有回退到桌面图/旧图，也没有创建临时Assets图片/meta。

- **`Recovery-DeathHidden.png`**：frame46/tick4，出生位置只见死亡特效，无玩家坦克/盾。
- **`Recovery-RespawnShield.png`**：frame596/tick64，出生点黄色玩家及可见盾。
- **`Recovery-ProtectionExpired.png`**：frame1665/tick184，同玩家仍在、盾消失。
- 图片/recipe SHA-256：`Recovery-Recipe-Screenshot-Hashes.json`。截图均是本次生成，不使用BC204或旧结果替代。

**DroppedTicks不得遗漏：**上次受控观察第一次从长Editor暂停解除造成4533 DroppedTicks，保留在 `Integration-Runtime-Death.json` 与本次报告priorRunDroppedTicks字段；此前无控制默认Play跑到耗尽也单独保留 `Integration-Runtime-Uncontrolled.json`。本次温机后的短受控路径DroppedTicks=0，不能据此声称性能合格或抹掉原4533，更不是人工体验通过。

## Console、设置、资源与清理

### 基线/最终

- 原入口：`Integration-Baseline-Files.txt`、`Integration-Baseline-Console.json`、恢复后的 `Integration-Baseline-Settings-Recovered.json`。
- 本次入口：`Recovery-Baseline-Status.json`、`Recovery-Baseline-Console.json`、`Recovery-Baseline-Settings.json`。原PID26924/端口7800/Unity6000.6.7f1/CLI1.0.0-beta.14不变，无升级/重启/强关。
- **`Recovery-Final-Settings-Resources.json` / `Recovery-Final-Editor.json`**：Stage01 clean/stopped，paused=false、compiling=false、failed=false、无domain reload；background=false，recent键仍不存在；autoTick仍true/16ms、SessionState interval=-1且bool双默认证明持久键不存在；无Pipeline临时配置、无Assets/Docs、无临时盾/敌显示对象；Stage01显式启用且可解析effect.shield.0/.1，实际GUID留存。
- `Recovery-Runtime-Cleanup.json`：finally已移除observer和本轮keyboard、还原backgroundfalse、会话paused。显式Cancel入口存在但正常流程无需调用；最终未留订阅。原run清理另见 `Integration-Cleanup-Observer.json`（移除1 observer/1 keyboard）及 `Integration-Cleanup-Final-State.json`，不重复误处置。
- 停止Play命令返回success但内容是Already in edit mode，产生warning556；命令前为什么已经退出Play未确定，不编造原因。其前自动观察已完整落盘并finally清理；随后当前原Editor clean/stopped已直接核实。未重新开Play/重跑观察。

### Console增量，不清Console

- 原集成基线cursor480、历史error计数2；最终同session `316dee6ba1864c7dab134a9af37d8ce3` cursor556。
- **`Recovery-Final-Console-All-Integration-Delta.json`**完整返回480之后76条，dropped=false/reset=false；新增error仅seq481为已修NUnit编译错误，新增warn仅seq556为停止时Already in edit mode；最终累计error3/warn1（包含历史）。最终groundTruth compilationFailed=false/compiling=false/consoleErrors0/consoleWarnings1。
- 本次恢复单独 `Recovery-Console-Delta.json`（555→556）：仅上述1条warning，无新runtime异常/编译错误。不能称Console零警告/所有工具零失败。

### 文件保护与差异

- **`Recovery-Protected-Baseline.json` → `Recovery-Protected-Final-Comparison.json`**：全部259个Assets/ProjectSettings/Packages文件及.vsconfig hash逐一比较，changed=[]、missing=[]。证明恢复没有修改任何生产/测试/场景/资源/meta/设置文件。
- **`Recovery-Original-Baseline-Comparison.json`**：从原集成接手hash只变化上述9个预期既有实现/场景/旧测试文件，无丢失；新2测试+2 Editor meta另列；模型与其他保护资源不变。
- **`Recovery-Source-Final-Comparison.json`**：已验证64源码/asmdef无漂移，含模型和现有测试。
- `Recovery-Implementation.diff`为既有Assets patch；新测试完整diff见 `Integration-New-Tests.diff`，hash见 `Integration-Handoff-Implementation-Hashes.json`及当前manifest。
- `Recovery-Final-Git-Status.txt`、`Recovery-Staged-Files.txt`（空）、`Recovery-Diff-Check.txt`（通过）。git仅警告Stage01工作树LF未来可能转CRLF，未强制改行尾/清用户文件。总计划既有父代理变化不在本lane改动中。

## 已知失败/警告与处理记录（原始文件全部保留）

1. 根路径Core-Rules.md读取ENOENT，随后定位并读取Docs/Stage0/Core-Rules；旧证据Runtime-Arm文件名查询ENOENT后按实际目录读Runtime-Probe-Request。只是文件定位错误，无mutation。
2. `Integration-Baseline-Settings.json`：错误假设UserSettings/UnityCliSettings.json存在，eval exit6 COMMAND_FAILED。父代理批准有界本地包检查与guarded只读恢复，真实基线随后取得；不是桥接断连。
3. 本地Python编辑脚本三引号SyntaxError，尚未写任何文件；后改targeted edits。最终hash比较脚本一次反斜杠转义SyntaxError后修正为chr(92)重新处理保存的文件；不是Unity故障。
4. `Integration-Compile-Initial.json`：NUnit Is.AnyOf不兼容CS0117，修为Does.Contain；保留原编译失败/Console481，未clear。
5. `Integration-Compile-Second.json`虽exit0/errors0仍compilationFailed=true，随后scene/test请求各EDITOR_NOT_RUNNING；原预组合shell没有在第一失败后截断，已披露。经父代理批准诊断确认原进程/成功域重载/HTTP7800恢复，readiness+真实状态failed=false后才继续，未重启。
6. `Integration-Play-Special.json`同步HTTP Play测试被协议拒绝，outer success/exit0但nested success=false、Total0；不是通过/测试失败/实际执行。父代理批准实际schema的async_tests+test status，后续所有Play用同CLI原Editor异步流程，有界且未重复提交同一run。
7. `Integration-Full-Play-Status-01.json`原59/60：旧Stage01测试空sprite解引用，最小guard修补后独立最终全Play60通过；旧断言没删除/禁用，原失败保留。
8. `Integration-Compile-Verified.json`未知HTTP400失败请求不能当成功；批准只读诊断中后续实际构建/加载成功且当前编译无失败，原因未确定，不断言一定reload；没有机械循环compile。
9. 原run硬timeout：未及时写Verification/托管最终输出。父代理核实其已成功清理后作唯一有限恢复，本次无Assets改动/不重跑成功测试。
10. 原独立观察DroppedTicks4533、初次无控制默认运行已耗尽生命，均保留；本次只一次warmup自动安排补画面，不修改时钟/计数掩盖。
11. Pipeline发现更新0.9.0-exp.1→0.9.1-exp.1仅提示，未升级；Unity/CLI未升级。
12. 本次显式停止时Already in edit mode警告1条（seq556），原因不作猜测，最终现场直接核实；不称零工具警告。

完整调用索引见 `Integration-Commands.txt`，原始JSON中保留实际code/parameters/target/嵌套结果；每个Editor命令显式带 `--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity`。只读诊断/恢复均按父代理逐次授权，不无限重试/换模式。

## 未覆盖 / 风险 / 下一步

- 本次完成冻结契约要求的自动残余验证，无已知未修产品缺陷；仍须fresh只读审查，**不能把本worker ready当父代理Acceptance**。
- 尚未进行用户人工BC205体验（操作手感/自然敌军长期运行/实际OS失焦操作）；已准确区分自动真实Update、生产焦点handler测试和人工体验。原型3/60/120等仍待校准，不声称原版数值事实。
- BC206完整胜负/同tick败优先、重开、HUD/阶段三有意未实现，本项只提供锁存请求/一次事件。
- 原4533 DroppedTicks和未知HTTP400/提前退出Play原因没有被性能结论覆盖；短受控0不等于长期性能通过。
- 建议父代理仅安排fresh独立只读审查本diff/原始证据与人工检查清单；未经新授权不继续实现或勾总计划。
