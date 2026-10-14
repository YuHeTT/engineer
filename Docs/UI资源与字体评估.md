# UI 资源与字体评估（前期准备阶段）

> 状态：探讨阶段，不做实现。
> 对象：`Assets/UIresources/`（三套 UI 资源，615 文件 / 47.6 MB）、`Assets/fonts/`（SDF + TTF 两个字体）
> 参考：`Assets/画风参考/`（4 张图）、`Unity界面.docx`（策划第一版）

---

## 零、开工前必须先定的一件事：走哪个画风

当前工程里存在**两套互相冲突的视觉方向**：

| 来源 | 风格 | 特征 |
|---|---|---|
| `Assets/画风参考/` （4 张） | **工业数字孪生 / 智慧大屏** | 深蓝黑底、青色发光描边、细线半透明面板压在 3D 场景上、数据密集仪表盘 |
| `Unity界面.docx` 的 12 张占位图 | **暖橙棕扁平** | 扁平圆角色块、柔和暖色调、留白多 |
| `Assets/UIresources/Dark_Brown_GUI_kit` | **暖棕橙奇幻 RPG** | 石雕 / 旧金属厚边框 + 暖棕橙，明度高、质感重 |
| `Assets/UIresources/GUI_Parts` | **暗色 + 暗红游戏 UI** | 深底、暗红按钮，同上同源 |
| `Assets/UIresources/Unity UI Samples` | **官方教学示例（蓝色圆角）** | 扁平蓝紫圆角，风格最接近文档占位图 |

**观察**：`Dark_Brown_GUI_kit` 的**色彩**方向和策划文档是一致的（暖橙棕），差的是**质感厚度**（石雕金属 vs 扁平色块）；
但和 `画风参考`（冷蓝科技）是**完全相反**的方向。

> **结论**：先决问题不是"资源合不合适"，而是"最终画面是暖橙农业风还是冷蓝科技风"。
> 这个不定，任何 UI 资源都无法评价合不合适。

---

## 一、三套 UI 资源的实际评估

### ① `Dark_Brown_GUI_kit` —— 可用，但必须克制使用

**内容规模**：6 个大类 209 张 PNG
`background/`（大中小三级 + 纯色/纹理）、`bar/`（含横向纵向、暗色/亮色）、`button/`（circle / rect / square 三类各 3 态）、`frame/`（九宫格框，23%–56% 不透明度）、`icon/`（simple 线框 + full_simple 实心两组）、`ornament/`、`skill_icon/`（dark / fire / holy / ice / thunder 五系）

**关键色值（实测平均色）**

| 元素 | 色值 | 说明 |
|---|---|---|
| 主背景 | `#372413` / `#312011` | 暖深棕，2048×1024 |
| 按钮常态 | `#593115` | |
| 按钮 hover | `#673f21` | |
| 按钮按下/高亮 | `#9c511d` | 明亮橙，很抢眼 |
| 面板框 | `#463b30`（23% 不透明） | 九宫格 |
| 进度条底/填充 | `#970000` 红 / `#934f00` 橙 / `#005e94` 蓝 | 三种色系可选 |

