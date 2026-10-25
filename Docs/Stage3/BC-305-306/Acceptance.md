# BC-305 / BC-306 验收

## 结论

BC-305与BC-306通过本项验收，阶段三必选 **7/10**，M3仍未通过。不开展BC307～309、不提交/推送。

2026-10-25；基线main/bdb20b0e509276be1d4818c09638e5f419e463e4及Temp/BC305306-Before（包含BC304未提交成果）。实施/实际Unity验证由三个串行worker完成，独立fresh reviewer只读审查，均使用openai/gpt-6.1-sol；最终验收与源码/原始结果/hash核对由主代理完成，不冒称用户人工反馈。

## 交付

- Clock：默认600未来完整战斗tick，刷新不叠加；冻结当前及后来出生的全部敌方坦克AI/RNG、移动/履带、射击/冷却；仍可受击/阻挡。玩家、在途弹、特效、波次继续；暂停不耗时，玩家非终局死亡不取消。
- Grenade：新收据清除拾取时全部活敌含装甲，通过共享幂等死亡/爆炸/基础分数结算；不伪造弹伤、不触发携带者首次弹伤掉落，不伤玩家/基地/地图、不清在途弹、不杀预留/未出生。不二次Step波次，刷新当tick活敌快照，最后敌当tick胜利、失败优先，终局事件一次。
- Shovel：默认1200未来完整战斗tick，基地左/上/右8px厚U环临时Steel。首次启动快照当前地形和残砖mask，刷新不覆盖快照，到期恢复快照而非固定布局；补强及恢复阻挡格受活坦克/出生预留占用时逐格延期。不挤开/击杀/传送；最高级弹可按既有规则毁钢，重复拾取可补钢。无基地不创建隐形墙；终局/退休/重开停止计时并强制恢复、清理待办。
- 地图仍为唯一碰撞真相，精确地形API同步revision，既有Tilemap增量显示复用。配置会话快照，未加生产作弊入口/第二套时间源/BC308奖励。
- 铁锹能力要求基地中心8px网格对齐、16px足迹在界内；Stage01(104,8)符合，无铁锹能力的旧会话不扩大校验。规则皆为待校准项目默认，非已核实原版。

## 自动证据与边界

三lane执行证据按FullName去重后为 **95唯一最终Passed：89 EditMode + 6 PlayMode**，并非一次全量/首轮全绿。主代理逐项解析原始JSON并核对汇总型例外，保存于 `Parent-Audit.json`。

| lane | 自身唯一最终通过 | 证据 |
| --- | --- | --- |
| BC305 | 24：22 Edit + 2 Play（含新14 Edit + 2 Play） | ../BC-305/Verification.md、Audit.json与原始结果 |
| BC306地图组件 | 49 Edit | ../BC-306/Map-Handoff.md、Map-Audit.json与原始结果 |
| BC306集成 | 26：20 Edit + 6 Play（含新16 Edit + 2 Play） | ../BC-306/Verification.md、Integration-Audit.json与原始结果 |

集成与BC305有4项重复，24+49+26-4=95。本轮新专项合计83（79 Edit + 4 Play），其余12为相邻既有专项。

**原始失败与证据限制：** 集成首轮16项为11 Passed/5 Failed，原因是占位夹具贴边无正面积重叠；一次有界修正夹具后恢复2项有逐项最终JSON，激活3项仅保留精确请求filter和Console seq1889/1890的3 total/3 passed/0 failed完成汇总，没有逐项最终JSON。主代理核对请求/三项测试名/汇总后接受这3项为汇总级通过，不伪造逐项证据，不为了格式重复已成功测试。组件阶段3条CLI命令分发error已纠正，原件保留；不声称整个实施过程0 error。最终编译成功，无新增C#或runtime错误/警告；最后集成Console172条全log，0 warn/error。

真实运行：BC305 Input System键盘/Controller.Update拾取、AI冻结/手雷计分及重开；BC306 frame13→38、tick0→1201，真实键盘拾取/暂停/末tick恢复实际Tilemap的mask5及连续active/待恢复重开。中段明确经公开AdvanceFrame加速，非20秒自然运行。相邻304三道具/HUD及302破钢显示通过。未新增截图、用户试玩、自然通关、独立构建、长期压力、像素矩阵或BC309完整组合回归；无需用户机械补跑已成功项。

## 独立审查与主代理核对

- `FinalReview.md`：fresh只读独立review，**OK with notes**，未发现具体P0/P1/P2；未改项目/Unity、未重新执行测试或自行重算hash。
- 主代理读取最终会话/地图组件/精确写入/共享死亡/计时及关键Edit/Play断言；独立解析95项证据并去重；未重新运行成功测试。
- 主代理独立重算批次296项基线：仅7个预期既有文件改变，289项一致；另核对95个当前源码hash与12个新源码/meta hash均符合最终Integration-Audit。场景、PNG、地图/波次资产、相机/HUD、ProjectSettings、BC304其他成果均保护，不覆盖用户.vsconfig。
- `git diff --check`通过；Git原LF/CRLF提示不是Unity警告，无提交/推送。
- 主代理最终只读CLI `Parent-Final-State.json`：Editor ready、stopped、compiling=false、runInBackground=false。集成现场记录Stage01 not paused/clean，临时键盘/场景清理；无活跃writer/测试/观察器。

下一项BC307不在本轮授权范围，等待用户决定。
