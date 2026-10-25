# 三星破铁：从单块改为命中面整排

## 用户要求与根因

用户指出三星破铁当前逐个小块消失，正面命中一个2×2方格时应按一排消除，留下2×1或1×2。

地图的16×16铁块区域由四个8×8地形格组成；旧`MapState.ApplyBulletHits`只删除接触的一个8px钢格。并非显示更新漏格，而是逻辑破坏范围本身只有一格。

本轮基于HEAD `7b93ef8`及前两轮未提交的弹速/星星继承补丁。先保存本轮涉及的三个既有源码文件到`Temp/SteelRow-Backup`，保留全部既存变更和`.vsconfig`。

## 修改

唯一生产文件：`Assets/BattleCity/Scripts/Map/MapState.cs`。

- 三星子弹命中铁块时，在命中层横向清除**16px宽、8px深**的一排，即最多两个相邻8×8铁块。
- 上/下射击削横排；左/右射击削竖排。对齐正面射击2×2区域，第一发留下2×1或1×2，下一发再清除剩余排。
- 横向下边缘落在8px网格，地图边缘只裁剪、不向内平移补足；只清除该排中的Steel，不误伤同排其他类型。
- 继续使用冻结的`DamageCell`接触层；同向同帧重复命中不向深处重新搜索，不因第一发挖出新洞而穿透。子弹仍在第一次碰撞处回收。
- 返回值仍以4×4损伤单元计：每个8×8铁块记4，完整双格一排记8。每个实际变化的地形格独立更新revision，既有MapView同帧投影两个变化。
- 没有把铁块改为砖墙的4px薄层，也没有缩小钢格或碰撞尺寸；砖墙仍为原16×4破坏规则。
- 普通/一星/二星不能破铁的门禁不变；未改弹速、冷却、升级继承、死亡/重开、AI、道具、素材或关卡。

`ArenaMapView`和`BulletWorld`无需修改；仍是项目自定义网格/连续扫掠的唯一碰撞真相，不新增Rigidbody2D/Collider2D或其他物理系统。

## 测试

### 修复前复现

新增`SteelDestructionTests.cs`的25项。旧实现下10项通过、15项明确失败（期望移除8个4px单元，实际4个），覆盖单格代替整排的缺陷；见`Regression-BeforeFix.json`。这是修复前红灯证据，不计为通过。

### 修复后

| 验证 | 结果 |
| --- | --- |
| 全EditMode | 790/790 Passed |
| PlayerUpgradePlayModeTests | 5/5 Passed |
| 去重合计 | **795唯一最终Passed：790Edit＋5定向Play** |

新增25项纯逻辑测试：

- 四方向×贴墙/8px间隔共8项，真实扫掠后首发清前排、再发清后排；核对地形、子弹/坦克阻挡、每格revision、命中重放幂等、清理后坦克通道与不写回关卡资产。
- 四方向同向同帧两发只清一排、不额外钻穿及不重复计数。
- 三组横向位置/8px网格下边缘对齐。
- 四方向地图低边缘裁剪，不向内移动多删一格。
- 钢排旁的Brick/Water/Grass/Ice不受损。
- 三星打到砖时仍走原砖墙规则，不把旁边铁块误删。
- 非破钢子弹不能删除任一钢格。

既有PlayerUpgrade孤立钢格/对角深层邻居测试保留全部断言，仅把“只能删除一格”的旧方法名改为准确描述孤立夹具；全Edit同时覆盖砖墙、子弹连续碰撞/池/同tick顺序、残墙转向、钢墙/铁锹恢复、升级/死亡和前一轮初始继承等级等现有逻辑。

新增1项PlayMode：加载Stage01并升级到三级，在临时运行地图中设置2×2钢格，四方向各发两发真实子弹；经Session推进和实际Controller渲染逐格核对首发两个Tile消失、后排两格仍在且阻挡，第二发清空并开放坦克占位，重开恢复原地图/Tilemap和零星。该测试控制发射位置并按逻辑tick推进，不冒称用户自然移动到钢墙射击或自然通关。

同套件原4项仍通过，包含实际三颗Star收集、双发间隔/能力、真实原有钢格与Tilemap消失、重开清理、实际Update推进及Sprite/暂停验证。不重复其他源码未变的Play专项，也不宣称全Play或独立构建通过。

## 一次编译域重载窗口处理

首次新增测试脚本触发域重新加载后，紧接的compile_status/test请求返回`PIPELINE_UNREACHABLE`，两份原始结果已保留为`Reload-*-Unreachable.json`，不是测试断言失败。

按提示只做一次`pipeline list`诊断：原Editor26924/7800仍在运行、服务器恢复可达，无Safe Mode证据。随后用文档要求的`status --until-ready`完成一次有界纠正，编译状态/缺陷复现测试恢复正常。没有反复重启、关闭Editor、升级Pipeline或修改设置；包更新提示不作为本轮升级授权。诊断/ready结果同目录保留。

## 最终证据与保护

- Unity6000.6.7f1 / CLI1.0.0-beta.14 / Editor7800，编译成功；`Compile.json`。
- Console cursor2853→2897新增44条全log，无新增warn/error，最终groundTruth错误0/警告0、compilationFailed=false；历史缓冲未清。见`Console*.json`。
- `SinglePlayer.unity`非Play/非暂停、场景clean、runInBackground=false，无临时输入设备或观察器遗留；`Before.json` / `After.json`。
- 新测试`.meta`由Unity导入自动生成；本轮未修改任何已有资产、场景、Prefab、Sprite/meta或ProjectSettings，没有手写序列化YAML。
- 5项本轮源码/自动生成meta的SHA256、795项去重统计及前两轮22项未变源码/资产的hash保护见`Audit.json`。两个已有测试文件只增加本专项/修正方法名，不删除此前弹速和继承测试。
- `git diff --check`通过，仅LF→CRLF提示。未提交/推送；用户人工试玩确认仍待反馈。
