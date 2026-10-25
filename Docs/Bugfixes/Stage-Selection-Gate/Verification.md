# 选关入口仅限首次开始或明确重新开始

## 用户要求与原因

用户要求首次开始/重新开始时可选关，正常过关后的结算与续关不再允许选关。旧实现只区分 `StageIntro` 屏幕，首次开局和正常推进都进入此屏幕，因而正常通关后也能跳关。

本轮保留现有贴墙转向、草丛可见性及其他未提交变更，不提交/推送，不开展BC403。

## 当前规则

- 标题开始：允许在STAGE提示界面上下/W/S选关。
- 战斗中按R重新开始：回到当前关提示并重新允许选关（沿用现有R重开当前关的语义）。
- Game Over或全部通关后的PLAY AGAIN：重新开始Campaign，允许选关；返回标题后再次开始也允许。
- 正常胜利：结算界面不能选关，确认后固定推进Campaign下一项。下一关STAGE提示界面不显示选关文字，上下/W/S不能改关号、Session或地图；Enter/Space仍可开始下一关。
- 战斗/暂停中仍不允许选关，长按、同帧输入、失焦抑制沿用既有规则。

## 实现

- `SinglePlayerFlowState.cs`：增加 `CanSelectStage` 只读能力；首次开始/明确重开开启，开始战斗/正常推进关闭。只有有选关能力的提示界面才发出导航动作。
- `ArenaController.cs`：`SelectStage` 同时检查该能力，避免绕过UI后直接调用API选关。
- `SinglePlayerFlow.cs`：根据能力显示或隐藏选关说明，将能力纳入显示缓存条件。
- 未改场景、关卡数据、Campaign资产、输入绑定或显示布局。

## 自动验证

Unity 6000.6.7f1，连接ready。编译成功见 `Compile.json`。

| 套件 | 结果 |
| --- | --- |
| StageSelectionTests | 14/14 EditMode Passed（原7项＋新增7项） |
| SinglePlayerFlowTests | 6/6 EditMode Passed |
| CampaignPlayModeTests | 1/1 PlayMode Passed（增强既有双关真实Update用例） |
| SinglePlayerFlowPlayModeTests | 3/3 PlayMode Passed（增强Game Over重玩断言） |
| 合计 | 24唯一Passed：20 Edit＋4 Play |

新增/增强断言包括：

- 四种导航键在正常推进提示界面均不产生选关动作，方向＋确认仍可启动下一关。
- 实际双关受控弹击胜利后，结算API拒绝选关；进入第二关提示后，四键及正反向直接API都不能切关，保持相同Session、关号23、tick0、标题STAGE23和隐藏的选关说明。
- R重开再次显示选关说明，真实上下按键可在两关间切换，随后正常启动和完成最后一关。
- 全部通关/失败后的PLAY AGAIN恢复能力；初次选关35关循环、失焦、长按、确认隔离等既有真实键盘用例继续通过。
- 首轮编译与上述测试全部通过；纯状态与实际输入的原始结果见同名JSON。受控击杀不冒称自然通关。

命令为 `unity command test run --caller plugin --skill unity-cli --mode editor|playmode --filter <套件名> --format json`。Play用异步请求并以 `test status` 获取最终结果；不合并筛选，不重跑35关地形及已通过的无关测试。

Console cursor2600→2637，37条新增全为log，最终编辑器Console错误0/警告0、compilationFailed=false，见 `Console*.json`。原缓冲历史错误保留，未清Console。

收尾：`SinglePlayer.unity`，非Play/非暂停、场景clean、runInBackground=false，见 `Editor.json`。6项本轮源码SHA256与24唯一结果对账见 `Audit.json`。

## 人工确认

尚无本轮用户试玩结论或截图，不标记人工通过。用户可确认：初次开局仍能选关；正常通关后提示下一关但不再有选关说明；按R或终局PLAY AGAIN后重新能选关。无需重复全量测试。
