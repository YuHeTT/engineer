# Unity glb 导入问题排查记录

> 日期：2026-10-15
> 起因：跑 `Engineer / 农场 / 一键搭建农场场景` 报"没找到土壤块 / 没指定预制体"
> 后续：装好 glTFast 后土壤出来了，但**玉米不显示** —— 见第七节

---

## 一、结论先说

**Unity 2022.3 内置不支持 `.glb` / `.gltf`。**

项目里所有 `.glb` 都被 Unity 的 `DefaultImporter` 接管，
也就是**当成普通文件**，而不是 3D 模型。所以：

```
AssetDatabase.LoadAssetAtPath<GameObject>(".../玉米田土壤_四垄.glb")  →  null
```

明明文件在那里，却取不到 —— 这就是"找不到土壤块 / 没指定预制体"的**全部原因**。

**修法**：装 Unity 官方 glTF 导入包 **`com.unity.cloud.gltfast`**。 → **已装好，土壤正常了**

---

## 二、证据链

### 1. Unity 日志里的原始报错

```
[农场构建器] 天空盒用 sky-7（8K 白天晴空）。...
[农场构建器] 没找到土壤块：Assets/Art/Models/Soil/玉米田土壤_四垄.glb
[农场构建器] 有 9 个玉米模型没找到，已留空（会用第一个非空的顶替）：
[农场构建器] 场景已生成并保存到 Assets/Scenes/Farm.unity
[农场] 没有指定土壤块预制体，跳过地皮生成。
[农场] 已生成：土壤 0 块，玉米 0 株 ... 田块 2×2 = 4 块（亩）...
```

**注意最后一行**：田块布局、株距、面积算得都对，说明**代码逻辑没问题**，
纯粹是模型取不到。天空盒也正常（`.mat` 是真资产，能加载）。

### 2. `.glb.meta` 用的是 `DefaultImporter`

```
Assets/Art/Models/Soil/玉米田土壤_四垄.glb.meta:
    fileFormatVersion: 2
    guid: 0692b6c0c2b91be4f95926dc6fd7a37f
    DefaultImporter:              ← ★ 关键：当成普通文件
      externalObjects: {}

对比 房车 .fbx.meta:
    ModelImporter:                ← 这个才是真模型
      serializedVersion: 22200
      ...
```

**而且旧位置 `Assets/可能用到的建模/` 下的 glb 也是 `DefaultImporter`** ——
说明这些 glb **从来就没在 Unity 里当模型用过**，
不是我搬运时弄坏的。

### 3. 项目里没有任何 glTF 包

```
Packages/manifest.json 的 dependencies：
  com.unity.collab-proxy / com.unity.feature.development / com.unity.textmeshpro
  com.unity.timeline / com.unity.ugui / com.unity.visualscripting
  + 一堆 com.unity.modules.*
  → 没有任何 gltf / gltfast 相关的包

Library/PackageCache → 也没有
```

### 4. 好消息：glb 文件本身完全正常

逐个解析了 20 个 glb 的 JSON 头：

```
全部 20 个都是 合法 glTF 2.0，Khronos glTF Blender I/O v5.2 导出，文件长度校验通过
材质全部内嵌，不依赖外部贴图
```

| 模型 | 网格 | 材质 | 动画 |
|---|---:|---:|---:|
| 玉米田土壤_四垄 | 111 | 6 | 0 |
| 玉米（9 个） | 16 ~ 101 | 3 ~ 7 | 0 |
| 地形（4 个） | 42 ~ 82 | 4 ~ 6 | 0 |
| 土地plus（4 个） | 6 ~ 9 | 13 ~ 17 | 0 |
| **房车（2 个）** | **957** | 19 | **1 ~ 3** ← 开合动画是现成的 |

**所以只要装上导入器，这些模型立刻可用。**

---

## 三、修法（要你在 Unity 里操作）

我无法代你装包（需要联网走 Package Manager）。步骤：

```
1. Unity 菜单：Window ▸ Package Manager
2. 左上角状态栏的 ＋ 按钮 ▸ Add package by name...
3. Name 填（Version 留空，取最新）：
        com.unity.cloud.gltfast
4. 点 Add，等它下载安装
5. 等 Unity 自动重新导入全部 .glb（可能几十秒，房车模型很大）
```

包名与安装方式来自 Unity 官方文档：
[Unity glTFast 安装说明](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.13/manual/installation.html)
（该文档引用了 `docs.unity3d.com/2022.3/...`，**支持 Unity 2022.3**）

### 装完怎么验证成功

