# 阶段四执行看板

## 当前授权

用户先授权BC400，现另行授权并完成BC401；主代理直接实施，随后授权BC402全部01～35分批实施，不包含BC403～407、可选双人/Construction、提交推送或发布。

## 当前状态

| 任务 | 状态 | 证据 / 下一步 |
|---|---|---|
| BC-400 | 已完成当前单关范围实现与定向自动验收 | 14唯一最终Passed（6 Edit+8 Play），原失败如实保留；真实键盘流程、受控胜利结算、短运行推进及标题/提示截图，见BC-400/Verification.md |
| BC-401 | 已验收 | 有序Campaign资产、同host快照加载/切关/最终完成；28唯一最终Passed（22Edit+6Play），见BC-401/Verification.md；正式列表仍1关 |
| BC-402 | 已验收，35/35交互数据 | 首批01～05与本轮06～35均完成；全35关23660格、显示/波次/同host最终完成遍历等39唯一最终Passed，限制见BC-402/Batch06-35/Verification.md |
| BC-403～407 | 未开始 | 未实现音频/存档/独立构建，不标M4通过 |

入口为`Assets/BattleCity/Scenes/SinglePlayer.unity`，原Stage01场景/数据/原素材/ProjectSettings不变。Editor最终stopped/clean、后台运行false。Unity最近使用场景UserSettings变更保留，既存.vsconfig未动。无提交/推送。阶段四3/8，M4未通过；BC402约定35关地形/默认波次已完成，不代表原版逐关波次或自然通关。BC403待授权确认音频来源。其后用户授权的STAGE上下选关已完成，17唯一定向Passed，见Stage-Selection/Verification.md；35关数据未改。
