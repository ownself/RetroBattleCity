# BC206 主代理收口决策：保留成功测试，人工补画面，独立审查先行

## 已解决

原唯一Play回归失败按Regression-Decision.md修补后，精确单例async复测1/1 Passed，仅执行一次。生产源码未改；68源码/asmdef相对最终全量基线只有该测试方法的yield/comment变化。有效证据集合=原407Edit+62Play通过+修正后1Play=470唯一用例，无未解决非Passed；不是一次470全绿运行。原407/407、62/63及失败原文仍保留，不覆盖。

## 自动化画面受阻及停止

复测child119859ae-cf0e-45b9-af1e-255b7ac8f109在独立runtime观察的第一条eval误引用不存在的Session.Flow（正确为Session.Phase），eval编译拒绝；不是项目生产源码编译失败。没有得到Ready/Playing tick采样或PNG，不能拿readiness.frameCount代替逻辑推进证据。

已立即停止该临时Play，最终Stage01 clean/stopped/未暂停、compiling/updating=false、后台false、autoTick=true/16ms、SessionState两键不存在、ProjectSettings diff为空。详见Recheck-Loaded-After.json、Recheck-Ready-01.json、Recheck-Console-Increment.json。既有失败/警告及全部现场保留。

**不恢复child，不再次委派截图，不重跑通过测试。** 本次实现/验证尚有多次本地工具错误，说明交接精确性和工具调用需要改进；不将这些错误归咎Unity或隐瞒。

## 人工证据与只读审查的边界

用户已确认前述人工检查均无问题，Ready需Enter的控制疑问已解决。依AGENTS.md允许人工补齐验证，不再要求重复独立自动observer，或让用户读取内部C#属性/手动调用API。精确逻辑tick和生产Controller.Update边界由已有Edit/Play断言与实际帧日志覆盖，人工观察只承担真实游玩反馈，不能伪装逐tick测量。

仅向用户请求两张Game视图画面留档：Ready（Play后未Enter）和Playing（Enter后可见敌军时）。无需重做完整操作检查、执行代码或跑测试。收到后记录用户提供/对应版本/可见内容，不编造截图frame/tick或元数据。暂缺画面如实保留，不以旧版本图替代。

独立fresh审查是无冲突的已授权工作，可在人工补图时先行，只读当前源码/测试/全部原始证据；不必因截图工具错误阻塞源码审查。主代理做原始集合和hash独立核对，审查发现再处置。所有必要证据处置后才Acceptance/勾选。

当前BC206仍未正式验收，阶段二6/10，M2未通过；BC207不开展。不得以ready/completed代替父代理验收，不提交/推送或改动用户现场。
