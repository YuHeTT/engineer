# UI 接入方案与完整程序差距清单

> 本文回答三件事：
> 1. 当前 UI 系统的实际状态（已做到哪一步）
> 2. 怎么把 UI 和数值内核接起来（具体改哪些文件、什么顺序）
> 3. 距离"完整程序"还差什么（按优先级）
>
> 相关：[脚本使用与集成指南.md](脚本使用与集成指南.md)、[数值内核代码框架说明.md](数值内核代码框架说明.md)

---

## 一、先说一个好消息：UI 场景已经完整接线了

我解析了 `Assets/Scenes/UI.unity`（1.5 MB / 5 万行 / 1792 个对象）。
**结论是场景侧的接线工作已经做完了**，不存在"UI 元件还没连上脚本"的问题。

### 场景结构

```
Directional Light
EventSystem
Main Camera
UI_Root
├── Screen_Canvas  (Canvas, RenderMode=0 屏幕空间覆盖)
│   └── Screens
│       ├── Screen_Welcome          state=0   actions=12 项
│       ├── Screen_Intro            state=1   actions=8 项
│       ├── Screen_LandSelect       state=2   actions=20 项
│       ├── Screen_LandParams       state=3   actions=8 项
│       ├── Screen_PlantSelect      state=4   actions=20 项
│       ├── Screen_Confirm          state=5   actions=8 项
│       ├── Screen_FarmStage        state=6   [FarmStageScreen] 专用脚本
│       ├── Screen_RobotCruise      state=7   [RobotCruiseScreen] 专用脚本
│       ├── Screen_Analyze          state=8   [AnalyzeScreen] 专用脚本
│       └── Screen_Revenue          state=9   actions=8 项
└── UI_Flow  [GameFlowController]   startState=0  screens=10 项（全部已赋值）
```

### 接线验证结果（逐个字段检查，全部非空）

| 脚本 | 字段 | 状态 |
|---|---|---|
| `GameFlowController` | `screens`（10 个屏） | ✅ 10/10 |
| | `startState` / `logTransitions` | ✅ |
| `FarmStageScreen` | `hintText` / `stageTitleText` / `stageStatusText` | ✅ |
| | `indicatorValueTexts`（9 个） | ✅ 9/9 |
| | `primaryButton` / `primaryButtonLabel` | ✅ |
| `RobotCruiseScreen` | `hintText` / `coverageFill` / `coverageText` / `uncoveredText` | ✅ |
| | `sourceValueTexts`（9）/ `probeValueTexts`（9） | ✅ 18/18 |
| | `updateButton` / `manualButton` / `autoButton` | ✅ |
| `AnalyzeScreen` | `stageTitleText` / `confirmButton` / `confirmLabel` | ✅ |
| | `dosageInputs`（5）/ `suggestionValues`（4） | ✅ 9/9 |
| 7 个 `PlaceholderScreen` | `actions` 列表 | ✅ 每条都已赋值（8~20 项） |
| `UiSkinSlot` | `role` | ✅ 67 个实例全部有角色 |

**所以"把 UI 和脚本连接起来"这一件事，你的队友已经做完了。**

真正缺的是另一件事：**UI 现在是一套"白盒"（能点、能走流程、但显示的都是编造的数据），它还没有接上数值内核。**

---

## 二、当前 UI 的真实状态

### 已经做到的

- ✅ 10 个状态、12 张界面图对应的流程骨架，能从头点到尾
- ✅ 状态机带历史栈（`Back()`）、能重启（`Restart()`）
- ✅ 每个屏的按钮行为已配置（`GoTo` / `Back` / `Restart` / `LogOnly`）
- ✅ 三种专用屏（农场 / 巡航 / 分析）有独立脚本，字段都已接
- ✅ 一套 UI 皮肤系统（`UiSkinSlot` + `UiSkin` + `UiSkinMapping`），67 个位置有角色标记
- ✅ 一套**编辑器白盒构建器**（`UiWhiteboxBuilder` 等 14 个 Editor 脚本），能生成/重建整个场景
- ✅ `PlaceholderScreen` 支持"开始新的模拟"时自动复位文本与输入框

