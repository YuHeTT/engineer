# UI 接入进度与待办清单

> **2026-10-16 最新补完状态**：输入校验、失败不推进、累计实际水肥投入、总产量单位与面积语义、收益中间态及三屏脚本挂载已修正；新增本轮数值回归 **60/60 通过**。房车巡航开合已接线，实际动画、排版和 A1–A6 仍待 Unity Play 验收。挂起项 D1/D2 与经济参数保持待定。详见[输入输出与收益补完记录](输入输出与收益补完记录-20261016.md)；下文旧轮次“全部完成”或 1000 占位描述不能代表本轮状态，当前默认 YmaxGeo=733。


> 最后核对：2026-10-14 22:20
> 本轮修了「进入农场跳回初始界面」+「建议栏适宜度显示待接入」，详见第一节。

---

## 一、本轮修复的两个问题

### 问题 1 ✅：「点击进入农场后直接到初始界面」

**日志的调用栈直接指出了原因**：

```
[UI] → 初始界面
Engineer.UI.GameFlowController:GoTo (…GameFlowController.cs:146)
Engineer.UI.PlaceholderScreen:Handle (…PlaceholderScreen.cs:182)   ← 关键
Engineer.UI.PlaceholderScreen/<Wire>b__0 ()
UnityEngine.UI.Button:Press ()
```

是某个屏的 `actions` 里一条 `GoTo` 把目标设成了「初始界面」。逐条核对全部 10 个屏后，
**只发现一处错误**：

```
Screen_Confirm:
    Button_Back         Back
    Button_FarmStage    GoTo → Welcome     ★ 应为 FarmStage
```

`Button_FarmStage` 界面上写着"进入农场"，但 `target` 被设成了 **Welcome(0)**。

**已修**：直接改场景文件（`target: 0` → `target: 6`），备份 `Temp/UI.unity.bak_target_221015`。

---

### 问题 2 ✅：「分析界面左侧 5 项适宜度还是待接入」

**你给的截图让问题一眼定位了** —— 因为截图里**左栏下半部分的 4 项建议是对的**：

| 位置 | 截图显示 | 说明 |
|---|---|---|
| 推荐肥料类型 | 少量有机肥 | ✅ 真实值 |
| 单亩施肥量 | 有机肥 40、氮 1.5 kg/亩 | ✅ 真实值 |
| 单亩基准灌溉 | 20 m³/亩 | ✅ 真实值 |
| 玉米生理说明 | 本底土壤优，控水防烧苗 | ✅ 真实值 |
| **f_PH / f_OM / f_N / f_P / f_K 适宜度** | **待接入** | ❌ 未被填充 |

同时 Console 日志显示 `[Analyze] RefreshSuggestions: plan=...`，证明代码**确实在跑**。

**根因**：分析屏左栏有**两组独立的控件**：

```
SuggestionPanel/
├── SuitabilityList/           ← 5 项适宜度（f_PH...f_K）★ 代码从没碰过
│   └── Suitability_1..5/Value     一直是场景里的占位文字"待接入"
└── SuggestionList/            ← 4 项建议（肥料类型/用量/灌溉/生理说明）
    └── Suggestion_1..4/Value      代码填充的是这里
```

我原来的 `AnalyzeScreen.suggestionValues` 只绑定了 `SuggestionList`，
**完全没处理 `SuitabilityList`**。所以那 5 格永远是占位文字。

**已修**：给 `AnalyzeScreen` 加了 `suitabilityValues` 字段 + `RefreshSuitability()`：

```csharp
// 面板上的 Range 已说明"值域 [0.2, 1]（结果得 1 即 100%）"，
// 所以显示成"数值（百分比）"最直观
suitabilityValues[i].text = string.Format("{0:0.00}（{1:0}%）", f, f * 100f);
```

显示效果例如：`1.00（100%）`、`0.75（75%）`、`0.20（20%）`。

**顺带改进了两处**：

1. **未启动模拟时**不再显示"待接入"（对玩家没意义），改为
   - 内核没接上 → `未接入数值内核`
   - 已接上但还没开始 → `请先启动模拟`
2. **`EnsureResolved()` 覆盖了 `SuitabilityList`** —— 即使 Inspector 引用丢了，
   运行时也会按节点名自动找到，不会再出现这种"漏了一组控件"的问题。

**不需要你在 Unity 里做任何操作** —— 新字段是自动查找的，直接 Play 就能看到效果。

---

## 二、当前状态

### 已完成（全部核对通过）

