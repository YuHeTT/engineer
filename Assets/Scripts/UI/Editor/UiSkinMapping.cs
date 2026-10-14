using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 「哪个元素用哪张图」的手工调整记录（JSON 快照）。
    ///
    /// 角色默认表（<see cref="UiSkin.Table"/>）只能解决「面板 / 按钮」这种谁都有的元素；
    /// 白盒上还有一堆具体的东西（巡航覆盖率进度条、某块仪表盘、某张卡片）需要人眼挑图。
    /// 于是留了一条「改完存下来」的路：
    ///   套用素材 → 在 Inspector 里逐屏微调（换图、Sliced、颜色）→「记录当前皮肤到映射文件」；
    ///   之后每次重建界面，<see cref="UiSkin.ApplyAll"/> 会在角色默认之后把这批手工调整套回去
    ///   （后套的赢，所以手工挑的图不会被角色默认值盖掉）。
    ///
    /// 映射文件是普通 JSON，放在 Assets 里可以直接手改 / diff / 删条目：
    ///   path   = 相对 UI_Root 的层级路径，例如 Screens/Screen_Welcome/Button_Intro
    ///   sprite = 素材资产路径；留空表示这个元素不要图（纯色）
    /// </summary>
    public static class UiSkinMapping
    {
        /// <summary>映射文件位置（跟着素材一起进版本库，重建界面时自动生效）。</summary>
        public const string MappingPath = "Assets/UIresources/白盒皮肤映射.json";

        /// <summary>一条「某元素 → 某张图」的记录。</summary>
        [System.Serializable]
        public sealed class Binding
        {
            public string path;
            public string sprite;
            public int type;
            public bool preserveAspect;
            public Color color = Color.white;
        }

        /// <summary>整份映射文件。</summary>
        [System.Serializable]
        public sealed class Mapping
        {
            public string note = "由 Tools ▸ UI ▸ 皮肤：记录当前皮肤到映射文件 生成；path 相对 UI_Root，可直接手改。";
            public List<Binding> bindings = new List<Binding>();
        }

        // ==================================================================
        // 记录（场景 → JSON）
        // ==================================================================

        /// <summary>把当前界面上所有「已经带图」的 Image 记下来，返回记录条数。</summary>
        public static int Record(GameObject root)
        {
            if (root == null) return 0;

            var mapping = new Mapping();
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;

                mapping.bindings.Add(new Binding
                {
                    path = PathOf(image.transform, root.transform),
                    sprite = AssetDatabase.GetAssetPath(image.sprite),
                    type = (int)image.type,
                    preserveAspect = image.preserveAspect,
                    color = image.color,
                });
            }

            mapping.bindings.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            File.WriteAllText(MappingPath, JsonUtility.ToJson(mapping, true), new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(MappingPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[UI皮肤] 已记录 " + mapping.bindings.Count + " 条皮肤映射 → " + MappingPath);
            return mapping.bindings.Count;
        }

        // ==================================================================
        // 套用（JSON → 场景）
        // ==================================================================

        /// <summary>按映射文件给界面贴图 / 清图，返回生效条数；没有映射文件时什么都不做。</summary>
        public static int ApplyOverrides(GameObject root)
        {
            if (root == null) return 0;

            var mapping = Load();
            if (mapping == null || mapping.bindings == null || mapping.bindings.Count == 0) return 0;

            var index = new Dictionary<string, Transform>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                var key = PathOf(child, root.transform);
                if (!index.ContainsKey(key)) index[key] = child;
            }

            var applied = 0;
            var lost = 0;

            foreach (var binding in mapping.bindings)
            {
                if (binding == null || string.IsNullOrEmpty(binding.path)) continue;

                if (!index.TryGetValue(binding.path, out var target))
                {
                    lost++;
                    continue;
                }

                var image = target.GetComponent<Image>();
                if (image == null) continue;

                Sprite sprite = null;
                if (!string.IsNullOrEmpty(binding.sprite))
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(binding.sprite);
                    if (sprite == null)
                    {
                        Debug.LogWarning("[UI皮肤] 映射里的素材已不存在：" + binding.sprite +
                                         "（元素 " + binding.path + "），这条跳过。");
                        continue;
                    }
                }

                image.sprite = sprite;
                image.type = (Image.Type)binding.type;
                image.preserveAspect = binding.preserveAspect;
                image.color = binding.color;
                applied++;
            }

            if (lost > 0)
            {
                Debug.LogWarning("[UI皮肤] 映射里有 " + lost + " 条路径在当前界面树上不存在" +
                                 "（界面层级 / 名字改过？）—— 重新「记录当前皮肤」会刷新这份映射。");
            }

            return applied;
        }

        // ==================================================================
        // 工具
        // ==================================================================

        /// <summary>相对 <paramref name="root"/> 的层级路径（根节点自身 = 空串）。</summary>
        public static string PathOf(Transform target, Transform root)
        {
            if (target == null || root == null) return string.Empty;

            var parts = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Mapping Load()
        {
            if (!File.Exists(MappingPath)) return null;

            try
            {
                return JsonUtility.FromJson<Mapping>(File.ReadAllText(MappingPath));
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[UI皮肤] 读不了映射文件 " + MappingPath + "：" + exception.Message);
                return null;
            }
        }
    }
}