### 关键事实：UI 完全没有引用数值内核

我扫了 `Assets/Scripts/UI/` 下全部 19 个脚本：

```
=== UI 脚本是否引用了 Core / Config ===
  (空)
```

`GameContext.cs` 里甚至**明确禁止**了引用：

```csharp
/// 注意：本类不引用 Core 数值内核，避免白盒界面阶段产生编译依赖。
```

这意味着 `GameContext` 现在是一个**纯数据容器**，所有数值都是手写的默认值：

```csharp
public float Ph = 6.5f;                 // 注意：这不是平川台区间中值（6.9）
public float OrganicMatter = 22f;
public float AvailableNitrogen = 95f;
public float LabilePhosphorus = 17f;
public float Potassium = 150f;
public float[] StaticIndicators = { 470f, 11f, 9f, 0.07f };   // 写死的，不随机
public readonly float[] StageDosage = new float[5];           // 只存最后一阶段
```

### 白盒的"假"在哪里

| 位置 | 现状 | 有没有接内核 |
|---|---|---|
| `AnalyzeScreen.suggestionValues` | 硬编码 4 条 `"待接入"` 占位文案 | ❌ |
| `FarmStageScreen.stageStatusText` | 写死 `"白盒占位：正常生长"` | ❌ |
| `FarmStageScreen` 9 项指标 | 直接读 `GameContext` 的手写默认值 | ❌ |
| `RobotCruiseScreen.RevealNext()` | 用 ±3% 抖动**编造**探测值 | ❌ |
| `GameContext.StageDosage` | 单数组，只保留最后一次输入 | ❌ |
| 指标演变 | **完全没有** —— 阶段之间土壤不会变 | ❌ |

**最关键的一条**：`GameContext` 没有"当前土壤会随水肥演变"的概念。
所以现在点完一轮流程，土壤数值是**恒定不变**的。

---

## 三、怎么接：具体操作

### 3.1 核心思路：加一个桥接层，不要改 Core

**原则：Core 一行都不用改。** 它是纯 C#、已通过 42 项测试，接 UI 不需要动它。

```
        ┌──────────────────────────────────────────┐
        │  Assets/Scripts/Core/  （不动）            │
        │  Simulation / SimConfig / 全部数值逻辑     │
        └────────────────────┬─────────────────────┘
                             │ 被引用
        ┌────────────────────▼─────────────────────┐
        │  Assets/Scripts/UI/Bridge/  （新建）       │
        │  ┌────────────────────────────────────┐   │
        │  │ GameSession.cs                      │   │
        │  │  · 持有 Simulation 与 SimConfig      │   │
        │  │  · 把 UI 的 1~5 阶段映射到 Core 的   │   │
        │  │    S1~S4 + Done                     │   │
        │  │  · 提供 UI 友好的读写方法            │   │
        │  └────────────────────────────────────┘   │
        │  ┌────────────────────────────────────┐   │
        │  │ StageMap.cs                         │   │
        │  │  阶段编号换算（唯一的转换点）        │   │
        │  └────────────────────────────────────┘   │
        └────────────────────┬─────────────────────┘
                             │ 被 GameContext 持有
        ┌────────────────────▼─────────────────────┐
        │  UI/  （改造 GameContext 与 3 个专用屏）   │
        └──────────────────────────────────────────┘
```

### 3.2 需要改/新建的文件清单

