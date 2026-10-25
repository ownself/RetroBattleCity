# 草丛可见性调整

## 用户反馈与修改

用户反馈穿过草地时坦克完全被遮挡，无法辨认自己的位置。

- `Assets/BattleCity/Scripts/Map/ArenaMapView.cs`：保留草丛在 Actors/1、坦克上方的层序；创建显示层时将 `foliage.color` 设为 `(1,1,1,0.6)`，即60%不透明、40%透明。统一作用于玩家及敌方坦克，不是仅玩家附近的局部揭示。
- 仅更改运行时草丛图层，不修改素材像素、共享材质、场景、关卡、ProjectSettings或碰撞/子弹规则；水、冰、砖、钢仍全不透明。
- 原贴墙转向补丁及 `.vsconfig` 保留不动；无提交/推送。

## 验证

Unity 6000.6.7f1；CLI 1.0.0-beta.14（检查为最新）。

- 编译成功，见 `Compile.json`。
- 在既有 `ClassicRulesPlayModeTests` 的地形分层/清除/重开用例中新增断言：草丛alpha为0.6，水冰及砖钢alpha为1，重复配置/重开后草丛alpha不被重置；保留原贴图象限、无Collider、层序、清理与无重复图层断言。
- 命令：`unity command test run --caller plugin --skill unity-cli --mode playmode --filter ClassicRulesPlayModeTests --async_tests true --format json`，以 `test status` 取得最终结果。
- **3/3 PlayMode Passed**，涵盖上述草丛显示配置/地形生命周期、真实键盘Update冰面移动/暂停、终局/重开。逐项见 `PlayMode-Results.json`。这是配置与运行回归，不冒称GPU截图对比或人工视觉验收。
- Console cursor2590→2600，10条增量全部log；最终编辑器Console错误0/警告0，compilationFailed=false。保留缓冲中的历史错误不计为本次新增，见 `Console*.json`。
- 收尾为 `SinglePlayer.unity`、非Play/非暂停、场景clean、runInBackground=false，见 `Editor.json`；未添加临时对象/观察器/设置。

## 待确认

请进入含草地的关卡走过一片草丛，确认黄色玩家坦克的轮廓/朝向是否容易辨认，以及整体草丛覆盖感是否合适。60%不透明为本次可用性默认值，不宣称原版规则精确复现或用户已确认该强度。
