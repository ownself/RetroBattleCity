# M0 阶段验收

**结论：通过。BC-000～BC-007 完成。**

本次交付是资源与显示基线，不是可玩坦克大战；不勾选任何阶段1/2任务。当前Editor打开 `Assets/BattleCity/Scenes/PixelBaseline.unity`，已退出Play Mode，后台运行覆盖已还原为原项目值off。

## 交付与证据

| 任务 | 验收结果 |
| --- | --- |
| BC-000 | 本地标签 `bc-stage0-start-d58f3b4` + 工程外ZIP快照；Library/Temp/Logs等无跟踪项；未创建远程仓库 |
| BC-001 | [像素/坐标规范](Pixel-and-Coordinates.md)：256×240、PPU16、208×208地图、屏幕地图偏移16/16、原点/Pivot/Rect换算；参考关卡实测208×208 |
| BC-002 | [319项矩形清单](SpriteInventory.csv)：256坦克、63地形/特效/子弹/道具/HUD；未归类/缺失资产单独记录 |
| BC-003 | 派生图语义切图完成，源PNG未变（[SHA256](Source-sha256.txt)）；Point/无压缩/无Mip Maps/PPU16；精确键色遮罩+HUD墨迹保护区；1967个真黑像素完整保留 |
| BC-004 | URP PixelPerfectCamera + Sprite Unlit + 排序层；独立展台场景、8px Tilemap接缝样本；整数倍与非整数窗口黑边通过 |
| BC-005 | SpriteCatalog 319直接引用可校验；TankDefinition / LevelDefinition ScriptableObject格式；PlayerPrototype标记待校准；目录与测试程序集建立 |
| BC-006 | [核心规则与参数表](Core-Rules.md)覆盖输入、暂停、tick、碰撞、对齐、胜负优先级；原版未核实值明确标记 |
| BC-007 | 自动测试、活跃Play Mode、各方向帧截图、像素比对及Console检查通过，见下文 |

## 自动测试

通过运行中Editor的实际命令目录调用：

```bash
unity command test run --caller plugin --skill unity-cli --mode editor --filter BattleCity.Tests --async_tests true --format json
unity command test status --caller plugin --skill unity-cli --format json
```

最终结果：**7 total / 7 passed / 0 failed / 0 skipped**，约1.11秒。[完整结果](EditMode-results.json)。

覆盖：
1. 坐标往返、负坐标floor、边界、单元中心及源图Rect转换。
2. Catalog全部319项与CSV逐项匹配、Sprite引用完整、PPU/尺寸/Pivot正确、未知ID拒绝。
3. 全部256坦克Sprite的画布、中心Pivot、XY bounds一致。
4. 三张纹理Importer配置。
5. 原图/派生图逐像素比较，确认只改变遮罩允许的Alpha，保护真黑及HUD近黑墨迹。
6. 配置资产校验、未知地形/越界出生点/NaN速度拒绝、玩家Sprite前缀完整。
7. 已保存场景中的相机、材质、排序层唯一ID、Sprite画面范围、96格连续Tile样本、管线与抗锯齿配置。

## Play Mode 与显示冒烟

- 启用已存在的Editor auto-tick，并临时覆盖run-in-background为true。
- `editor.playmode start` 后重新等待连接，再用 `job wait` 监听 `UnityEngine.Time.frameCount changed`。
- **initialValue=944 → value=988；met=true；framesObserved=2；约114ms**。[帧推进证据](PlayMode-ticking.json)。这证明运行确实推进，而不是只进入Play Mode。
- 展台32个坦克（4色×8外观）逐一切换4方向×2帧，保留8张768×720实际Game View截图；共享16×16画布/8,8 Pivot保证位置稳定。通过程序断言和画面检查未见画布跳位、裁边、地形接缝；履带图案差异保留，不用旋转整图代替朝向。
- HUD数字0～9及旗帜完整；透明背景、基地有效灰色、彩色轮廓与纯黑墨迹均保留。
- [原生256×240](Screenshots/baseline-256x240.png)、[3倍768×720](Screenshots/gallery-up-0.png)、[非整数窗口1000×700](Screenshots/baseline-1000x700.png)。
- 1000×700实际内容 **512×480（2倍）**，左右各244px黑边、上下各110px黑边；运行中PixelRatio=2。[运行时状态](Windowbox-runtime.json)。
- 两种放大画面均与原生参考逐RGBA像素比对：**内容差异0、非法黑边像素0**，不存在分数倍拉伸或滤波颜色。[像素报告](Visual-pixel-check.txt)。
- [8张朝向/帧截图](Screenshots/) 使用 `gallery-{up|left|down|right}-{0|1}.png` 命名。
- 最终Console：**compilationFailed=false；0 errors；0 warnings**。[Console证据](Console-final.json)。
- 已退出Play Mode，恢复原Game View预设选择，移除本次临时分辨率预设和Assets中的截图目录；正式证据保留在Docs。保留的PixelBaseline展台属于本阶段交付，不是游戏入口，也未加入Build Settings。

## 实施中发现并解决的问题

| 问题 | 处理与复验 |
| --- | --- |
| 按旧Skill示例名称无法发现新命令 | 从实际目录确认Pipeline0.9使用分组命令，如`script eval_file` / `editor.playmode start` / `job wait`；不沿用旧命令名 |
| 首次切图脚本API命名差异 | TextureImporterSettings设置FullRect；SpriteNameFileIdPair实际位于UnityEditor。仅eval编译失败，未修改Sprite元数据；修正后执行成功 |
| 第一次测试5/7：排序层名退回Default | TagManager uniqueID为unsigned，负int被截为0；改用唯一正整数，修复已有非法0 ID；增加ID唯一性和renderer层检查 |
| 第一次测试：Sprite bounds Z厚度不是0 | Unity为Sprite保留0.2的Z bounds；验收关心XY画布，改为校验XY而不是错误假定Z=0 |
| 初次画面：STAGE/数字消失 | 它们与背景同为RGB(0,0,1)，不能全图按颜色删背景；新增`(328,176,40,24)`墨迹保护矩形，并逐像素校验 |
| 初次画面：展台右侧样本越界 | 修正地图像素→屏幕像素的展示位置；添加全体SpriteRenderer画面边界断言，最终截图全部可见 |
| 新建空Editor asmdef时短暂警告 | 随后添加正式Editor脚本，重新编译成功；记录并清理旧Console条目后完整复跑测试/Play Mode，警告未再出现 |
| 编译/退出Play Mode时连接短暂断开、Hold on提示 | 等待域重载后重新检查ready与编译状态，没有另开Editor或手写资源文件 |
| capture的save_path实际限制在Assets下 | 通过实际返回路径读取截图，再复制到Docs；最后用AssetDatabase删除临时截图资源 |

## 回退与未覆盖范围

- 回退点、快照及原始`.vsconfig`备份位置见[像素规范](Pixel-and-Coordinates.md#回退bc-000)。本批没有自动提交，用户可审核差异后自行提交。
- 原来未跟踪的`.vsconfig`保持未跟踪；未改变SampleScene、原始PNG字节或模板Renderer2D。
- 未做Windows独立构建；低于256×240的窗口、真实操作手感、30/60/144逻辑一致性、碰撞/破墙都属于后续阶段。
- 当前参数是项目默认值，未声称复原了原版速度、掉落、AI或帧时序。
- 素材授权、音频来源、剩余敌军图标和完整关卡集仍需落实，但不阻塞M0资源/显示基线。

**下一任务：BC-100 固定逻辑更新与输入适配。**
