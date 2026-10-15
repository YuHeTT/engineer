// ---------------------------------------------------------------------------
//  FarmTerrainRing.cs —— 田块外围的地形
// ---------------------------------------------------------------------------
//  作用：把地形模型（平川台等）铺在田块外围，让"一片田"有落脚的地形，
//        而不是孤零零浮在一块深色底板上。
//
//  【地形模型实测规格】平川台.glb
//      X/Z 各 6.0 m，Y 2.0 m（另有 1 m 是基座，底部在 y = −1）
//      但它**不是可平铺的贴片** —— 是一个 ~6 m 见方的"三面台地"造型：
//      中心低、三边抬起、一侧开口。所以是当成景观件摆，
//      靠"多块相邻摆放"来铺出一圈地形，而不是靠拉伸一块。
//
//  【默认只铺 +Z 方向一条】
//    实测一块 12,606 三角面。铺满一圈要 37 块 ≈ 47 万面，对演示太重；
//    铺一条（9 块）约 11 万面，可接受，而且足够让画面"有远方"。
//    要铺满一圈就把 ringMode 设成 Full。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>在田块外围铺地形。</summary>
    [DisallowMultipleComponent]
    public class FarmTerrainRing : MonoBehaviour
    {
        public enum RingMode
        {
            /// <summary>不铺。</summary>
            None = 0,
            /// <summary>只在 +Z 方向铺一条（默认，性能友好）。</summary>
            FarStrip = 1,
            /// <summary>四面都铺一圈（画面最完整，面数高）。</summary>
            Full = 2,
        }

        [Tooltip("留空会在场景里自动查找 FarmFieldBuilder。")]
        [SerializeField] private FarmFieldBuilder field;

        [Tooltip("地形预制体。默认平川台（glTF，需装 glTFast）。")]
        [SerializeField] private GameObject terrainPrefab;

        [SerializeField] private RingMode ringMode = RingMode.FarStrip;

        [Tooltip("植被/裸地色，铺在地形块之间当缝隙。")]
        [SerializeField] private Color groundColor = new Color(0.30f, 0.30f, 0.20f);

        private Transform root;
        private readonly List<Transform> blocks = new List<Transform>();

        public int BlockCount { get { return blocks.Count; } }
        public RingMode Mode { get { return ringMode; } }

        private void Start()
        {
            // 只在运行时生成 —— 否则编辑期生成的地形会被序列化进 Farm.unity，
            // Play 时再生成一遍就成了两套。详见 FarmFieldBuilder.Awake 的说明。
            if (Application.isPlaying) Build();
        }

        /// <summary>重新生成。</summary>
        public void Build()
        {
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
            if (field == null || field.Config == null) return;
            if (ringMode == RingMode.None) return;

            Clear();

            if (root == null)
            {
                var existing = transform.Find("TerrainRing");
                if (existing != null) root = existing;
                else
                {
                    var go = new GameObject("TerrainRing");
                    go.transform.SetParent(transform, false);
                    root = go.transform;
                }
            }

            var cfg = field.Config;
            cfg.Sanitize();

            // 地形块规格（可以从预制体量，这里用实测常量兜底）
            var pitch = TerrainPitch;
            var fieldSize = cfg.FieldSize;
            var pad = cfg.RingPathWidth + cfg.PathWidth;

            // 一条边需要几块才能盖住田块
            var perSide = Mathf.Max(3, Mathf.CeilToInt(fieldSize.x / pitch) + 2);

            // 地形条带放在田块边缘之外：田块半宽 + 环田路 + 半个地形块
            var offsetZ = fieldSize.y * 0.5f + pad + pitch * 0.5f;
            var offsetX = fieldSize.x * 0.5f + pad + pitch * 0.5f;

            // 铺之前先垫一层草/土色底面，盖住地形块之间的缝
            EnsureGroundPad(fieldSize + Vector2.one * (pitch * 2f + pad * 2f));

            if (ringMode == RingMode.FarStrip)
            {
                AddRow("Terrain_N", new Vector3(0f, 0f, offsetZ), perSide, pitch, true);
            }
            else
            {
                var perSideZ = Mathf.Max(3, Mathf.CeilToInt(fieldSize.y / pitch) + 2);
                AddRow("Terrain_N", new Vector3(0f, 0f, offsetZ), perSide, pitch, true);
                AddRow("Terrain_S", new Vector3(0f, 0f, -offsetZ), perSide, pitch, true);
                AddRow("Terrain_E", new Vector3(offsetX, 0f, 0f), perSideZ, pitch, false);
                AddRow("Terrain_W", new Vector3(-offsetX, 0f, 0f), perSideZ, pitch, false);
            }

            Debug.Log(string.Format(
                "[农场] 外围地形已铺 {0} 块（模式 {1}，地形块间距 {2:0.#} m）。" +
                "平川台一块约 1.26 万三角面，所以 {0} 块 ≈ {3:0.#} 万面。",
                blocks.Count, ringMode, pitch, blocks.Count * 1.2606f), this);
        }

        /// <summary>清掉已生成的。</summary>
        public void Clear()
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] == null) continue;
                if (Application.isPlaying) Destroy(blocks[i].gameObject);
                else DestroyImmediate(blocks[i].gameObject);
            }
            blocks.Clear();
        }

        /// <summary>地形块的摆放间距（米）。平川台实测 X/Z 各 6.0。</summary>
        private const float TerrainPitch = 6f;

        /// <summary>沿一个方向铺一排地形块。</summary>
        private void AddRow(string rowName, Vector3 center, int count, float pitch, bool alongX)
        {
            if (terrainPrefab == null) return;

            var span = (count - 1) * pitch;
            for (var i = 0; i < count; i++)
            {
                var t = -span * 0.5f + pitch * i;
                var pos = alongX
                    ? new Vector3(center.x + t, 0f, center.z)
                    : new Vector3(center.x, 0f, center.z + t);

                var go = Instantiate(terrainPrefab, root);
                go.name = string.Format("{0}_{1}", rowName, i);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.identity;

                // 地形模型底部在 y = −1，抬到 0 就正好贴地
                go.transform.localScale = Vector3.one;
                go.transform.localPosition = pos + Vector3.up * 1f;

                blocks.Add(go.transform);
            }
        }

        private void EnsureGroundPad(Vector2 size)
        {
            var pad = root.Find("TerrainPad");
            if (pad == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "TerrainPad";
                go.transform.SetParent(root, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
                pad = go.transform;
            }

            pad.localPosition = new Vector3(0f, -0.12f, 0f);
            pad.localScale = new Vector3(size.x, 0.2f, size.y);

            var r = pad.GetComponent<Renderer>();
            if (r != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader == null) shader = Shader.Find("Diffuse");

                var m = new Material(shader);
                m.name = "FarmTerrainPad";
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", groundColor);
                if (m.HasProperty("_Color")) m.SetColor("_Color", groundColor);
                r.sharedMaterial = m;
            }
        }
    }
}
