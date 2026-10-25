# 部分破砖后贴墙转向卡住

## 范围与原因

- 用户报告：第一关截图红框附近，打掉砖墙一层并贴近后，视觉上有上下通路，但按上下无法移动。
- 基线：`80ddf1ac2b49f3e06b2376da71712df522a06f91`，工作区只有既存未跟踪 `.vsconfig`；保留不动，无提交/推送。
- 项目使用自定义逻辑网格/扫掠 AABB，不使用 Rigidbody2D、Collider2D 或另一套物理碰撞。
- 砖墙按 4px 破坏，坦克侧向转向按 8px 对齐。贴残墙时，最近对齐线可能落在残墙内部；旧逻辑直接拒绝转向，不再尝试空侧对齐线。
- 自动复现使用真实 WeaponState/BulletWorld 命中、MapState 破坏及 TankMotor 移动；在独立地图的四面各测试两个侧向请求，共8项均复现卡住。这是逻辑复现，不冒称逐像素重建用户截图或已人工复测第一关现场。

## 修改

- `Assets/BattleCity/Scripts/Tanks/TankMotor.cs`：最近网格线因地形无法到达时，只尝试夹住当前位置的另一条相邻网格线；两端与完整扫掠路径均检查地形及动态阻挡。沿用正常速度渐进对齐和原有最多2px小修正。
- 保留16px坦克尺寸、8px对齐、4px破砖、输入优先级、冰面规则。动态对象单独阻挡最近对齐线时仍等待，不新增自动绕坦克行为。
- `Assets/BattleCity/Tests/EditMode/TurnAlignmentTests.cs`：新增8项四面残墙侧转用例和1项两侧对齐都阻挡时不得穿过动态对象的用例。检查残墙阻挡、空侧小步对齐、最终转向、每步合法占地和地图不被移动修改。
- 未修改地图、关卡/场景、Prefab、素材、ProjectSettings或输入配置。

## 实际验证

Unity 6000.6.7f1，CLI 1.0.0-beta.14（检查为最新），原编辑器连接 ready。

1. **修复前**：新9项中1通过、8失败，原始结果 `Before-Results.json`。
2. **初版修复**：侧转74、Phase1 24、破砖33、敌脑40、特殊地形20通过；TankState 22/24，2项指出通用回退改变了动态对象单独阻挡时的既有等待语义，见 `Intermediate-Dynamic-Regression.json`。一次有界纠正，将回退限定为地形导致的对齐失败；未改旧断言。
3. **最终版本**：重新编译成功 `Compile.json`，以下6组在最终源码运行，共215个唯一EditMode用例全部Passed（非全量工程测试）：

| 套件 | Passed |
| --- | ---: |
| TurnAlignmentTests | 74 |
| Phase1Tests | 24 |
| BrickDestructionTests | 33 |
| TankStateTests | 24 |
| EnemyBrainTests | 40 |
| SpecialTerrainTests | 20 |
| 合计 | 215 |

每组原始结果见同名JSON。命令：`unity command test run --caller plugin --skill unity-cli --mode editor --filter <套件名> --format json`。同步请求的有效结果在返回的 `Summary/Results`，非后续 `test status` 的空结果。

命令发现时一次误传无效的 `--group` 参数仅返回默认目录，已改用 `--query`。一次Python读取UTF-8结果未指定编码失败，显式编码后完成。一次尝试用 `|` 合并filter得到0项，未计为通过，改为每套件独立filter；以上未修改源码或造成编译/运行错误。

4. **Console**：cursor2450→2590的140条增量全为log，最终编辑器Console错误0/警告0、compilationFailed=false。Pipeline保留缓冲含历史错误，不将其冒称清零，见 `Console.json` / `Console-Increment.json`。
5. **收尾**：`SinglePlayer.unity`，非Play、非暂停、场景clean，见 `Editor.json`。没有进入Play或改临时设置。修改源码SHA256与唯一结果对账见 `Audit.json`。

## 尚待用户体验确认

请在第一关原红框位置，按原方式打掉砖墙一层、靠近残墙，然后分别按上/下（或W/S）。预期不再一直卡住，会先向空侧小幅对齐，再沿所按方向移动，且不会穿入残墙。无需重跑完整测试；如仍复现，回传靠墙前后短视频或截图及按键顺序，以确认实际墙体剩余形状与坦克位置。

未宣称人工手感通过、实际第一关Play输入复测、完整PlayMode回归、自然通关或独立构建通过。
