# UI 接入完成报告与 Unity 内待办清单

> **2026-10-16 最新补完状态**：输入校验、失败不推进、累计实际水肥投入、总产量单位与面积语义、收益中间态及三屏脚本挂载已修正；新增本轮数值回归 **60/60 通过**。房车巡航开合已接线，实际动画、排版和 A1–A6 仍待 Unity Play 验收。挂起项 D1/D2 与经济参数保持待定。详见[输入输出与收益补完记录](输入输出与收益补完记录-20261016.md)；下文旧轮次“全部完成”或 1000 占位描述不能代表本轮状态，当前默认 YmaxGeo=733。


> 本轮已完成：**asmdef 分层 + 桥接层 + 五个屏接入 + 60 项集成测试**。
> 本文说明：做完了什么、**你必须在 Unity 里手动做什么**、以及**我做不到的事**。
>
> 相关：[UI接入方案与完整程序差距清单.md](UI接入方案与完整程序差距清单.md)（方案）、
> [脚本使用与集成指南.md](脚本使用与集成指南.md)（用法）

---

## 一、本轮完成的内容

### 1.1 程序集分层（新增 2 个 asmdef）

| 程序集 | 位置 | 引用 |
|---|---|---|
| `Engineer.UI` | `Scripts/UI/Engineer.UI.asmdef` | Core + Config + Unity.TextMeshPro |
| `Engineer.UI.Editor` | `Scripts/UI/Editor/Engineer.UI.Editor.asmdef` | UI + Core + Config + TMP + TMP.Editor |

**解决什么问题**：之前 `Scripts/UI/` 没有 asmdef，全落在 `Assembly-CSharp`，
那 11 个编辑器白盒构建器脚本会被编进**玩家包体**。现在它们进了 `Editor` 平台专属程序集。

### 1.2 桥接层（新增 2 个文件）

| 文件 | 作用 |
|---|---|
| `UI/Bridge/StageMap.cs` | UI 的 1~5 ↔ Core 的 S1~S4+Done，**唯一换算点**；另含 UI 指标下标 → `SoilMetric` 映射 |
| `UI/Bridge/GameSession.cs` | 包住 `Simulation`，对外只给"界面视角"的方法（`Begin` / `Soil` / `Fit` / `Recommend` / `CommitFromDosage` / `Revenue`） |

`GameSession` 是**纯 C#，不引用 UnityEngine** —— 所以它能在 Unity 外被测试。

### 1.3 改造的现有文件（4 个）

| 文件 | 改动 |
|---|---|
| `UI/Core/GameContext.cs` | 加 `BindSession` / `Session`；`GetIndicator` 改为"开始了读内核、没开始读输入"；`SetIndicator` 在模拟开始后拒绝写入并警告；六项默认值改为平川台区间中值；`Reset()` 同时复位内核 |
| `UI/Flow/GameFlowController.cs` | 新增 `configAsset` 字段与 `Session` 属性；`Awake` 创建 `GameSession` 并注入 context；新增 `CommitStageDosage()` / `BeginSimulation()`；`AdvanceStage` 标注为"仅白盒走查用" |
| `UI/Screens/AnalyzeScreen.cs` | 左栏 4 项建议改为由 `RecommendedPlan` 填充（不再是"待接入"）；输入框进入时预填系统建议值；确认时走内核 `Commit`；走了兜底规则会在标题上提示 |
| `UI/Screens/FarmStageScreen.cs` | 状态文字改用 `GrowthLevel`；9 项指标从内核读（会随水肥变化）；新增可选的 `showDelta` 变化箭头 |
| `UI/Screens/RobotCruiseScreen.cs` | 探测值改为读内核真实土壤；新增 `probeErrorAmplitude` 配置（当前 0 = 无误差）；说明"更新数据"在误差模型定下来之前不写回内核的原因 |

### 1.4 新增的屏脚本（3 个）

| 文件 | 作用 |
|---|---|
| `UI/Screens/RevenueScreen.cs` | 收益屏。公式区（四阶段肥力系数/平均/水分/产量）+ 结果框 + S2~S5 表格 + 静态四项。**大部分字段能按节点名自动查找**，减少手工绑定 |
| `UI/Screens/LandParamsScreen.cs` | **补上了此前的阻塞缺口**：六项基础数据之前没有任何脚本读取。现在读六个输入框 → 按 `SimConfig` 区间裁剪 → 写回 context → 启动内核。字段同样支持自动查找 |
| `UI/Screens/OptionCardButton.cs` | 选项卡片按钮：点击时把选中的地形/作物写回 `GameContext`（之前只跳转不记录） |

### 1.5 验证

