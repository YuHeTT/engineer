// ---------------------------------------------------------------------------
//  PlanRule.cs —— 推荐方案表的一行（条件 + 结果）
// ---------------------------------------------------------------------------
//  作用：把文档 TABLE 13 的一行绑成"条件 -> 方案"。
//        SimConfig.PlanRules 是这些行的集合，PlanTable 负责按优先级匹配。
//
//  【为什么必须带 Priority】
//    文档的表不是互斥的条件表，而是有先后顺序的分支：
//      S1 行 1："任意项＜0.6"
//      S1 行 2："全部≥0.6 且≥3 项≥0.8"
//    两者在"全部≥0.8"时可能同时成立，必须靠优先级决定用哪个。
//    判定顺序见 PlanTable。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>推荐方案表的一行。</summary>
    public sealed class PlanRule
    {
        /// <summary>所属阶段。</summary>
        public Stage Stage;

        /// <summary>判定优先级，越小越先判定。</summary>
        public int Priority;

        /// <summary>判定条件。</summary>
        public PlanCondition Condition = new PlanCondition();

        /// <summary>推荐肥料类型的文案。</summary>
        public string Label;

        /// <summary>玉米生理说明文案。</summary>
        public string Physiology;

        /// <summary>肥料用量。</summary>
        public FertilizerAmounts Amounts;

        /// <summary>单亩基准灌溉 m³/亩。</summary>
        public float BaseIrrigation;

        /// <summary>
        /// 是否可作兜底规则。
        /// 当一条精确规则都没匹配上时，会优先用"缺什么补什么"的兜底规则，
        /// 而不是简单取优先级最高的那条。见 PlanTable.Match 的说明。
        ///
        /// 对应关系：文档里"某指标严重不足"的那条规则，就是该指标的兜底。
        ///   S1 -> P1（任意项 &lt; 0.6，施有机肥 80）
        ///   S2 -> P1（fN &lt; 0.6，施氮 12 磷 5）
        ///   S3 -> P1（N/P/K 任一 &lt; 0.6，氮磷钾协同）
        ///   S4 -> P1（fK &lt; 0.6，施钾 9）
        /// </summary>
        public bool IsFallback;

        /// <summary>是否满足条件。</summary>
        public bool Matches(SuitabilityResult fit)
            => Condition != null && Condition.Evaluate(fit);

        /// <summary>转成结果对象。</summary>
        public RecommendedPlan ToPlan()
        {
            return new RecommendedPlan
            {
                Stage = Stage,
                Priority = Priority,
                Label = Label,
                Physiology = Physiology,
                Amounts = Amounts,
                BaseIrrigation = BaseIrrigation,
            };
        }

        public override string ToString()
            => $"[{Stages.ShortName(Stage)} P{Priority}] {(Condition != null ? Condition.Describe() : "?")} " +
               $"-> {Label}";
    }
}
