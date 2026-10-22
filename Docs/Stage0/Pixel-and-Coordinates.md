# 阶段 0：像素、坐标与资源基线

## 核对与冻结值（BC-001）

- 工程：Unity 6000.6.7f1，URP 17.6.0，2D Renderer；Windows 单人原型。
- 参考帧 **256×240**，游戏地图 **208×208**；PPU **16**。
- 第一关原始 PNG 实测 **208×208**，无外围 HUD、边框，基地中心在源图 `(104,200)` 附近。参考图仅用于核对，不是运行时碰撞或地形数据。
- 参考帧左下为屏幕像素 `(0,0)`；地图左下在屏幕 `(16,16)`，半开范围 `[16,224)×[16,224)`。左边距16、上下边距16、右侧HUD槽宽32。**这些外围布局为本项目冻结值，不宣称已从原版整屏截图验证。**
- 地图世界原点 `(0,0,0)` 为左下角；世界边界 `[0,13)×[0,13)`。固定相机 `(7,6.5,-10)`，正交半高7.5，世界参考帧 `[-1,15]×[-1,14]`。
- 逻辑坐标：地图局部像素、X向右/Y向上，浮点位置，速度单位 px/s；不得直接拿 Transform 反推碰撞状态。
- 显示换算：`world=(logic.x/16,logic.y/16,0)`；`screenPixel=logic+(16,16)`。集中定义在 `PixelGrid`。
- 大格16px（13×13）；地形/对齐参考8px（26×26）；砖墙损坏单元4px（52×52）。后两者在阶段1完成窄通道/残墙试验前仍是**待玩法校准**的粒度，不把显示Tile当作碰撞真相。
- 地形数据8px单元按左下起、行主序保存，`index=y*26+x`。单元索引使用 floor，不能用向零截断；边界为半开。
- Tilemap：Grid cellSize `(0.5,0.5,1)`、原点零、Tile Anchor `(0.5,0.5,0)`；8×8 Sprite 中心Pivot，与逻辑单元中心一致。16px显示Tile可放在独立1-unit Grid，不混用尺寸。
- 所有切片画布为偶数尺寸、Custom中心Pivot（16×16→8,8；8×8→4,4；32×32→16,16），坦克不裁剪到可见边缘；朝向/履带帧均保持16×16画布，防止跳位。
- 图集清单使用**左上原点**、Y向下的矩形 `(x,y,w,h)`；导入转换：Unity Rect `(x,H-y-h,w,h)`。代码及CSV显式区分这两种坐标。

## 素材盘点与透明方案（BC-002 / BC-003）

| 项目 | 实测 / 决策 |
| --- | --- |
| 原始总图 | `Assets/Sprites/NES - Battle City (JPN) - Miscellaneous - General Sprites.png`，400×256，完全不透明 |
| 第一关参考图 | `Assets/Sprites/NES - Battle City (JPN) - Backgrounds - Stage 01.png`，208×208，完全不透明 |
| 运行用派生图 | `Assets/BattleCity/Art/GeneralSprites.png`，与原图同尺寸/同RGB，只有背景遮罩区域的键色Alpha改变 |
| 背景键色 | 背景主要为 **RGB(0,0,1)**；但STAGE/数字也用同色作墨迹！明确保护左上矩形 `(328,176,40,24)`，其外才按精确键色置透明；不采用阈值/全局删黑 |
| 真黑 | **1967个 RGB(0,0,0) 像素全部保留**，包括HUD文字；灰色 `#636363` 是地形/基地/HUD有效颜色，不删除 |
| 透明校验 | 自动逐像素比较原图与派生图，只有HUD保护区外的精确键色Alpha可变；保护区保持不透明；原始PNG字节保持不变 |
| 导入 | Point、Uncompressed、No Mip Maps、PPU16、NPOT None、Clamp、Standalone无覆盖；派生图Multiple/FullRect |
| 可重复切图 | `Tools/Phase0/PrepareSprites.cs`，运行前检查全部Sprite编辑能力，更新name/fileID映射；再次运行保留同名Sprite GUID |

完整 **319** 个语义切片矩形、Pivot和ID在 [`SpriteInventory.csv`](SpriteInventory.csv)。该清单由同一个切图配方导出，EditMode测试逐条校验实际导入Sprite。命名为小写点分层级，帧从0开始，不使用图集长文件名作为运行时查找键。

### 坦克（256个切片）

- `tank.{yellow|white|green|red}.{player.level0..3|enemy.basic|enemy.fast|enemy.power|enemy.armor}.{up|left|down|right}.{0|1}`。
- 每个颜色128×128区块，yellow左上/white右上/green左下/red右下；每行一种外观，每方向两帧、每帧16×16。
- 源矩形：`x=(paletteIndex%2)*128+directionIndex*32+frame*16`；`y=(paletteIndex/2)*128+variantIndex*16`；颜色/方向/外观索引顺序与上述一致。
- “等级/敌人类型”命名是当前视觉语义映射，**等级对应能力、装甲调色及原版行为尚未核实**；阶段3验证，不能仅凭名字当作玩法事实。

