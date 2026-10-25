# 显式60 FPS渲染上限

## 变更

用户要求显式把游戏帧率上限设为60。新增`Assets/BattleCity/Scripts/Core/GameFrameRate.cs`：

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
public static void Apply()
{
    QualitySettings.vSyncCount = 0;
    Application.targetFrameRate = TargetFramesPerSecond; // 60
}
```

每次运行时在场景加载前自动生效，无需给场景挂新组件，也适用于独立Player。关闭运行时VSync是为了避免桌面平台忽略targetFrameRate、改由高刷新率屏幕决定帧率。官方说明：[Application.targetFrameRate](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Application-targetFrameRate.html)。这是软件目标上限，不保证所有帧严格等长或硬件不足时仍能达到60；未宣称独立Player已构建/性能实测。

只控制渲染策略，不改`LogicClock`/`PixelGrid`的独立60Hz逻辑、timeScale、fixedDeltaTime、移动/弹速或游戏规则。未编辑或保存ProjectSettings、场景、Prefab、资产。三个新脚本及由Unity自动生成的meta，保留此前全部补丁和`.vsconfig`，无提交/推送。

## 验证

Unity6000.6.7f1，原Editor7800，编译成功。

- `GameFrameRateTests` 3/3 EditMode：从VSync=0/1/2、旧cap=-1/144/30均应用为60及VSync0；timeScale/fixedDeltaTime不变；模拟30/60/144个渲染帧推进1秒仍各为60逻辑tick，无丢tick。每项finally恢复原生设置。
- `GameFrameRatePlayModeTests` 1/1 PlayMode：测试不手动调用Apply，直接验证启动钩子已设置60/0；加载SinglePlayer真实场景后设置保持、帧实际推进。一次0.510秒Editor样本推进30帧，约58.8fps，仅短时Editor观察，不是独立Player或全性能统计。
- 共4唯一Passed，原始逐项结果见`EditMode.json`/`PlayMode.json`，编译见`Compile.json`。
- Console cursor2897→2911新增14条全log，最终groundTruth错误0/警告0、compilationFailed=false；历史缓冲未清。见`Console*.json`。
- 停止测试后targetFrameRate原生值仍为60，而VSync自动恢复原值1；已仅在非Play下将本轮测试遗留的Editor targetFrameRate恢复为原值-1。最终SinglePlayer非Play/非暂停、场景clean、background=false、cap=-1/VSync1，与开始一致；见`Before.json`/`AfterTests.json`/`After.json`。下次Play仍由持久源码自动应用60/0，不是撤销游戏限帧策略。
- `git diff --check`通过（仅LF→CRLF提示），ProjectSettings无diff。27项既有补丁源码/资产hash保持不变，新6项源码/meta hash见`Audit.json`。无临时输入设备、观察器或设置遗留。

未机械重跑未变的游戏规则套件；本轮不宣称全回归、独立构建或用户人工试玩通过。