| # | 文件 | 操作 | 说明 |
|---|---|---|---|
| 1 | `UI/Engineer.UI.asmdef` | **新建** | UI 运行时程序集，引用 `Engineer.Core` + `Engineer.Config` |
| 2 | `UI/Editor/Engineer.UI.Editor.asmdef` | **新建** | 仅编辑器，引用上面的 + `Engineer.Core` |
| 3 | `UI/Bridge/GameSession.cs` | **新建** | 桥接层核心，包住 `Simulation` |
| 4 | `UI/Bridge/StageMap.cs` | **新建** | 阶段编号换算（UI 1~5 ↔ Core S1~S4/Done） |
| 5 | `UI/Core/GameContext.cs` | **改造** | 加 `GameSession`，指标读写转发给它 |
| 6 | `UI/Screens/AnalyzeScreen.cs` | **改造** | 建议文案从 `RecommendedPlan` 取；确认时 `Commit` |
| 7 | `UI/Screens/FarmStageScreen.cs` | **改造** | 状态与指标从 `SuitabilityResult` / `CurrentSoil` 取 |
| 8 | `UI/Screens/RobotCruiseScreen.cs` | **改造** | 探测值改为读真实土壤（误差模型待策划） |
| 9 | `UI/Screens/RevenueScreen.cs` | **新建** | 收益屏目前是 `PlaceholderScreen`，需要用 `RevenueReport` |
| 10 | `UI/Screens/LandParamsScreen.cs` | **新建** | 土地基础数据六项目前没有输入，见第五节 F2 |
| 11 | `UI/Flow/GameFlowController.cs` | **小改** | `Restart()` 时重置 `GameSession`；`AdvanceStage()` 走 `Commit` |

**顺序很重要**：先 1→2→3→4（骨架），再 5（桥接），最后 6~11（屏幕）。
每做完一步都跑一次 `Temp/compile-all.ps1` 确认没编译错。

### 3.3 阶段编号的坑（必须处理的第一个问题）

| | UI 现状 | Core |
|---|---|---|
| 阶段编号 | `StageIndex` = **1 ~ 5** | `Stage` = **S1~S4 + Done** |
| 含义 | 1..4 是生长阶段，5 是收获期 | S1..S4 参与数值，收获期不属于 `Stage` |
| 阶段名 | `GameContext.StageNames[]` 5 项 | `Stages.DisplayName()` 4 项 |

**必须有一个唯一的转换点**，不能在各处手写 `+1` / `-1`：

```csharp
// UI/Bridge/StageMap.cs
public static class StageMap
{
    /// UI 的 1~4 → Core 的 S1~S4；UI 的 5（收获期）返回 false
    public static bool TryToCore(int uiStage, out Stage stage)
    {
        stage = Stage.S1;
        if (uiStage < 1 || uiStage > Stages.Count) return false;
        stage = Stages.FromIndex(uiStage - 1);
        return true;
    }

    /// Core 的 S1~S4/Done → UI 的 1~5
    public static int ToUi(Stage stage)
        => stage == Stage.Done ? 5 : Stages.ToIndex(stage) + 1;
}
```

### 3.4 桥接层的接口设计

`GameSession` 应该提供**UI 视角**的方法，而不是把 `Simulation` 原样暴露：

```csharp
namespace Engineer.UI
{
    /// <summary>UI 与数值内核之间的唯一桥梁。GameContext 持有它。</summary>
    public sealed class GameSession
    {
        private readonly SimConfig _config;
        private readonly Simulation _sim;

        public GameSession(SimConfig config)
        {
            _config = config ?? SimConfig.CreateDefault();
            _sim = new Simulation(_config);
        }

        public SimConfig Config => _config;
        public ISimulationView View => _sim.View;      // 只读，给界面显示

        public bool IsStarted => _sim.CurrentSoil != null;

        /// 玩家在「土地基础数据设置」填完六项后调用
        public void Begin(SoilState input) => _sim.Initialize(input);

        /// 当前 UI 阶段（1~5）
        public int UiStage => StageMap.ToUi(_sim.CurrentStage);

        /// 当前土壤（只读展示）
        public SoilState Soil => _sim.CurrentSoil;

        /// 当前适宜度 / 建议 / 长势
        public SuitabilityResult Fit => _sim.Evaluate();
        public RecommendedPlan Recommend() => _sim.Recommend();
        public GrowthLevel Growth() => _sim.CurrentGrowth();

        /// 本阶段建议是否走了兜底（文档规则空洞）
        public bool RecommendUsedFallback => _sim.PlanTable.LastMatchUsedFallback;

        /// 提交本阶段水肥，返回结果
        public StageResult Commit(FertilizerApplication applied) => _sim.Commit(applied);

        /// 收益
        public RevenueReport Revenue(float areaMu) => _sim.Revenue(areaMu);

        /// 重置
        public void Restart() => _sim.Reset();
    }
}
```

