// ---------------------------------------------------------------------------
//  SoilAdvance.cs —— 阶段演算（本阶段水肥 -> 下一阶段指标）
// ---------------------------------------------------------------------------
//  作用：数值内核里唯一的"演化"逻辑。它决定了玩家的操作能不能被看见。
//
//  【公式全部来自文档"补充"节】
//      有效水比例 = 实际灌溉量 ÷ 该阶段建议灌溉量；超过 1 时按 1 计算
//      超灌比例   = 实际灌溉量超出建议量的部分 ÷ 建议量；未超出时为 0
//
//      新pH = 当前pH + (6.25 - 当前pH) × 有机肥量/80 × 0.1 × 有效水比例
//                     - 0.15 × 氮肥量/12 × 有效水比例
//      新OM = 当前值 + 7.5 × 有机肥量/80 × 有效水比例 - 1.5 - 1.5 × 超灌比例
//      新N  = 当前值 + 20 × 氮肥量/12 × 有效水比例 - 本阶段氮消耗 - 4 × 超灌比例
//      新P  = 当前值 + 7.5 × 磷肥量/5 × 有效水比例 - 本阶段磷消耗 - 1.5 × 超灌比例
//      新K  = 当前值 + 50 × 钾肥量/10 × 有效水比例 - 本阶段钾消耗 - 10 × 超灌比例
//
//  【三个必须知道的行为，都已在文档记录】
//    1. 有效水比例封顶为 1，所以"灌够了"之后多灌不再加分，
//       但超灌比例还在涨 —— 即多灌只会有惩罚，没有收益。
//    2. 有机肥项是"朝 6.25 收敛"，天然越靠近推力越小；氮肥项是线性拉低。
//       两者配比相反（0.1 vs 0.15），照抄推荐方案 pH 会净下降。
//       详见 AdvanceCoef.pH_OmCoef 的注释。
//    3. pH 的公式里没有"超灌比例"项，所以灌多灌少对 pH 完全一样。策划已知，待后续优化。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>阶段演算。纯函数，不持有状态。</summary>
    public static class SoilAdvance
    {
        /// <summary>有效水比例 = min(实际灌溉 / 建议灌溉, 1)。建议量为 0 时返回 1。</summary>
        public static float EffectiveWaterRatio(float actual, float recommended)
        {
            if (recommended <= 0f) return 1f;
            return MathX.Min(actual / recommended, 1f);
        }

        /// <summary>超灌比例 = max(实际 - 建议, 0) / 建议。建议量为 0 时返回 0。</summary>
        public static float OverWaterRatio(float actual, float recommended)
        {
            if (recommended <= 0f) return 0f;
            return MathX.Max(actual - recommended, 0f) / recommended;
        }

        /// <summary>
        /// 演算下一阶段的土壤状态。
        /// </summary>
        /// <param name="current">当前阶段土壤（不会被修改）</param>
        /// <param name="stage">当前阶段</param>
        /// <param name="applied">玩家实际投入的水肥</param>
        /// <param name="recommended">本阶段系统建议（提供基准灌溉量作为分母）</param>
        /// <param name="config">数值配置</param>
        /// <returns>新的土壤状态，静态四项原样带过去</returns>
        public static SoilState Advance(SoilState current,
                                        Stage stage,
                                        FertilizerApplication applied,
                                        RecommendedPlan recommended,
                                        SimConfig config)
        {
            if (current == null) return null;

            // 静态四项不变，直接复制过去
            var next = new SoilState(current);
            if (config == null || !Stages.IsPlayable(stage)) return next;

            // 基准灌溉量：演算的两个比例都以它为分母
            float baseIrrigation = recommended != null ? recommended.BaseIrrigation : 0f;
            float eff = EffectiveWaterRatio(applied.Irrigation, baseIrrigation);
            float over = OverWaterRatio(applied.Irrigation, baseIrrigation);

            var a = config.Advance;
            if (a == null) return next;

            var consume = config.ConsumeOf(stage);
            float consumeN = consume != null ? consume.N : 0f;
            float consumeP = consume != null ? consume.P : 0f;
            float consumeK = consume != null ? consume.K : 0f;

            // ---- pH：有机肥推向 6.25，氮肥酸化 ----
            //   注意：此项不受超灌影响（文档公式里只有有效水比例）
            next.pH = current.pH
                    + (a.pH_Target - current.pH)
                      * (applied.Organic / SafeDiv(a.pH_OmRef)) * a.pH_OmCoef * eff
                    - a.pH_NCoef * (applied.N / SafeDiv(a.pH_NRef)) * eff;

            // ---- 腐殖丰度：有机肥增肥，每阶段固定衰减 + 超灌淋失 ----
            next.OM = current.OM
                    + a.OM_Gain * (applied.Organic / SafeDiv(a.OM_GainRef)) * eff
                    - a.OM_Decay
                    - a.OM_OverPenalty * over;

            // ---- 速效氮：施氮 - 作物消耗 - 超灌淋失 ----
            next.N = current.N
                   + a.N_Gain * (applied.N / SafeDiv(a.N_GainRef)) * eff
                   - consumeN
                   - a.N_OverPenalty * over;

            // ---- 磷活度：施磷 - 作物消耗 - 超灌淋失 ----
            next.P = current.P
                   + a.P_Gain * (applied.P / SafeDiv(a.P_GainRef)) * eff
                   - consumeP
                   - a.P_OverPenalty * over;

            // ---- 钾储量：施钾 - 作物消耗 - 超灌淋失 ----
            next.K = current.K
                   + a.K_Gain * (applied.K / SafeDiv(a.K_GainRef)) * eff
                   - consumeK
                   - a.K_OverPenalty * over;

            return next;
        }

        /// <summary>除零保护。配置里参考量为 0 或负数时退回 1，避免出现 NaN 污染整条链路。</summary>
        private static float SafeDiv(float reference)
            => (reference > 1e-6f) ? reference : 1f;

        /// <summary>
        /// 只演算单个指标，供调试与"如果我这样施会怎样"的预览功能用。
        /// 正式流程走 Advance()。
        /// </summary>
        public static float AdvanceMetric(SoilState current, Stage stage,
                                          SoilMetric metric,
                                          FertilizerApplication applied,
                                          RecommendedPlan recommended,
                                          SimConfig config)
        {
            var next = Advance(current, stage, applied, recommended, config);
            return next != null ? next[metric] : 0f;
        }
    }
}