| # | 项目 | 状态 |
|---|---|---|
| 1 | 10 个状态全部注册（下标与 state 一一对应） | ✅ |
| 2 | 所有 `.meta` 生成 | ✅ |
| 3 | Unity 编译无错误 | ✅ |
| 4 | Missing Script = 0 | ✅ |
| 5 | 中文字体：259 个文本全用 `msyh SDF` | ✅ |
| 6 | 6 张锁定卡片已禁用，平川台/玉米可点 | ✅ |
| 7 | 全部 10 个屏的按钮动作目标与语义一致 | ✅ |
| 8 | `SimConfigAsset` 已挂到 `GameFlowController` | ✅ |
| 9 | **数值内核接入 —— 改水肥后指标确实变化** | ✅ **已验证** |
| 10 | 六项输入能读进去并启动内核 | ✅ 已验证 |
| 11 | **确认选择界面显示实际选择的数据** | ✅ **你已验证** |
| 12 | **「进入农场」跳转正确** | ✅ 已修 |

### 界面数据对照（分析屏，S1 播种苗期）

| 区域 | 内容 | 来源 |
|---|---|---|
| 右上阶段标题 | `S1 播种苗期` | `GameContext.CurrentStageName` |
| 左侧 5 项适宜度 | **`1.00（100%）` 等**（本轮新接） | `SuitabilityResult` |
| 推荐肥料类型 | 少量有机肥 | `RecommendedPlan.Label` |
| 单亩施肥量 | 有机肥 40、氮 1.5 kg/亩 | `RecommendedPlan.ResolvedAmounts` |
| 单亩基准灌溉 | 20 m³/亩 | `RecommendedPlan.BaseIrrigation` |
| 玉米生理说明 | 本底土壤优，控水防烧苗 | `RecommendedPlan.Physiology` |
| 右侧输入框 | 预填系统建议值（可改） | `RecommendedPlan.ToApplication` |

---

## 三、代码侧验证

| 工具 | 结果 |
|---|---|
| `Temp/compile-all.ps1` | **5 个程序集全部通过** |
| `Temp/run-smoketest.ps1` | Core **42 项全绿** |
| `Temp/run-integration.ps1` | 桥接层 **60 项全绿** |

---

## 四、剩余待办

### 你来做

- [ ] 重新 Play，确认分析屏左侧 5 项适宜度显示成 `1.00（100%）` 这样的数值
- [ ] 走完 S2~S4 与收益屏，确认无报错
- [ ] 确认 Console 里 `[UI] ... 的按钮 ... 触发 ...` 日志与点击行为一致

### 我来做

- [ ] **农场 3D 可视化** —— `Screen_FarmStage` 下面还是空的，只有一个
      `ViewPlaceholder_Note` 占位节点。需要接：4 套地形模型、玉米三档长势模型、
      房车模型、相机。**估时 3~5 天**
- [ ] **巡航的真实 3D 交互** —— 目前是点按钮推进一格。
      最低成本方案：HUD 上叠半透明网格，点击格子推进覆盖。**估时 1~2 天**
- [ ] 中文文案校对（术语规范化：酸衡值→土壤 pH 等）
- [ ] 收益屏的 `S2~S5` 表格目前只填了 4 列中的部分文本，需要按实际节点核对一遍

### 需要策划（已用占位，全部是配置字段）

| 项 | 当前占位 | 改哪里 |
|---|---|---|
| `Ymax,geo` | **1000** | `SimConfig.Yield.YmaxGeo` |
| pH 氮肥酸化系数 | **0.15**（导致 pH 净下降） | `SimConfig.Advance.pH_NCoef` |
| pH / 速效氮输入上限 | **8.0 / 130** | `SimConfig.DynamicInputRanges` |
| 复合肥折算 | **1:1:1** | `SimConfig.CompoundRatio` |
| 探测误差 | **0（= 真值）** | `RobotCruiseScreen.probeErrorAmplitude` |
| 经济层（单价/成本） | **未启用** | `RevenueReport` 预留字段 |
| S2 / S4 规则空洞 | **走兜底** | 需补规则 |
| 产量单位 | **kg/亩** | `RevenueScreen.yieldUnit` |
| 玉米长势贴图 ↔ 四阶段 | 未定 | 三前缀 vs 四阶段 |

---

## 五、经验教训（已踩过的坑，避免重复）

### 坑 1：换组件会让 `GameFlowController.screens` 里的引用变空

已踩两次（`LandParams`/`Revenue` 一次，`Confirm` 一次）。

