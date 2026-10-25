# BC402 首批01～05

## 结果

本批01复核、02～05交互数据录入及五关Campaign绑定完成；BC402总目标35关，仅5/35已具备交互数据，BC402保持未勾选，M4未通过。本批停在可审查检查点，06～35留后续批次，不声称35关复刻完成。

起点为BC401未提交工作区（HEAD a0c9a75）；保留全部既存变更和.vsconfig。主代理直接实施，无委派、提交或推送。

## 作者输入和来源

- `Assets/BattleCity/Editor/Stage02Layout.txt`～`Stage05Layout.txt`：26行×26字符top-down，`.`空、B砖、S钢、G草、W水、H基地；转为LevelDefinition bottom-left terrain。运行时不读图片。
- 对应原PNG来源及SHA256、矩阵hash和每类格数见Evidence/Extraction.json。Python标准库PNG解码提取（本机无Pillow，未安装依赖）；所有非基地单元按类型的完整8×8图案各只有1种，无未识别格。第一关提取文本与既存Stage01Layout逐字符一致。
- 基地在图上(96,192)16×16处，逻辑(104,8)；H4格归独立基地，不伪装成可破坏地形。
- 01：砖220/钢8；02：砖192/钢52/草40；03：砖130/钢26/草152；04：砖262/钢16/草56/水12；05：砖136/钢26/水60。
- 出生坐标与20混合敌序列、上限4、道具索引沿用已验收Stage01项目原型，每关校验足迹，无静默清障。这些不是PNG可证明或原版已核实的逐关波次。

## 资源交付

- 新Stage02.asset～Stage05.asset由`StageBatchAuthoring.BuildFirstBatch()`通过Editor API创建/保存。
- 原Stage01资产/场景及规则配置未变；Campaign有序引用Stage01～05，不复制战斗场景。
- 新recipe先校验所有矩阵再写资产，拒绝覆盖已有更长列表；不是运行时导图工具，不自动录入06～35。
- 原素材/Importer/ProjectSettings保持不变，见Evidence/Protected.json。源码未改战斗/地图模型/加载框架。

## 测试：8唯一Passed

1. StageBatchDataTests 6/6：五个关卡独立读取原始PNG（Unity Texture2D.LoadImage，不读作者矩阵），逐格对比全部3380格；校验关号/尺寸/出生/基地/20敌映射/4上限/道具配置和五资产顺序。见Evidence/edit-result.json。
2. StageBatchPlayModeTests 1/1：加载实际SinglePlayer hierarchy，暂停宿主Update后分别创建五个真实ArenaSession，每关运行62个固定tick，验证自然首敌生成、基地/玩家存活、HUD关号、676个砖钢/草/水显示格正确与旧图层清理；实际渲染frame推进。此项是受控固定tick显示/生成验证，不是五关自然通关。见Evidence/play-result.json。
3. CampaignPlayModeTests 1/1：实际键盘/Update与生产扫掠双关转换完整回归；夹具11/23不计作正式关卡。见Evidence/campaign-result.json。

新代码编译成功无代码error/warn，测试首轮均通过。无全量测试/自然5关通关/人工试玩/独立构建声明。没有因源码未变重跑BC401全部28项。

## 最终现场

- SinglePlayer clean、Editor stopped、runInBackground=false、正式列表5关，见Evidence/final-state.json。
- Console起点cursor2342；Evidence/console.json包含少量此前BC401结尾log（抓取since2337），本批无新增warn/error；原捕获结果完整保留。
- 无临时Play scene保留，不修改原场景/Build Settings；git diff --check通过，仅Windows行尾提示。

## 后续

下一批06～10，沿相同数据/验证门禁推进。每批保存数量、来源、未知数值与结果；35关完成前不关闭BC402。