| 工具 | 结果 |
|---|---|
| `Temp/compile-all.ps1` | **5 个程序集全部编译通过**（新增覆盖 UI 两个） |
| `Temp/run-smoketest.ps1` | Core 逻辑 **42 项全绿** |
| `Temp/run-integration.ps1` | **新增**，桥接层 **60 项全绿** |

集成测试覆盖的关键点（这些是编译不会报错、只有跑才能发现的）：

```
=== 4. 完整流程 ===
  [OK] S1 提交后土壤发生了变化（★ 关键验证点）
  [OK] S1 静态四项保持不变
  ... S2 / S3 / S4 同上
  [OK] 结束后 UI 阶段为 5（收获期）
  [OK] 总产量 = 单位面积产量 × 10 亩（9375.00）

=== 5. 关键风险：StageDosage 字段顺序 ===
  [OK] 施钾 100 后钾储量上升（145.0 → 645.0）
  [OK] 没施氮时氮不会暴涨（100.0 → 96.7）—— 说明钾/氮没接反
```

> **`compile-all.ps1` 能编 UI 层**，是因为它借用了 `Library/ScriptAssemblies` 里
> Unity 已构建的 `UnityEngine.UI.dll` / `Unity.TextMeshPro.dll`
> （2022.3 里它们是源码包，Unity 安装目录没有 dll）。
> **这意味着 UI 层的语法/类型错误现在不用打开 Unity 就能发现。**

---

## 二、⚠️ 我做不到的事（必须你在 Unity 里手动完成）

### 2.1 无法创建 `.meta` 文件（Unity 专属）

新增的 5 个文件（`StageMap.cs` / `GameSession.cs` / `RevenueScreen.cs` /
`LandParamsScreen.cs` / `OptionCardButton.cs`）和 2 个 asmdef **没有 `.meta`**。
Unity 首次导入时会自动生成，**这是正常的，不用管**。

### 2.2 无法替换场景里的组件（3 处）

场景里有三个屏挂的还是通用 `PlaceholderScreen`，需要换成专用脚本。
`.unity` 是 5 万行的 YAML，手工改引用容易写坏，**建议你在 Inspector 里操作**：

| 场景对象 | 现在挂的 | 要换成 |
|---|---|---|
| `UI_Root/Screen_Canvas/Screens/Screen_Revenue` | `PlaceholderScreen` | **`RevenueScreen`** |
| `UI_Root/Screen_Canvas/Screens/Screen_LandParams` | `PlaceholderScreen` | **`LandParamsScreen`** |
| `UI_Root/Screen_Canvas/Screens/Screen_Confirm` | `PlaceholderScreen` | **`ConfirmScreen`**（修「本局配置一览」12 行永远是占位值的问题） |


**操作步骤**（Inspector 里）：

```
1. 选中该屏对象
2. 在 PlaceholderScreen 组件上右键 → Remove Component
   【注意】先记下 actions 列表里的按钮配置！换组件会丢失它
3. Add Component → 搜索新的脚本名 → 添加
4. 新组件里：
   - state 会自动保持（因为新脚本继承 PlaceholderScreen）
   - 重新填 actions（或者依赖脚本内部的自动查找 + 自己在 Inspector 里挂按钮）
5. 重新拖引用（RevenueScreen / LandParamsScreen 支持按节点名自动查找，
   所以 TMP 文本可以先留空，运行时会自己找；但 Button 建议手动挂）
```

> **更省事的办法**：`RevenueScreen` 与 `LandParamsScreen` 都写了
> `EnsureResolved()`，会在运行时按节点路径自动找文本与按钮。
> 所以理论上**添加组件后什么都不填也能跑**。先这样试，缺什么再补。

> **【2026-10-16 更新】这三屏不用再手工删/加组件了 —— 有一键工具：**
> `Tools ▸ UI ▸ 修复屏脚本挂载（确认/土地参数/收益）`
> 会把 `PlaceholderScreen` 换成对应专用脚本，**保留按钮接线**（`actions`）并同步改
> `GameFlowController.screens` 列表；动手前自动把 `UI.unity` 备份到 `Temp/UI.unity.bak_screens_*`。
> 命令行：`-executeMethod Engineer.UI.EditorTools.ScreenScriptFixer.Run`。
> 另外 `UiWhiteboxBuilder` 现在**直接产出**这三个专用脚本，重建场景不会再退回 `PlaceholderScreen`。

### 2.3 无法给卡片加 `OptionCardButton`（需在场景里挂）

`Screen_LandSelect` 和 `Screen_PlantSelect` 的卡片按钮需要挂 `OptionCardButton`：

```
1. 选中卡片按钮对象，例如
   UI_Root/Screen_Canvas/Screens/Screen_LandSelect/Body/LandCards/Cell_Land1/Card_Land1/Button_Select
2. Add Component → OptionCardButton
3. 填：
   - Option Id    = flat_plain
   - Display Name = 平川台
   - Category     = Land
   - Target State = LandParams
4. 同时**从 PlaceholderScreen 的 actions 里删掉这颗按钮的 GoTo 配置**
   （否则会跳转两次：先 OptionCardButton 跳一次，actions 再跳一次）
```

