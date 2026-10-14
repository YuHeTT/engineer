# EPS 素材库 · 清单与使用说明

> 目录：`Assets/UIresources/EPS素材/`
> 来源：工程根目录 `UI/`（EPS 导出流水线）。本目录只放**成品图**副本，共 **188 个文件 / 10.2 MB**；
> 原始目录 `UI/` 里 434 MB 的 Python 工具链（`Tools/.python`、`Tools/.work`、`__pycache__`）**没有**复制进来，Unity 也不会扫到它们（它们在 `Assets` 之外）。

---

## 1. 目录清单

| 目录 | 数量 | 体积 | 文件前缀（= 设计源 EPS 编号） | 建议用途 |
| --- | --- | --- | --- | --- |
| `图标/` | 17 | 290 KB | EPS07 / EPS19 / EPS24 | 指标图标、状态小图 |
| `圆形仪表/` | 17 | 855 KB | EPS07 / EPS12 | 巡航覆盖率、圆环进度 |
| `文本框与数据面板/` | 79 | 4.6 MB | EPS07/11/12/16/17/18/19/24/25/28/29/30/31 | 面板底、标题条、分隔条、数据行 |
| `点击按钮/` | 64 | 1.9 MB | EPS07/09/11/12/16/17/24/25/28/29/30/31 | 按钮常态/高亮/按下、图标按钮 |
| `边框与装饰/` | 4 | 122 KB | EPS07 / EPS12 | 边框、角花 |
| `预览/` | 6 | 2.4 MB | EPS07 / EPS09 / EPS12 / EPS16 / EPS24 | 设计拼版预览图（**只作参考**，别当切图用） |
| `asset_manifest.json` | 1 | 100 KB | — | 181 个素材的清单：来源 EPS、`source_rect_normalized` 取图区、`sha256`、尺寸 |

**命名规则**：`EPS<源编号>_<类别>_<序号>.png`，例如 `EPS07_Panel_03.png`、`EPS12_Gauge_01.png`。
`asset_manifest.json` 里的 `source_directory` 指向最初的设计源文件夹，`sha256` 可用于校验素材有没有被改过。

---

## 2. 导入参数（统一为「uGUI 能直接用」）

由 `Assets/Scripts/UI/Editor/UiEpsSpriteImporter.cs`（`UiEpsSpriteImporter`）保证：

| 参数 | 值 | 原因 |
| --- | --- | --- |
| Texture Type | `Sprite (2D and UI)` | `Image` / `Button.Source Image` 只吃 Sprite |
| Sprite Mode | `Single` | 素材已经是切好的单图 |
| Mesh Type | `Full Rect` | UI 里会拉伸/九宫格，Tight 网格容易出怪边 |
| Pixels Per Unit | `100` | 与 UI 常用习惯一致 |
| Generate Mip Maps | 关 | 界面贴图用不到，关掉更清晰、更省显存 |
| Alpha Is Transparency | 开 | 大量素材带透明通道 |
| Wrap / Filter | `Clamp` / `Bilinear` | 避免边缘重复、避免像素感 |
| Max Size | `2048` | 白盒阶段够用 |
| Compression | `Compressed HQ` | 体积与画质折中 |
| NPOT Scale | `None` | 非二次幂尺寸保持原样，不被缩放 |

用法：

* **新加的图**：放进 `Assets/UIresources/EPS素材/` 就会自动按上表导入；
* **已经导入过的图**：脚本里带了一个 `[InitializeOnLoadMethod]` 兜底检查——Unity 编译完这个脚本后如果发现素材还不是 Sprite，会**自动补导一次**并打印日志「检测到素材还不是 UI 导入参数，自动补导一次」；检查通过就什么都不做，不会反复重导；
* **想手动重刷**：菜单 **Tools ▸ UI ▸ EPS 素材：修正导入设置（按 Sprite 重导）**，Console 会打印「已按 Sprite 参数重导 N 张图」；
* 只影响这个目录下的 png/jpg，`Assets/UIresources/` 里原有的三套 GUI Kit 与工程其他贴图不受影响。

