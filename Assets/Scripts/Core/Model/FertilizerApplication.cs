// ---------------------------------------------------------------------------
//  FertilizerApplication.cs —— 玩家本阶段实际投入的水肥
// ---------------------------------------------------------------------------
//  作用：承载"玩家在水肥输入界面填了什么"。
//
//  【对应文档的输入面板】
//      灌溉量（m³/亩）：       -> Irrigation
//      有机肥(kg/亩)：         -> Organic
//      钾肥(kg/亩)：           -> K
//      氮肥(kg/亩)：           -> N
//      磷肥(kg/亩)：           -> P
//
//  注意：文档的推荐方案表里还有一条"复合肥 14"，但输入面板上并没有这一项。
//    所以这里【不】包含 Compound 字段 —— 折算发生在推荐方案侧
//    （见 RecommendedPlan / FertilizerAmounts.ToApplication）。
//    这样"玩家输入"和"系统建议"两种来源统一成同一个结构，演算只认这一个。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>玩家本阶段的灌溉与施肥量。</summary>
    public struct FertilizerApplication
    {
        /// <summary>灌溉量，m³/亩</summary>
        public float Irrigation;

        /// <summary>有机肥，kg/亩</summary>
        public float Organic;

        /// <summary>氮肥（有效成分），kg/亩</summary>
        public float N;

        /// <summary>磷肥（有效成分），kg/亩</summary>
        public float P;

        /// <summary>钾肥（有效成分），kg/亩</summary>
        public float K;

        public FertilizerApplication(float irrigation, float organic,
                                     float n, float p, float k)
        {
            Irrigation = irrigation;
            Organic = organic;
            N = n; P = p; K = k;
        }

        /// <summary>全零（不投入）。</summary>
        public static FertilizerApplication None
            => new FertilizerApplication(0f, 0f, 0f, 0f, 0f);

        /// <summary>把另一份投入叠加进来（用于把复合肥折算结果并进 N/P/K）。</summary>
        public void Add(float organic, float n, float p, float k)
        {
            Organic += organic;
            N += n; P += p; K += k;
        }

        public override string ToString()
            => $"水={Irrigation:F2} 有机={Organic:F2} 氮={N:F2} 磷={P:F2} 钾={K:F2}";
    }
}
