# BC-108 回归验证（已验收，M1通过）

## 当前裁定

主代理已读取实现交接、检查新增测试和既有 PlayMode 等待修补，并核对最终结果与测试发现集合。用户收到本轮5项人工检查清单后确认：“经过了一轮人工检查，感觉非常不错”。结合自动218/218、运行/Console证据与本版本整体人工反馈，**BC-108已验收，阶段一9/9完成，M1通过**，允许沿原授权进入阶段二。人工证据为用户整体确认，没有独立逐项测量报告；不扩展为三档硬件fps性能基准、独立构建或长期压力测试通过。

工程基线为 `master@8e91090` 加未提交的已验收 BC-107 改动；本轮只修改/增加测试与证据，不改生产逻辑、地图、参数或场景。未暂存、提交、推送。

工作流 `77bfb8f5-ccc6-4712-9f2e-728069b2dba5`；实现子任务 `69979f3c-7f5f-451e-8d66-45f59c8624bf`。完整子代理报告存于运行管理的 `bc108/validation.md`；本文件为工程内持久摘要，下面证据路径均相对于本目录。

## 全量测试

| 模式 | 通过/总数 | 失败/跳过/不确定 | 原始结果 |
| --- | ---: | --- | --- |
| EditMode | 185/185 | 0/0/0 | EditModeFinalResults.json |
| PlayMode | 33/33 | 0/0/0 | PlayModeFinalResults.json |
| 合计 | **218/218** | **0/0/0** | CoverageCounts.json |

最终两次运行均无筛选；`CoverageCounts.json` 记录发现集合与实际执行集合完全一致，无 missing/unexpected。全量指当前工程发现的 BattleCity 两个测试程序集，不包含未启用的 UPM 包内部自测或独立构建测试。

覆盖既有 65 项转向回归、边界、扫掠/目标过滤、砖墙局部破坏和显示、武器、输入、特效及重载清理。新增 6 项模拟渲染 delta 的战斗一致性测试和 1 项真实 Update 暂停恢复/键盘射击钢墙接入测试。

## 帧率一致性结论边界

30/60/144 档为**模拟渲染 delta 的确定性测试**，不是硬件稳定达到这些实际 fps 的性能实测。三档同等时间/输入下均无丢弃 tick，并得到一致移动、冷却、弹数限制、边界回收/复用、破砖 revision/残墙和命中特效年龄。

例：持续移动开火 1 秒，tick=60、移动60px、第二弹位置(101,24)/age3、cooldown13；另有破砖→钢墙矩阵在10/20/30/40/50/60 tick逐次检查非空中间状态。源码见 `Assets/BattleCity/Tests/EditMode/RenderDeltaCombatTests.cs`，日志采样见 `CombatSamples.json`。

本轮据此接受逻辑速度/计时一致性证据，不声称完成三档实测性能基准。Windows 独立构建和长期压力测试不在本轮完成范围。

## 实际运行与 Console

- 真实 Play Mode 帧数持续改变；运行样本 tick1490→1750，Input System 事件经生产 Update 驱动移动、射击和破砖。
- 暂停 frame13148→24588，tick保持1750，冷却、地图和特效年龄冻结；恢复约0.9734秒推进58tick、DroppedTicks=0，未补算此前暂停时长。
- 真实键盘测试是 Input System 状态事件链路，不能冒充用户物理键盘手感确认。
- 最终编译 `completed`、`errors=[]`。Console 基线 cursor273 到394、同session新增 error entries 为空，无丢弃；最终当前 Console error/warning均0。历史buffer 71条错误来自基线前，保留且不解释为本轮新增。
- 入口证据：`RuntimeFrameProof.json`、`RuntimeMoving1.json`、`RuntimePaused1.json`、`RuntimePaused2.json`、`RuntimeResume.json`、`RuntimeResumedSample.json`、`ConsoleFinalNewErrors.json`、`ConsoleFinal.json`、`CompileWaitFixResult.json`。

## 失败与修正记录

1. 新增 EditMode 用例首轮3项失败：第二弹精确位置/年龄的手算预期晚了1tick。核对既有“tick末接触即命中”契约后修正本轮新测试的精确预期，保留全部断言，不改生产逻辑。原始 `EditModeInitialResults.json` 保留。
2. 既有 PlayMode 键盘用例首轮失败，单独重跑通过：原仅等待2个渲染帧，不保证经过1个60Hz tick。修改为最长1秒等待tick实际前进，并新增tick前进断言，未移除原弹数/朝向/显示/回收等断言。完整重跑最终218/218；原始 `PlayModeInitialResults.json` 保留。
3. 一次CLI参数位置校验失败、一次临时Python路径脚本失败，均未提交有效测试或改变生产资源；调整后成功，保留 `Commands.jsonl`，不算作成功命令。本轮未出现连接中断、协议切换或编辑器重启。

## 用户人工检查（约5分钟）

打开 `Assets/BattleCity/Scenes/CombatArena.unity`，进入 Play 并点击 Game 视图。WASD/方向键移动，Space射击，Esc暂停/恢复。需要恢复地图时退出后重新进入 Play；当前没有游戏内完整重开键。

1. **贴墙转向**：贴着砖墙、钢墙及墙角，向墙移动后改按左右两侧方向。应正常对齐后转向，不长期无响应、不穿墙、不突然传送；重点复查旧的侧向无响应问题。
2. **通道和边缘**：16px及24px通道可通过，8px窄缝不能挤入；四边贴边转向不越界、不跳动。试验场中左16px通道位于x64..80，中间8px缝位于x120..128，右侧24px通道位于x168..192。
3. **残墙和通行**：出生点沿左侧向上分次射击砖墙。残墙仍挡车；逐层打通足够车宽后可通过；邻墙保留；钢墙挡弹但不破坏。
4. **开火节奏**：空地、近砖墙、近钢墙分别点射/持续射击、边走边打；朝向和炮口正确，不异常多弹或留弹。默认冷却约0.25秒且在场上限1，远处子弹未回收时射速更慢属于预期。
5. **暂停和失焦**：移动/子弹/特效出现时Esc暂停数秒，松开移动和Space再恢复、重新输入。应冻结且恢复不补走补发；切出窗口自动暂停，切回需手动Esc恢复。

请逐项回报通过或具体异常即可，不要求用户重跑218项。此前爆炸外观的初步确认及BC-102旧反馈不替代本轮完整人工手感验收。

## 最终环境与限制

最终编辑器ready、非Play、未编译/更新，CombatArena活动场景dirty=false，后台运行false；保留启动已开启的16ms auto-tick。截图临时树通过AssetDatabase清理，测试输入设备已移除。`.vsconfig`及BC-107改动保留。

`UserSettings/HierarchyWindow.settings` 状态曾显示M但没有内容diff，worker未扩大清理或修改索引；主代理同样核查其numstat为空。`git diff --check`无空白错误，仅LF→CRLF提示。

最终人工反馈已收到并接受，人工门禁关闭；后续若出现新异常仍按复现修补和受影响回归处理。下一步已更新为委派BC-200第一关可交互数据，BC-108/M1通过不代表M2或第一关闭环已完成。
