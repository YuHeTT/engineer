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

1. **Inspector 拖引用（推荐）**：直接把 Sprite 拖到 `Image.Source Image` / `Button.Source Image`。
2. **按钮四态**：按钮素材用「同一张图 + 不同 `Image.color`」区分常态/高亮/按下，颜色取 `Assets/Scripts/UI/Editor/UiWhiteboxTheme.cs` 里的 `UiTheme.ButtonNormal / ButtonHighlighted / ButtonPressed`（`Button.colors` 的 transition 也走这套值）。
3. **代码加载**：`Resources.Load` 只认 `Resources` 目录；本目录不在 `Resources` 下，所以脚本里要用就**序列化引用**（白盒构建器就是这么按名字找然后写字段的）。
4. **九宫格**：面板/边框类素材若要任意拉伸，在 Inspector 里开 `Sprite Editor ▸ Border` 设置九宫格边距，并把 `Image.Type` 设为 `Sliced`。

---

## 4. 和《Unity界面.docx》的关系

* `预览/` 里的 6 张拼版图是**设计侧**的参考（源文件编号 07/09/12/16/24），和 `Docs/白盒界面-元素对照表.md` 第 3 节里的 image1…image12 编号**不是同一套号**，对照时请看图内容、不要按编号对。
* 白盒界面（`UiWhiteboxBuilder` 生成的 12 屏）当前**没有引用**任何 EPS 素材，纯色块 + TMP 文本即可跑通流程；换皮阶段再把这些 Sprite 接进 `UiTheme` / `UiWhiteboxFactory` 的 `Image.sprite` 即可，不影响逻辑层。

---

## 5. 原始目录 `UI/` 怎么处理

`UI/` 留在工程根目录（`Assets` 之外）就完全不影响 Unity 导入，`.gitignore` 已排除 `Tools/.work`、`Tools/.python`、`Tools/__pycache__` 三个可再生产物。若要回收磁盘（约 430 MB），确认不再重跑导出流水线后执行：

```powershell
Remove-Item 'UI\Tools\.work','UI\Tools\__pycache__' -Recurse -Force
```

> ⚠️ `.work` 是几何缓存（`geometry*.json`、`native*.pkl`）与中间 PDF，重跑流水线需要原始 EPS 源文件才能重建，删之前请确认源文件还在。