**✅ 推荐使用**
- `background\big\big_dark_background0.png`（2048×1024）→ 全屏底 / 大地图底
- `background\mid\middle_dark_background0.png`（1024×1024）→ 弹窗底
- `background\mini\mini_dark_background.png`（256×256）→ 卡片底
- `frame\big\big_frame.png` / `frame\mid\mid_frame.png` → **九宫格面板框**（23% 透明，可叠加）
- `frame\other\button_frame.png` → 按钮外框
- `button\rect\button0/1/2.png`（512×256，100% 不透明）→ **正好是 Normal / Hover / Pressed 三态**
- `bar\other\low_dark_bar.png`、`bar\small\small_dark_bar.png` → 进度条
- `icon\simple\`（线框系列，17%–30% 不透明）→ 返回、设置、加减、勾选、问号，这些**和农业主题不冲突**
- `Demo.unity` → 建议在 Unity 里打开看一眼整体效果，比逐张看贴图直观

**❌ 不要使用（会瞬间把农业科普变成奇幻游戏）**
- `skill_icon\` 全部（fire / ice / thunder / holy / dark 魔法球、火球、冰霜、雷电剑）
- `ornament\corner_ornament.png`
- `icon\simple\` 和 `full_simple\` 里的：`sword`、`shild`、`potion`、`coins`、`chest`、`magic`、`scroll`、`book`、`craft`
- `icon\other\potion\`、`icon\other\craft\`（三叶草、蘑菇、昆虫翅）
- `icon\full_simple\numbers\1–9.png`（奇幻字型数字，**数据面板绝对不能用**）
- `background\other\health_bar_background*.png`（血条语义）

**⚠️ 注意事项**
- 面板框 23% 不透明：叠在复杂 3D 场景上时文字可读性会不够，**需要自己垫一层半透明深色底**
- 大背景 2048×1024 是 2:1，界面是 16:9 → **必须做九宫格或拉伸策略**，直接铺会变形
- 石雕金属边框 + 暖橙，在"数字孪生"语境里偏"游戏"，若评审期待"专业感"，需要配合排版压住

---

### ② `GUI_Parts` —— 可用，与 ① 同源，可混用

**内容**：`Gui_parts/`（28 张）+ `Icons/`（12 张）

**关键色值**：按钮 `#2c0f0c`–`#42211c` 暗红；背景 `#181514`–`#1c1819` 近黑；框 `#3c342f`

**✅ 推荐使用**
- `button_ready_off.png` / `button_ready_on.png`（512×256）→ **标准未激活/激活态**，正好做"自动巡航 / 正在检测"的按钮切换
- `button2_ready_off/on.png`（256×256）→ 方形小按钮
- `button_agree.png` / `button_cancel.png` → 确认/取消
- `Frame_big.png` / `Frame_mid.png` / `Frame_mid_2.png` → 面板框
- `Mini_frame0/1/2.png` → 小卡片框
- **`Hp_frame.png` + `Hp_line.png`（1024×64）→ 非常适合做"巡航覆盖进度条"**，直接对应文档里"球形轨迹覆盖玉米田面积，探测数据随覆盖面积增加而更新"这个机制
- `big_background.png` / `mid_background.png` / `Mini_background.png` → 底图
- `lil_roundframe_ready*.png`（64×64）→ 圆形状态指示

**❌ 不要使用**
- `warrior_silhouette_man.png` / `warrior_silhouette_woman.png`（战士剪影）
- `Icons\` 全部（武器、盔甲、石头、技能图标 01–04，含紫色和金色魔法风）
- `name_bar*.png`（血条式名字栏）
- `Hp_line.png` 如果用来表达"血量"语义

---

### ③ `Unity UI Samples` —— 建议移出 `Assets`，不要进正式工程

**问题**：这是 Unity 官方的**完整教学示例工程**，包含：
`Animation/`、`Fonts/`、`Materials/`、`Models/`、`Prefabs/`、**`Scenes/`**、**`Scripts/`**、`Textures and Sprites/`

直接放在 `Assets` 下会：
1. 污染正式工程（一堆示例场景、脚本、Prefab 出现在你的项目里）
2. 它用**旧版 uGUI `Text` 组件**（而不是 TMP），会和你的 TMP 方案并存，造成混乱
3. 引入不必要的材质、Shader（`UI-Default-No-Alpha.shader`）、Model
4. 打包时可能被一起构建进去

**唯一真正有价值的部分**：`Textures and Sprites\Rounded UI\`

| 文件 | 尺寸 | 实测色 | 用途 |
|---|---|---|---|
| `UIButtonDefault.png` | 128×128 | `#575984` 蓝紫 | 圆角按钮底 |
| `UIButtonCorner.png` | 128×128 | `#5c5f95` | 圆角按钮角 |
| `UIPanel.png` | 256×256 | `#595a6b` | 圆角面板 |
| `UIToggleButton.png` | 32×32 | `#535983` | 开关 |

**这 4 张反而是三套资源里最接近策划文档那种扁平圆角卡片设计的**（虽然色相是蓝紫，可以调）——建议提取出来当备选，其余全部移出。

**授权（已确认，可放心）**
- `Fonts\Jupiter\LICENSE.txt`：Jupiter 字体由 Isurus Labs 创建，**public domain**
- `Textures and Sprites\SF UI\Background\LICENSE.txt`：NASA 背景图，**public domain**