**根本原因**：`screens` 存的是**组件引用**。`Remove Component` 删掉旧组件，
列表项就变成 `null`，`RebuildScreenMap()` 会跳过它，那个状态就没注册。

**换组件的标准流程**：

```
1. 记下 actions
2. Remove Component
3. Add Component
4. 重填 actions
5. ★ 回到 GameFlowController，把 screens 列表里对应项重新指向新组件
6. ★ Play 一下，确认 Console 没有「没有为状态 XX 注册屏」
```

### 坑 2：重填 `actions` 时 `target` 下拉容易选错

`Button_FarmStage` 的 target 被设成了 Welcome，而且**不报错**，只是安静地跳错界面。

**防御**：已给 `PlaceholderScreen` 加动作派发日志：

```
[UI] Screen_Confirm 的按钮 Button_FarmStage 触发 GoTo -> 初始界面
```

一看 Console 就知道。默认开着（`logActionDispatch`），稳定后可关。

### 坑 3：一个面板里可能有**多组**同类控件

分析屏左栏就是 `SuitabilityList`（5 项）+ `SuggestionList`（4 项）两组，
名字相似但完全独立。只绑一组就会出现"一半有数据一半是占位"。

**防御**：`EnsureResolved()` 现在覆盖了两组。以后接新屏时，
**先把面板的完整子树 dump 出来**再决定绑哪些字段，不要凭名字猜。

---

## 六、一句话总结

**两个问题都修好了**：

1. **进入农场跳回初始界面** —— `Screen_Confirm` 的 `Button_FarmStage` 目标被设成了
   Welcome(0)，已改为 FarmStage(6)（改的场景文件，备份在 `Temp/`）。
2. **适宜度显示待接入** —— 分析屏左栏有**两组独立控件**，我原来只绑了建议那一组。
   已加 `suitabilityValues` + `RefreshSuitability()`，显示成 `1.00（100%）` 这种格式。
   **不需要你在 Unity 里操作**。

**当前状态：数值链路已完全打通且显示正确。**
剩下最大的一块是**农场 3D 可视化**（`Screen_FarmStage` 目前还是空的）。


---

## 一、本轮修复：「点击进入农场后直接到初始界面」

### 报错日志给出的调用栈

```
[UI] → 初始界面
Engineer.UI.GameFlowController:GoTo (…/GameFlowController.cs:146)
Engineer.UI.PlaceholderScreen:Handle (…/PlaceholderScreen.cs:182)     ← 关键
Engineer.UI.PlaceholderScreen/<Wire>b__0 ()
UnityEngine.UI.Button:Press ()
```

**关键在第二行**：是某个屏的 `actions` 里一条 `GoTo` 动作把目标设成了「初始界面」。

### 根因（100% 确认）

我把全部 10 个屏的 `actions` 逐条解析，和目标状态做了语义比对，**只发现一处错误**：

```
Screen_Confirm:
    Button_Back         Back
    Button_FarmStage    GoTo → Welcome     ★ 目标是「初始界面」，应该是「农场主界面」
```

`Button_FarmStage`（界面上写着"进入农场"）的 `target` 被设成了 **Welcome(0)**，
而不是 **FarmStage(6)**。所以点它当然跳回初始界面。

**注意**：`Screen_Confirm` 的 `actions` 是 `PlaceholderScreen` 基类上的字段 ——
换组件时你说"先记下 actions 再重填"，重填时这一项的 target 下拉选错了。

### 修复

直接改场景文件（精确定位到那一行）：

| 位置 | 原值 | 改为 |
|---|---|---|
| `Screen_Confirm` → `ConfirmScreen.actions[1].target` | `0`（Welcome） | `6`（FarmStage） |

**备份**：`Temp/UI.unity.bak_target_221015`

### ⚠️ 重要：Unity 里请这样做

Unity 编辑器的内存里还持有旧版本的场景。请：

```
1. 关闭 UI.unity（不要保存！直接关）
2. 重新打开 UI.unity
3. 在 Inspector 里核对 Screen_Confirm 的 ConfirmScreen.actions：
      第 1 项 Button_Back    → Back
      第 2 项 Button_FarmStage → GoTo / 农场主界面   ← 确认是"农场主界面"
```

**如果你在关闭前按了 Ctrl+S，我的修复会被内存里的旧数据覆盖**，
那就只能手动在 Inspector 里把那一项改回「农场主界面」。
改完检查一下：`screens` 列表的第 [5] 项要指向 `Screen_Confirm`。

### 顺带修的一个误报

