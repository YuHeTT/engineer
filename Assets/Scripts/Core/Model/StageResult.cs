// ---------------------------------------------------------------------------
//  StageResult.cs —— 单阶段的完整结果快照
// ---------------------------------------------------------------------------
//  作用：一次阶段结算的全部信息，供 UI 显示与调试导出。
//
//  【为什么需要这个类】
//    UI 在"开始分析"界面要同时显示：本阶段土壤、五个适宜度、
//    系统建议、玩家实际填的值、算完之后下一阶段变成什么样。
//    如果结算方法只返回一个 SoilState，UI 就得自己重新算一遍，容易不一致。
//    所以让 Commit() 一次性返回全部结果，UI 只负责显示。
//
//  数据流向：
//      SoilBefore ──适宜度──> Fit ──┬──> Recommended（建议）
//                                   ├──> Growth（长势，决定贴图）
//                                   └──> StageFertility（进入产量平均）
//      Applied（玩家填的） + Fit + Recommended ──演算──> SoilAfter
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>一个阶段从进入到结算的全部数据。</summary>
    public sealed class StageResult
    {
        /// <summary>本阶段编号。</summary>
        public Stage Stage;

        /// <summary>本阶段开始时的土壤（玩家看到的"当前数据"）。</summary>
        public SoilState SoilBefore;

        /// <summary>演算后的土壤，即下一阶段的起始土壤。S4 之后是最终状态。</summary>
        public SoilState SoilAfter;

        /// <summary>本阶段五个适宜度。</summary>
        public SuitabilityResult Fit;

        /// <summary>阶段肥力系数 Ffert,stage，会被计入四阶段平均。</summary>
        public float StageFertility;

        /// <summary>玩家实际提交的水肥投入。</summary>
        public FertilizerApplication Applied;

        /// <summary>系统给出的推荐方案（用于和玩家实际填写对比）。</summary>
        public RecommendedPlan Recommended;

        /// <summary>本阶段长势等级，决定玉米贴图。</summary>
        public GrowthLevel Growth;

        /// <summary>
        /// 玩家实际投入与建议的偏差，用于 UI 提示与调试。
        /// 正数=超过建议，负数=不足。null 表示无建议（理论上不该发生）。
        /// </summary>
        public FertilizerApplication DeviationFromRecommendation(SimConfig config)
        {
            if (Recommended == null) return FertilizerApplication.None;
            var rec = Recommended.ToApplication(config);
            return new FertilizerApplication(
                Applied.Irrigation - rec.Irrigation,
                Applied.Organic    - rec.Organic,
                Applied.N          - rec.N,
                Applied.P          - rec.P,
                Applied.K          - rec.K);
        }

        /// <summary>有效水比例。实际灌溉 ÷ 建议灌溉，封顶 1。</summary>
        public float EffectiveWaterRatio()
        {
            if (Recommended == null || Recommended.BaseIrrigation <= 0f) return 1f;
            return MathX.Min(Applied.Irrigation / Recommended.BaseIrrigation, 1f);
        }

        /// <summary>超灌比例。超出建议的部分 ÷ 建议，未超出为 0。</summary>
        public float OverWaterRatio()
        {
            if (Recommended == null || Recommended.BaseIrrigation <= 0f) return 0f;
            return MathX.Max(Applied.Irrigation - Recommended.BaseIrrigation, 0f)
                 / Recommended.BaseIrrigation;
        }

        public override string ToString()
            => $"{Stages.ShortName(Stage)} | {Fit} | 长势={GrowthLevels.DisplayName(Growth)} | " +
               $"投入({Applied}) | 建议({Recommended})";
    }
}
