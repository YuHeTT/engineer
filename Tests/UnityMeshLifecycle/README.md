# 合并模板生命周期回归

该检查必须在真实 Unity Play 模式运行，不能用纯 .NET 模拟 Destroy 的帧末语义。

本轮临时工程：`Temp/mesh-lifecycle-check`。工程 Assets 中复制当前 `Assets/Scripts/UI/Farm/MeshCombiner.cs`，并把本目录的 `MeshLifecycleCheck.cs` 放到临时工程的 `Assets/Editor`。使用相同 Unity 2022.3.62f2c1。

命令行参数：`-batchmode -nographics -projectPath "<临时工程绝对路径>" -executeMethod MeshLifecycleCheck.Run -logFile "<日志绝对路径>"`。不传 `-quit`，检查会进入 Play 并以 0（通过）或 1（失败）自动退出。不要对用户正在打开的主工程执行该入口。

通过标志 `[MeshLifecycle] ALL 7 PASSED`。旧代码的同帧删除检查必须失败；修复后全部通过。本检查不测 FPS，帧率需要真实农场 F10 采样。
