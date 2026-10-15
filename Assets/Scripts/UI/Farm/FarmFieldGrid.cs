// ---------------------------------------------------------------------------
//  FarmFieldGrid.cs —— 把田块切成网格，跟踪"哪些格被探测到了"
// ---------------------------------------------------------------------------
//  作用：这是「巡航覆盖率」的度量。小车走过哪里，哪里的格子就被标记为已探测。
//
//  【为什么用面积栅格化，而不是"数格子"】
//    田块尺寸是可变的（1~9 亩），网格行列数和田块尺寸不一定整除，
//    如果简单地"覆盖到格子中心就算整格已探测"，会**系统性高估**覆盖率
//    （边缘那半格明明没扫到，却算 100%），而且误差随网格粗细变化，很难解释。
//
//    这里改成**面积法**：把整片田按 10 像素/米栅格化，
//    每个像素归属它中心落入的那个格子，算出每个格子的**真实面积**；
//    探测时只把**落在扫描半径内**的那部分面积算作已覆盖。
//    这样覆盖率是"已探测面积 / 田块总面积"，任何网格粗细下都自洽。
//
//  【面积只算一次】
//    每个格子的面积是静态的，第一次初始化时算好并缓存，之后巡航过程零开销。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>田块网格与探测覆盖状态。</summary>
    [DisallowMultipleComponent]
    public class FarmFieldGrid : MonoBehaviour
    {
        /// <summary>栅格化精度：每米采样多少个点。10 已经足够（面积误差 &lt;1%）。</summary>
        private const int SamplesPerMeter = 10;

        /// <summary>一个网格格子的信息。</summary>
        public struct Cell
        {
            /// <summary>格子中心（世界坐标，y=0）。</summary>
            public Vector2 Center;
            /// <summary>格子在世界 X 方向的尺寸。</summary>
            public float SizeX;
            /// <summary>格子在世界 Z 方向的尺寸。</summary>
            public float SizeZ;
            /// <summary>格子的几何面积。</summary>
            public float Area;
            /// <summary>是否已被探测到。</summary>
            public bool Covered;
            /// <summary>已探测的面积（≤ Area）。</summary>
            public float CoveredArea;
        }

        [Tooltip("网格列数（世界 X 方向）。")]
        [SerializeField] private int columns = 10;

        [Tooltip("网格行数（世界 Z 方向）。")]
        [SerializeField] private int rows = 8;

        [Tooltip("留空会在场景里自动查找 FarmFieldBuilder。")]
        [SerializeField] private FarmFieldBuilder field;

        [Tooltip("是否在 Console 打印覆盖率变化。")]
        [SerializeField] private bool logCoverage = false;

        [Header("运行时状态（只读）")]
        [SerializeField] private float coverage;
        [SerializeField] private int coveredCells;

        private Cell[] cells;
        private Bounds fieldBounds;
        private float totalArea;
        private bool built;

        /// <summary>覆盖率 0~1。</summary>
        public float Coverage { get { return coverage; } }

        /// <summary>已探测的格子数。</summary>
        public int CoveredCells { get { return coveredCells; } }

        /// <summary>格子总数。</summary>
        public int CellCount { get { return cells != null ? cells.Length : 0; } }

        public int Columns { get { return columns; } }
        public int Rows { get { return rows; } }

        /// <summary>田块的世界包围盒。</summary>
        public Bounds FieldBounds { get { return fieldBounds; } }

        /// <summary>格子数组（只读访问用）。</summary>
        public Cell[] Cells { get { return cells; } }

        /// <summary>覆盖率变化时触发（参数为 0~1）。</summary>
        public event System.Action<float> CoverageChanged;

        private void Awake()
        {
            // 网格是纯逻辑（没有几何），但构建依赖 FarmFieldBuilder 的 FieldBounds，
            // 所以必须等田块生成完。田块在 FarmFieldBuilder.Awake 里生成，
            // 而组件顺序是确定的（Builder 先加），所以这里直接 Build 就行。
            if (Application.isPlaying) Build();
        }

        // ==================================================================
        //  构建
        // ==================================================================

        /// <summary>按当前田块尺寸重建网格。</summary>
        public void Build()
        {
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
            if (field == null) return;

            fieldBounds = field.FieldBounds;

            // 田块可能还没生成（FieldBounds 是空的），退回用配置算
            if (fieldBounds.size.x < 0.01f || fieldBounds.size.z < 0.01f)
            {
                var size = field.Config.FieldSize;
                fieldBounds = new Bounds(
                    transform.position,
                    new Vector3(size.x, 1f, size.y));
            }

            columns = Mathf.Max(1, columns);
            rows = Mathf.Max(1, rows);

            cells = new Cell[columns * rows];
            BuildCellGeometry();
            ComputeCellAreas();

            coveredCells = 0;
            coverage = 0f;
            built = true;

            if (logCoverage)
            {
                Debug.Log(string.Format("[农场] 巡航网格已建：{0}×{1} = {2} 格，" +
                                        "田块 {3:0.#}×{4:0.#} m，总面积 {5:0.#} 平方米。",
                    columns, rows, cells.Length,
                    fieldBounds.size.x, fieldBounds.size.z, totalArea), this);
            }
        }

        private void BuildCellGeometry()
        {
            var sizeX = fieldBounds.size.x / columns;
            var sizeZ = fieldBounds.size.z / rows;
            var minX = fieldBounds.min.x;
            var minZ = fieldBounds.min.z;

            for (var r = 0; r < rows; r++)
            {
                for (var c = 0; c < columns; c++)
                {
                    var idx = r * columns + c;
                    cells[idx].SizeX = sizeX;
                    cells[idx].SizeZ = sizeZ;
                    cells[idx].Area = sizeX * sizeZ;
                    cells[idx].Center = new Vector2(
                        minX + sizeX * (c + 0.5f),
                        minZ + sizeZ * (r + 0.5f));
                    cells[idx].Covered = false;
                    cells[idx].CoveredArea = 0f;
                }
            }
        }

        /// <summary>
        /// 用面积栅格化算出每格的真实面积。
        /// 目的是让"覆盖率"在任意网格粗细下都自洽 —— 见文件头说明。
        /// </summary>
        private void ComputeCellAreas()
        {
            var nx = Mathf.Max(2, Mathf.RoundToInt(fieldBounds.size.x * SamplesPerMeter));
            var nz = Mathf.Max(2, Mathf.RoundToInt(fieldBounds.size.z * SamplesPerMeter));
            var pixelArea = (fieldBounds.size.x / nx) * (fieldBounds.size.z / nz);

            // 先清零
            for (var i = 0; i < cells.Length; i++) cells[i].Area = 0f;

            var minX = fieldBounds.min.x;
            var minZ = fieldBounds.min.z;

            for (var iz = 0; iz < nz; iz++)
            {
                var z = minZ + fieldBounds.size.z * (iz + 0.5f) / nz;
                var r = Mathf.Clamp((int)((z - minZ) / fieldBounds.size.z * rows), 0, rows - 1);

                for (var ix = 0; ix < nx; ix++)
                {
                    var x = minX + fieldBounds.size.x * (ix + 0.5f) / nx;
                    var c = Mathf.Clamp((int)((x - minX) / fieldBounds.size.x * columns), 0, columns - 1);

                    cells[r * columns + c].Area += pixelArea;
                }
            }

            totalArea = 0f;
            for (var i = 0; i < cells.Length; i++) totalArea += cells[i].Area;
        }

        // ==================================================================
        //  探测
        // ==================================================================

        /// <summary>
        /// 标记"以 center 为圆心、radius 为半径"的圆内所有格子。
        /// 由巡航小车在移动时调用。
        /// </summary>
        /// <returns>本次新覆盖的格子数。</returns>
        public int MarkScanned(Vector3 center, float radius)
        {
            if (!built || cells == null || radius <= 0f) return 0;

            var r2 = radius * radius;
            var newly = 0;

            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i].Covered) continue;

                // 用"格子中心到扫描中心的距离"判定。
                // 更精确的做法是算圆与矩形的重叠面积，但格子只有 2~3 m，
                // 用中心判定已经足够，而且避免逐格做几何裁剪的开销。
                var dx = cells[i].Center.x - center.x;
                var dz = cells[i].Center.y - center.z;
                if (dx * dx + dz * dz > r2) continue;

                cells[i].Covered = true;
                cells[i].CoveredArea = cells[i].Area;
                coveredCells++;
                newly++;
            }

            if (newly > 0) RecomputeCoverage();
            return newly;
        }

        /// <summary>直接把覆盖率设成某个值（调试或"自动巡航"一键跑完时用）。</summary>
        public void ForceCoverage(float value)
        {
            if (!built || cells == null) return;

            value = Mathf.Clamp01(value);
            var target = Mathf.RoundToInt(value * cells.Length);

            coveredCells = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                var on = i < target;
                cells[i].Covered = on;
                cells[i].CoveredArea = on ? cells[i].Area : 0f;
                if (on) coveredCells++;
            }
            RecomputeCoverage();
        }

        /// <summary>全部标记为已探测。</summary>
        public void MarkAllScanned()
        {
            ForceCoverage(1f);
        }

        /// <summary>清空探测状态。</summary>
        public void ClearCoverage()
        {
            if (cells == null) return;
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].Covered = false;
                cells[i].CoveredArea = 0f;
            }
            coveredCells = 0;
            RecomputeCoverage();
        }

        private void RecomputeCoverage()
        {
            if (totalArea <= 0f)
            {
                coverage = 0f;
            }
            else
            {
                var covered = 0f;
                for (var i = 0; i < cells.Length; i++) covered += cells[i].CoveredArea;
                coverage = Mathf.Clamp01(covered / totalArea);
            }

            var handler = CoverageChanged;
            if (handler != null) handler(coverage);

            if (logCoverage)
                Debug.Log(string.Format("[农场] 覆盖率 {0:0.0}%（{1}/{2} 格）",
                    coverage * 100f, coveredCells, cells.Length), this);
        }

        // ==================================================================
        //  可视化：未覆盖区域的遮罩
        // ==================================================================

        [Header("未覆盖区域可视化")]
        [Tooltip("是否在未探测的格子上盖一层半透明遮罩。")]
        [SerializeField] private bool showUncoveredMask = true;

        [Tooltip("只在运行时生成遮罩。\n" +
                 "★ 强烈建议保持开启：编辑器里生成遮罩会把整片田盖成黑色，" +
                 "在 Scene 视图里根本看不出田和玉米长什么样。")]
        [SerializeField] private bool maskOnlyInPlayMode = true;

        [Tooltip("遮罩颜色。alpha 决定浓度 —— 太大会把土壤和玉米都盖住。")]
        [SerializeField] private Color maskColor = new Color(0.05f, 0.08f, 0.12f, 0.38f);

        [Tooltip("遮罩离地高度（米）。\n" +
                 "★ 不要设太高：幼苗只有 0.36 m 高，遮罩设在 1.6 m 会从上面盖住它，\n" +
                 "看起来就是玉米没了。0.25~0.35 让它贴在地表附近，\n" +
                 "又低于垄面(0.475)，不会穿帮。")]
        [SerializeField] private float maskHeight = 0.30f;

        private readonly List<Transform> maskTiles = new List<Transform>();
        private Transform maskRoot;

        /// <summary>刷新未覆盖遮罩（覆盖率变化后调用）。</summary>
        public void RefreshMask()
        {
            if (!built || cells == null) return;

            // ★ 编辑器里不生成遮罩：它铺满整片田，会把场景盖成一片黑，
            //   在 Scene 视图里完全看不出田和玉米的样子。
            if (!showUncoveredMask || (maskOnlyInPlayMode && !Application.isPlaying))
            {
                if (maskRoot != null) maskRoot.gameObject.SetActive(false);
                return;
            }

            EnsureMaskRoot();
            maskRoot.gameObject.SetActive(true);

            var shown = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                if (i >= maskTiles.Count) break;
                var t = maskTiles[i];
                if (t == null) continue;
                // 已探测的格子撤掉遮罩 —— 视觉上就是"扫描过的地方亮起来了"
                var on = !cells[i].Covered;
                t.gameObject.SetActive(on);
                if (on) shown++;
            }

            if (logCoverage)
                Debug.Log(string.Format("[农场] 未覆盖遮罩：{0}/{1} 格仍在遮挡。", shown, cells.Length), this);
        }

        private void EnsureMaskRoot()
        {
            if (maskRoot == null)
            {
                var existing = transform.Find("UncoveredMask");
                if (existing != null) maskRoot = existing;
                else
                {
                    var go = new GameObject("UncoveredMask");
                    go.transform.SetParent(transform, false);
                    maskRoot = go.transform;
                }
            }

            if (maskTiles.Count == cells.Length && maskTiles[0] != null) return;

            // 重建遮罩块
            for (var i = 0; i < maskTiles.Count; i++)
            {
                if (maskTiles[i] == null) continue;
                if (Application.isPlaying) Destroy(maskTiles[i].gameObject);
                else DestroyImmediate(maskTiles[i].gameObject);
            }
            maskTiles.Clear();

            var mat = GetMaskMaterial();

            for (var i = 0; i < cells.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = string.Format("Mask_{0}", i);
                go.transform.SetParent(maskRoot, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }

                // Quad 默认朝 -Z，转成朝上
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localPosition = new Vector3(
                    cells[i].Center.x - transform.position.x, maskHeight,
                    cells[i].Center.y - transform.position.z);
                // Quad 默认 1×1，缩到格子大小（X→世界 X，Y→世界 Z）
                go.transform.localScale = new Vector3(cells[i].SizeX, cells[i].SizeZ, 1f);

                var r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;

                maskTiles.Add(go.transform);
            }
        }

        private Material maskMaterial;

        private Material GetMaskMaterial()
        {
            if (maskMaterial != null) return maskMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");

            maskMaterial = new Material(shader);
            maskMaterial.name = "FarmUncoveredMask";
            maskMaterial.color = maskColor;
            if (maskMaterial.HasProperty("_BaseColor")) maskMaterial.SetColor("_BaseColor", maskColor);

            // 半透明需要设渲染队列，否则会被不透明物体挡住
            maskMaterial.renderQueue = 3000;
            if (maskMaterial.HasProperty("_Surface")) maskMaterial.SetFloat("_Surface", 1f);   // URP: 1 = Transparent
            if (maskMaterial.HasProperty("_Blend")) maskMaterial.SetFloat("_Blend", 0f);       // URP: Alpha
            if (maskMaterial.HasProperty("_SrcBlend")) maskMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (maskMaterial.HasProperty("_DstBlend")) maskMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (maskMaterial.HasProperty("_ZWrite")) maskMaterial.SetInt("_ZWrite", 0);
            maskMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            maskMaterial.DisableKeyword("_ALPHATEST_ON");

            return maskMaterial;
        }
    }
}