四张地形卡 + 四种作物卡，共 8 处。**一张卡一个 ID**（与 `SimConfig.Terrains[].Id` 对应）：

| 卡片 | Option Id | Display Name | Category | Target State |
|---|---|---|---|---|
| Cell_Land1 | `flat_plain` | 平川台 | Land | LandParams |
| Cell_Land2 | `low_wetland` | 泽洼域 | Land | LandParams |
| Cell_Land3 | `steep_ridge` | 崇岭阶 | Land | LandParams |
| Cell_Land4 | `plateau` | 高原坪 | Land | LandParams |
| Cell_Plant1 | `corn` | 玉米 | Plant | Confirm |
| Cell_Plant2 | `beet` | 甜菜 | Plant | Confirm |
| Cell_Plant3 | `sweet_potato` | 红薯 | Plant | Confirm |
| Cell_Plant4 | `potato` | 土豆 | Plant | Confirm |

> v1 只开放平川台 + 玉米，其余卡片的 `Button` 应保持 `interactable = false`。

### 2.4 无法创建 `SimConfigAsset` 资产并挂上

```
1. Project 窗口右键 → Create → Engineer → 数值配置
2. 命名如 Assets/Config/SimConfig_Main.asset
3. 选中 UI_Root/UI_Flow，在 GameFlowController 上把 configAsset 拖进去
```

**不挂也能跑** —— 代码会 `Debug.Log` 一句"未指定数值配置资产，使用文档默认值"，
然后用 `SimConfig.CreateDefault()`。所以这一步**可以延后**。

### 2.5 无法验证的事（需要你打开 Unity 看）

| 要验证什么 | 怎么看 |
|---|---|
| **中文字体** | 打开 `UI.unity`，看 10 个屏的中文是不是正常显示。若是方块，说明 TMP 文本用的字体资产不含中文字形，需要换成 `Assets/fonts/msyh SDF.asset` |
| **场景引用是否还在** | 加了 asmdef 后脚本 GUID 不变，理论上引用不会丢。但请在 Console 看有没有 `Missing (Mono Script)` 之类警告 |
| **组件替换后是否报错** | 换 `RevenueScreen` / `LandParamsScreen` 之后 Console 有没有红字 |
| **实际跑一遍** | Play → 走完整流程，见下面的自测清单 |

### 2.6 ⚠️ 重建场景前必须知道的事

**重建 `UI.unity`（白盒构建器）前，先看清楚"什么会被覆盖"。**

它会重新生成整个 `UI.unity`，后果：
- ~~`Screen_Revenue` 会被改回 `PlaceholderScreen`（你刚换的 `RevenueScreen` 没了）~~ ✅ **已修**：构建器现在直接产出 `RevenueScreen` / `LandParamsScreen` / `ConfirmScreen`
- 已经手工换过组件的旧场景，可以先用 `Tools ▸ UI ▸ 修复屏脚本挂载` 一键修回来，不必整场重建
- 卡片上挂的 `OptionCardButton` 全丢
- `GameFlowController.configAsset` 引用丢失

构建器已按上面更新；重建后仍然要手动补的是 `OptionCardButton` 与 `GameFlowController.configAsset`。

---

## 三、Unity 内自测清单

替换组件之后，按这个流程跑一遍。**第 6 步是关键**。

| # | 操作 | 期望 |
|---|---|---|
| 1 | 打开 `UI.unity`，Play | 初始界面出现，Console 有 `[UI] → 初始界面` |
| 2 | 点「开始体验」→ 选平川台 | 进入土地基础数据设置，六个输入框显示 6.9 / 21.5 / 100 / 16.5 / 145 / 10 |
| 3 | 改一下 pH（比如 6.2），点「选择植物」 | 进入植物选择；Console 无红字 |
| 4 | 选玉米 → 确认 → 开始模拟 | 进入农场界面；右上显示 `S1 播种苗期`，状态显示「长势良好/正常」 |
| 5 | 点「开始分析」 | **左栏 4 项建议不再是"待接入"**，而是真实方案（如「少量有机肥」/「有机肥 40、氮 1.5 kg/亩」/「20 m³/亩」） |
| 6 | 记下农场界面 9 项指标的数值 → 改输入框（比如钾改成 0）→ 确认 | 回到农场界面，**9 项指标变了**（钾储量应当明显变化）★ **这一步通了就说明内核真的接上了** |
| 7 | 一路点完 S2 / S3 / S4 | 每个阶段的建议都不同，指标持续演变 |
| 8 | S4 确认后 | 自动进入收益屏，显示产量与四阶段肥力系数 |
| 9 | 点「开始新的模拟」 | 回到初始界面；再走一遍时数据是新的，土壤不会残留上一局 |