**方法 A（最直观）**：在 Project 窗口点中任意一个 `.glb`，
看 Inspector 顶部：
- 出现 **glTF 导入设置**（Model / Materials / Animation 等折叠项）→ ✅ 成功
- 只有几行 Name / Path / Asset Bundle → ❌ 还是没装上

**方法 B**：重新跑 `Engineer / 农场 / 一键搭建农场场景`，
Console 里"没找到土壤块"的报错消失即成功。

> 我已经把那条报错改成**明确的诊断信息**，以后一看 Console 就知道是不是导入器的问题，
> 不用再去翻 `.meta`。

---

## 四、如果 glTFast 装不上（备选方案）

| 方案 | 说明 | 成本 |
|---|---|---|
| **A. 先试 glTFast** | 首选，官方维护、支持 Built-in 管线 | 5 分钟 |
| **B. 装 UnityGLTF** | Khronos 的 UnityGLTF（GitHub 包），也能导入 glb | 15 分钟 |
| **C. 让建模的人导 FBX** | 项目里**房车就是 FBX，Unity 原生支持**。让队友用 Blender 把 20 个 glb 批量导成 FBX | 队友 15 分钟 |
| **D. 自己写 GLB 解析器** | 我可以在工程里写一个简易 glb → Mesh 的导入器。**不依赖任何包，任何 Unity 版本都能用** | 我 1~2 天 |

**C 其实很香**：`房车.fbx` 已经是现成的反例 —— Unity 原生支持 FBX，
所以只要 Blender 在手边，批量转一次就永久解决，不引入任何第三方依赖。
**但项目里目前没装 Blender**，需要队友那边有。

**D 是最后的兜底**：这些 glb 结构很简单（顶点 + 索引 + 内嵌材质颜色、无贴图、无蒙皮），
写个解析器不难，而且能顺便解决"模型是 2×2×2 包围盒、`节环` 是单位球"这类
需要在导入时修正的问题。缺点是要自己维护。

**我的建议**：先花 5 分钟试 A。装不上再考虑 C。

---

## 五、附带修的一个隐患

`FarmSceneBuilder` 里加载模型失败的报错**太含糊**（原来只写"没找到土壤块：<路径>"），
会让人误以为是路径写错，从而去反复改代码。

已改成明确的诊断：

```
[农场构建器] 找不到土壤块模型：Assets/Art/Models/Soil/玉米田土壤_四垄.glb
如果文件确实存在，几乎可以肯定是【Unity 没有把 .glb 当模型导入】。
  自检：选中那个 .glb，看 Inspector 顶部是 glTF 导入设置（正常）
        还是只显示几行 Name/Path（说明被当成普通文件了）。
  修法：装 Unity 官方 glTF 导入包 —— Window ▸ Package Manager ▸
        + ▸ Add package by name ▸ com.unity.cloud.gltfast
        （Unity 2022.3 内置不支持 glb，必须装这个包）
  装完等 Unity 重新导入，再跑一次本菜单。
```

---

## 六、当前状态

| 项 | 状态 |
|---|---|
| 素材整理（模型/天空盒/UI） | ✅ 完成 |
| 代码路径对齐新结构 | ✅ 11 个路径全部就位 |
| 五个程序集编译 | ✅ 全部通过 |
| **glb 能否被 Unity 导入** | ❌ **缺 `com.unity.cloud.gltfast` 包** ← 当前唯一阻塞 |
| 房车 FBX | ✅ 原生可用（不需要导入器） |

**下一步就一件事**：装 `com.unity.cloud.gltfast`，然后重跑菜单。

---

## 七、装好 glTFast 之后：玉米不显示（已修）

### 现象

日志里（连续几次运行都一样）：

```
[农场] 已生成：土壤 36 块，玉米 0 株（当前阶段只用了其中一组）。...
```

**土壤 36 块正常**（4 个田块 × 3×3 块土壤），但**玉米 0 株**。

### 原因一：`FarmFieldBuilder.currentStage` 默认成了 `None`

```csharp
[SerializeField] private CornStage currentStage = CornStage.None;   // ★
```

而 `CornStage.None` 的语义是 **"S1 播种苗期 = 只见土地、不显示植株"**（策划定的）。
`ApplyStage(None, ...)` 会直接返回、一株都不建。

于是**编辑器里点菜单时就固定生成 0 株玉米** —— 这个默认值是错的，
它让"田里应该有玉米"这件事永远不成立。

**修法**：默认改成 `CornStage.Mature`，并加一句注释说明
"S1 不显示植株是**运行时按阶段切出来的**，不是构建时的默认状态"。