---

## 3. 在界面里怎么用

白盒界面（`UiWhiteboxBuilder` 生成的 12 屏）的几何全部来自代码，重建是**幂等**的（先删 `UI_Root` 再整棵重建）。
所以素材不能只靠「手工拖」——重建一次就没了。按持久程度给三条路：

### 3.1 一键套皮（推荐的起点）

菜单 **Tools ▸ UI ▸ 皮肤：套用 EPS 素材（当前场景）**：遍历界面树，按元素的**角色**
（`UiSkinSlot`，构建时由 `UiWhiteboxFactory` 顺手登记）把素材填进 `Image.sprite`。当前默认只配了三个角色：

| 角色 | 默认素材 | 说明 |
| --- | --- | --- |
| `Panel` | `文本框与数据面板/EPS07_Panel_01.png` | 弹窗 / 结算页的主面板底图 |
| `Button` | `点击按钮/EPS09_Button_01.png` | 普通按钮的发光边框（Sliced 九宫格） |
| `ButtonPrimary` | `点击按钮/EPS09_Button_02.png` | 主行动按钮的亮色边框 |

其余角色（`ScreenBackdrop` / `ScreenDim` / `PanelSoft` / `Card` / `BarTrack` / `BarFill` / `Divider` / `InputField` / `Gauge`）
**故意留空**：保持白盒纯色，Console 会按「角色×个数」把它们列出来（例如 `Card×8、Divider×8、InputField×11、PanelSoft×4、ScreenBackdrop×3`），
对照同一句里的「共处理 N 个元素」就知道没套上图的元素去哪了。想给某个角色配图，
改 `Assets/Scripts/UI/Editor/UiSkin.cs` 里 `Table` 的**那一行**就够了（路径相对 `EPS素材/`，含扩展名）。

配套两个开关（菜单里带对勾）：

* **Tools ▸ UI ▸ 皮肤：构建时自动套用** —— **默认就是开的**：每次「构建白盒界面」都会顺手把素材（角色默认 + 映射覆盖）铺上，
  重建后不会莫名退回纯色块；想要纯色版构建就把这个勾去掉；
* **Tools ▸ UI ▸ 皮肤：文字配色同步为青蓝** —— 白盒原来的暖橙文字色和青蓝素材放一起会打架，
  这个开关把 `TextPrimary / TextSecondary / TextMuted / ValueMuted / TextAccent` 五种**主题色**换成冷色
  （手工改过的颜色不动），可随时切回；按钮的 `ColorTint` 也会换成中性白，免得悬停反馈把素材染成橙色。

### 3.2 手工微调 + 存成映射（推荐的正经做法）

角色表只能覆盖「面板 / 按钮」这类通用元素，具体某块仪表、某张卡片还得人眼挑。流程：

1. 先套一次素材（3.1），让界面有个雏形；
2. 在 Hierarchy 选元素 → Inspector 的 `Image` → 拖 `Source Image`，按需改 `Image Type`
   （`Simple` / `Sliced`）、颜色，必要时开 `Preserve Aspect`；
3. 菜单 **Tools ▸ UI ▸ 皮肤：记录当前皮肤到映射文件** → 生成 `Assets/UIresources/白盒皮肤映射.json`；
4. 以后每次重建界面，映射会在**角色默认之后**套回去（后套的赢，所以手工挑的图不会被角色默认值盖掉）。

映射文件是普通 JSON，可以直接手改 / diff / 删条目：`path` = 相对 `UI_Root` 的层级路径
（如 `Screens/Screen_Welcome/Button_Intro`），`sprite` = 素材资产路径（留空 = 这个元素不要图）。