### 3.5 `GameContext` 怎么改造

**思路：保留现有公开字段名不动**（因为场景里已经引用了它们），内部改为转发。

```csharp
public class GameContext
{
    private GameSession _session;

    /// <summary>由 GameFlowController 在 Awake 时注入。</summary>
    public void BindSession(GameSession session) { _session = session; }

    public GameSession Session => _session;

    // ---- 六项基础数据：仍然是玩家输入，直接存字段（不改名）----
    public float LandAreaMu = 10f;
    public float Ph = 6.9f;              // 改：与平川台区间中值一致
    public float OrganicMatter = 21.5f;
    public float AvailableNitrogen = 100f;
    public float LabilePhosphorus = 16.5f;
    public float Potassium = 145f;

    // ---- 静态四项：改为从 session 读（不再手写死值）----
    public float[] StaticIndicators
    {
        get
        {
            if (_session?.Soil == null) return new float[] { 470f, 11f, 9f, 0.07f };
            var s = _session.Soil;
            return new[] { s.Eh, s.BaseIon, s.As, s.Hg };
        }
    }

    /// <summary>GetIndicator 改为：模拟开始后读内核，未开始读玩家输入。</summary>
    public float GetIndicator(int index)
    {
        if (_session?.Soil != null && index >= 0 && index < SoilIndicators.Count)
            return _session.Soil[(SoilMetric)index];
        return FromInputFields(index);          // 未开始时读上面的字段
    }
}
```

**为什么保留字段名**：场景 YAML 里已经引用了这些字段的序列化名，
改名会导致 Inspector 里的赋值丢失。这是"能不动就不动"的地方。

### 3.6 三个专用屏怎么改

#### `AnalyzeScreen`（最关键的一屏）

```csharp
public override void OnEnter(object args)
{
    base.OnEnter(args);
    var s = Flow?.Context?.Session;
    if (s == null) return;

    // 标题
    if (stageTitleText != null)
        stageTitleText.text = "S" + s.UiStage + " " + Stages.DisplayName(s.View.CurrentStage);

    // 建议：从 RecommendedPlan 取，替换掉 "待接入" 占位
    var rec = s.Recommend();
    if (rec != null && suggestionValues != null)
    {
        var amounts = rec.ResolvedAmounts(s.Config);     // 复合肥已折算
        SetText(suggestionValues, 0, rec.Label);
        SetText(suggestionValues, 1, $"有机肥 {amounts.Organic:0.#}、氮 {amounts.N:0.#}、磷 {amounts.P:0.#}、钾 {amounts.K:0.#} kg/亩");
        SetText(suggestionValues, 2, $"{rec.BaseIrrigation:0.#} m³/亩");
        SetText(suggestionValues, 3, rec.Physiology);
    }

    // 文档规则空洞：提示策划未定义
    if (s.RecommendUsedFallback && stageTitleText != null)
        stageTitleText.text += "（该情况文档未定义，使用兜底建议）";

    // 输入框预填建议值（玩家可改）—— 这是「采用建议」的默认行为
    PrefillDosageInputs(rec, s.Config);
}

public void OnConfirmDosage()
{
    var s = Flow?.Context?.Session;
    if (s == null) return;

    var applied = ReadDosageInputs();        // 5 个输入框 → FertilizerApplication
    var result = s.Commit(applied);          // ← 数值内核在这里推进

    if (result != null) Flow.GoTo(GameState.FarmStage);
    else Flow.GoTo(GameState.Revenue);       // S4 提交完 = 结束了
}
```