**建议做法**：整体移到 `Assets` 外面（比如工程根目录建一个 `UIReferences/`），或在 Unity 里干脆不导入。需要那几张圆角贴图时单独拷出来。

---

### 三套共同的缺口

不管选哪一套，下面这些**都必须自己做**，市面上的通用 GUI Kit 都不会有：

- **水肥五项数值输入**（灌溉量 / 有机肥 / 钾肥 / 氮肥 / 磷肥）——文档里玩家要"填数字"，需要输入框 + 加减步进器 + 单位标签
- **9 项土壤指标的多参数面板**（pH、OM、N、P、K、Eh、盐基离子、砷、汞）——需要可读的表格/列表布局 + 数值变化高亮
- **巡航覆盖率可视化**（未覆盖区域、球形扫描轨迹）
- **"原数据 / 探测数据"双列对比**——文档要求的核心机制，需要特殊的双栏对照组件
- **适宜度条 / 三档长势指示**
- **当前阶段进度指示**（S1–S5）

**加一句**：`Dark_Brown_GUI_kit` 和 `GUI_Parts` 的按钮/框都是**位图**，缩放多了会糊。如果最终确定用它们，建议走 **九宫格（Sprite Editor 里设 Border）**，而不是整张拉伸。

---

## 二、SDF 和 TTF 的区别

### 本质：不是"同一个字体的两种格式"，而是两套完全不同的流程

```
   微软雅黑.ttf  ──[Unity TrueTypeFontImporter]──►  Font 资源  ──►  旧版 UI.Text 使用
        │                                                                 （运行时按字号光栅化）
        │
        └──[TMP Font Asset Creator]──►  SDF FontAsset(.asset)  ──►  TextMeshProUGUI 使用
                                        （预烘焙灰度距离位图）
```

- **TTF（`.ttf` / `.otf`）是源文件**。Unity 把它导入成 `Font` 资源，交给旧版 `UI.Text`；也可以在 Project 视图右键 → `Create → TextMeshPro → Font Asset` 现场烘成 SDF。
- **SDF（`.asset`）是 TextMeshPro 的字体资产**。它是"Signed Distance Field"——把每个字形按固定字号光栅化后，记录**每个像素到字形边缘的距离**存成灰度图。渲染时着色器按距离场重建边缘，所以能做到：边长边不糊、描边/发光/投影一次搞定。

### 对比表

| 维度 | `.ttf`（源文件） | TMP SDF `.asset` |
|---|---|---|
| 缩放 | 放大会糊 | 无损，任意字号都锐利 |
| 描边/发光/投影 | 不支持 | 支持，且不额外增加图集 |
| 字符集 | 含**整个字体所有字** | **只含烘焙时选定的字** |
| 文件体积 | 整个字体（你的：26.1 MB） | 图集 + 度量表（你的：35.7 MB / 7923 字） |
| 运行时开销 | 按需光栅化，有开销和 GC | 零光栅化开销 |
| 缺字表现 | 几乎不会缺 | **显示成方块或空白** |
| 用于 | `UI.Text`（旧版） | `TextMeshProUGUI` |

### 你这套的具体情况（已解析文件内部）

#### `Assets/fonts/msyh SDF.asset`

```
类型        ：TextMeshPro FontAsset（Material + MonoBehaviour !u!114）
字体家族    ：Microsoft YaHei / Regular
烘焙字号    ：36
图集        ：4096 × 4096，padding 5，单图集（多图集未启用）
图集模式    ：m_AtlasPopulationMode: 0  →  静态（Static），运行时不会动态补字
字符总数    ：7923
  · CJK 汉字 ：6763
  · 拉丁/数字：95
源码字体GUID：80cdae398347ed548b89ee02885dc04a
```

**✅ 好消息：覆盖够用。** 我拿策划文档里所有会用到的字逐一核对过——
`酸衡值 腐殖丰度 速效氮元 磷活度 钾储量 土地面积 平川台 泽洼域 崇岭阶 高原坪 玉米 甜菜 红薯 土豆 机器人 巡航 探测 更新数据 开始分析 灌溉 施肥 收益计算 土壤指标 氧还势 骨离子 毒砷潜势 汞积量 播种苗期 营养旺长 生殖关键 果实成熟 收获 阶段 返回 下一步 确认选择 模拟 简介 项目 体验 亩`
——**全部命中，零缺失**。当前直接可用。