> 想整体回到纯色白盒：**Tools ▸ UI ▸ 皮肤：清除皮肤**（删映射 + 关自动套用 + 重建）。
> 它在删除前会**自动备份**映射到 `UserSettings/UiSkinMappingBackup.json`（Assets 之外，不会被当资源删掉），
> 误清之后用 **Tools ▸ UI ▸ 皮肤：从备份恢复映射** 就能找回，再跑一次「套用」即可。
> 注意「清除皮肤」是**唯一**会碰映射文件的菜单——手工挑图的清单只活在 `Assets/UIresources/白盒皮肤映射.json` 里，
> 想留档就顺手提交到版本库。

### 3.3 纯手工拖（只在临时试看时用）

直接把 Sprite 拖到 `Image.Source Image` 就能立刻看效果，**但重跑构建会清空**；
想让选择留下来，走 3.2 的「记录当前皮肤」。
另外，代码里要引用素材请用**序列化引用**——素材不在 `Resources/` 下，`Resources.Load` 取不到。

### 3.4 九宫格边距

* `点击按钮/EPS09_*.png` 这类「中空边框」按钮图，导入器已经替它们写好九宫格边距
  （`UiEpsSpriteImporter.SliceBorders`，左右 80px、上下 28px），直接 `Image.Type = Sliced` 拉伸不变形；
  位置不合适就改这个常量，再跑一次「EPS 素材：修正导入设置（按 Sprite 重导）」。
* 其它素材要用九宫格，得在 **Sprite Editor ▸ Border** 里自己设边距；面板这类带斜切角的形状一般不适合九宫格，
  直接 `Simple` 铺满摆放框更省事。

---

## 4. 挑图前先看：素材风格速查

`预览/` 里那 6 张 JPG 是缩略图总表（图名 = 前缀），挑图先翻它，比一张张点开快：

| 前缀 | 风格 / 内容 | 白盒阶段 |
| --- | --- | --- |
| EPS07 | 青蓝科幻 HUD：面板、六边/三角图标、圆形仪表 | ✅ 科技调性，首选 |
| EPS09 | 发光「中空边框」按钮（金 / 紫 / 蓝各两档） | ✅ 按钮、标签页 |
| EPS11 / 12 / 16 / 17 / 18 / 19 | 文本框、数据面板、圆形仪表 | ✅ 数据类元素 |
| EPS24 / 25 / 28 / 29 / 30 / 31 | 直播（LIVE / FOLLOW / DONATE）、活动页等**别的主题** | ⚠️ 和本项目调性不符，先别配 |

> 同一个 EPS 编号下的 `_01/_02/…` 是同一套的不同档位（粗细、明暗、方向），**同屏尽量用同一个编号**才不会花。

---

## 5. 和《Unity界面.docx》的关系

* `预览/` 里的 6 张拼版图是**设计侧**的参考（源文件编号 07/09/12/16/24），和 `Docs/白盒界面-元素对照表.md` 第 3 节里的 image1…image12 编号**不是同一套号**，对照时请看图内容、不要按编号对。
* 白盒界面（`UiWhiteboxBuilder` 生成的 12 屏）**默认不引用**任何 EPS 素材：纯色块 + TMP 文本即可跑通流程，逻辑层与素材无关；
  素材是通过 `UiSkin` 角色表 / `白盒皮肤映射.json` 挂到 `Image.sprite` 上的，随时可以整体摘掉（3.2 的「清除皮肤」）。

---

## 6. 原始目录 `UI/` 怎么处理

`UI/` 留在工程根目录（`Assets` 之外）就完全不影响 Unity 导入，`.gitignore` 已排除 `Tools/.work`、`Tools/.python`、`Tools/__pycache__` 三个可再生产物。若要回收磁盘（约 430 MB），确认不再重跑导出流水线后执行：

```powershell
Remove-Item 'UI\Tools\.work','UI\Tools\__pycache__' -Recurse -Force
```

> ⚠️ `.work` 是几何缓存（`geometry*.json`、`native*.pkl`）与中间 PDF，重跑流水线需要原始 EPS 源文件才能重建，删之前请确认源文件还在。