**注意顺序**：`StageDosage` 的字段顺序是
`灌溉量 / 有机肥 / 钾肥 / 氮肥 / 磷肥`（**钾在氮前面**），
构造 `FertilizerApplication` 时别接错：

```csharp
new FertilizerApplication(
    irrigation: dosage[0],
    organic:    dosage[1],
    k:          dosage[2],     // ← 钾
    n:          dosage[3],     // ← 氮
    p:          dosage[4]);
```

#### `FarmStageScreen`

```csharp
public void RefreshFromContext()
{
    var s = Flow?.Context?.Session;
    ...
    // 状态：由长势决定，替换掉 "白盒占位：正常生长"
    if (stageStatusText != null && s != null && s.IsStarted)
        stageStatusText.text = GrowthLevels.DisplayName(s.Growth());

    // 9 项指标：从内核的土壤读，而不是 GameContext 的手写值
    if (s != null && s.Soil != null)
        for (var i = 0; i < indicatorValueTexts.Length; i++)
            indicatorValueTexts[i].text = SoilIndicators.Format(i, s.Soil[(SoilMetric)i]);
}
```

#### `RobotCruiseScreen`

```csharp
private void RevealNext()
{
    var s = Flow?.Context?.Session;
    var index = revealedCount++;

    // 真实值（探测误差模型待策划定义，先按"探测值 = 真值"实现）
    float truth = s?.Soil != null ? s.Soil[(SoilMetric)index] : 0f;
    probeValues[index] = truth;
    ...
}
```

> ⚠️ **这里有个已知的策划漏洞**（I2）：探测值等于真值时，"更新数据"这一步没有信息增益。
> 按你的要求**先采用占位**（= 真值），但把接口留好：
> 将来加误差只需要改 `RevealNext()` 里的一个乘法。
> 建议把误差系数放到 `SimConfig` 里，这样策划能自己调。

---

## 四、按顺序执行的操作清单

### 第 1 步：加两个 asmdef（5 分钟）

**为什么必须做**：现在 `Assets/Scripts/UI/` 没有 asmdef，
所有 UI 脚本都落在 `Assembly-CSharp` 里。而 `Assembly-CSharp`
是"自动引用所有 asmdef"的，**能编译，但有两个问题**：

1. `UI/Editor/` 那 14 个编辑器脚本会被编进**玩家包体**（白盒构建器不该进构建）
2. 没有明确依赖声明，将来容易出现"谁引用了谁"说不清的情况

```jsonc
// Assets/Scripts/UI/Engineer.UI.asmdef
{
    "name": "Engineer.UI",
    "rootNamespace": "Engineer.UI",
    "references": ["Engineer.Core", "Engineer.Config", "Unity.TextMeshPro"],
    "includePlatforms": [],
    "noEngineReferences": false
}
```

```jsonc
// Assets/Scripts/UI/Editor/Engineer.UI.Editor.asmdef
{
    "name": "Engineer.UI.Editor",
    "rootNamespace": "Engineer.UI.EditorTools",
    "references": ["Engineer.UI", "Engineer.Core", "Unity.TextMeshPro"],
    "includePlatforms": ["Editor"],
    "noEngineReferences": false
}
```

> `Unity.TextMeshPro` 这个引用名字需要确认——TMP 的 asmdef 名叫
> `Unity.TextMeshPro`（在 `Library/PackageCache/com.unity.textmeshpro@3.0.7/Scripts/Runtime/`）。
> 写完先在 Unity 里看一下 Console 有没有报引用找不到。

### 第 2 步：建桥接层（半天）

- `UI/Bridge/StageMap.cs`
- `UI/Bridge/GameSession.cs`

**这一步不碰任何现有文件**，所以不会破坏现在能跑的流程。
写完跑 `compile-all.ps1` 确认编译通过。

### 第 3 步：改造 `GameContext`（半天）

