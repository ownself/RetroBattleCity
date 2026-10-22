# BC-104：武器、子弹与发射限制

## 结论

**BC-104 完成。M1 仍未完成，下一任务 BC-105。**

- EditMode：**111/111**，含首批 31 例、转向补丁 65 例、本任务武器 15 例。
- PlayMode：**3/3**，含真实 Update / Input System 射击、显示、暂停与重用验证。
- 证据：[EditMode-results.json](EditMode-results.json)、[PlayMode-results.json](PlayMode-results.json)、[Console-final.json](Console-final.json)。
- 本批没有运行后的人工手感确认；用户此前确认的是 BC-102 转向补丁，不借用为本任务的人工验收。

## 操作与当前限制

打开 `Assets/BattleCity/Scenes/CombatArena.unity`，Play 后点击 Game 视图取得焦点。WASD / 方向键移动，**Space 射击 / 按住连发**，Escape 暂停与恢复。

默认只允许一发子弹在场：即使冷却结束，前一发还未回收也不会再发射。子弹到达地图边界后释放名额。

**当前子弹穿过砖墙、钢墙，不造成伤害或破坏。** 本任务只交付发射、直线运动、边界生命周期及回收。BC-105 才接入地形 / 目标扫掠、最早命中排序、敌我过滤及精确命中位置；BC-106 接入局部破砖，BC-107 接入特效。不将本批称为完整战斗或第一关。

## 规则与职责

新增 `Assets/BattleCity/Scripts/Combat/`：

- `WeaponState.cs`：共享坦克武器，不依赖玩家或 AI 输入来源；从 `TankCommand.FireHeld/FirePressed` 消费请求。首发立即可用，成功发射后冷却 15 tick；下一发最早在相隔 15 tick 后允许。冷却中 / 满弹时的请求直接拒绝，不排队，不延长冷却。
- 炮口中心为 `坦克中心 + 朝向 × 10px`（坦克半宽 8px + 子弹半宽 2px），四方向一致。位置与方向取**本 tick 移动 / 转向结算后的实际状态**，不是尚未完成的请求方向。
- 默认子弹速度 180px/s（3px/tick）、逻辑碰撞盒 4×4px、显示 8×8px。炮口 10px 与既有速度 / 冷却均为原型值，尚未核实原版。
- `BulletWorld.cs`：拥有唯一子弹状态、直线移动、地图边界回收、显式 `Release(id)` / `Clear()`。边界按 4×4 盒体接触地图边界释放；本批无需通过查询地形来决定寿命。
- 每颗子弹记录 Owner、唯一租约 Id、朝向、速度、当前位置、上一步位置、AgeTicks。结束时只释放所属武器的一个名额，并彻底清空复用状态；重复释放及复用后的旧 Id 不影响新子弹。
- 逻辑对象复用采用小型 Stack；没有预建大框架或每帧实例化子弹。`Active` 为只读列表，显示不写回逻辑。
- `BulletView.cs`：缓存四向 `bullet.*` Sprite，复用 SpriteRenderer，在 `Projectiles` 排序层显示，只在显示层取整像素；无碰撞器 / 刚体。多发子弹按活动列表槽位投影，不维护第二套运动状态。
- `ArenaSession` 顺序：输入 → 坦克移动 / 转向 → 武器冷却与发射 → 子弹移动。新子弹本 tick 就移动一次，且保留炮口作为 PreviousPosition，便于 BC-105 扫掠接入。
- 暂停 / 失焦不推进武器冷却、子弹位置或年龄；恢复时按住的 Space 必须松开再按。重载场景新建 session，不继承弹数或冷却。

## 覆盖与实际结果

### EditMode 111/111

本任务 15 例覆盖：四向炮口、3px/tick 运动与四侧边界回收；首发与恰好 15 tick 的冷却间隔；满弹不重置冷却、释放后恢复发射；可配置的两发上限与独立 Owner 计数；重复释放、Clear 幂等；池对象重用时所有字段与 Owner 重设，旧 Id 无效；按住连发；拒绝的短按不滞后触发；移动后发射并同 tick 移动；暂停冻结、恢复抑制按住射击、短按边沿；30 / 60 / 144 帧时间输入下半秒均移动 90px。

此外，上次转向补丁的 65 例已在本批全部通过，补足之前只有编译和用户人工体验确认、未执行补丁后完整自动回归的空缺。

### PlayMode 3/3

新增测试 `Arena_KeyboardFiresAndRendererIsReusedAfterRelease`：

1. 加载真实 CombatArena 场景，创建 Input System Keyboard，发送 D + Space，跨真实 player-loop 帧后确认右向子弹已生成。
2. 真实 `ArenaController.Update` 创建并显示 `bullet.right`，使用 Projectiles 排序层，位置是逻辑坐标的像素取整。
3. 暂停后跨帧确认子弹位置 / AgeTicks 不变。
4. 显式释放后显示隐藏，释放弹数；再次射击复用同一个 SpriteRenderer，逻辑租约换为新 Id，在场计数为 1。

原两项真实时钟 / 玩家渲染 / 暂停及卸载重载测试仍通过；重载测试增加子弹、武器计数、冷却初始状态断言。

验证时临时开启后台运行覆盖，完成后退出 Play Mode 并恢复项目原值 false。测试完成状态到场景恢复之间有短暂清理窗口，场景写入保护曾拒绝一次操作；确认已恢复到无未保存修改的 CombatArena 后才更新操作提示并保存，没有覆盖其他场景。最终 Console 无编译失败、无错误、无警告。

## 工程影响

- 不重建地图，不修改玩家参数资产、M0 画廊、图集或 Build Settings。
- 已有场景仅更新 Arena 对象的操作提示；子弹显示根对象在运行时创建，无需新增 authoring 引用。
- `Phase1Arena.Build()` 的提示更新为 Space fires，仍可显式重建试验场；本批未调用该生成器覆盖关卡。
- PlayMode 测试程序集新增 Input System 引用。
- 未执行 Git 提交；用户原有 `.vsconfig` 与开始本批前已存在的编辑器个人布局变更不处理。
