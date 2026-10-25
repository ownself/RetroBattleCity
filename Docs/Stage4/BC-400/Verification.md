# BC-400 实现与验证

## 结论

BC-400 已完成当前单关范围的实现及定向自动验收；M4 未通过，BC401～407未开展。无提交/推送。

- 起点：main / `32f1237`，仅既存 `.vsconfig` 未跟踪，保留。
- Unity 6000.6.7f1，CLI 1.0.0-beta.14（版本检查为最新）。
- 入口：`Assets/BattleCity/Scenes/SinglePlayer.unity`。原 `Stage01.unity` 保持原样，仍供直接战斗与历史回归使用。
- 标题 → 关卡提示 → 战斗 → 分类结算 → 首关完成 / Game Over → 重玩 / 标题。
- 当前仅一关交互数据，胜利后明确显示 `END OF CURRENT SINGLE STAGE`，不伪装第二关或35关已交付；下一关数据/加载属BC401/402。

## 实现

- `SinglePlayerFlowState`：纯屏幕状态与确认/上下选择边沿，终局帧不消费确认，持续按住不跳屏，Enter/Space组合不产生重复确认；失焦不接受菜单输入，焦点回调抑制当前物理按键。
- `SinglePlayerFlow`：由既有Controller单点驱动，不另建Update、事件订阅或键盘读取。战斗外不推进session；转换帧调用SuppressInput，按住Space或移动键不会带入战斗。
- Controller保持未绑定流程时的历史行为；仅SinglePlayer场景显式绑定新流程。暂停沿用原会话，失焦暂停后须Esc恢复。
- uGUI WorldSpace固定256×240、PPU16，与原像素相机共用投影；opaque全屏面板覆盖HUD与战场，进入战斗隐藏面板并恢复HUD。
- 沿用PixelHudText，补全数字及本次需要的字母/选择符。没有TMP或新字体依赖。
- 本UI是键盘专用的自定义导航，不使用Selectable/Button、鼠标命中或EventSystem，不同时启用第二套默认导航/Submit。
- `SinglePlayerAuthoring.Build()`通过Editor API从Stage01派生入口并绑定。标题PNG为原素材top-left `(24,40,192,72)`派生裁剪、保留黑色；Single Sprite/Point/无压缩/无MipMap/PPU16，原始PNG与meta不变。不启用2 Players/Construction，不新增版权许可声明。
- 结算读取不可变StageResult；显示实际击杀类型、数量、敌人分、道具数量/分与总分，不重新结算奖励。

## 自动结果（14唯一最终Passed）

| 集合 | 结果 | 证据 |
|---|---|---|
| 新流程EditMode | 6/6 | Evidence/bc400-edit-result.json |
| 新真实键盘Update整合 | 1/1 | Evidence/bc400-play-final-result.json中的Passed项 |
| 新受控胜利结算投影 | 1/1 | Evidence/bc400-victory-result.json |
| 原Session生命周期Play | 3/3 | Evidence/bc400-legacy-result.json |
| 原HUD Play | 3/3 | Evidence/bc400-hud-result.json |

真实键盘整合覆盖标题→提示、长按确认不跳屏/不移动不射击、实际tick前进、Esc暂停冻结/恢复、真实扫掠弹击基地→失败→结算→Game Over→重玩重置→再次失败→上下选择返回标题。胜利测试以单敌受控会话通过生产扫掠击杀得到Won/100分不可变结果，投影出分类和完成屏；不是自然第一关通关证据。

独立短运行观察frame15131→19094，tick0→537，Playing；标题屏frame推进而战斗tick0。1280×720实际screen截图已读取核对，最终标题与提示无覆盖/缺字，见Evidence/BC400-Title-Final.png及BC400-Intro.png。非像素逐点缩放矩阵或性能基准。

## 原失败与有界纠正

1. 新测试误将long Score当ScoreState.Total，编译失败；更正为Score。该次Play发现0项，不计通过。
2. 新胜利测试调用internal Release，编译失败；更正为公开子弹/特效清理。
3. 两个新Play依次运行时，TearDown移除键盘后未置null，第二项复用已移除设备失败；置null后仅复测失败胜利用例1/1 Passed。首项原Passed保留。不声称首轮全绿。
4. 分号合并filter未发现用例，改为各类单独执行，不把0项当通过。
5. auto-tick命令误用enabled，工具拒绝且未修改；按实际enable参数纠正。之前已启用，本轮session-only设置不跨domain reload保留。

Console2235→2283共48条，2条上述已修复测试编译error，其他46条log、无warn/运行时error；最终groundTruth compilationFailed=false、Console错误0/警告0。原始失败/运行/结果均保存在Evidence，不删除原件。

## 保护、清理与限制

- 原Sprites、全部Data、原Stage01场景及ProjectSettings共保护文件均与HEAD一致，清单及hash见Evidence/Protected-Audit.json。
- 新场景由Editor API保存，不改Build Settings；Editor最终stopped，SinglePlayer场景clean，runInBackground=false，见Evidence/Cleanup.json。
- 截图工具实际落Assets/Temp，本轮3个截图已复制Docs证据后通过AssetDatabase删除；没有删除其他用户文件。
- Unity更新了UserSettings/EditorUserSettings.asset的最近使用场景项，保留而非覆盖用户设置。
- 未做用户人工试玩、自然胜利/生命耗尽的新流程全程、完整全量回归、独立构建、音频/存档或多关加载。

## 人工体验建议（非声称已执行）

1. 打开SinglePlayer场景Play，Enter进入STAGE 01，再按Enter开始。长按Enter/Space不应连续跳屏或立即射击。
2. Esc暂停/恢复，切换窗口应暂停；返回后Esc继续。
3. 失败后确认结算→Game Over，使用上下选择PLAY AGAIN或TITLE，Enter确认；分数与地图应正确重置。
4. 自然胜利后应显示分类/总分，再确认到STAGE COMPLETE，明确当前单关结束。