- 加 `BindSession` / `Session`
- `GetIndicator` / `SetIndicator` 改为"开始了就读内核，没开始读输入字段"
- 六项默认值改成与平川台区间中值一致

### 第 4 步：接 `GameFlowController`（半小时）

```csharp
[SerializeField] private SimConfigAsset configAsset;   // Inspector 里挂配置资产
private GameSession session;

private void Awake()
{
    var cfg = configAsset != null ? configAsset.BuildSimConfig() : SimConfig.CreateDefault();
    session = new GameSession(cfg);
    context.BindSession(session);
    ...
}
```

**这里是唯一需要动场景的地方**：在 `UI_Flow` 对象的
`GameFlowController` 上挂一个 `SimConfigAsset` 引用。
（或者干脆先不挂，代码里 `CreateDefault()` 兜底，跑通之后再挂资产。）

### 第 5 步：接三个专用屏（1~2 天）

按 3.6 的顺序：`AnalyzeScreen` → `FarmStageScreen` → `RobotCruiseScreen`。

**先接 `AnalyzeScreen`**，因为它一接上，整条"填水肥 → 土壤演变"的链路就活了，
演示效果立刻能看出来。

### 第 6 步：接收益屏（半天）

`Screen_Revenue` 现在是 `PlaceholderScreen`，需要新建 `RevenueScreen.cs`：
- 显示 `RevenueReport` 的四阶段肥力系数、平均肥力、`Y`、达成率
- 显示四个静态指标（文档要求列出来）
- `Button_Restart` 改为调 `GameFlowController.Restart()`

**注意**：场景里的 `Screen_Revenue` 已经挂的是 `PlaceholderScreen`，
换成 `RevenueScreen` 需要在 Unity 里手动替换组件（或用白盒构建器重新生成）。
替换后要**重新赋值** `actions` 列表。

### 第 7 步：补 `LandParamsScreen` 与选择回写（1~2 天）

见第五节 F1 / F2。

### 第 8 步：农场可视化（3~5 天，见第五节 F5）

---

## 五、距离"完整程序"还差什么

按"不做就没法演示"到"可以砍"排序。

### 🔴 必须做（否则 Demo 跑不通）

#### F1 · `LandParamsScreen` 没有真正的输入（阻塞！）

`Screen_LandParams` 现在挂的是 `PlaceholderScreen`，
场景里虽然有 `ParamForm/Row_Param1..6/Input_1..6` 这些输入框，
但**没有脚本去读它们**。

**后果**：玩家在第四屏填的六项基础数据**根本没进入 `GameContext`**，
所以模拟一直用的是代码里写死的默认值。这会让"用户可自主调整"这个卖点完全不成立。

**要做**：
1. 新建 `LandParamsScreen : PlaceholderScreen`
2. 暴露 `TMP_InputField[] paramInputs`（6 个，顺序：面积 / pH / 腐殖丰度 / 速效氮 / 磷活度 / 钾储量）
3. `OnEnter` 时用 `SimConfig.DynamicInputRanges` 初始化范围提示
4. 点"选择植物"时把 6 个值写回 `GameContext`，然后调 `session.Begin(soil)` 启动模拟
5. 在 Unity 里把 `Screen_LandParams` 的组件换成新脚本，重新赋 `actions`

#### F2 · 地形/作物的选择没有回写

`Screen_LandSelect` 的卡片（`Cell_Land1..4/Card_Landn/Button_Select`）
和 `Screen_PlantSelect` 的卡片，按下的行为是 `GoTo`，
**没有把选中的地形/作物写进 `GameContext`**。

**后果**：`LandTypeId` / `PlantTypeId` 永远是默认的"平川台 / 玉米"。
由于 v1 只做这两种，**演示时看不出来**，所以优先级低于 F1。
但扩展时会立刻成为坑。

**要做**：给这些卡片按钮加一个轻量组件（如 `OptionCardButton`），
按下时写 `GameContext` 再 `GoTo`。

