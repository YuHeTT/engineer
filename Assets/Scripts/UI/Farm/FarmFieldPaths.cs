// ---------------------------------------------------------------------------
//  FarmFieldPaths.cs —— 田埂与田间小路
// ---------------------------------------------------------------------------
//  作用：在田块之间和整片田外围铺出田埂/小路。
//
//  【为什么需要】
//    田块是四块独立的土壤，之间只有深色底板。远看像"四块地飘在水泥地上"，
//    不像一整片农田。铺上土色的小路之后，视觉上才连成一块地。
//
//  【尺寸怎么算的】（都要和 <see cref="FarmVisualConfig"/> 保持一致）
//    田块间隔 BlockGap 平分三份：
//       两侧各 (BlockGap − PathWidth)/2 = 田埂，紧贴种植区
//       中间 PathWidth                  = 小路
//    最外圈再铺 RingPathWidth 宽的环田小路（可选）。
//
//  所有几何都是扁平的 Cube，运行时生成，不需要美术资源。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>在田块之间与外围铺田埂、小路。</summary>
    [DisallowMultipleComponent]
    public class FarmFieldPaths : MonoBehaviour
    {
        [Tooltip("留空会在场景里自动查找 FarmFieldBuilder。")]
        [SerializeField] private FarmFieldBuilder field;

        [Tooltip("田埂/小路离地高度（米）。稍微高过底板、低于土壤顶面。")]
        [SerializeField] private float pathHeight = 0.06f;

        [Header("颜色")]
        [SerializeField] private Color pathColor = new Color(0.34f, 0.27f, 0.18f);
        [SerializeField] private Color ridgeColor = new Color(0.28f, 0.22f, 0.14f);

        private Transform root;
        private readonly List<Transform> pieces = new List<Transform>();

        public int PieceCount { get { return pieces.Count; } }

        private void Start()
        {
            // 只在运行时生成 —— 否则编辑期生成的路面会被序列化进 Farm.unity，
            // Play 时再生成一遍就成了两套。详见 FarmFieldBuilder.Awake 的说明。
            if (Application.isPlaying) Build();
        }

        /// <summary>重新生成。</summary>
        public void Build()
        {
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
            if (field == null || field.Config == null) return;

            Clear();

            if (root == null)
            {
                var existing = transform.Find("Paths");
                if (existing != null) root = existing;
                else
                {
                    var go = new GameObject("Paths");
                    go.transform.SetParent(transform, false);
                    root = go.transform;
                }
            }

            var cfg = field.Config;
            cfg.Sanitize();

            BuildBetweenBlocks(cfg);
            if (cfg.RingPath) BuildRing(cfg);

            Debug.Log(string.Format("[农场] 田埂/小路已铺 {0} 块（田埂宽 {1:0.##} m、小路宽 {2:0.##} m、环田路 {3}）。",
                pieces.Count, cfg.FieldMargin, cfg.PathWidth,
                cfg.RingPath ? cfg.RingPathWidth.ToString("0.##") + " m" : "关闭"), this);
        }

        /// <summary>清掉已生成的。</summary>
        public void Clear()
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                if (Application.isPlaying) Destroy(pieces[i].gameObject);
                else DestroyImmediate(pieces[i].gameObject);
            }
            pieces.Clear();
        }

        // ==================================================================
        //  田块之间：两侧田埂 + 中间小路
        // ==================================================================

        private void BuildBetweenBlocks(FarmVisualConfig cfg)
        {
            var g = cfg.BlockGroupSize;
            var gap = cfg.BlockGap;
            if (gap <= 0.01f) return;

            var soilW = (gap - cfg.PathWidth) * 0.5f;      // 每侧田埂宽
            if (soilW < 0f) soilW = 0f;

            var total = cfg.FieldSize;
            var stepX = g.x + gap;
            var stepZ = g.y + gap;
            var x0 = -total.x * 0.5f + cfg.FieldMargin + g.x * 0.5f;
            var z0 = -total.y * 0.5f + cfg.FieldMargin + g.y * 0.5f;

            // ---- 竖直方向的小路（田块列之间）----
            for (var i = 0; i < cfg.BlockColumns - 1; i++)
            {
                var xLeftEdge = x0 + stepX * i + g.x * 0.5f;

                if (cfg.PathWidth > 0.01f)
                    AddSlab(string.Format("Path_V{0}", i),
                            new Vector3(xLeftEdge + gap * 0.5f, 0f, 0f),
                            new Vector3(cfg.PathWidth, pathHeight, total.y),
                            pathColor);

                if (soilW > 0.01f)
                {
                    AddSlab(string.Format("Ridge_V{0}a", i),
                            new Vector3(xLeftEdge + soilW * 0.5f, 0f, 0f),
                            new Vector3(soilW, pathHeight, total.y), ridgeColor);
                    AddSlab(string.Format("Ridge_V{0}b", i),
                            new Vector3(xLeftEdge + gap - soilW * 0.5f, 0f, 0f),
                            new Vector3(soilW, pathHeight, total.y), ridgeColor);
                }
            }

            // ---- 水平方向的小路（田块行之间）----
            for (var j = 0; j < cfg.BlockRows - 1; j++)
            {
                var zBottomEdge = z0 + stepZ * j + g.y * 0.5f;

                if (cfg.PathWidth > 0.01f)
                    AddSlab(string.Format("Path_H{0}", j),
                            new Vector3(0f, 0f, zBottomEdge + gap * 0.5f),
                            new Vector3(total.x, pathHeight, cfg.PathWidth),
                            pathColor);

                if (soilW > 0.01f)
                {
                    AddSlab(string.Format("Ridge_H{0}a", j),
                            new Vector3(0f, 0f, zBottomEdge + soilW * 0.5f),
                            new Vector3(total.x, pathHeight, soilW), ridgeColor);
                    AddSlab(string.Format("Ridge_H{0}b", j),
                            new Vector3(0f, 0f, zBottomEdge + gap - soilW * 0.5f),
                            new Vector3(total.x, pathHeight, soilW), ridgeColor);
                }
            }
        }

        // ==================================================================
        //  最外圈环田小路
        // ==================================================================

        private void BuildRing(FarmVisualConfig cfg)
        {
            var total = cfg.FieldSize;
            var w = cfg.RingPathWidth;
            var bw = cfg.FieldMargin;         // 环田路宽度取田埂宽，正好贴在田块外沿
            if (bw < 0.05f) bw = w;

            var halfX = total.x * 0.5f + bw * 0.5f;
            var halfZ = total.y * 0.5f + bw * 0.5f;

            // 前后两条（沿 X 延伸）
            AddSlab("Ring_N", new Vector3(0f, 0f, halfZ), new Vector3(total.x + bw * 2f, pathHeight, bw), pathColor);
            AddSlab("Ring_S", new Vector3(0f, 0f, -halfZ), new Vector3(total.x + bw * 2f, pathHeight, bw), pathColor);
            // 左右两条（沿 Z 延伸）
            AddSlab("Ring_E", new Vector3(halfX, 0f, 0f), new Vector3(bw, pathHeight, total.y), pathColor);
            AddSlab("Ring_W", new Vector3(-halfX, 0f, 0f), new Vector3(bw, pathHeight, total.y), pathColor);
        }

        // ==================================================================
        //  工具
        // ==================================================================

        private void AddSlab(string pieceName, Vector3 center, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = pieceName;
            go.transform.SetParent(root, false);

            // 只要外观，不要碰撞体
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            }

            go.transform.localPosition = new Vector3(center.x, center.y + pathHeight * 0.5f, center.z);
            go.transform.localScale = size;

            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = GetMaterial(color);

            pieces.Add(go.transform);
        }

        private readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();

        private Material GetMaterial(Color color)
        {
            Material m;
            if (materials.TryGetValue(color, out m) && m != null) return m;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            m = new Material(shader);
            m.name = "FarmPath_" + ColorUtility.ToHtmlStringRGB(color);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.05f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);

            materials[color] = m;
            return m;
        }
    }
}
