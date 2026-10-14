using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒界面「填素材」工具：把 <see cref="SpriteFolder"/> 下的 EPS 素材按**角色**填到白盒元素上。
    ///
    /// 为什么是「角色」而不是「逐个拖」：
    ///   白盒界面的几何全在代码里（<see cref="UiWhiteboxBuilder"/> + <see cref="UiLayout"/>），
    ///   菜单一跑整棵树会重建。所以素材也得挂在「可重建」的东西上 ——
    ///   构建时给每个元素贴 <see cref="UiSkinSlot"/>（角色），套皮时按角色查表取图。
    ///
    /// 三档用法（由浅入深，见《Docs/EPS素材-清单与使用说明.md》）：
    ///   1) 菜单「皮肤：套用 EPS 素材」—— 按 <see cref="Table"/> 的默认图铺一遍，先看效果；
    ///   2) 在 Inspector 里手工挑图 / 调 Sliced、颜色，再「皮肤：记录当前皮肤到映射文件」，
    ///      之后重建界面会连手工调整一起还原（<see cref="UiSkinMapping"/>）；
    ///   3) 勾上「皮肤：构建时自动套用」，以后点一次构建 = 白盒 + 素材。
    ///
    /// 换图只要改 <see cref="Table"/> 里那一行路径。
    /// </summary>
    public static class UiSkin
    {
        /// <summary>素材根目录（与导入器共用同一常量）。</summary>
        public const string SpriteFolder = UiEpsSpriteImporter.FolderPath;

        private const string MenuApply = "Tools/UI/皮肤：套用 EPS 素材（当前场景）";
        private const string MenuRecord = "Tools/UI/皮肤：记录当前皮肤到映射文件";
        private const string MenuClear = "Tools/UI/皮肤：清除皮肤（删映射 + 重建纯色白盒）";
        private const string MenuAutoApply = "Tools/UI/皮肤：构建时自动套用";
        private const string MenuCoolText = "Tools/UI/皮肤：文字配色同步为青蓝";
        private const string MenuOpenMapping = "Tools/UI/皮肤：打开映射文件";

        private const string AutoApplyPrefKey = "Engineer.UI.UiSkin.AutoApplyOnBuild";
        private const string CoolTextPrefKey = "Engineer.UI.UiSkin.CoolText";

        /// <summary>构建白盒时是否自动套素材（存 EditorPrefs，按机器记忆）。</summary>
        public static bool AutoApplyOnBuild
        {
            get => EditorPrefs.GetBool(AutoApplyPrefKey, false);
            set => EditorPrefs.SetBool(AutoApplyPrefKey, value);
        }

        /// <summary>是否把白盒的暖橙文字色换成与素材配套的青蓝色（只动主题色，手改过的颜色不碰）。</summary>
        public static bool CoolTextOn
        {
            get => EditorPrefs.GetBool(CoolTextPrefKey, true);
            set => EditorPrefs.SetBool(CoolTextPrefKey, value);
        }

        [MenuItem(MenuApply, false, 20)]
        private static void ApplyMenu()
        {
            var root = FindRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("UI 皮肤",
                    "当前场景里找不到 " + UiWhiteboxBuilder.RootName + "。\n\n" +
                    "先执行 Tools ▸ UI ▸ 构建白盒界面（12 屏）生成界面，再回来套素材。", "好");
                return;
            }

            var applied = ApplyAll(root);
            SaveSceneOf(root);
            Debug.Log("[UI皮肤] 套用完成：角色默认 + 映射覆盖共处理 " + applied + " 个元素。");
        }

        [MenuItem(MenuRecord, false, 21)]
        private static void RecordMenu()
        {
            var root = FindRoot();
            if (root == null)
            {
                EditorUtility.DisplayDialog("UI 皮肤", "当前场景里找不到 " + UiWhiteboxBuilder.RootName + "。", "好");
                return;
            }

            var count = UiSkinMapping.Record(root);
            EditorUtility.DisplayDialog("UI 皮肤",
                count > 0
                    ? "已把当前界面上 " + count + " 个带图元素记录到：\n" + UiSkinMapping.MappingPath +
                      "\n\n以后重建界面会自动套回（前提是勾上了「皮肤：构建时自动套用」）。"
                    : "当前界面上没有任何 Image 带图，没记录。\n\n先执行「皮肤：套用 EPS 素材」，或者手工往元素上拖图。",
                "好");
        }

        [MenuItem(MenuClear, false, 22)]
        private static void ClearMenu()
        {
            if (!EditorUtility.DisplayDialog("UI 皮肤",
                "清除皮肤会：\n" +
                "  1) 删掉映射文件 " + UiSkinMapping.MappingPath + "\n" +
                "  2) 关闭「构建时自动套用」\n" +
                "  3) 重建整棵白盒界面（= 纯色块版本）\n\n" +
                "几何由代码生成，重建不会丢东西；但手工在场景里加过的额外物体不会保留。确定继续？",
                "清除并重建", "取消"))
            {
                return;
            }

            AutoApplyOnBuild = false;

            if (System.IO.File.Exists(UiSkinMapping.MappingPath))
            {
                AssetDatabase.DeleteAsset(UiSkinMapping.MappingPath);
                Debug.Log("[UI皮肤] 已删除映射文件 " + UiSkinMapping.MappingPath + "。");
            }

            UiWhiteboxBuilder.BuildFromMenu();
        }

        [MenuItem(MenuAutoApply, false, 23)]
        private static void ToggleAutoApply()
        {
            AutoApplyOnBuild = !AutoApplyOnBuild;
            Debug.Log("[UI皮肤] 构建时自动套用 EPS 素材：" + (AutoApplyOnBuild ? "开" : "关"));
        }

        [MenuItem(MenuAutoApply, true, 23)]
        private static bool ToggleAutoApplyChecked()
        {
            Menu.SetChecked(MenuAutoApply, AutoApplyOnBuild);
            return true;
        }

        [MenuItem(MenuCoolText, false, 24)]
        private static void ToggleCoolText()
        {
            CoolTextOn = !CoolTextOn;

            var root = FindRoot();
            if (root == null) return;

            var changed = SwapTextPalette(root, CoolTextOn);
            SaveSceneOf(root);
            Debug.Log("[UI皮肤] 文字配色切到" + (CoolTextOn ? "青蓝（配套素材）" : "暖橙（白盒原色）") +
                      "，共改动 " + changed + " 处文字。");
        }

        [MenuItem(MenuCoolText, true, 24)]
        private static bool ToggleCoolTextChecked()
        {
            Menu.SetChecked(MenuCoolText, CoolTextOn);
            return true;
        }

        [MenuItem(MenuOpenMapping, false, 25)]
        private static void OpenMappingMenu()
        {
            if (!System.IO.File.Exists(UiSkinMapping.MappingPath))
            {
                EditorUtility.DisplayDialog("UI 皮肤",
                    "还没有映射文件：\n" + UiSkinMapping.MappingPath +
                    "\n\n先套一次素材、手工调好，再用「皮肤：记录当前皮肤到映射文件」生成。", "好");
                return;
            }

            EditorUtility.OpenWithDefaultApp(UiSkinMapping.MappingPath);
        }

        // ==================================================================
        // 角色 → 素材
        // ==================================================================

        /// <summary>一个角色用哪张图、怎么摆。</summary>
        private sealed class SkinEntry
        {
            /// <summary>相对素材目录的路径；null = 该角色暂不套图（保持白盒纯色）。</summary>
            public readonly string File;

            public readonly Image.Type Type;
            public readonly bool PreserveAspect;

            /// <summary>套图后写进 Image.color 的色（乘算）。中空边框按钮要保持底色，所以逐条可配。</summary>
            public readonly Color Tint;

            public SkinEntry(string file, Image.Type type = Image.Type.Simple,
                bool preserveAspect = false, Color? tint = null)
            {
                File = file;
                Type = type;
                PreserveAspect = preserveAspect;
                Tint = tint ?? Color.white;
            }
        }

        /// <summary>
        /// 默认映射表：**起手式**，不是最终定稿。
        ///
        /// 挑图依据（素材风格差异很大，别乱配）：
        ///   * EPS07 = 青蓝科幻 HUD（面板 / 图标 / 圆形仪表），和《Unity界面.docx》里的科技调性最接近；
        ///   * EPS09 = 发光的「中空边框」按钮（金 / 紫 / 蓝各两档），中间透明，
        ///            正好叠在白盒按钮上 —— 图负责描边，色块负责底，文字由 Label 提供；
        ///   * EPS24 / EPS25 / EPS28~31 是直播、活动页等别的主题，白盒阶段先别用。
        /// 想看全部素材长什么样：打开 预览 目录里的 6 张 JPG 缩略图（图名 = 素材前缀）。
        /// </summary>
        private static readonly Dictionary<UiSkinRole, SkinEntry> Table = new Dictionary<UiSkinRole, SkinEntry>
        {
            // 主面板底图：整张矩形图，直接铺满摆放框（形状带斜切角，拉伸后仍是同一个风格）。
            // 换图：EPS07_Panel_0x / EPS11_Panel_0x / EPS16_Panel_0x…（挑图先看 预览 目录的缩略图）
            { UiSkinRole.Panel, new SkinEntry("文本框与数据面板/EPS07_Panel_01.png") },

            // 普通按钮：两端有装饰，用 Sliced + 导入器里的九宫格边距拉伸，装饰不会被压扁。
            { UiSkinRole.Button, new SkinEntry("点击按钮/EPS09_Button_01.png", Image.Type.Sliced) },

            // 主行动按钮：同一套里的亮色款，和普通按钮一眼能分开。
            { UiSkinRole.ButtonPrimary, new SkinEntry("点击按钮/EPS09_Button_02.png", Image.Type.Sliced) },
        };

        // ==================================================================
        // 套用
        // ==================================================================

        /// <summary>
        /// 给整棵白盒套素材：先按角色铺默认图，再套 <see cref="UiSkinMapping"/> 里记录的手工调整
        /// （后者优先级更高，所以手工挑过的图不会被角色默认值盖掉）。返回处理过的元素个数。
        /// </summary>
        public static int ApplyAll(GameObject root)
        {
            if (root == null) return 0;

            var applied = 0;
            var unconfigured = new List<string>();
            var missing = new List<string>();
            var noBorder = new List<string>();

            foreach (var slot in root.GetComponentsInChildren<UiSkinSlot>(true))
            {
                if (!Table.TryGetValue(slot.role, out var entry))
                {
                    unconfigured.Add(slot.role.ToString());
                    continue;
                }

                var image = slot.GetComponent<Image>();
                if (image == null) continue;

                var sprite = Load(entry.File);
                if (sprite == null)
                {
                    if (!missing.Contains(entry.File)) missing.Add(entry.File);
                    continue;
                }

                image.sprite = sprite;
                image.type = entry.Type;
                image.preserveAspect = entry.PreserveAspect;
                image.color = entry.Tint;

                // Sliced 靠 Sprite 自带的九宫格边距；素材导入时没设边距就只会退化成 Simple（两端装饰会被拉伸）。
                if (entry.Type == Image.Type.Sliced && sprite.border == Vector4.zero && !noBorder.Contains(entry.File))
                {
                    noBorder.Add(entry.File);
                }

                // 按钮：把 ColorTint 换成中性色，否则橙色的悬停 / 按下反馈会把素材本身染成橙色。
                if (IsButtonRole(slot.role))
                {
                    var button = slot.GetComponent<Button>();
                    if (button != null) button.colors = NeutralButtonColors;
                }

                applied++;
            }

            ReportUnconfigured(unconfigured);

            foreach (var file in missing)
            {
                Debug.LogWarning("[UI皮肤] 找不到素材：" + SpriteFolder + "/" + file +
                                 "（素材还没导入？等 Unity 导入完，或跑 Tools ▸ UI ▸ EPS 素材：修正导入设置）");
            }

            foreach (var file in noBorder)
            {
                Debug.LogWarning("[UI皮肤] " + file + " 还没有九宫格切片边距，Sliced 会退化成 Simple（两端装饰被拉伸）；" +
                                 "跑一次 Tools ▸ UI ▸ EPS 素材：修正导入设置即可（边距表见 UiEpsSpriteImporter.SliceBorders）。");
            }

            applied += UiSkinMapping.ApplyOverrides(root);

            if (CoolTextOn) SwapTextPalette(root, true);

            return applied;
        }

        /// <summary>构建流程末尾的钩子：只有勾了「构建时自动套用」才动手。</summary>
        public static void ApplyAfterBuild(GameObject root)
        {
            if (!AutoApplyOnBuild || root == null) return;

            var applied = ApplyAll(root);
            Debug.Log("[UI皮肤] 构建后自动套用 EPS 素材，共处理 " + applied + " 个元素" +
                      "（想回到纯色白盒：Tools ▸ UI ▸ 皮肤：清除皮肤）。");
        }

        private static bool IsButtonRole(UiSkinRole role)
        {
            return role == UiSkinRole.Button || role == UiSkinRole.ButtonPrimary;
        }

        /// <summary>没配素材的角色只提示一次，避免刷屏；这些元素保持白盒纯色。</summary>
        private static void ReportUnconfigured(List<string> roles)
        {
            if (roles.Count == 0) return;

            roles.Sort();
            Debug.Log("[UI皮肤] 这些角色还没有默认素材（保持纯色白盒，需要时在 UiSkin.Table 里补一行）：" +
                      string.Join("、", roles));
        }

        private static Sprite Load(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "/" + relativePath);
        }

        // ==================================================================
        // 配套配色：素材是青蓝系，白盒的暖橙文字色留着会打架
        // ==================================================================

        /// <summary>中性按钮色：保留悬停 / 按下的明暗反馈，但不再往素材上叠颜色。</summary>
        private static readonly ColorBlock NeutralButtonColors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 0.96f, 0.90f, 1f),
            pressedColor = new Color(0.86f, 0.82f, 0.76f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.60f, 0.60f, 0.60f, 0.60f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f,
        };

        /// <summary>白盒主题色（暖橙）↔ 与 EPS 素材配套的冷色；只替换这几个主题色，手工调过的颜色不动。</summary>
        private static readonly (Color Warm, Color Cool)[] TextPalette =
        {
            (UiTheme.TextPrimary, new Color(0.925f, 0.965f, 1.000f, 1f)),
            (UiTheme.TextSecondary, new Color(0.702f, 0.804f, 0.859f, 1f)),
            (UiTheme.TextMuted, new Color(0.549f, 0.639f, 0.690f, 1f)),
            (UiTheme.ValueMuted, new Color(0.478f, 0.565f, 0.612f, 1f)),
            (UiTheme.TextAccent, new Color(0.400f, 0.859f, 0.965f, 1f)),
        };

        /// <summary>把整棵界面的主题文字色在「暖橙 ↔ 青蓝」之间对调，返回改动处数。</summary>
        public static int SwapTextPalette(GameObject root, bool toCool)
        {
            if (root == null) return 0;

            var changed = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                foreach (var pair in TextPalette)
                {
                    var from = toCool ? pair.Warm : pair.Cool;
                    var to = toCool ? pair.Cool : pair.Warm;
                    if (!SameColor(text.color, from)) continue;

                    text.color = to;
                    changed++;
                    break;
                }
            }

            return changed;
        }

        /// <summary>颜色比较留一点余量：Color 经过序列化 / TMP 内部转换后末位可能有极小误差。</summary>
        private static bool SameColor(Color a, Color b)
        {
            const float epsilon = 0.004f;
            return Mathf.Abs(a.r - b.r) < epsilon
                   && Mathf.Abs(a.g - b.g) < epsilon
                   && Mathf.Abs(a.b - b.b) < epsilon
                   && Mathf.Abs(a.a - b.a) < epsilon;
        }

        // ==================================================================
        // 场景工具
        // ==================================================================

        /// <summary>取当前场景里的白盒根节点（没有界面时返回 null）。</summary>
        public static GameObject FindRoot()
        {
            return GameObject.Find(UiWhiteboxBuilder.RootName);
        }

        private static void SaveSceneOf(GameObject root)
        {
            var scene = root.scene;
            if (!scene.IsValid() || !scene.isLoaded) return;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }
    }
}