#### F3 · 场景里的农场是"占位注释"，没有 3D 内容

`Screen_FarmStage` 下有 `ViewPlaceholder_Note` 节点，
说明当前农场画面是**空的**（只有 HUD）。

**要做**：
- 4 套地形模型接入（`Assets/可能用到的建模/土地/`，已有 4 个 glb）
- 玉米三档长势模型接入（`玉米_幼苗/成长/成熟 × 好/正常/差`，已有 9 个 glb）
- 房车模型接入（已有 fbx + glb）
- 第三人称/俯视相机

**⚠️ 顺便发现一个对应缺口**：现有玉米资源是 `幼苗 / 成长 / 成熟` **三个前缀**，
但阶段有 **四个**（S1 苗期 / S2 旺长 / S3 生殖 / S4 灌浆）。
文档说 S1 只显示土地、S5 与 S4 共用，所以中间的对应关系需要策划确认。

#### F4 · 巡航没有真正的 3D 交互

现在 `RobotCruiseScreen` 的"手动巡航"是**点一次按钮推进一格**，
不是真的操控机器人在田里走。

**最低成本方案（推荐）**：不建 3D 机器人，直接在农场 HUD 上叠一层
"半透明网格"，玩家点击网格格子来推进覆盖 —— 视觉上就是"机器人扫过"，
实现成本极低，演示效果够用。

**完整方案**：3D 田块网格 + 机器人模型 + 球形触发器 + 相机跟随（见项目执行流程 W4–W6）。

### 🟠 应该做（影响说服力）

| # | 缺什么 | 说明 |
|---|---|---|
| F5 | **收益屏没接** | `Screen_Revenue` 还是 Placeholder，需要显示 `RevenueReport`（见 3.4 第 6 步） |
| F6 | **经济层参数缺失** | `RevenueReport` 预留了 `PricePerKg` / `TotalCost`，但没数据。**需要策划给单价与成本** |
| F7 | **中文文案校对** | 文档里的术语建议改（酸衡值→土壤pH、速效氮元→速效氮、土骨离子→盐基离子…） |
| F8 | **中文字体检查** | 场景里的 TMP 文本用的是哪个字体资产？需要确认是 `msyh SDF`（36 MB 那个），否则中文可能显示为方块 |

### 🟡 可以砍或延后

| # | 缺什么 | 建议 |
|---|---|---|
| F9 | 探测误差模型 | 策划未定义（I2）。先占位=真值 |
| F10 | 存档 / 回放 | 无需求，不做 |
| F11 | 过渡动画、音效 | 打磨阶段 |
| F12 | 4 种地形 / 4 种作物 | 你已确认 v1 只做平川台 + 玉米 |
| F13 | 无人机 | 简介里提了，但没设计。建议从文案里删掉或补设定 |

---

## 六、策划漏洞：占位值集中登记表

按你的要求，这些先采用占位，后续要能改。
**建议把它们全部放进 `SimConfig`，这样改的时候不碰代码。**

| 漏洞 | 占位值 | 现在在哪 | 策划改了之后 |
|---|---|---|---|
| `Ymax,geo`（产量上限） | **1000** | `SimConfig.Yield.YmaxGeo` | 改一个数 |
| pH 氮肥酸化系数 | **0.15**（文档原值，偏强） | `SimConfig.Advance.pH_NCoef` | 改一个数（建议 0.05） |
| 初始 pH 输入上限 | **8.0**（会落进下降段） | `SimConfig.DynamicInputRanges[0].Max` | 改一个数（建议 7.0） |
| 速效氮输入上限 | **130**（会落进下降段） | `DynamicInputRanges[2].Max` | 改一个数（建议 110） |
| S1 判定空洞 | 走**行1**（兜底） | `PlanTable` 三级匹配 | 补规则 |
| S2/S4 规则空洞 | 走**兜底规则** | 同上 | 补规则 |
| 复合肥折算 | **1:1:1** | `SimConfig.CompoundRatio` | 改一个数 |
| 经济层（单价/成本） | **未启用**（`PricePerKg = 0`） | `RevenueReport` 预留字段 | 补参数 |
| 探测误差 | **= 真值**（无误差） | 待定 | 见 F9 |
| 长势三档判定 | 其余归**正常** | `SimConfig.Growth` | 策划已确认 |
| 输入水肥上限 | **建议量 × 5** | `SimConfig.Inputs` | 改倍数或绝对值 |