### 原因二：`FarmGrowthBinder` 的"未开始"分支一次都没生效

这是**更隐蔽**的一个 bug。原来的变更检测：

```csharp
private int lastUiStage = -1;
private GrowthLevel lastGrowth = (GrowthLevel)(-1);
private bool lastStarted;                       // 初值 false

public void Sync() {
    var started = session != null && session.IsStarted;   // 未开始 = false
    var uiStage = started ? session.UiStage : -1;         // 未开始 = -1
    if (started == lastStarted && uiStage == lastUiStage && growth == lastGrowth) return;
    ...
}
```

**"还没开始模拟"这个状态，第一次进来就正好等于缓存初值**
（`false / -1 / Normal` vs `false / -1 / Normal`），
于是判定为"没变化"→ **直接 return，`idleStage` 从来没被应用过一次**。

再加上 `uiStage = -1` 本身是错的：`StageFromUiStage(-1)` 会走 `default` 分支
返回 `Mature`。"−1" 只是个哨兵值，不该被当成阶段编号。

**修法**：
1. 用 `int?`（`null` = 没有阶段）代替 `-1` 哨兵，语义清晰
2. 缓存初值用 `int.MinValue`（**永不可能等于真实状态**）：
   ```csharp
   // 【注意】如果用 started=false / uiStage=-1 当缓存初值，
   //   "还没开始模拟"第一次进来会被判成"没变化"而跳过 Apply ——
   //   结果就是 idleStage 一次都没生效过，玉米永远不显示。（踩过这个坑）
   private int lastUiStage = int.MinValue;
   ```
3. 独立跑 Farm 场景（没有 `GameFlowController`）时，只应用一次 `idleStage`，
   不再刷屏

### 原因三：相机的 `FieldBounds` 是空的

`FarmBuilder.FieldBounds` 是在 `Build()` 里算的，
而**编辑器模式下 `AddComponent` 不会触发 `Awake`**，
所以 `BuildCamera` 里读到的 `FieldBounds` 是一个空包围盒，
`FrameField` 按它取景会算出垃圾距离。

**修法**：`BuildFarmScene` 里改成 **`BuildFieldRoot` → `builder.Build()` → `BuildCamera`**，
确保取景前田块已经生成完。

### 顺带做的一个改进：玉米**自动贴垄面**

之前 `CornBaseOffsetY` 默认 0，靠人手调。但这批模型是 Blender 导出的，
**根部未必落在 pivot 上**（实测玉米包围盒最低点 ≈ y 0.42，土壤垄面是 0.475），
靠试参数既慢又不可靠。

现在默认开启 **`AutoAlignCornToSoil`**：创建每株玉米时量出它的包围盒，
把最低点移到垄面高度上。做法是把实例先摆到原点、scale 1，
取所有 `Renderer.bounds` 的 8 个角变换到本地空间再合并 ——
**与 pivot 在哪无关**。

手动微调仍然保留：关掉自动对齐，再用 `CornBaseOffsetY`。

### 运行后的日志长什么样

```
[农场] 已生成：土壤 36 块；玉米已建 768 株（三个生长阶段各一组，只显示当前那一组），当前显示 256 株。
田块 2×2 = 4 块（亩） | 每块 3×3 块土壤 (12×9 m) | 玉米 8×8 = 64 株/块，全田 256 株
| 株距 0.6 m | 整片 26×20 m | 对齐=自动贴垄面
```

**注意看两个数**：
- `已建 768 株` = 3 个阶段 × 256（运行时按需建的）
- `当前显示 256 株` = 当前阶段实际可见的

如果 **`当前显示 0 株`**，且当前阶段是 **S1**，那是**正常的**（S1 只见土地）。

### ⚠️ 一个要注意的设计后果

按策划定的映射（已确认）：

| UI 阶段 | 玉米 | 显示株数 |
|---|---|---|
| **S1 播种苗期** | 不显示植株 | **0** ← 田里是空的，这是设计 |
| S2 营养旺长期 | 幼苗 | 256 |
| S3 生殖关键期 | 成长 | 256 |
| S4 灌浆成熟期 | 成熟 | 256 |
| S5 收获期 | 同 S4 | 256 |

所以**刚进农场（S1）时田里看不到玉米，这是对的**。
如果你希望 S1 也有点绿色（纯为好看），把 `FarmGrowthBinder.idleStage`
和 S1 的处理改一下即可 —— 但那会偏离"只见土地"的设定，**要策划点头**。
