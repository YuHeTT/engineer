using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// EPS 素材（《UI》目录导出流水线的成品）导入参数统一器。
    ///
    /// 素材已经挑干净的副本放在 <see cref="FolderPath"/> 下（181 张 PNG + 6 张预览 JPG），
    /// 它们都是给 uGUI 用的界面元素。工程默认按 3D 贴图导入的话，Texture Type 是 Default、
    /// 还带 mipmap，Image / Image(Sprite) 拖进去会不匹配、贴图也会被压缩发糊。这里做两件事：
    ///   1) 之后新增的素材自动套用同一组参数（Texture Type = Sprite、FullRect、关 mipmap…）；
    ///   2) 已经导入过、参数不对的素材，脚本编译完会**自动补导一次**（见 <see cref="EnsureImportedSettings"/>），
    ///      也可以随时用菜单 Tools ▸ UI ▸ EPS 素材：修正导入设置 手动重刷；
    ///   3) 顺带给边框型按钮图（EPS09 系列）写好九宫格切片边距，套素材时按 Sliced 拉伸不会压扁两端装饰。
    ///
    /// 只处理 <see cref="FolderPath"/> 目录下的 png/jpg，工程里其他贴图（含三套 GUI Kit）不受影响。
    /// </summary>
    public class UiEpsSpriteImporter : AssetPostprocessor
    {
        /// <summary>素材根目录。</summary>
        public const string FolderPath = "Assets/UIresources/EPS素材";

        /// <summary>像素密度：100 像素 = 1 世界单位，符合 UI 习惯。</summary>
        private const float PixelsPerUnit = 100f;

        /// <summary>白盒阶段够用的最大边长，避免个别大图白白吃显存。</summary>
        private const int MaxSize = 2048;

        /// <summary>
        /// 九宫格切片边距（像素，按 L/B/R/T 顺序，这里左右 / 上下对称）——按**文件名前缀**匹配。
        ///
        /// 只给「边框型」的图设：它们中间是空的（透明），两端有装饰，用 Sliced 九宫格拉伸才不会被压扁。
        /// 按钮套素材时（Tools ▸ UI ▸ 皮肤）会自动用 Image.Type.Sliced 配合这里的边距；
        /// 觉得切片位置不对就改下面这个数字（单位：原图像素）。
        /// </summary>
        private static readonly Dictionary<string, Vector4> SliceBorders = new Dictionary<string, Vector4>
        {
            // EPS09 系列是发光中空边框按钮（约 730×225）：两端装饰约占宽 11%、上下边框约占高 12%。
            { "EPS09_Button_", new Vector4(80f, 28f, 80f, 28f) },
        };

        private void OnPreprocessTexture()
        {
            if (!IsTarget(assetPath)) return;
            Apply(assetImporter as TextureImporter, assetPath);
        }

        /// <summary>
        /// 兜底：素材先于本脚本被导入时（Unity 导入资源与编译脚本的先后顺序并不保证），
        /// 域重载后检查一遍目录，发现还有没按 Sprite 导入的就自动补导一次，做到「零手动操作」。
        /// 检查通过就什么都不做，所以不会反复重导。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureImportedSettings()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!NeedsFix()) return;

                Debug.Log("[EPS素材] 检测到素材还不是 UI 导入参数，自动补导一次。");
                FixAll(silentWhenEmpty: true);
            };
        }

        /// <summary>
        /// 把 <see cref="FolderPath"/> 下已有的图全部按 UI 参数重导一遍。
        /// 重导会再次触发 <see cref="OnPreprocessTexture"/>，两者写入的参数一致，重复执行安全。
        /// </summary>
        [MenuItem("Tools/UI/EPS 素材：修正导入设置（按 Sprite 重导）")]
        public static void FixAllImported()
        {
            FixAll(silentWhenEmpty: false);
        }

        /// <summary>实际执行批量重导，返回处理张数。</summary>
        private static int FixAll(bool silentWhenEmpty)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath });
            var total = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsTarget(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                Apply(importer, path);
                importer.SaveAndReimport();
                total++;
            }

            AssetDatabase.SaveAssets();

            if (total > 0 || !silentWhenEmpty)
            {
                Debug.Log("[EPS素材] 已按 Sprite 参数重导 " + total + " 张图（目录：" + FolderPath + "）。");
            }

            return total;
        }

        /// <summary>目录里是否还有「不像 UI Sprite」的图（判断要不要补导）。</summary>
        private static bool NeedsFix()
        {
            if (!AssetDatabase.IsValidFolder(FolderPath)) return false;

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsTarget(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled) return true;

                // 需要九宫格边距的按钮图：边距没设对也算「还没配好」。
                if (TryGetSliceBorder(path, out var expected))
                {
                    var current = new TextureImporterSettings();
                    importer.ReadTextureSettings(current);
                    if (current.spriteBorder != expected) return true;
                }
            }

            return false;
        }

        /// <summary>按文件名前缀查这张图要不要九宫格边距。</summary>
        private static bool TryGetSliceBorder(string path, out Vector4 border)
        {
            border = Vector4.zero;

            var fileName = System.IO.Path.GetFileName(path);
            foreach (var pair in SliceBorders)
            {
                if (!fileName.StartsWith(pair.Key, System.StringComparison.Ordinal)) continue;

                border = pair.Value;
                return true;
            }

            return false;
        }

        /// <summary>把导入器调成「uGUI 能直接用」的状态。</summary>
        private static void Apply(TextureImporter importer, string path)
        {
            if (importer == null) return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect; // UI 里会被拉伸/九宫格，紧贴网格容易出怪边
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.mipmapEnabled = false;                    // UI 用不到 mipmap，关掉更清晰、更省显存
            settings.alphaIsTransparency = true;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;
            settings.npotScale = TextureImporterNPOTScale.None; // 非 2 次幂尺寸保持原样，避免被缩放

            // 边框型按钮图：写九宫格边距，套素材时按 Sliced 拉伸才不会把两端装饰压扁。
            if (TryGetSliceBorder(path, out var border)) settings.spriteBorder = border;

            importer.SetTextureSettings(settings);
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        /// <summary>只认素材目录下的 png/jpg。</summary>
        private static bool IsTarget(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var normalized = path.Replace('\\', '/');
            if (!normalized.StartsWith(FolderPath + "/", System.StringComparison.Ordinal)) return false;

            return normalized.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)
                   || normalized.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
