# M0 authoring and verification

这些`.cs`文件是通过Unity Pipeline `script eval_file`运行的独立语句块，**不放进Assets编译**。它们不使用AssetPostprocessor/MenuItem，不自动在导入时修改素材。

实际验证环境：Unity6000.6.7f1 / URP17.6.0 / Pipeline0.9.0-exp.1 / Unity CLI1.0.0-beta.14。命令形状来自该Editor目录；升级工具后先重新发现，不假定旧名称。

## 资源重建

在工程根目录、非Play Mode、所有场景已保存的情况下：

```bash
unity status --format json
unity command script --format json
unity command script eval_file --caller plugin --skill sprite-editor --file Tools/Phase0/PrepareSprites.cs --format json
unity command script eval --caller plugin --skill 2d-pixel-perfect --code 'return BattleCity.Editor.Phase0Baseline.Build();' --format json
```

如果新代码还未编译，先执行`unity command script recompile --caller plugin --skill unity-cli`，等待Editor重新ready，再用`script recompile_status`确认completed、failed=false。编译期间listener可能重启，不连续盲发写入命令。

- `PrepareSprites.cs`：保留原始PNG，生成派生透明图，设置Importer，做Sprite编辑能力检查，按照显式语义配方切319片，保留同名Sprite GUID，更新name/fileID pairs，并导出CSV。
- `Phase0Baseline.Build()`：已编译的Editor辅助类，创建/更新Catalog、玩家原型参数、Unlit材质、排序层、专用URP配置、Tile样本与展台。重复调用会重建展台，拒绝覆盖未保存场景；不重置已编辑的玩家参数，不移动原素材，不加入构建入口。
- 本阶段不创建运行时关卡；关卡格式为`LevelDefinition` ScriptableObject，后续填充内容。

## 自动测试

```bash
unity command test run --caller plugin --skill unity-cli --mode editor --filter BattleCity.Tests --async_tests true --format json
unity command test status --caller plugin --skill unity-cli --format json
```

等待status=completed并检查summary，不把“started”当作通过。最终结果见`Docs/Stage0/EditMode-results.json`。

## Play Mode显示验证

1. 运行`SetGameViewSize.cs`，默认768×720；它记录原Game View选择，在当前Editor会话添加命名为`BattleCity M0 ...`的固定分辨率。该脚本使用本Unity版本内部Editor API，仅作验证工具，不进入玩家构建。
2. `editor set_auto_tick`，记录/临时打开`editor.playmode set_run_in_background --enable true`，然后`editor.playmode start`，等待ready。
3. `job wait --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout 10`，确认met=true。
4. 运行以下命令采集每个方向/帧；direction为up/left/down/right，frame为0/1：

```bash
unity command script eval --caller plugin --skill 2d-pixel-perfect --code 'BattleCity.Editor.Phase0Baseline.SetGalleryFrame("up",0);'
unity command capture game_view --caller plugin --skill 2d-pixel-perfect --source screen --width 768 --height 720 --save_path BattleCitySmoke/gallery-up-0.png
```

全部8张采集后，恢复图库up/0，再分别设置256×240和1000×700：

```bash
unity command script eval --caller plugin --skill 2d-pixel-perfect --code 'BattleCity.Editor.Phase0Baseline.SetGalleryFrame("up",0); UnityEditor.SessionState.SetInt("BattleCity.M0.Width",256); UnityEditor.SessionState.SetInt("BattleCity.M0.Height",240);'
unity command script eval_file --caller plugin --skill 2d-pixel-perfect --file Tools/Phase0/SetGameViewSize.cs
# 等待帧推进后捕获；另一次改为1000/700，并使用对应输出名
unity command capture game_view --caller plugin --skill 2d-pixel-perfect --source screen --width 256 --height 240 --save_path BattleCitySmoke/baseline-256x240.png
```

5. 确认已生成`gallery-up-0.png`、`baseline-256x240.png`、`baseline-1000x700.png`等10个最终截图，再运行：

```bash
unity command script eval_file --caller plugin --skill 2d-pixel-perfect --file Tools/Phase0/VerifyPixels.cs
unity command console status --caller plugin --skill unity-cli --format json
```

`VerifyPixels.cs`逐RGBA字节比较1/2/3倍画面并验证黑边，差异非0即抛错；合格证据复制到`Docs/Stage0/Screenshots/`，不依赖截图看起来清晰来推断整数缩放。

6. `editor.playmode stop`，等待ready；恢复原run-in-background覆盖（本次原值off，故`--enable false`）。最后运行`RestoreValidationState.cs`恢复Game View选择、删除本次分辨率预设与Assets内临时截图。

本Pipeline版本`capture`实际把`save_path`放到Assets下；以命令返回的`savedPath`为准。不要将整个临时截图目录当作正式游戏资源提交。此配方不是Windows构建或战斗玩法的验收。
