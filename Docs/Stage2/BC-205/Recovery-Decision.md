# BC-205 集成超时：父代理核查与有限恢复决定

## 事实与保留现场

- workflow c0930f1a-52b1-49f9-a899-c2890a374cd7已failed；模型child e16003a4成功，不重跑。integration child 71a142c8-e39e-41e6-b7fe-b647ba99e041在2700000ms硬期限终止，process terminal observed，无活跃writer，fresh审查尚未启动。
- 托管输出仅timeout，Verification.md缺失；不能把超时视为功能通过或失败。子代理停止前已实际清理，Checkpoint没来得及补尾。
- 父代理直接读Integration-Cleanup-Observer：移除本轮observer1、keyboard1；Cleanup-Final-State：stopped、clean Stage01、compiling/failed=false、background=false、recent键仍不存在、autoTick=true/16ms且持久键仍不存在、observer=false、无临时Pipeline配置。父代理同CLI status再次确认原PID26924/7800 ready。无需重启或升级。
- 父代理已审阅Session与TankState/Motor/Weapon/BulletWorld接缝及旧Stage01空sprite保护diff；git diff --check通过，HEAD仍d010f12f1a814d3595f8c065eedd7732475cddc7，index未暂存。
- 父代理独立重算当前Integration-Validated-Source-Hashes.json的64源码文件，全部匹配；Model API未重写。359 Edit/60 Play原始Results及419集合报告已留存，后续主代理验收仍须逐条重算；不重复运行成功测试。
- Integration-Handoff-File-Comparison列9个预期existing实现/场景/测试变更，无丢失。原真实观察首段tick0→8已死亡隐藏，DroppedTicks4533如实保留，不归零冒称零丢帧/性能合格。
- 恢复快照：`Temp/BC205-Recovery/Tracked.patch`、`Snapshot.json`（935文件hash）、`Untracked/`（114未跟踪源码/meta/本项证据副本）。不删除用户.vsconfig、旧验收或失败原始结果。

## 决定与剩余范围

在既有阶段二实施授权内批准**原retained integration会话、同openai/gpt-6.1-sol、同CLI/原Editor**有限恢复，仍当前工作区唯一writer。不是重跑整个BC205，更不重跑成功模型/419测试。

只做：
1. 先核实上述清理与源码hash没有漂移，读取原报告/Checkpoint，不重复长发现。
2. 补齐独立真实Update死亡→60tick等待→复活可见盾→120tick保护结束、暂停冻结/恢复和画面证据。可最小修正Docs中本轮临时observer recipe，不修改Assets生产/测试/场景；若暴露产品缺陷先报告父代理。设置可按实际基线临时改并精确恢复。
3. 避免上一观察第一次从长时间Editor暂停直接解除导致大unscaledDelta：仅测试安排先在会话暂停时让真实Update温机数帧，再运行受控死亡流程；不改LogicClock/Time.maximumDeltaTime、不改HP/生命/直接AdvanceFrame冒充Update，不掩盖原4533。优先自动阶段观察/捕获，避免每段人工CLI往返耗尽时间。recipe须有取消hook和有界deadline/finally清理。
4. 保存最终Console基线→所有增量/错误分类、Editor/资源与设置恢复、当前源码/保护资源hash。写Verification.md完整命令、成功和失败、未覆盖项；报告ready仅在这些残余门禁完成。
5. 恢复完成后才运行fresh只读审查，父代理仍保留验收/勾选权；不提前BC206。

恢复中未知基础设施异常仍保留现场报告，不无限重试/切模式/重启/升级。时限30分钟、提前5分钟收尾；再遇耗时问题先交接blocked，不继续新测试撞硬期限。