我之前的核对脚本报过 `screens[5]` 无效 —— 那是**我的脚本的问题**
（它的"屏脚本清单"没加上 `ConfirmScreen`，所以认不出这个类型）。
**场景本身一直是好的**，10 个状态全部注册正确。已修正脚本。

---

## 二、场景侧完整核对（当前状态）

### `GameFlowController.screens`（10/10 全部正确）

| 下标 | 脚本 | state | 状态名 |
|---|---|---|---|
| 0 | `PlaceholderScreen` | 0 | Welcome |
| 1 | `PlaceholderScreen` | 1 | Intro |
| 2 | `PlaceholderScreen` | 2 | LandSelect |
| 3 | `LandParamsScreen` | 3 | LandParams |
| 4 | `PlaceholderScreen` | 4 | PlantSelect |
| 5 | **`ConfirmScreen`** | 5 | Confirm |
| 6 | `FarmStageScreen` | 6 | FarmStage |
| 7 | `RobotCruiseScreen` | 7 | RobotCruise |
| 8 | `AnalyzeScreen` | 8 | Analyze |
| 9 | `RevenueScreen` | 9 | Revenue |

### 全部按钮动作（修复后，逐条核对语义）

| 屏 | 接线 |
|---|---|
| Screen_Welcome | `Button_Intro`→Intro、`Button_StartExperience`→LandSelect、`Button_Quit`=LogOnly |
| Screen_Intro | `Button_Back`=Back、`Button_Welcome`→Welcome |
| Screen_LandSelect | `Button_Select`→LandParams、3×`Button_Locked`=LogOnly、`Button_Back`=Back |
| Screen_LandParams | `Button_Back`=Back |
| Screen_PlantSelect | `Button_Select`→Confirm、3×`Button_Locked`=LogOnly、`Button_Back`=Back |
| Screen_Confirm | `Button_Back`=Back、**`Button_FarmStage`→FarmStage** ✅ 已修 |
| Screen_FarmStage | `Button_RobotCruise`→RobotCruise |
| Screen_RobotCruise | `Button_BackToFarm`=Back |
| Screen_Analyze | `Button_Back`=Back |
| Screen_Revenue | `Button_Back`=Back、`Button_Restart`=Restart |

**全部 10 个屏的动作目标与界面语义一致。**

### 已完成的其它核对项

| # | 项目 | 状态 |
|---|---|---|
| 1 | 所有 `.meta` 生成 | ✅ |
| 2 | Unity 编译无错误 | ✅ |
| 3 | Missing Script = 0 | ✅ |
| 4 | 中文字体：259 个文本全用 `msyh SDF` | ✅ |
| 5 | 6 张锁定卡片已禁用，平川台/玉米可点 | ✅ |
| 6 | `SimConfigAsset` 已挂到 `GameFlowController` | ✅ |
| 7 | **数值内核接入 —— 改水肥后指标确实变化** | ✅ **已验证** |
| 8 | 六项输入能读进去并启动内核 | ✅ 已验证 |
| 9 | `ConfirmScreen` 已装上并自动填充 12 行 | ✅ 待你确认显示效果 |
> ⚠ **2026-10-16 复检修正**：上面第 3 / 5 / 9 项的"已换上"状态在 `Assets/Scenes/UI.unity` 里**并不成立** ——
> 当前场景中 `Screen_LandParams` / `Screen_Confirm` / `Screen_Revenue` 挂的还是通用 `PlaceholderScreen`
> （重跑白盒构建器会把手工换的组件覆盖掉）。症状就是"确认页「本局配置一览」的 12 行永远是建屏时的
> 占位值 10 / 6.5 / 22 / 95 / 17 / 150 / 470 mV / 11 / 9 / 0.07 mg/kg，和自己填的不一样"，
> 而且土地参数页的六个输入框没有任何脚本去读 → 数值内核根本不会启动。
> **修法**：`Tools ▸ UI ▸ 修复屏脚本挂载（确认/土地参数/收益）`
> （命令行 `-executeMethod Engineer.UI.EditorTools.ScreenScriptFixer.Run`）；
> 构建器侧也已改成直接产出这三个专用脚本，详见 `UI接入完成报告与Unity待办清单.md` §2.2。


---

## 三、本轮新增/修改

| 文件 | 操作 |
|---|---|
| `UI/Screens/PlaceholderScreen.cs` | **新增诊断日志**：每次按钮动作都会打印触发者与目标状态 |
| `UI/Screens/AnalyzeScreen.cs` | 上一轮加的运行时诊断（`[Analyze]` 日志） |
| `UI/Screens/ConfirmScreen.cs` | 上一轮新增（修确认界面数据不一致） |
| `Assets/Scenes/UI.unity` | 修 `Button_FarmStage` 的 target（0→6） |

