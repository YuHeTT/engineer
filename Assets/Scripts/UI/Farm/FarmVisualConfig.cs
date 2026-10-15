// ---------------------------------------------------------------------------
//  FarmVisualConfig.cs —— 农场可视化的统一配置
// ---------------------------------------------------------------------------
//  作用：把"田块怎么摆、玉米多密"这类可视化参数集中到一处，
//        供 <see cref="FarmFieldBuilder"/> 与 <see cref="FarmGrowthBinder"/> 共用。
//
//  【单位约定】全工程 1 Unity 单位 = 1 米（模型也按 1 Blender 单位 = 1 m 制作）。
//
//  【长度来源（实测 glb 包围盒，不是猜的）】
//    土壤块 `玉米田土壤_四垄.glb`：X=4.000  Y=0.475  Z=3.000，最低点在 y=0
//      —— 也就是一块 "4 m × 3 m" 的地，垄面高度 0.475 m
//    所以：
//      SoilPieceSize   = (4, 3)
//      SoilRidgeTopY   = 0.475
//
//  【"1 块 = 1 亩"怎么落地】
//    策划定的规则是「玩家选的亩数 = 块数，1 块 = 1 亩，实际尺寸与这无关」。
//    所以"块"是**玩法单位**，不是物理尺寸。本类把它实现为：
//      每个"块" = 一个 3×3 的土壤块组（物理上 12 m × 9 m），
//                 中心留出 CornPatchSize 见方的一块作为玉米种植区。
//    这样：
//      · 玩法上按块算面积（块数 = 亩数）
//      · 视觉上每个块都是饱满的一小片田，而不是"一整块只种 4 株玉米"
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>农场可视化的调参集合。</summary>
    [System.Serializable]
    public sealed class FarmVisualConfig
    {
        // ==================================================================
        //  田块布局（"块" = 玩法上的 1 亩）
        // ==================================================================

        [Header("田块布局")]
        [Tooltip("玩家选的亩数（= 块数）。UI 那边一改这个，田块行列就会自动重排。\n" +
                 "调用 ApplyBlockCount() 生效，可选值见 SelectableBlockCounts。")]
        public int BlockCount = 4;

        [Tooltip("田块列数（X 方向）。一般不用手改 —— 由 BlockCount 自动算。")]
        public int BlockColumns = 2;

        [Tooltip("田块行数（Z 方向）。一般不用手改 —— 由 BlockCount 自动算。")]
        public int BlockRows = 2;

        [Tooltip("田块之间的间隔（米）。这个空隙会被铺成田埂/小路。")]
        public float BlockGap = 1.8f;

        [Tooltip("每个田块用几乘几块土壤拼。3 表示 3×3 = 9 块土壤。")]
        public int SoilPiecesPerSide = 3;

        [Tooltip("田块外围留出的田埂宽度（米）。种植区 = 田块尺寸 − 2 × 这个值。")]
        public float FieldMargin = 0.75f;

        [Tooltip("田块之间的小路宽度（米）。(田块间隔 − 这个值) / 2 = 两侧田埂。")]
        public float PathWidth = 0.6f;

        [Tooltip("是否在最外圈再铺一条环田小路。")]
        public bool RingPath = true;

        [Tooltip("环田小路的宽度（米）。")]
        public float RingPathWidth = 0.8f;

        // ==================================================================
        //  玉米种植
        // ==================================================================

        [Header("玉米种植")]
        [Tooltip("每块田里用来种玉米的矩形（米）。田块本身是 12 × 9 m。\n" +
                 "默认 10.8 × 7.8：正好放下 12 条垄（垄顶 x = ±0.5 ~ ±5.5），\n" +
                 "四周留 0.6 m 田埂，和田间小路同宽。")]
        public Vector2 CornPatchSize = new Vector2(10.8f, 7.8f);

        [Tooltip("株距基准（米）。这个值**决定株数**：\n" +
                 "  株数 = 四舍五入(种植区深度 / 株距)，再乘 PlantDensityScale。\n" +
                 "1.7 目前约 200 株/全田；调到 3.4 就约 100 株。")]
        public float CornSpacing = 1.7f;

        [Tooltip("玉米数量倍数（1 = 按株距算出的数量）。\n" +
                 "★ 想整体减半就直接设 0.5，不用去算株距。\n" +
                 "这是给「太卡了 / 太密了」用的快速旋钮。")]
        [Range(0.1f, 2f)] public float PlantDensityScale = 1f;

        [Tooltip("玉米是否从种植区这一边一直铺到那一边（包括边缘）。\n" +
                 "开启：第一株和最后一株分别落在种植区的两条边界上，\n" +
                 "      实际间距 = 种植区深度 / (株数 − 1)，边缘不留空。\n" +
                 "关闭：按 CornSpacing 从中心往两边排，边缘会剩一段空白（旧行为）。")]
        public bool EdgeToEdgePlanting = true;

        [Tooltip("是否让玉米行**对齐到土壤的垄**上。\n" +
                 "土壤块实测：4 m 宽 4 条垄，垄距 1.0 m，垄是沿 Z 方向延伸的。\n" +
                 "开启后玉米行距 = 垄距，玉米正好站在垄背上（真实农田的样子）；\n" +
                 "关掉就退回「在整个种植区里均匀撒点」，会出现玉米站在沟里的情况。")]
        public bool AlignRowsToRidges = true;

        [Tooltip("垄距（米）。土壤块实测 = 1.0，改这个要同步改模型。")]
        public float RidgePitch = 1.0f;

        [Tooltip("是否给每株玉米加一点随机旋转和缩放，避免看起来像复制粘贴。")]
        public bool RandomizePlants = true;

        [Tooltip("随机旋转的最大角度（度）。")]
        public float PlantYawJitter = 18f;

        [Tooltip("随机缩放范围（±比例）。0.08 表示 0.92~1.08 倍。")]
        public float PlantScaleJitter = 0.08f;

        // ==================================================================
        //  高度对齐
        // ==================================================================

        [Header("高度对齐")]
        [Tooltip("土壤块的垄面高度（米）。模型实测 0.475，玉米就摆在这个高度上。")]
        public float SoilRidgeTopY = 0.475f;

        [Tooltip("自动把每株玉米的最低点贴到垄面上。\n" +
                 "开启后不用手调 CornBaseOffsetY —— 代码会量出植株包围盒再对齐。\n" +
                 "如果发现玉米整体偏高/偏低，先关掉这个开关再手动调。")]
        public bool AutoAlignCornToSoil = true;

        [Tooltip("自动对齐之外的**额外**垂直微调（米，正值上移）。\n" +
                 "自动对齐开着时，这个值通常保持 0。")]
        public float CornBaseOffsetY = 0f;

        // ==================================================================
        //  派生量（只读）
        // ==================================================================

        /// <summary>总块数（= 玩法上的亩数 = 玩家选的亩数）。</summary>
        public int TotalBlocks
        {
            get { return Mathf.Max(1, BlockColumns) * Mathf.Max(1, BlockRows); }
        }

        /// <summary>一个田块组的物理尺寸（米）。</summary>
        public Vector2 BlockGroupSize
        {
            get
            {
                return new Vector2(SoilPieceSize.x * Mathf.Max(1, SoilPiecesPerSide),
                                   SoilPieceSize.y * Mathf.Max(1, SoilPiecesPerSide));
            }
        }

        /// <summary>整片田的物理包围尺寸（米），**含最外圈田埂**。</summary>
        public Vector2 FieldSize
        {
            get
            {
                var g = BlockGroupSize;
                var m = FieldMargin * 2f;      // 两侧田埂
                return new Vector2(
                    g.x * Mathf.Max(1, BlockColumns) + BlockGap * (Mathf.Max(1, BlockColumns) - 1) + m,
                    g.y * Mathf.Max(1, BlockRows) + BlockGap * (Mathf.Max(1, BlockRows) - 1) + m);
            }
        }

        /// <summary>
        /// 一个田块里的玉米垄数。
        ///
        /// ★ 密度倍数会同时作用在【垄数】和【每垄株数】上，这样布局才是均匀缩放的。
        ///   之前只缩了每垄株数，导致行距（垄距）不变、株距却拉得很开 ——
        ///   看起来就是"行还在、株稀了"，分布极不均衡。
        /// </summary>
        public int CornRowsAcross
        {
            get
            {
                int count;
                float firstX, actualPitch;
                RidgeLines(out count, out firstX, out actualPitch);
                return count;
            }
        }

        /// <summary>
        /// 算出种植区里的垄数、第一条垄的 x、以及**实际行距**。
        ///
        /// 【两种模式】
        ///   · 密度倍数 = 1：按土壤垄顶格点排（x = 0.5 + n × 垄距），
        ///     行距 = 垄距，玉米正好站在垄背上。
        ///   · 密度倍数 ≠ 1：按「等比缩放」排 —— 垄数也乘倍数，并在整个种植区里
        ///     均匀铺开，行距 = 种植区宽度 / (垄数 − 1)。
        ///     这样行距和株距**一起变**，布局始终均匀。
        ///     （之前只缩每垄株数，行距不变、株距拉大，看起来就是"行还在、株稀了"）
        /// </summary>
        public void RidgeLines(out int count, out float firstX, out float actualPitch)
        {
            var pitch = Mathf.Max(0.2f, RidgePitch);
            var half = Mathf.Max(pitch, CornPatchSize.x) * 0.5f;
            var density = Mathf.Clamp(PlantDensityScale, 0.1f, 2f);

            // 基准垄数：土壤垄顶格点能放下几条
            var phase = pitch * 0.5f;
            var nMin = Mathf.CeilToInt((-half - phase) / pitch);
            var nMax = Mathf.FloorToInt((half - phase) / pitch);
            var baseCount = Mathf.Max(1, nMax - nMin + 1);

            if (Mathf.Approximately(density, 1f))
            {
                // 密度 1：贴垄
                count = baseCount;
                actualPitch = pitch;
                var first = phase + nMin * pitch;
                var span = (count - 1) * pitch;
                firstX = first - (first + first + span) * 0.5f;
                return;
            }

            // 密度 ≠ 1：垄数等比缩放，并在种植区内均匀铺开
            count = Mathf.Max(1, Mathf.RoundToInt(baseCount * density));
            if (count == 1)
            {
                firstX = 0f;
                actualPitch = 0f;
                return;
            }

            var usable = Mathf.Max(pitch, CornPatchSize.x);
            actualPitch = usable / (count - 1);
            firstX = -usable * 0.5f;
        }

        /// <summary>
        /// 一垄上种几株（沿垄方向）。
        /// 这里决定的是【株数】，最终间距由 <see cref="EdgeToEdgePlanting"/> 摊开决定。
        /// 用四舍五入而不是向下取整，这样调 CornSpacing 时株数是连续变化的，
        /// 不会出现"调了半天株数没变"。
        /// </summary>
        public int CornPlantsPerRow
        {
            get
            {
                var spacing = Mathf.Max(0.1f, CornSpacing);
                var depth = Mathf.Max(spacing, CornPatchSize.y);

                // ★ 基准株数：**不加**密度倍数
                var baseCount = Mathf.Max(1, Mathf.RoundToInt(depth / spacing));

                // 再乘密度倍数 —— 和垄数用同一个倍数，
                // 这样行距与株距等比变化，布局始终均匀（不会出现"行还在、株稀了"）。
                var density = Mathf.Clamp(PlantDensityScale, 0.1f, 2f);
                var n = Mathf.RoundToInt(baseCount * density);
                return Mathf.Max(1, n);
            }
        }

        /// <summary>整片田的玉米总株数。</summary>
        public int TotalPlants
        {
            get { return CornRowsAcross * CornPlantsPerRow * TotalBlocks; }
        }

        /// <summary>
        /// 算出一个田块内所有玉米的位置（田块本地坐标，y = 垄面高度）。
        ///
        /// 【按垄对齐】土壤块实测是"4 m 宽 4 条垄、垄距 1.0 m、垄沿 Z 延伸"，
        /// 所以让玉米行正好落在垄顶（x = ±0.5, ±1.5 ...）上，
        /// 看起来才是"种在垄背上"的真实农田，而不是浮在土面上。
        /// </summary>
        public List<Vector3> ComputePlantPositions()
        {
            var result = new List<Vector3>();
            var y = SoilRidgeTopY;
            var patch = CornPatchSize;
            var spacing = Mathf.Max(0.1f, CornSpacing);
            var block = BlockGroupSize;

            if (AlignRowsToRidges)
            {
                int rows;
                float firstX, rowPitch;
                RidgeLines(out rows, out firstX, out rowPitch);

                var perRow = CornPlantsPerRow;
                float actualSpacing;
                if (EdgeToEdgePlanting && perRow > 1)
                {
                    // 【关键】间距由"倍数"直接推算，而不是"把株数铺满种植区"。
                    //
                    // 为什么不能铺满：株数被四舍五入到整数，株数越小时取整误差越大。
                    // 例：基准 5 株、倍数 0.5 → 2 株；铺满的话间距 = 7.8/(2-1) = 7.8 m，
                    // 而行距只有 2.7 m —— 两个方向差 3 倍，看起来就是"行还在、株稀了"。
                    // 改成"株距 = 基准株距 / 倍数"后，两个方向等比缩放，布局始终均匀。
                    var density = Mathf.Clamp(PlantDensityScale, 0.1f, 2f);

                    // 基准株距：密度 = 1 时首尾贴边算出的间距（和实际生成保持一致）
                    var basePerRow = Mathf.Max(2, Mathf.RoundToInt(patch.y / spacing));
                    var baseSpacing = patch.y / (basePerRow - 1f);

                    actualSpacing = Mathf.Max(0.15f, baseSpacing / density);
                }
                else
                {
                    actualSpacing = spacing;
                }

                var spanZ = (perRow - 1) * actualSpacing;
                var z0 = -spanZ * 0.5f;

                for (var r = 0; r < rows; r++)
                {
                    // 用 rowPitch（由 RidgeLines 给出）而不是固定 RidgePitch，
                    // 这样密度倍数变化时行距也跟着等比变 —— 布局才均匀。
                    var x = rows <= 1 ? firstX : firstX + rowPitch * r;
                    for (var i = 0; i < perRow; i++)
                        result.Add(new Vector3(x, y, z0 + actualSpacing * i));
                }
                return result;
            }

            // 兜底：在种植区里均匀撒点
            var spacingSafe = spacing;
            var nx = Mathf.Max(1, Mathf.FloorToInt(patch.x / spacingSafe));
            var nz = Mathf.Max(1, Mathf.FloorToInt(patch.y / spacingSafe));
            var startX = -(nx - 1) * spacingSafe * 0.5f;
            var startZ = -(nz - 1) * spacingSafe * 0.5f;

            for (var ix = 0; ix < nx; ix++)
                for (var iz = 0; iz < nz; iz++)
                    result.Add(new Vector3(startX + spacingSafe * ix, y, startZ + spacingSafe * iz));

            return result;
        }

        // ==================================================================
        //  土壤块尺寸：实测常量，不要随便改
        // ==================================================================

        /// <summary>单块土壤的物理尺寸（X=4 m, Y=3 m）。来自 glb 实测包围盒。</summary>
        public static readonly Vector2 SoilPieceSize = new Vector2(4f, 3f);

        /// <summary>土壤块的厚度（米）。用于对齐，不影响布局。</summary>
        public const float SoilPieceThickness = 0.475f;

        // ==================================================================
        //  合法性
        // ==================================================================

        /// <summary>
        /// 给 UI 用的"可选亩数"列表。
        /// 策划要求"给一个选择范围"，这里给出 1×1 到 4×4 的方块数（1/4/9/16 块）。
        /// </summary>
        public static readonly int[] SelectableBlockCounts = { 1, 2, 4, 6, 9, 12, 16 };

        /// <summary>
        /// 把块数拆成行/列。16:10 是常见的横屏宽高比，
        /// 所以列比行多一点（3×2、4×3）在屏幕上更饱满，也接近一块真田的形状。
        /// </summary>
        public static void SplitIntoGrid(int blocks, out int columns, out int rows)
        {
            blocks = Mathf.Max(1, blocks);
            const float targetAspect = 16f / 10f;

            var bestC = 1;
            var bestR = blocks;
            var bestScore = float.MaxValue;

            for (var c = 1; c <= blocks; c++)
            {
                if (blocks % c != 0) continue;
                var r = blocks / c;

                // 主目标：接近横屏比例；次目标：行列不要差太远（避免 16×1 那种长条）
                var aspectScore = Mathf.Abs((float)c / r - targetAspect);
                var balanceScore = Mathf.Abs((float)c / r - 1f) * 0.25f;
                var score = aspectScore + balanceScore;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestC = c;
                    bestR = r;
                }
            }

            columns = bestC;
            rows = bestR;
        }

        /// <summary>
        /// 按"玩家选的块数（= 亩数）"设置田块行列。
        /// 这是"多亩数布局"的入口：UI 那边玩家选了几亩，调这个就行。
        /// 改完要重新 <c>Build()</c> 才会生效（构建器里已经这么做了）。
        /// </summary>
        public void ApplyBlockCount(int blocks)
        {
            int c, r;
            SplitIntoGrid(blocks, out c, out r);
            BlockColumns = Mathf.Clamp(c, 1, 8);
            BlockRows = Mathf.Clamp(r, 1, 8);
        }

        /// <summary>把配置裁剪到合法范围。由构建器在开始生成前调用。</summary>
        public void Sanitize()
        {
            BlockColumns = Mathf.Clamp(BlockColumns, 1, 8);
            BlockRows = Mathf.Clamp(BlockRows, 1, 8);
            SoilPiecesPerSide = Mathf.Clamp(SoilPiecesPerSide, 1, 6);
            BlockGap = Mathf.Clamp(BlockGap, 0f, 20f);
            CornSpacing = Mathf.Clamp(CornSpacing, 0.2f, 5f);
            RidgePitch = Mathf.Clamp(RidgePitch, 0.2f, 5f);
            PlantDensityScale = Mathf.Clamp(PlantDensityScale, 0.1f, 2f);
            FieldMargin = Mathf.Clamp(FieldMargin, 0f, 5f);
            PathWidth = Mathf.Clamp(PathWidth, 0f, 10f);
            RingPathWidth = Mathf.Clamp(RingPathWidth, 0f, 10f);

            // 种植区不能超出田块的物理尺寸，否则玉米会长到隔壁田块上
            var block = BlockGroupSize;
            CornPatchSize = new Vector2(
                Mathf.Clamp(CornPatchSize.x, 1f, block.x),
                Mathf.Clamp(CornPatchSize.y, 1f, block.y));

            PlantYawJitter = Mathf.Clamp(PlantYawJitter, 0f, 180f);
            PlantScaleJitter = Mathf.Clamp(PlantScaleJitter, 0f, 0.5f);
            SoilRidgeTopY = Mathf.Clamp(SoilRidgeTopY, -1f, 5f);
            CornBaseOffsetY = Mathf.Clamp(CornBaseOffsetY, -1f, 1f);
        }

        public override string ToString()
        {
            return string.Format(
                "田块 {0}×{1} = {2} 块（亩） | 每块 {3}×{3} 块土壤 ({4:0.#}×{5:0.#} m) | " +
                "玉米 {6} 垄 × {7} 株 = {8} 株/块，全田 {9} 株 | 垄距 {10:0.##} 株距 {11:0.##} m | " +
                "种植区 {12:0.#}×{13:0.#} m | 整片 {14:0.#}×{15:0.#} m（含田埂 {16:0.##}、小路 {17:0.##}） | 对齐={18}",
                BlockColumns, BlockRows, TotalBlocks, SoilPiecesPerSide,
                BlockGroupSize.x, BlockGroupSize.y,
                CornRowsAcross, CornPlantsPerRow, CornRowsAcross * CornPlantsPerRow, TotalPlants,
                RidgePitch, CornSpacing,
                CornPatchSize.x, CornPatchSize.y,
                FieldSize.x, FieldSize.y, FieldMargin, PathWidth,
                AutoAlignCornToSoil ? "自动贴垄面" : "手动偏移 " + CornBaseOffsetY.ToString("0.###"));
        }
    }
}