**第 6 步没通过** 的话，检查：`GameFlowController` 上有没有 Console 报"没有注入 GameSession"，或者 `Screen_LandParams` 是否真的换成了 `LandParamsScreen`。

---

## 四、还剩什么（未完成，需后续）

### 需要策划决定的（工程侧已用占位）

| 项 | 当前占位 | 在哪改 |
|---|---|---|
| `Ymax,geo` | **1000** | `SimConfig.Yield.YmaxGeo` |
| pH 氮肥酸化系数 | **0.15**（偏强，导致 pH 净下降） | `SimConfig.Advance.pH_NCoef` |
| pH / 速效氮输入上限 | **8.0 / 130**（会落进下降段） | `SimConfig.DynamicInputRanges` |
| 复合肥折算 | **1:1:1** | `SimConfig.CompoundRatio` |
| 探测误差 | **0（= 真值）** | `RobotCruiseScreen.probeErrorAmplitude` |
| 经济层（单价/成本） | **未启用** | `RevenueReport` 预留字段 |
| S2 / S4 规则空洞 | **走兜底** | 策划补规则 |
| 产量单位 | **kg/亩**（文档未定义） | `RevenueScreen.yieldUnit` |

### 仍需编码/美术的

| 项 | 说明 | 估时 |
|---|---|---|
| **农场 3D 可视化** | `Screen_FarmStage` 下还是空的（只有 `ViewPlaceholder_Note`）。需要接地形/玉米/房车模型 + 相机 | 3~5 天 |
| **巡航的真实 3D 交互** | 现在是点按钮推进一格。最低成本方案：HUD 上叠半透明网格，点击格子推进覆盖 | 1~2 天 |
| **玉米长势贴图对应** | 现有资源是 `幼苗/成长/成熟` 三前缀，阶段有四个 —— **对应关系要问策划** | 依赖策划 |
| 中文文案校对 | 术语建议规范化（酸衡值→土壤pH 等） | 0.5 天 |
| 存档 / 回放 | 无需求，不做 | — |

---

## 五、本轮新增/修改的文件清单

### 新增（7 个）

```
Assets/Scripts/UI/Engineer.UI.asmdef
Assets/Scripts/UI/Editor/Engineer.UI.Editor.asmdef
Assets/Scripts/UI/Bridge/StageMap.cs
Assets/Scripts/UI/Bridge/GameSession.cs
Assets/Scripts/UI/Screens/RevenueScreen.cs
Assets/Scripts/UI/Screens/LandParamsScreen.cs
Assets/Scripts/UI/Screens/OptionCardButton.cs
```

### 修改（5 个）

```
Assets/Scripts/UI/Core/GameContext.cs
Assets/Scripts/UI/Flow/GameFlowController.cs
Assets/Scripts/UI/Screens/AnalyzeScreen.cs
Assets/Scripts/UI/Screens/FarmStageScreen.cs
Assets/Scripts/UI/Screens/RobotCruiseScreen.cs
```

### 验证工具（`Temp/`，不进 Assets、不进构建）

```
Temp/compile-all.ps1          五个程序集的编译验证（已扩展到 UI）
Temp/run-smoketest.ps1        Core 逻辑 42 项
Temp/run-integration.ps1      桥接层 60 项（本轮新增）
Temp/integration/IntegrationTest.cs
```

**每次改完脚本，跑这两条就能覆盖绝大部分问题**：

```powershell
$src = Get-Content -LiteralPath 'Temp\compile-all.ps1' -Raw -Encoding UTF8; Invoke-Expression $src
$src = Get-Content -LiteralPath 'Temp\run-integration.ps1' -Raw -Encoding UTF8; Invoke-Expression $src
```

---

## 六、一句话总结

**改动完成度：代码侧 100%，Unity 场景侧需要你手动做 3 件事。**

**代码侧已完成**：asmdef 分层、桥接层、五个屏接入、60 项集成测试全绿。
"填水肥 → 土壤演变 → 收益"这条链路**已经在脱离 Unity 的测试里验证通了**。

**你需要在 Unity 里做的三件事**（按重要性）：
1. 把 `Screen_Revenue` 和 `Screen_LandParams` 的组件换成 `RevenueScreen` / `LandParamsScreen`
2. 给 8 张选项卡片挂 `OptionCardButton` 并填 ID
3. 确认中文字体正常、Play 走一遍自测清单

**我做不到的**：创建 `.meta`、改 5 万行 `.unity` YAML 里的组件引用、在 Unity 里实际运行验证。
这三件事都必须由你在编辑器里完成。