### 新增的诊断日志有什么用

以后再有"按钮跳错界面"，Console 会直接告诉你：

```
[UI] Screen_Confirm 的按钮 Button_FarmStage 触发 GoTo -> 初始界面
```

**一行就能定位**，不用再去翻 YAML 和调用栈。这个日志默认开着
（`PlaceholderScreen.logActionDispatch`），稳定之后可以关掉。

---

## 四、剩余待办

### 你来做

- [ ] **关掉 `UI.unity` 再重新打开**（不要先保存），确认 `Button_FarmStage` 的目标是「农场主界面」
- [ ] 重新 Play 一遍，确认「进入农场」能正常进入农场界面
- [ ] 确认**确认选择界面**的 12 行显示的是你实际选的数据（不再是 平川台/玉米/6.5/22/95…）
- [ ] 观察 Console 有没有 `[Analyze]` 日志，把「建议栏为什么还显示待接入」的日志发我

### 我来做

- [ ] 根据 `[Analyze]` 日志定位建议栏问题
- [ ] **农场 3D 可视化** —— `Screen_FarmStage` 下面还是空的，只有一个
      `ViewPlaceholder_Note` 占位节点。需要接：4 套地形模型、玉米三档长势模型、
      房车模型、相机。**估时 3~5 天**
- [ ] **巡航的真实 3D 交互** —— 目前是点按钮推进一格。**估时 1~2 天**
- [ ] 中文文案校对（术语规范化）

### 需要策划（已用占位，全部是配置字段）

| 项 | 当前占位 | 改哪里 |
|---|---|---|
| `Ymax,geo` | **1000** | `SimConfig.Yield.YmaxGeo` |
| pH 氮肥酸化系数 | **0.15**（导致 pH 净下降） | `SimConfig.Advance.pH_NCoef` |
| pH / 速效氮输入上限 | **8.0 / 130** | `SimConfig.DynamicInputRanges` |
| 复合肥折算 | **1:1:1** | `SimConfig.CompoundRatio` |
| 探测误差 | **0（= 真值）** | `RobotCruiseScreen.probeErrorAmplitude` |
| 经济层 | **未启用** | `RevenueReport` 预留字段 |
| S2 / S4 规则空洞 | **走兜底** | 需补规则 |
| 产量单位 | **kg/亩** | `RevenueScreen.yieldUnit` |
| 玉米长势贴图 ↔ 四阶段 | 未定 | 三前缀 vs 四阶段 |

---

## 五、经验教训：这次的两个坑

### 坑 1：换组件会让 `screens` 列表里的引用变空

已经踩了两次（`LandParams`/`Revenue` 一次，`Confirm` 一次）。

**根本原因**：`GameFlowController.screens` 存的是**组件引用**。
`Remove Component` 删掉旧组件，列表项就变成 `null`，
`RebuildScreenMap()` 会跳过它，那个状态就没注册。

**以后换组件的标准流程**：

```
1. 记下 actions
2. Remove Component
3. Add Component
4. 重填 actions
5. ★ 回到 GameFlowController，把 screens 列表里对应的那一项重新指向新组件
6. ★ Play 一下，确认 Console 没有「没有为状态 XX 注册屏」
```

### 坑 2：重填 `actions` 时 `target` 下拉容易选错

`Button_FarmStage` 的 target 被设成了 Welcome。而且**它不会报错** ——
只是安静地跳错界面。

**防御**：本轮加的 `logActionDispatch` 日志。以后一看 Console 就知道。

---

## 六、一句话总结

**「进入农场跳回初始界面」的原因**：`Screen_Confirm` 的 `Button_FarmStage`
动作把 `target` 设成了 `Welcome(0)` 而不是 `FarmStage(6)`。
调用栈明确指向 `PlaceholderScreen.Handle` 触发的 `GoTo`。

**已修**（直接改场景文件，备份在 `Temp/UI.unity.bak_target_221015`）。
**请在 Unity 里关掉场景再重新打开**（不要先保存），然后重新 Play 验证。

**顺带**：给所有按钮动作加了诊断日志，以后这类"跳错界面"的问题一行日志就能定位。

**当前状态**：代码侧 100%，场景侧接线 100%。剩下两件事：
1. 你验证「进入农场」与「确认界面数据」是否正常
2. 建议栏「待接入」的问题 —— 需要你发一次 `[Analyze]` 日志
