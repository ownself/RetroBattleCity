# 关卡提示界面选关

用户授权在开始游戏前STAGE界面加入上下选关，便于测试；沿BC400～402实现增补，不开展音频任务，无提交推送。

## 行为

- STAGE提示界面↑/W上一关、↓/S下一关；按Campaign顺序循环，01向上为35、35向下为01。Enter/Space开始所选关。
- 每次物理按下切一关，长按不连跳；方向别名合并，失焦不响应。新方向和确认同帧时导航优先，确认须松开重按。
- 只允许Ready且显示StageIntro时选择；标题、战斗、暂停、结算均不能通过此API选关。
- 加载所选关私有快照，沿用新Session重建：分数/地图/实体/计时归零、3生命/0级，复用显示host。正常胜利仍进入所选关之后的下一关，R重开当前关。
- 提示界面新增`UP DOWN OR W S SELECT STAGE`，Render缓存加入关号，确保同屏选关时标题即时刷新。无需重建场景或改关卡资产。
- 这是项目选关操作方案；不对原版按键/边界/长按节奏做精确复现声明。

## 验证：17唯一Passed

- StageSelectionTests 7/7：四键、长按、别名、冲突、同帧确认、焦点及战斗输入隔离。
- SinglePlayerFlowPlayModeTests 3/3：新增真实Input System键盘01→35→01→02→01→35→34、标题即时更新、失焦、长按、逐格所选地图、初始生命/等级/分数、进入战斗实际tick与HUD34、不带入Space射击；另两项原失败/胜利投影回归。
- SinglePlayerFlowTests 6/6，CampaignPlayModeTests 1/1：原状态与实际切关/失败/重开/焦点回归。
- 首轮编译和全部定向测试通过；原始结果见Evidence。未重复35关地形/遍历成功测试，没有新人工试玩/截图/自然通关声明。
- 上轮保存的关卡/矩阵/Campaign/作者工具hash全部一致，见Data-Protection.json；场景未修改。git diff --check通过。
- 最终SinglePlayer场景clean，Editor stopped/backgroundfalse，见final-state.json；Console起点2406，结束结果见console.json，无新增warn/error。

## 试玩

打开SinglePlayer场景Play，Enter进入STAGE 01，用↑/↓或W/S选关，再Enter开始。可以从01向上立即到35，便于测试末关；选择过程中战斗tick不推进。