**⚠️ 坏消息 1：源 TTF 不在仓库里。**
`m_SourceFontFileGUID` 指向 `80cdae398347ed548b89ee02885dc04a`，我全盘搜了 `Assets`、`Packages`、`ProjectSettings` 的所有 `.meta`，**没有任何文件带这个 GUID**。
后果：**这是一个"冻结字集"**——策划以后加任何新文案，只要出现图集外的字，就会显示成方块，而且**你没法补字**（静态模式不支持运行时补，也没有源字体重新烘）。
> **行动项**：把烘焙时用的那份微软雅黑 TTF 存档到 `BlenderSources/` 之类的非 `Assets` 目录（或团队网盘），否则以后遇到缺字会很难受。（同时注意：这份 TTF 不要提交到 GitHub，见第三节）

**⚠️ 坏消息 2：体积。** 35.7 MB 是当前 `Assets` 里最大的单个资源文件。可以接受，但如果后面还要加字体，仓库会迅速膨胀。

#### `Assets/fonts/字魂镇魂手书.ttf`

```
类型        ：TrueType（sfnt 0x10000），完整字体
字形数      ：9725
CJK 汉字    ：约 9171
code points ：约 9701
内部名称    ：zihunzhenhunshoushu
体积        ：26.1 MB
```

**这是书法/手写风格装饰字体**（字魂网出品）。

**✅ 可以用于**：标题、LOGO、"开始体验"这类大号美术字
**❌ 绝对不能用于**：数据正文、表格、"酸衡值 6.2"、"灌溉量 25 m³/亩" 这类需要精确辨读的内容——手写体读数字是灾难
**⚠️ 体积警告**：26.1 MB 的 TTF，如果直接生成全字集 SDF，图集会非常大（可能 100 MB+）。
> **建议**：只用它烘一个**标题专用子集**（比如 200–400 个常用字），图集能压到几 MB。

**另外一个注意点**：如果它就放在 `Assets` 下且不给 TMP 用，Unity 仍会导入成 `Font` 资源，构建时可能被算进包体。用不到就移出 `Assets`。

---

## 三、微软雅黑能在竞赛中使用吗

### 结论：**不建议，即使非商用。**

### 理由

**1. 微软雅黑的授权是随 Windows 授权，不是独立的字体授权。**
微软的字体 EULA 只允许在 Windows 环境下使用这些字体。关键限制是：
- ❌ 不允许把字体**嵌入**到文档或应用程序中
- ❌ 不允许**再分发**字体文件

**2. 把 TTF 提交到 GitHub 就是在公开分发。**
"非商用"降低的是**实际被追究的概率**，不改变"违反授权"这个**定性**。授权条款不区分商用/非商用，只区分"在 Windows 里用"和"把文件拷出来给别人"。

**3. 竞赛场景下这是最容易被问倒的点。**
竞赛申报材料一般明确要求素材版权合规。你手里有 Windows 正版授权，**不能证明你有权分发这份字体文件**。一旦被问"这个字体哪来的、有授权吗"，很难辩护——而且这是个**一键就能问倒**的问题，不值得为省几小时踩。

### 推荐替代：开源可商用的中文字体

| 字体 | 许可 | 说明 |
|---|---|---|
| **思源黑体 / Noto Sans CJK SC** | SIL OFL 1.1 | **首选**。Adobe + Google 联合出品，明确允许免费使用、修改、再分发、嵌入应用。竞赛/学术场景最稳 |
| **思源宋体 / Noto Serif CJK SC** | SIL OFL 1.1 | 需要衬线时用 |
| 阿里巴巴普惠体 | 免费商用 | 需遵守其授权说明 |
| 得意黑、霞鹜文楷等 | OFL / 免费商用 | 有设计感，风险低 |

> **最稳妥路径**：用 **Noto Sans CJK SC** 重新烘一套 TMP SDF，一步到位，以后再也不用操心授权。
> 代价是要重烘一次图集、并把工程里所有 TMP 组件换字体引用——**现在工程里还没有 UI，这个代价此时最低。**

