# BC-202 本轮命令索引

cwd=`H:/Projects/Unities/BattleCity`。所有Editor命令都附加公共参数：

```text
--caller plugin --skill unity-cli --project-path H:/Projects/Unities/BattleCity --format json
```

以下`[公共参数]`只是本索引缩写，实际调用都传入完整参数；每个原始响应的command/parameters/target可复核实际请求。未使用外部batch Editor或另一种执行模式。

## 起点与发现

```text
git status --short
git rev-parse HEAD
unity self-update --check --format json
unity status --project-path H:/Projects/Unities/BattleCity --format json
unity command [公共参数]
unity command [公共参数] --group-by flat
```

读实际目录及schema后使用下面分组命令。完整目录仅保留于`Temp/BC202-AllSchemas.json`，本项相关schema保留`CommandSchemas.json`，避免提交重复大目录。一次探索`--query 'console|test|script|eval|autotick'`只返回空目录/CLI搜索建议（substring而非正则），随后改用flat实际目录，不把空结果当不存在命令。

## Console及Editor设置

```text
unity command console status [公共参数]
unity command script eval [公共参数] -- --code '<读取runInBackground/PlayerSettings/Play/场景path及dirty状态>'
unity command editor set_auto_tick [公共参数] -- --enable true --interval_ms 16 --persist false
unity command console get [公共参数] -- --since 217 --since_session 316dee6ba1864c7dab134a9af37d8ce3 --tail 1000
```

script eval两次的完整code在`Editor-StartState.json`及`Editor-FinalState.json`参数中；均只读，不修改场景或项目设置。autoTick原本已启用，本次仅session-only维持16ms。中途Console读取保存在Temp，不作为最终报告。

## 编译及环境恢复

```text
unity command script recompile [公共参数]
unity status --until-ready --timeout 60 --project-path H:/Projects/Unities/BattleCity --format json
unity command script recompile_status [公共参数]
```

- 初版：`Compile-Request.json`、`Compile-Result.json`（compiling）、`Compile-Completed.json`（completed）。
- 测试出生点修正后：`Compile2-Request.json`启动；`Recovery-BlockedStatus.json`保存status的Reloading Domain对话框阻塞；`Compile2-Result.json`保存随后EDITOR_NOT_RUNNING。
- 只读定位：`unity pipeline list --format json`→`Pipeline-Blocked.json`，确认原PID26924/7800重新可达，不升级。
- 联系父代理获一次同协议恢复批准后：同一上述status命令→`Recovery-Ready.json`；`unity command [公共参数] --group-by flat --query recompile_status`→`Recovery-CommandSchema.json`；同一recompile_status→`Recovery-Compile-Result.json`，实际completed/failed=false；最终再读状态→`Release-Compile-Result.json`。

没有自动循环重试、重启Editor或强制关对话框。恢复后未修改生产源码，最终编译结果对应最终全量源码。

## 测试

```text
unity command test run [公共参数] -- --mode EditMode --filter BattleCity.Tests.EnemyBrainTests --async_tests true
unity command test status [公共参数]
unity command test run [公共参数] -- --mode PlayMode --filter BattleCity.Tests.EnemyBrainPlayModeTests --async_tests true
unity command test status [公共参数]
```

- 初版EditMode专项→`AI-EditMode-Request/Results.json`：29/29。
- 首次Play专项→`AI-PlayMode-Request/Results.json`：0/1，非法测试出生点异常保留。
- 修正后Play专项（同命令，等待Play域重载ready）→`AI-PlayMode-Fixed-Request/Results.json`：1/1。

最终重新执行全量，不以初版或BC-201历史结果替代：

```text
unity command test run [公共参数] -- --mode EditMode --filter BattleCity.Tests --async_tests true
unity command test status [公共参数]
unity command test run [公共参数] -- --mode PlayMode --filter BattleCity.Tests --async_tests true
unity command test status [公共参数]
unity command test list [公共参数] -- --mode all
```

`Release-EditMode-Request/Results.json`：256/256；`Release-PlayMode-Request/Results.json`：38/38。`TestDiscovery.json`及Python逐条解析生成的`Test-SetComparison.json`：294发现=294唯一执行，missing/extra/nonPassed空。

## 最终静态核对

```text
git diff --check
git diff --cached --name-only
git status --short
```

另用Python hashlib比较`Start-Hashes.json`所有基线文件，生成`Item-Delta.json`；重建3个共享源码的起点文本，逐一校验其SHA256与起点相同，生成`Item-Implementation-Delta.patch`。检查新增源码尾空白、最终测试逐条Passed/唯一性及Console所有69条增量级别，均通过。没有git add/commit/push操作。
