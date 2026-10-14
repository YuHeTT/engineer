// ---------------------------------------------------------------------------
//  RecommendedPlan.cs —— 系统给出的推荐水肥方案
// ---------------------------------------------------------------------------
//  作用：承载"本阶段建议怎么浇水施肥"，即文档 TABLE 13 的一行。
//
//  【与 FertilizerApplication 的区别】
//      RecommendedPlan 是"系统建议"，含复合肥/基准灌溉/文案；
//      FertilizerApplication 是"实际投入"，只有五项数值。
//      两者用 ToApplication() 转换 —— 玩家点"采用建议"时就用它。
//
//  【复合肥折算】
//      文档 TABLE 13 的 S3 有一行是"复合肥 14"，
//      但演算公式只认氮/磷/钾/有机肥四项，所以必须折算。
//      已确认比例：氮:磷:钾 = 1:1:1。
//      折算发生在 ToApplication()，通过 SimConfig.CompoundRatio 配置，
//      后续策划改比例不用动代码。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>肥料用量（有效成分）。</summary>
    public struct FertilizerAmounts
    {
        /// <summary>有机肥，kg/亩</summary>
        public float Organic;

        /// <summary>氮，kg/亩</summary>
        public float N;

        /// <summary>磷，kg/亩</summary>
        public float P;

        /// <summary>钾，kg/亩</summary>
        public float K;

        /// <summary>
        /// 复合肥，kg/亩。这是个"包装量"，不是有效成分。
        /// 折算见 RecommendedPlan.ToApplication。
        /// </summary>
        public float Compound;

        public static FertilizerAmounts None
            => new FertilizerAmounts();

        public bool IsEmpty
            => Organic == 0f && N == 0f && P == 0f && K == 0f && Compound == 0f;
    }

    /// <summary>文档 TABLE 13 的一行：某个阶段在某种土壤状况下的推荐方案。</summary>
    public sealed class RecommendedPlan
    {
        /// <summary>所属阶段 S1..S4。</summary>
        public Stage Stage;

        /// <summary>判定优先级，越小越先判定。见 PlanTable 的说明。</summary>
        public int Priority;

        /// <summary>推荐肥料类型的文案，如"有机肥 + 少量氮磷肥"。UI 直接显示。</summary>
        public string Label;

        /// <summary>玉米生理说明，如"苗期需水低；有机质决定出苗"。UI 直接显示。</summary>
        public string Physiology;

        /// <summary>肥料用量（有效成分 + 复合肥包装量）。</summary>
        public FertilizerAmounts Amounts;

        /// <summary>单亩基准灌溉，m³/亩。演算里的"有效水比例"以它为分母。</summary>
        public float BaseIrrigation;

        /// <summary>
        /// 转成可直接送进演算的"实际投入"。
        /// 复合肥在这里按 CompoundRatio 折算成 N/P/K。
        /// </summary>
        /// <param name="config">提供 CompoundRatio</param>
        public FertilizerApplication ToApplication(SimConfig config)
        {
            float ratio = config != null ? config.CompoundRatio : (1f / 3f);
            float each = Amounts.Compound * ratio;

            return new FertilizerApplication(
                BaseIrrigation,
                Amounts.Organic,
                Amounts.N + each,
                Amounts.P + each,
                Amounts.K + each);
        }

        /// <summary>只取肥料部分（不含灌溉），用于 UI 分别显示。</summary>
        public FertilizerAmounts ResolvedAmounts(SimConfig config)
        {
            float ratio = config != null ? config.CompoundRatio : (1f / 3f);
            float each = Amounts.Compound * ratio;

            return new FertilizerAmounts
            {
                Organic = Amounts.Organic,
                N = Amounts.N + each,
                P = Amounts.P + each,
                K = Amounts.K + each,
                Compound = Amounts.Compound,
            };
        }

        public override string ToString()
            => $"[{Stages.ShortName(Stage)} P{Priority}] {Label} | " +
               $"水{BaseIrrigation} 有机{Amounts.Organic} N{Amounts.N} P{Amounts.P} K{Amounts.K}" +
               (Amounts.Compound > 0f ? $" 复合肥{Amounts.Compound}" : "");
    }
}
