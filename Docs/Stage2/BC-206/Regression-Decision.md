# BC206 唯一最终Play回归失败：主代理处置

## 原结果与因果

只验证workflow1601cec3-ef8f-4bb3-b9f4-7e9b554789db已完成，validation child de08fb4a-a9ee-48b7-a0f3-13fdc2cd438a按要求遇失败停止，无超时恢复/重复测试。原始完整结果：407/407 Edit、62/63 Play，470发现/唯一执行集合无缺失、额外或重复；本项新3个Play用例全部Passed。唯一失败为旧PlayerLifeIntegrationPlayModeTests的生产Update死亡/复活/盾测试，在5秒WaitUntil超时。原始报告持久副本为Final-Verification.md，保留失败，不冒称整套通过。

主代理直接读测试和ArenaSession源代码，确定可达原因：测试新增StartBattle后立即给enemy设置一次FirePressed。StartBattle置transitionPending=true；下一真实Controller.Update进入AdvanceFrame的transitionPending分支，按契约ClearCommands并丢弃转换帧。因此注入的单次FirePressed在首个战斗tick前被清除，enemy不会按该请求射击，首个死亡等待无法满足。

这是旧测试接入新增开始状态的时序错误，不修改“转换帧清输入”的产品规则，也不延长等待掩盖失败。验证报告本身不能辨识是哪次WaitUntil；以上因果由主代理源码核对给出，须以最小修补后的实际复测验证。

## 唯一修补与验证边界

在PlayerLifeIntegrationPlayModeTests.cs中StartBattle后、设置敌方命令之前增加一次`yield return null`，让真正Controller.Update处理开始转换，再记录frame/tick并注入射击。无手动Step/AdvanceFrame，不伪装Update。原死亡、等待、对象引用、护盾保护/到期、暂停/失焦及清理全部断言保留，源码其余位置不改。

- 仅针对原失败测试执行一次真实async Play复测；不重跑模型专项、已成功BC206三项或完整407+63。
- 此修补只改一个测试方法，不改生产源码/资源，原407Edit和其余62Play保留成功证据。最终用mode+FullName与当前hash关联补回唯一失败项，不将多次运行混称同一单次全量通过。
- 若复测失败或有未知基础设施问题，及时停下请求人工，不重试整个集成。
- 若通过，补极短Ready/Playing真实Update观察与最多2图，然后fresh独立只读审查（含本修补及原完整增量）；主代理才验收。

用户“前述均无问题”仍只作为人工反馈，不伪装Test Runner计数。未暂存/提交、未改游戏实现、未启动207。
