# BC402 剩余06～35录入与35关收口

## 结论

用户授权一次完成余下30关。06～35交互数据/矩阵录入完成，正式Campaign为01～35有序35项，BC402验收通过；阶段四3/8，M4未通过。只声明参考地形与项目默认玩法可加载，不声明原版完整规则/逐关敌军波次或自然35关通关。

工作区基于a0c9a75及未提交BC401/首批BC402继续，不撤销既存变更；无提交推送、无委派。Unity6000.6.7f1/CLI beta.14，版本检查最新。

## 交付与来源

- 新`Stage06Layout.txt`～`Stage35Layout.txt`及对应LevelDefinition资产，通过标准库PNG解码提取可审查26×26矩阵；资产由Editor API生成，游戏不读取参考PNG。
- 输入`Assets/Sprites/Stages/...Stage 06.png`～35.png，原图SHA256/矩阵SHA256/地形数量见Evidence/Extraction.json。
- 无未知格；除基地四格外每类型完整8×8图案在每张图各只有一种。冰面按37浅灰/14白/13深灰完整格区分钢墙：17关208格、24关216格、28关212格、32关320格。核看Stage32原图确认斜纹冰面，不把灰白都归钢墙。
- 基地与出生采用已冻结坐标，所有35关足迹验证通过，无静默清障或移动出生点；所有关卡使用现有20混合敌/上限4/掉落索引与AI节奏，不将其称为原版逐关波次。
- `StageBatchAuthoring`保留首批入口，新增BuildRemainingStages并复用范围构建；先预检全部30张矩阵再写资产，不重建Stage01～05，不修改场景/运行逻辑/原始Importer。
- Campaign仅扩展引用01～35；战役结尾、每关独立生命/升级/分数按BC401规则不变。

## 本轮39唯一最终Passed（36 Edit + 3 Play）

| 证据 | 结果 | 覆盖 |
|---|---|---|
| Evidence/edit-result.json | 36/36 | 35关各676格独立原PNG读取对比（总23660格），关号/尺寸/足迹/敌序列与道具配置，35独立有序资产 |
| Evidence/play-result.json | 1/1 | 35真实Session各62固定tick、自然首敌、基地/玩家存活、HUD号、三类Tilemap逐格与旧层清理、实际frame推进 |
| Evidence/traversal-retry-result.json | 1/1 | 同Controller真实35资产01→35生产波次/受控700击杀与Won结算、逐关加载Ready/分数归零、最终Complete/禁止36关、重玩01；host/视图/场景数量不增 |
| Evidence/campaign-result.json | 1/1 | 已有真实Input System键盘/Update双关流程、R当前关重开、失败返回标题与快照隔离回归 |

遍历测试禁用Controller自动Update，调用公开flow路由并按固定tick推进Session，通过生产扫掠/HP/计分得到Won，未强改胜负状态。测试炮口受控放入敌坦克足迹，保证不被贴近出生点的墙截获；不是人工开路、自然通关或手感证据。已有键盘回归为另一个测试，二者证据不混称。

## 原失败与纠正

- 首次recompile Pipeline HTTP400，连接ready后一次同协议重试成功，无升级重启；保留compile.json/compile-recovery.json。
- 首次35关遍历在Stage09 Lost：受控外置炮口可能位于贴出生地形阻挡内，导致未及时消灭敌人并自然失败。一次有界夹具纠正将炮口置入目标足迹、向上射击，生产地图/AI/血量/波次不变；复测35关1/1通过。原失败traversal-result.json保留，不声称首轮全绿。
- 图格与显示测试首轮通过，没有为匹配图片削弱地形断言。
- Console2369→2406共37条全log，0warn/error；原失败测试有独立result记录，不以Console无error冒称首轮无断言失败。

## 保护与最终现场

- 起点保护文件的实际数量以Evidence/Protected-Audit.json的compared为准；所有原PNG/meta、Stage01～05资产、场景、规则、ProjectSettings不变，唯一起点资产变更为预期SinglePlayerCampaign扩展。清单baseline-hashes.json及实现hash均保存。
- SinglePlayer场景clean、Editor stopped、runInBackground=false、35关绑定验证通过，见final-state.json；git diff --check通过（Windows行尾提示不是错误）。
- 无临时场景/素材落盘，无安装依赖；保留.vsconfig与此前未提交工作。

## 明确限制

未验证自然游玩35关通关、每条通道寻路可达、原版逐关敌型/数值、人工视觉手感、完整全量测试、音频/保存/独立构建。BC402验收为约定35图地形录入、基地/出生/波次合法性、显示/加载与受控整条流程，不等于全部原版复刻。

下一任务BC403需另行授权并确认音频来源/使用条件，本轮不开展。
