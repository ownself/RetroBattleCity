# BC203-view — 只读敌方显示组件交接

状态：组件 ready，仍须独立只读审查；不是 BC-203 整体验收。仅新增 EnemyTankView、独立 PlayMode 专项、Editor 自动生成的两个脚本 meta 及 View-* 证据。不改已有逻辑、其他 owner 文件、场景/资产、主计划或看板；不暂存/提交/推送。

## 起点与保留

- main / `7de290635ca8b097022b379e911fbef1234baefc`。
- `View-Starting-State.json` 在本组件代码写入前保存 Git 状态与全部已跟踪/未忽略既存文件 SHA256；BC-201/202、wave、主代理文档和 `.vsconfig` 均为起点工作，不归因本组件。
- `View-File-Integrity.json` 最终检查既存文件：内容变化 0、丢失 0；index 为空。只新增本组件文件。Unity 原实例 PID26924 / port7800。

## 公开 API 与调用方式

```csharp
public sealed class EnemyTankView : MonoBehaviour
{
    public void Initialize(TankState tank, TankDefinition definition, SpriteCatalog catalog, Material material);
    public void Render();
    public void Clear();
}
```

集成 owner 负责创建 view 所在 GameObject、传入 **同一个**已注册的共享敌方 TankState 和其对应 TankDefinition、现有 SpriteCatalog / SpriteUnlit Material，完成每帧逻辑结算后调用 `Render()`。本组件没有 Update、移动、动画时钟、武器、伤害、出生计数或 AI。玩家仍走现有 renderer。

```csharp
var view = new GameObject("Enemy display").AddComponent<EnemyTankView>();
view.transform.SetParent(displayRoot, false);
view.Initialize(sharedEnemy, matchingDefinition, sprites, existingSpriteUnlitMaterial);
// 每渲染帧，在共享逻辑结算后：
view.Render();
// 宿主回收/重开：
view.Clear(); // 或销毁 view/owner GameObject
```

- Initialize 先 Clear 旧绑定，再校验非空参数、Enemy 阵营、definition.Validate，并一次性快照 `definition.spritePrefix.{up|left|down|right}.{0|1}` 八个现有 Sprite 引用。绑定失败抛出清晰异常且旧显示保持清空；不继续显示旧坦克。宿主负责定义与逻辑实例匹配（TankState 本身没有公开保存定义身份）。
- 已核对 SpriteInventory.csv 第98～105行及真实 SpriteCatalog：白色基本敌前缀 `tank.white.enemy.basic`，四方向两帧，16×16，PPU16。无重新切片/生成 Sprite。
- 单个自有 child SpriteRenderer；Actors 排序，直接 `sharedMaterial` 引用，不使用 `.material`，不生成材质副本。Render 不访问 catalog/拼接字符串/创建对象，仅取共享 `Motor.Facing`、`TrackFrame`、Position、IsAlive。
- 位置采用与玩家相同的 `Mathf.Round(px)` 后 `PixelGrid.ToWorld`，不插值、不写回 Motor；child 使用世界位置，宿主显示层应维持单位缩放/无旋转以保持原像素尺寸。
- 死亡后下一次 Render 立即禁用 renderer 并置空 sprite。无碰撞体/刚体，不释放或修改共享 tank/武器/弹。
- Clear 幂等：解绑 tank，释放八帧引用、sprite、material，禁用 renderer；保留唯一空 renderer 供重新 Initialize 复用。
- OnDisable 清空绑定（组件禁用或 owner 不激活均生效）；重新启用必须显式 Initialize，不能重现旧 session 的显示。Initialize 会立即 Render（不活动时不显示）。
- OnDestroy 先清空/隐藏，再销毁自有 child；即使只销毁组件，child 也不残留。Unity Destroy 延迟到帧末，隐藏同步生效。销毁 owner 则 child 随层级清理。共享逻辑实例由宿主拥有，view 从不 Destroy 它。

## 实际验证