### 一个关键的有利事实

我解析了 `.git/index`：目前仓库共 **188 个跟踪文件**，最后一次提交是干净的。
**这两个字体文件和整个 `UIresources` 都还没有被提交**——`msyh SDF.asset`、`字魂镇魂手书.ttf`、`UIresources/*` 在索引里**一个都没有**。

**这意味着现在是处理授权问题的最佳窗口期**：还没有任何字体文件进入 Git 历史，一旦提交，即使后面删掉，历史里也会永久保留（Git 会记住每一个版本）。

### 立即可以做的两条风险规避

**① 加 `.gitignore` 规则，不提交字体源文件**

现在 `.gitignore` 里**没有任何字体相关规则**。建议加：

```gitignore
# ---------- 字体源文件 ----------
# 字体授权不允许再分发，且体积大（26MB+）；只提交烘焙好的 TMP SDF 资产。
# 例外：Unity UI Samples 里的 Jupiter.ttf 是 public domain，需要保留。
/[Aa]ssets/**/*.ttf
/[Aa]ssets/**/*.otf
!/[Aa]ssets/UIresources/Unity UI Samples/**/*.ttf
```

好处：既规避授权风险，又省 26 MB 仓库体积。
注意：抓 `*.ttf` 时要显式排除那个 public domain 的 `Jupiter.ttf`，否则会误伤。

**② 提交 SDF 资产是可以的**

`msyh SDF.asset` 存的是**字形位图**，不含字体文件本身。加上课设/校内展示这个使用场景，风险很低。
但如果要投正式竞赛，建议按上面的替代方案换成 Noto Sans CJK SC 重烘一套。

---

## 四、行动清单（探讨阶段，不实现）

**必须先定的（决定后面所有事）**
1. ☐ 定画风方向：**暖橙农业扁平风**（跟文档）还是 **冷蓝科技 HUD 风**（跟画风参考）？
2. ☐ 定字体政策：继续用 `msyh SDF`（零成本，有授权瑕疵）还是换 Noto Sans CJK SC 重烘（一次性成本，彻底干净）？

**可以马上做、成本极低的**
3. ☐ 加 `.gitignore` 字体规则（上面那 4 行），把授权风险挡在提交之前
4. ☐ 把烘焙 `msyh SDF` 用的源 TTF **存档到非 `Assets` 目录**（如 `BlenderSources/fonts/`），解决"以后无法补字"的隐患
5. ☐ 把 `Unity UI Samples` 整体移出 `Assets`（或至少不导入 `Scenes`/`Scripts`/`Prefabs`/`Models`），只留 `Rounded UI` 那 4 张贴图

**需要 Unity 里动手看的**
6. ☐ 打开 `Dark_Brown_GUI_kit/Demo.unity`，直观判断整体质感能不能接受
7. ☐ 把 `Dark_Brown_GUI_kit` 的按钮/框设成**九宫格（Border）**，避免拉伸糊掉
8. ☐ 用 `GUI_Parts/Hp_frame + Hp_line` 试做一版**巡航覆盖进度条**（对应文档的核心机制）

**待策划确认后再说**
9. ☐ 三套资源的具体取舍清单（等画风定了再终审）
10. ☐ `字魂镇魂手书` 只用于标题的字符子集该包含哪些字

---

## 五、一句话总结

**资源能救，但救不到底**：`Dark_Brown_GUI_kit` + `GUI_Parts` 的**暖棕橙方向其实和策划文档一致**，选出一小部分（背景/框/三态按钮/进度条/线框图标）是够用的，但它们**和 `画风参考` 的冷蓝科技风完全冲突**——先定画风，再谈资源合不合适。`Unity UI Samples` 不要进正式工程。

**字体的问题比资源更值得现在就处理**：`msyh SDF`（微软雅黑 / 7923 字 / 静态 / 汉字全覆盖）**当前够用**，但它的**源 TTF 不在仓库里**，意味着以后加字就没辙；而微软雅黑本身**授权不允许再分发**，提交到 GitHub 是实质风险。**好在两个字体都还没提交**——这是加 `.gitignore` 规则、甚至整套换成 Noto Sans CJK SC 的最佳时机，工程里还没有任何 UI 引用，改动成本此刻最低。
