# 巡航到分析状态链回归

在独立临时 Unity 工程的真实 Play 模式中运行。本轮工程在 `Temp/patrol-chain-check`。

准备：先编译 `Engineer.UI.Editor.csproj`；把 `Temp/bin/Debug` 中最新的 Engineer.Core.dll、Engineer.Config.dll、Engineer.UI.dll，以及主工程 Library/ScriptAssemblies 中的 Unity.TextMeshPro.dll、UnityEngine.UI.dll 复制到临时工程 Assets/Plugins。将 PatrolChainCheck.cs 放到临时工程 Assets/Editor。使用与主工程一致的 Unity 版本。

Unity 参数：`-batchmode -nographics -projectPath "<临时工程>" -executeMethod PatrolChainCheck.Run -logFile "<日志路径>"`。不传 -quit，入口完成后自动以0/1退出。不要对已打开的主工程运行此批处理入口。

检查基于真实 GameFlowController、FarmFieldGrid 与 FarmCruiseController，土壤和网格用固定合成数据。不使用用户场景，不做视觉验收或帧率测量。
