# 阶段四执行看板

## 当前授权

用户授权开始Phase 4的BC-400；主代理直接实施，不包含BC401～407、可选双人/Construction、提交推送或发布。

## 当前状态

| 任务 | 状态 | 证据 / 下一步 |
|---|---|---|
| BC-400 | 已完成当前单关范围实现与定向自动验收 | 14唯一最终Passed（6 Edit+8 Play），原失败如实保留；真实键盘流程、受控胜利结算、短运行推进及标题/提示截图，见BC-400/Verification.md |
| BC-401 | 待授权 | 多关加载、关卡校验和通关行为；当前只有Stage01交互数据，完成屏明确结束 |
| BC-402～407 | 未开始 | 未实现多关数据/音频/存档/独立构建，不标M4通过 |

入口为`Assets/BattleCity/Scenes/SinglePlayer.unity`，原Stage01场景/数据/原素材/ProjectSettings不变。Editor最终stopped/clean、后台运行false。Unity最近使用场景UserSettings变更保留，既存.vsconfig未动。无提交/推送。
