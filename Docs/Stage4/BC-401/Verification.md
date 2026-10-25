# BC-401 多关加载与校验

## 结论与范围

BC401完成并通过定向自动验收；阶段四2/8、M4未通过。用户本轮仅授权401，未录入402关卡、未提交推送。

起点main/a0c9a75，仅既存.vsconfig未跟踪。CLI beta.14最新、Unity6000.6.7f1 ready，原Editor进程未重启。

## 交付

- CampaignDefinition有序引用LevelDefinition；共享宿主敌型/道具规则，不通过PNG或文件名推断可玩关卡。
- Awake前校验全部关卡，错误包装Campaign entry索引、资产名及原因。覆盖尺寸、枚举、缺失/阻塞/越界/重叠出生、非空波次、未知敌型、缺失关卡/波次、1..9999唯一关卡号、道具掉落索引及铁锹基地8px对齐。
- 捕获全部关卡的私有ScriptableObject快照和列表顺序；运行中改作者数据不改变未来加载。快照由原Controller销毁生命周期统一清理。
- ArenaController.AdvanceStage只允许Won且非更新/替换期间切到下一项；没有下一关返回false。RestartCampaign回首项，RestartSession保留当前项。同一宿主/地图/子弹/特效视图复用，不复制逻辑场景。
- 胜利结算确认→下一关提示，转换帧不推进战斗；最后一关显示CAMPAIGN COMPLETE / ALL CONFIGURED STAGES CLEARED，不循环。Game Over重玩、返回标题回首关，R只重开当前关。
- 每关生命/升级/计分独立、按初始配置重置；无战役累计分或跨关生命继承；限时效果与战斗对象清空，旧StageResult不变。项目默认见Docs/Stage0/Core-Rules.md，不声称原版精度。
- SinglePlayerCampaign.asset通过Editor API创建，仅包含Stage01；SinglePlayer场景仅新增campaign绑定。作者recipe创建缺失列表时默认第一关，不覆盖未来已有列表。不开展BC402，不将双关夹具宣称为第二关交付。

## 自动验证：28唯一最终Passed

| 集合 | 数量 | 证据 |
|---|---:|---|
| 新CampaignDefinitionTests首批 | 15 | Evidence/edit-result.json |
| 新铁锹对齐校验 | 1 | Evidence/alignment-result.json |
| 新CampaignPlayModeTests（扩展后最终） | 1 | Evidence/play-expanded-result.json |
| 原BC400纯状态 | 6 | Evidence/flow-edit-result.json |
| 原BC400真实键盘/胜利投影 | 2 | Evidence/flow-play-result.json |
| 原Session生命周期Play | 3 | Evidence/legacy-result.json |

双关Play瞬态夹具关号11/23、各1敌，与正式资产隔离。真实Input System键盘驱动完整流程及生产扫掠击杀：第一关胜利→确认加载第二关Ready→长按不跳过→HUD23/战斗tick实际推进→R当前关重开→第二关胜利/最终完成→重玩回11；再进23、真实弹击基地失败→Game Over→选择标题回11。断言同Controller EntityId、同BulletView/EffectView/MapView、Scene数量不增、旧Session冻结/子弹清空、结果不变、分数/生命/升级重置；草层与水层双向切换无旧Tile残留；Awake后改第二关数据及campaign列表不影响快照。

最后新增8px校验仅运行新增用例1/1；此前正常地图路径未变，不重复成功Play。未执行全量、用户人工试玩、自然多关通关或独立构建，不声称所有原有测试重跑。

## 失败、修正与工具记录

- 新测试使用Unity6.6禁用GetInstanceID产生2条编译error，改为GetEntityId，编译和Play通过；原compile-play.json保留。无生产编译错误。
- 一次recompile返回Pipeline HTTP400，status仍ready，单次同协议重试成功；未升级/重启编辑器。原compile-final.json及compile-recovery.json保留。
- 自动过程无测试断言失败；扩展Play前后两次成功分别保留，不重复计数。
- Console起点2283、主体结束2337，共54条（2已修复测试编译error、52log，0warn/运行时error）；最后增补检查见final-console.json，groundTruth compilationFailed=false。

## 最终现场与保护

- SinglePlayer场景clean、Editor stopped、runInBackground=false，正式列表1关/关号1；见Evidence/final-state.json。
- 原Sprites、既有Data、Stage01场景、ProjectSettings全部tracked保护文件与HEAD一致，见Evidence/Protected.json；只新增Campaign资产，并以Editor API修改SinglePlayer的一条引用。
- git diff --check通过（Windows行尾提示不属于错误）。既存.vsconfig保留；无临时场景或测试关卡落盘，无提交推送。
- 原BC400报告保持历史；该报告的单关结束文字已被本次通用Campaign完成提示替代。

## 下一步

BC402实施前需用户明确可玩关卡数量与录入来源；现有01～35参考图不等于35关交互数据。本轮不代替该范围决策。