### 其他（63个切片）

- 地形：完整砖/钢16px、砖/钢各4个8px象限、水两帧、草、冰；地形象限ID末尾`x.y`的y为**源图向下**，不是世界单元y。
- 基地完整/摧毁；子弹四方向（8×8画布，保留内置空白，逻辑炮口不得从可见像素猜测）。
- 道具：helmet、clock、shovel、star、grenade、tank、gun。gun保留素材映射，但不纳入首版玩法承诺。
- 特效：出生4帧、小爆炸3帧、大爆炸2帧、护盾2帧、火焰1帧。
- HUD：100～500分、pause/game/over/stage、数字0～9、P1/P2、生命图形、旗帜。HUD灰底保持与侧栏一致，STAGE/数字近黑墨迹在保护区中不做透明转换。

### 缺失、未归类与不在本阶段使用的项

- 未确认单独的“剩余敌军”HUD图标；后续可从已有坦克图形制作语义别名/原创替代，不冒充已就绪。
- 图集右侧红色条/色样、额外碎片示例、文字旁未识别符号不切为运行资产；原图完整保留。
- 标题全图、完整字体、音频、其余关卡数据、公开发行授权未就绪。
- 第一关尚未录入交互数据；`LevelDefinition`仅建立格式，不生成假的“已完成第一关”资产。

## 显示与排序（BC-004）

- 专用场景：`Assets/BattleCity/Scenes/PixelBaseline.unity`。它是有意保留的资源展台，不含玩家输入、战斗、AI或运行时地图。
- URP自带 `UnityEngine.Rendering.Universal.PixelPerfectCamera`；不安装Built-in专用包。
- PPU16 / 256×240，`UpscaleRenderTexture + Windowbox`，先低分辨率渲染再整数倍上采样；不启用StretchFill。
- 相机HDR/MSAA/动态分辨率/后处理全部关闭；专用URP资产关闭MSAA、HDR，renderScale=1，各质量等级引用此资产，抗锯齿0、Anisotropic Disabled。
- 不修改模板Renderer2D与SampleScene；基线复制现有URP资产并复用只读Renderer2D。Sprite使用 `Universal Render Pipeline/2D/Sprite-Unlit-Default`。
- 排序由低到高：Ground → Obstacles → Actors → Projectiles → Effects → Foliage → HUD；草丛后续只遮挡规定对象，HUD最终采用uGUI，本阶段只展示HUD素材，不实现界面。
- 编辑器move snap=1/16；逻辑不量化为整数世界坐标，只在显示层对齐像素。
- 窗口不小于参考帧时，整数倍 `n=floor(min(W/256,H/240))`；内容256n×240n，剩余区域黑边。小于256×240的窗口不在首版清晰度保证范围，阶段4限制最小窗口尺寸。

## 数据格式与绑定（BC-005）

- `SpriteCatalog.asset`：序列化语义ID→直接Sprite引用，运行时在绑定目录内查询，不使用Resources/Find/路径加载。
- `TankDefinition`：ScriptableObject；ID、Sprite前缀、px/s速度、整数tick冷却、子弹上限、生命、分数、requiresCalibration。
- `LevelDefinition`：ScriptableObject schemaVersion=1；26×26 TerrainKind数组、地图像素中心出生点/基地、按坦克ID的波次序列、在场上限；运行时必须复制为唯一可变地图，砖格再展开4px损坏掩码。种类/尺寸/坐标基本校验已建立，通路和出生占用校验留到阶段1/2。
- 当前只交付 `PlayerPrototype.asset` 默认参数，全部标记待校准，不批量预建还未使用的敌人配置。
- 目录遵循实施计划；空目录用Unity目录meta保留；测试区分EditMode/PlayMode。M0不生成Prefab或修改Build Settings，以免把展台当作游戏入口。

## 回退（BC-000）

- 原始提交：`d58f3b4`；本地标签：`bc-stage0-start-d58f3b4`。
- 工程外快照：`../BattleCity-before-stage0-d58f3b4.zip`；原先未跟踪的`.vsconfig`另外备份到`../BattleCity-before-stage0.vsconfig`，不擅自纳入提交。
- Git已有Library/Temp/Logs/Build等忽略规则，且这些目录无已跟踪项；补充UserSettings/TestResults/Recordings忽略规则。原提交已跟踪的UserSettings不擅自移除。
- 未创建远程仓库，未重置分支或覆盖用户现有修改。回退应优先比对标签、定向恢复本批资源，不使用无差别hard reset。
