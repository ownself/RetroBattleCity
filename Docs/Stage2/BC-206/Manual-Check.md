# BC-206 用户人工体验反馈

## 用户反馈

用户进入Play Mode后首先报告无法移动/射击。主代理解释BC206新增Ready状态，需要点击Game画面并按Enter开始；Enter开始、Esc暂停/恢复、R重开回Ready。当前没有开始文字提示（BC207），易造成误解。

用户随后确认：

> 确实，前面需要验证我也都测试过了，都没有问题

## 归属与范围

- 记为用户对前述BC206人工检查的正向确认，Ready需Enter的疑问已解决；不再重复要求同一轮试玩。
- 用户未提供Test Runner三项的精确Passed/Failed计数、XML或截图，因此**不将此话伪装为3/3自动测试通过**。最终完整Play回归可覆盖修补后的这三项，无需再独立重跑专项。
- 不把概括反馈扩大为逐tick测量、所有终局组合、源代码哈希核对或完整M2验收。
- 工作树基线仍main/e2383bacf3b661f930f87667ed9d6d3062780f79；主代理此次只读预检确认Stage01 clean/stopped、未暂停/编译/更新，runtime/PlayerSettings后台值false，磁盘ProjectSettings diff已为空。见`PostManual-Status.json`、`PostManual-Editor.json`。

## 剩余收口的精简安排

原实现child不恢复、不再次整项委派；源码冻结，仅做验证证据收口，然后fresh只读审查。一次最终全量Edit/async Play结果包含本次3项修补测试，避免专项后再全量的重复执行。短画面观察只补必要图片/真实Update状态；不再构造复杂长时observer或重放用户刚确认的全套体验。

最新结果：最终407Edit全部通过，Play62/63；旧PlayerLife测试开始转换帧时序问题经主代理唯一修补后1/1复测通过。有效470唯一用例通过证据已由父代理独立对账。独立截图eval误引用Session.Flow后已停止；不恢复或再自动截图，依Closeout-Decision.md只请求用户Ready/Playing两张Game画面，不重复操作测试或要求内部tick数值。fresh只读审查进行中。

若再次遇到长时间无进展或反复失败，依AGENTS.md立即停下，不消耗到硬期限，不以反复恢复继续。尚未写Acceptance/勾选；BC207不启动。
