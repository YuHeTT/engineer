// ---------------------------------------------------------------------------
//  SimConfigParts.cs —— 配置模型的各个组成部分
// ---------------------------------------------------------------------------
//  作用：把"数值设定"拆成几个可独立编辑的小块，供 SimConfig 组合。
//        每个类对应一张配置表（CSV），策划可以单独改其中一张。
//
//  文件清单（对应 Assets/Config/Source/*.csv）：
//      StaticMetricRange  <- metrics.csv        静态四项的随机范围
//      StageConsume       <- stage_consume.csv  各阶段养分消耗量
//      AdvanceCoef        <- advance_coef.csv   指标演算系数
//      TerrainDef         <- terrains.csv       四套地形
//      CropDef            <- crops.csv          四种作物
//      YieldConfig        <- metrics.csv        产量模型参数
//      InputLimits        <- input_limits.csv   玩家输入的水肥边界
//
//  【注意】本程序集不引用 UnityEngine，所以这些类不能用 [SerializeField]。
//    它们是纯数据类，由 Config 层（SimConfigAsset）负责包成 ScriptableObject。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>静态指标的随机取值范围。第一阶段随机生成一次，之后不变。</summary>
    public sealed class StaticMetricRange
    {
        /// <summary>对应哪一项静态指标。</summary>
        public SoilMetric Metric;

        /// <summary>随机下限。</summary>
        public float Min;

        /// <summary>随机上限。</summary>
        public float Max;

        /// <summary>在该范围内取一个随机值。rng 由调用方提供，保证可复现。</summary>
        public float Sample(System.Random rng)
        {
            if (rng == null) return Min;
            return Min + (float)rng.NextDouble() * (Max - Min);
        }

        public override string ToString() => $"{Metric}: [{Min}, {Max}]";
    }

    /// <summary>某一阶段作物消耗掉的养分量。文档 TABLE 18。</summary>
    public sealed class StageConsume
    {
        /// <summary>所属阶段。</summary>
        public Stage Stage;

        /// <summary>本阶段氮消耗。</summary>
        public float N;

        /// <summary>本阶段磷消耗。</summary>
        public float P;

        /// <summary>本阶段钾消耗。</summary>
        public float K;

        public override string ToString()
            => $"{Stages.ShortName(Stage)}: N-{N} P-{P} K-{K}";
    }

    /// <summary>
    /// 指标演算系数。全部来自文档"补充：每一阶段的五个动态指标被上一阶段水肥影响的计算逻辑"。
    ///
    /// 【注意 pH 的两个系数存在已知问题 —— 详见字段注释，v1 按文档原值实现】
    /// </summary>
    public sealed class AdvanceCoef
    {
        // ---------------- pH ----------------
        //  公式（文档原文）：
        //    新pH = 当前pH + (6.25 - 当前pH) × 有机肥量/80 × 0.1 × 有效水比例
        //                    - 0.15 × 氮肥量/12 × 有效水比例

        /// <summary>有机肥把 pH 推向的目标值。文档：6.25</summary>
        public float pH_Target = 6.25f;

        /// <summary>有机肥参考量（分母）。文档：80 kg/亩</summary>
        public float pH_OmRef = 80f;

        /// <summary>
        /// 有机肥项系数。文档：0.1
        /// 【已知问题】按建议量 80kg/亩 代入，此项仅产生 +0.100 的推力；
        /// 而氮肥项产生 -0.150。两者配比相反，导致照抄推荐方案 pH 会净下降。
        /// 实测：起点 6.90，四阶段后 6.72。策划已确认 v1 先保留原值。
        /// 后续优化：建议调到 0.05 左右，详见 Docs/数值模拟验证记录.md
        /// </summary>
        public float pH_OmCoef = 0.1f;

        /// <summary>氮肥参考量（分母）。文档：12 kg/亩</summary>
        public float pH_NRef = 12f;

        /// <summary>氮肥酸化系数。文档：0.15。见 pH_OmCoef 的说明。</summary>
        public float pH_NCoef = 0.15f;

        // ---------------- 腐殖丰度 ----------------
        //  新OM = 当前值 + 7.5 × 有机肥量/80 × 有效水比例 - 1.5 - 1.5 × 超灌比例

        /// <summary>有机肥增肥参考量（分母）。文档：80</summary>
        public float OM_GainRef = 80f;

        /// <summary>有机肥增量系数。文档：7.5</summary>
        public float OM_Gain = 7.5f;

        /// <summary>每阶段基础衰减。文档：1.5</summary>
        public float OM_Decay = 1.5f;

        /// <summary>超灌惩罚系数。文档：1.5</summary>
        public float OM_OverPenalty = 1.5f;

        // ---------------- 速效氮 ----------------
        //  新N = 当前值 + 20 × 氮肥量/12 × 有效水比例 - 本阶段氮消耗 - 4 × 超灌比例

        /// <summary>氮肥参考量（分母）。文档：12</summary>
        public float N_GainRef = 12f;

        /// <summary>氮肥增量系数。文档：20</summary>
        public float N_Gain = 20f;

        /// <summary>超灌惩罚系数。文档：4</summary>
        public float N_OverPenalty = 4f;

        // ---------------- 磷活度 ----------------
        //  新P = 当前值 + 7.5 × 磷肥量/5 × 有效水比例 - 本阶段磷消耗 - 1.5 × 超灌比例

        /// <summary>磷肥参考量（分母）。文档：5</summary>
        public float P_GainRef = 5f;

        /// <summary>磷肥增量系数。文档：7.5</summary>
        public float P_Gain = 7.5f;

        /// <summary>超灌惩罚系数。文档：1.5</summary>
        public float P_OverPenalty = 1.5f;

        // ---------------- 钾储量 ----------------
        //  新K = 当前值 + 50 × 钾肥量/10 × 有效水比例 - 本阶段钾消耗 - 10 × 超灌比例

        /// <summary>钾肥参考量（分母）。文档：10</summary>
        public float K_GainRef = 10f;

        /// <summary>钾肥增量系数。文档：50</summary>
        public float K_Gain = 50f;

        /// <summary>超灌惩罚系数。文档：10</summary>
        public float K_OverPenalty = 10f;
    }

    /// <summary>一套地形的定义。文档 TABLE 0。v1 只有平川台可玩。</summary>
    public sealed class TerrainDef
    {
        /// <summary>地形 ID，配置与存档用。如 "flat_plain"。</summary>
        public string Id;

        /// <summary>显示名。如 "平川台"。</summary>
        public string DisplayName;

        /// <summary>地形介绍文案（卡片上显示）。</summary>
        public string Description;

        /// <summary>是否在 v1 中可选。策划备注："目前只能选择一个平川台"。</summary>
        public bool PlayableInV1;

        /// <summary>地形模型资源名/路径，供 Gameplay 层加载。</summary>
        public string ModelAssetName;
    }

    /// <summary>
    /// 一种作物的定义。文档 TABLE 1。
    ///
    /// 【重要】文档 TABLE 1 只有文字介绍，没有任何数值。
    ///   要支持甜菜/红薯/土豆，每种作物都需要一整套：
    ///     - 五条适宜度曲线（各指标最优区间不同）
    ///     - 一张推荐水肥方案表
    ///   v1 只做玉米，所以这里先只挂"用哪套曲线和方案表"的引用。
    /// </summary>
    public sealed class CropDef
    {
        /// <summary>作物 ID。如 "corn"。</summary>
        public string Id;

        /// <summary>显示名。如 "玉米"。</summary>
        public string DisplayName;

        /// <summary>作物介绍文案。</summary>
        public string Description;

        /// <summary>是否在 v1 中可选。策划备注："现在只能选择玉米"。</summary>
        public bool PlayableInV1;

        /// <summary>作物模型资源名/路径。</summary>
        public string ModelAssetName;

        /// <summary>
        /// 该作物使用的适宜度曲线集合（下标对齐 SoilMetric 前五项）。
        /// v1 只有玉米一套，所以指向 SimConfig.Curves。
        /// 将来多作物时，每种作物挂自己的一套。
        /// </summary>
        public SuitabilityCurve[] Curves;
    }

    /// <summary>
    /// 玩家输入的水肥边界。用来挡住荒谬输入（负数施肥、1e9 灌溉量）。
    ///
    /// 【为什么需要这个 —— 属工程健壮性，不改设计】
    ///   文档没有规定水肥量的上下限。但没有上限时，玩家输入超大值会让
    ///   土壤指标爆表、产量算出无意义的结果；输入负值更荒谬 ——
    ///   负施肥会"改良"土壤。这类输入不该进入数值链路。
    ///
    /// 【默认值的取法】
    ///   下限一律 0（不能负投入）。
    ///   上限默认取"建议量的若干倍"而不是某个硬编码数字 ——
    ///   因为建议量本身就是该阶段合理的量级，倍数比绝对值更稳定，
    ///   也不会和策划后续调整建议量脱节。
    ///
    /// 【注意】这里只做"防护"，不做"设计"。
    ///   如果策划将来要规定"每亩最多只能施 X kg"这类玩法约束，
    ///   应该改 AbsoluteMax 而不是 FormulaMaxMultiple。
    /// </summary>
    public sealed class InputLimits
    {
        /// <summary>是否启用边界钳制。关掉后玩家输入原样进入演算（用于调试对比）。</summary>
        public bool Enabled = true;

        /// <summary>是否允许负数输入。默认不允许。</summary>
        public bool AllowNegative;

        /// <summary>灌溉量上限 = 该阶段建议灌溉量 × 此倍数。</summary>
        public float IrrigationMultiple = 5f;

        /// <summary>各项肥料上限 = 该阶段建议量 × 此倍数。</summary>
        public float FertilizerMultiple = 5f;

        /// <summary>建议量为 0 时的绝对上限（如某阶段不施钾肥）。这里给一个宽松的天花板。</summary>
        public float AbsoluteMaxIrrigation = 1000f;

        /// <summary>建议量为 0 时的肥料绝对上限。</summary>
        public float AbsoluteMaxFertilizer = 1000f;

        /// <summary>
        /// 把一次玩家输入钳制进合法范围。
        /// </summary>
        /// <param name="applied">玩家原始输入</param>
        /// <param name="recommended">本阶段建议（提供"建议量"作为上限基准；可为 null）</param>
        public FertilizerApplication Clamp(FertilizerApplication applied, RecommendedPlan recommended)
        {
            if (!Enabled) return applied;

            float recWater = recommended != null ? recommended.BaseIrrigation : 0f;
            var amounts = recommended != null ? recommended.Amounts : FertilizerAmounts.None;

            return new FertilizerApplication(
                ClampOne(applied.Irrigation, recWater,                   IrrigationMultiple, AbsoluteMaxIrrigation),
                ClampOne(applied.Organic,    amounts.Organic,            FertilizerMultiple, AbsoluteMaxFertilizer),
                ClampOne(applied.N,          amounts.N,                  FertilizerMultiple, AbsoluteMaxFertilizer),
                ClampOne(applied.P,          amounts.P,                  FertilizerMultiple, AbsoluteMaxFertilizer),
                ClampOne(applied.K,          amounts.K,                  FertilizerMultiple, AbsoluteMaxFertilizer));
        }

        /// <summary>单项钳制。基准量为 0 时退回绝对上限。</summary>
        private float ClampOne(float value, float reference, float multiple, float absoluteMax)
        {
            float min = AllowNegative ? -absoluteMax : 0f;
            float max = reference > 0f ? reference * multiple : absoluteMax;
            if (max > absoluteMax) max = absoluteMax;
            if (max < min) max = min;
            return MathX.Clamp(value, min, max);
        }

        public override string ToString()
            => $"灌溉≤建议×{IrrigationMultiple} 肥料≤建议×{FertilizerMultiple}（绝对上限 {AbsoluteMaxIrrigation}/{AbsoluteMaxFertilizer}）";
    }

    /// <summary>产量 / 收益模型参数。</summary>
    public sealed class YieldConfig
    {
        /// <summary>
        /// 单位面积理论最大产量 Ymax,geo。
        /// 【占位值 1000】策划暂定，后续确定后只改这里。
        /// </summary>
        public float YmaxGeo = 1000f;

        /// <summary>
        /// 水分系数是否用加权平均（文档写 Fwater=(α1·Fw,s1+…+α4·Fw,s4)/4）。
        /// v1：系统总给最优灌溉，Fwater 恒为 1，此开关不用。
        /// 保留字段是为了后续接"水分胁迫"时不用改函数签名。
        /// </summary>
        public bool UseWeightedWater;

        /// <summary>四个阶段的水分权重 α1..α4。文档未给具体值，先均分。</summary>
        public float[] Alpha = { 0.25f, 0.25f, 0.25f, 0.25f };

        /// <summary>静态因素系数 Sstatic。文档：恒为 1。</summary>
        public float SStatic = 1f;
    }
}