- 实际脚本编译：首次 `script recompile` 启动 compiling；5秒后 status 暂报 `EDITOR_NOT_RUNNING`（`View-Compile-Result.json`），未冒称编译成功。
- 按父代理明确批准的一次同 CLI/原实例有界恢复：先将 Git/partial源码现场保存到 `Temp/BC203-view`，只读 pipeline list 确认同 PID26924/7800、reachable、instancesInSafeMode=0；`status --until-ready --timeout 60` 后 `script recompile_status` 实际 completed / failed=false / errors=[] / compilationFailed=false，见 `View-Recovery-*` 和 `View-Compile-Recovered-Result.json`。未重启/关对话框/换协议。
- Pipeline 0.9.0-exp.1 提示 0.9.1-exp.1 可更新：仅记录，未升级；CLI版本检查仍 beta.14/current。
- 实际 `test run --mode playmode --filter EnemyTankViewTests --async_tests true`，最终 **12/12 Passed，0 failed/skipped/inconclusive**。保存请求、逐条最终 JSON 和原始 NUnit `View-PlayMode-Results.xml`；独立解析 XML 核实12条 Passed。Console 的 started:50 是 runner 发现树计数，不是50条执行/通过；完成结果和XML均12条。
- 4个方向各逐 tick 推进真实 ArenaSession.SetTankCommand / AdvanceFrame，断言位移、共享 Facing、8tick切帧、16tick周期，以及每步对应 catalog 的同一 Sprite；停止24tick+100次Render不独立动画。
- 45px/s 实际共享运动到104.75px，显示105px，逻辑不反写；共享材质/Actors/PPU16/16×16及无2D/3D物理组件断言。
- 暂停时真实渲染帧前进，重复 Render 与暂停 AdvanceFrame 不改变 tick/共享姿态/帧，恢复仅走下一 tick、DroppedTicks=0。
- 用共享玩家阵营武器实际发弹，经 BulletWorld 命中及 Session 伤害使敌方死亡；断言命中目标、HP0/停止、renderer隐藏/清空、后续命令及 Render 不复活/重复扣血。无伪造私有字段/测试后门。
- Clear 幂等、五次绑定仅一个 renderer、替换后原 tank 继续移动但不影响显示、禁用/激活不自动重现、销毁组件与 owner 后无 child 残留且共享 tank 仍可控制，均实际断言。
- 快照测试绑定后修改临时定义前缀并销毁临时 catalog 副本，真实四向运动仍读取已快照帧；缺失前缀/null/玩家绑定失败会清旧显示。
- Console cursor294→302，8条全部 log，新增 warning/error=0，无 reset/dropped；历史 buffer error=1 是既存历史，不宣称历史全空。最终 Editor non-Play、compiling=false、compilationFailed=false，runInBackground=false，既有autoTick=true/16ms；本项未调用任何运行设置写命令，无临时设置需要恢复。
- `git diff --check` 通过；`git diff --cached --name-only` 空。Git仅提示既存文件未来LF→CRLF，不代表本项更改。

## 探索失败与未覆盖

- 一次只读 pipeline list 加 `--project-path` 被 CLI拒绝（INVALID_COMMAND_ARGS，pipeline list 本身不接受该参数）；移除此非支持参数后只读发现原项目实例，所有 `unity command` 始终带 project-path/caller/skill。
- 一次试读 AutoTick 的 eval 因内部类型不可直接 typeof 引用而 Roslyn 编译失败，原始 `View-Run-Settings-Probe-Failed.json` 保留；之后仅用反射只读查询成功。不是项目编译错误/PlayMode失败，没有生产代码或断言修补；本轮12个测试首跑全部通过。早先 Temp 中 EditorPrefs 查询键不代表实际 AutoTick baseline，不用于结论。
- 没有运行全量 EditMode/PlayMode 回归，不复用历史256/38冒充本轮结果；没有真实Stage01自然出生、场景绑定、截图、人工体验、构建、长期压力或图像像素比较。绑定四向帧/位置的断言验证 SpriteRenderer 投影，不冒称最终相机画面人工验收。
- 下一步建议：独立只读审查本组件，再由 integration owner 接入自然出生/管理 view 生命周期与真实Stage01验证。禁止本组件推进 BC-204 以后。