**好消息**：上表**没有一项需要改 Core 代码**。
全部都是 `SimConfig` 的字段，而且 `SimConfig.Validate()` 会挡住非法值。
策划改完之后，你只需要跑一次 `Temp/compile-all.ps1` 确认没写错。

---

## 七、工作量估算

| 步骤 | 内容 | 估时 |
|---|---|---|
| 1 | 加 2 个 asmdef | 0.5 h |
| 2 | 桥接层（`StageMap` + `GameSession`） | 0.5 天 |
| 3 | 改造 `GameContext` | 0.5 天 |
| 4 | 接 `GameFlowController` | 0.5 h |
| 5 | 接三个专用屏 | 1.5 天 |
| 6 | 收益屏 | 0.5 天 |
| 7 | `LandParamsScreen` + 选择回写 | 1.5 天 |
| 8 | 农场可视化（模型 + 相机） | 3~5 天 |
| — | **合计（到"能完整演示"）** | **约 8~10 天** |

**其中第 1~6 步（约 4 天）做完，程序就已经"数值通了"** ——
能填水肥、土壤会演变、能算出产量和收益。这是最关键的里程碑。

第 8 步（农场可视化）是纯表现，**可以和第 1~7 步并行**，也可以先用 HUD 顶替。

---

## 八、下一步建议

### 立刻能做的三件事

1. **加两个 asmdef** —— 5 分钟，但能避免编辑器脚本进包体
2. **确认 TMP 中文字体** —— 打开 Unity 看界面上的中文是否正常显示（F8）
3. **建桥接层** —— 不碰现有文件，零风险

### 建议的验证方式

每接完一屏，用这个流程自测：

```
Unity 里点开 UI.unity → Play
→ 走到「开始分析」
→ 看左侧建议是否从 "待接入" 变成了真实方案（如"少量有机肥 / 20 m³/亩"）
→ 随便改一个输入值 → 确认
→ 回到农场界面，看 9 项指标是否**变了**
```

**最后一步是关键验证点** —— 如果指标会随输入变化，说明内核真的接上了。

### 我要提醒的一个风险

`UI/Editor/` 下有一套**白盒构建器**（`UiWhiteboxBuilder` 等），
它能重新生成整个 `UI.unity` 场景。

**接入内核的过程中，不要跑这个构建器。** 否则：
- `Screen_Revenue` 之类的改动会被它覆盖回 `PlaceholderScreen`
- 你手动在 Inspector 里挂的 `SimConfigAsset` 引用会丢

等内核接入稳定之后再决定要不要更新构建器。

---

## 九、一句话总结

**场景侧的接线已经做完了**（10 个屏、状态机、所有字段都已赋值），
真正缺的是**把白盒 UI 接到数值内核上**。

**接的方式**：新建一个 `UI/Bridge/` 桥接层持有 `Simulation`，
改造 `GameContext` 让它转发到内核，然后逐个改造三个专用屏。
**Core 一行都不用改。**

**必须处理的两个坑**：
1. 阶段编号（UI 用 1~5，Core 用 S1~S4+Done）—— 要有唯一转换点
2. `StageDosage` 的字段顺序是 `灌溉/有机肥/**钾**/**氮**/磷肥`，钾在氮前面，别接错

**最关键的一步是 `AnalyzeScreen`** —— 它一接上，
"填水肥 → 土壤演变 → 产量"这条链路就活了，演示效果立刻能看出来。

**策划漏洞全部是配置字段，不含代码改动**，改完跑一次编译验证即可。
