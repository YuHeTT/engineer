// ---------------------------------------------------------------------------
//  RevenueReport.cs —— 收益报告
// ---------------------------------------------------------------------------
//  作用：最终界面的数据源。对应文档"收益计算"那一屏。
//
//  【唯一组装入口】
//    报告只允许由 RevenueReport.Create() 生成 —— 它内部调用 YieldModel.Compute() 取公式结果。
//    调用方（Simulation.Revenue / 批量验证窗口 / 将来的单测）都不许再自己拼 Ffert / Fwater / Y，
//    否则会出现"界面显示的中间量"和"算 Y 用的中间量"不是同一个数。
//
//  【文档要求这一屏列出】
//      - 第二、三、四、五阶段下方的表格参数
//      - 四个不变参数：氧还势、土骨离子、毒砷潜势、汞积量
//      - 以及由公式算出的最终收益 Y
//
//  【注意文档里的一处不一致】
//    文档说"输出 Y 作为最终收益"，但 Y 的单位是 kg/亩（产量），不是钱。
//    界面上叫"收益计算"却没有价格和成本。
//    策划已确认成本问题后续再加，所以这里同时保留：
//      - Yield：产量（当前就能算）
//      - 预留 PricePerKg / TotalCost 两个字段，将来补经济层时直接用
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>四阶段结束后的收益报告。</summary>
    public sealed class RevenueReport
    {
        /// <summary>四个阶段的肥力系数，下标 0=S1。</summary>
        public float[] StageFertility;

        /// <summary>全生育期平均肥力系数 Ffert。</summary>
        public float OverallFertility;

        /// <summary>
        /// 水分系数 Fwater（= α 加权合成的各阶段水分因子）。
        /// 由 <see cref="YieldModel.Compute"/> 写入；v1 因 Fw,s 全为 1 而恒为 1。
        /// </summary>
        public float WaterFactor = 1f;

        /// <summary>静态因素系数 Sstatic。v1 恒为 1。</summary>
        public float StaticFactor = 1f;

        /// <summary>
        /// 各阶段的水分因子 Fw,s1..Fw,s4（下标 0=S1）。
        /// v1 全为 1 —— 见 <see cref="YieldModel.SimplifiedWaterFactors"/>（挂起项 D2 的唯一改动点）。
        /// </summary>
        public float[] StageWaterFactors;

        /// <summary>
        /// 水分因子是否仍按 v1 简化（全为 1）处理。
        /// 界面据此决定"本版按最优灌溉简化处理"这句提示要不要显示，
        /// 不要把这个判断写死在 UI 里。
        /// </summary>
        public bool IsWaterSimplified = true;

        /// <summary>单位面积实际产量 Y = Ymax,geo × Ffert × Fwater × Sstatic。</summary>
        public float Yield;

        /// <summary>使用的 Ymax,geo，便于界面说明"满分是多少"。</summary>
        public float YmaxGeo;

        /// <summary>最终土壤状态（含四个静态指标），用于界面展示。</summary>
        public SoilState FinalSoil;

        /// <summary>土地面积（亩）。为 0 表示未设置。总产量 = Yield × Area。</summary>
        public float Area;

        /// <summary>已结算阶段实际累计投入（每亩）；使用钳制后的实际值，不是原始输入。</summary>
        public FertilizerApplication CumulativeInputPerMu;

        /// <summary>是否提供了投入汇总；直接调用公式工厂时可以不提供。</summary>
        public bool HasInputSummary;

        /// <summary>全田累计投入：灌溉为 m³，其余为 kg；无面积时返回零。</summary>
        public FertilizerApplication TotalInput => HasArea
            ? new FertilizerApplication(CumulativeInputPerMu.Irrigation * Area,
                CumulativeInputPerMu.Organic * Area, CumulativeInputPerMu.N * Area,
                CumulativeInputPerMu.P * Area, CumulativeInputPerMu.K * Area)
            : FertilizerApplication.None;

        /// <summary>
        /// 已结算的阶段数（0~4）。
        /// 小于 4 时本报告是按【已结算的那几个阶段】估算的中间态（见 IsPartial），
        /// 不是最终结果 —— Ffert 只对已结算阶段求平均，未结算阶段不会按 0 摊进去。
        /// </summary>
        public int CommittedCount = Stages.Count;

        /// <summary>四阶段是否只结算了一部分（即本报告是中间态）。</summary>
        public bool IsPartial => CommittedCount < Stages.Count;

        // ---------------- 组装入口 ----------------

        /// <summary>
        /// 【唯一组装入口】由各阶段数据生成收益报告。
        ///
        /// 内部只有一条计算链：YieldModel.Compute() → 填字段。
        /// 不要在别处 new RevenueReport 或再拼一遍公式。
        /// </summary>
        /// <param name="stageFertility">各阶段肥力系数 Ffert,stage（长度通常是 4，下标 0=S1）</param>
        /// <param name="stageWaterFactors">各阶段水分因子 Fw,s；传 null 按 v1 简化处理（全为 1）</param>
        /// <param name="yieldConfig">产量配置（YmaxGeo / Sstatic / α 权重）；传 null 则 Y = 0</param>
        /// <param name="areaMu">土地面积（亩），只用于算总产量</param>
        /// <param name="finalSoil">最终土壤状态，供界面展示四个静态指标</param>
        /// <param name="committedCount">
        /// 已结算阶段数 —— **只对已结算的阶段求平均**。
        /// 传负数表示"不指定"，按全部阶段（4）处理，兼容老调用方。
        /// </param>
        /// <param name="pricePerKg">【预留】玉米单价（元/kg），0 = 经济层未启用</param>
        /// <param name="totalCost">【预留】总成本（元），0 = 未启用</param>
        public static RevenueReport Create(
            float[] stageFertility,
            float[] stageWaterFactors,
            YieldConfig yieldConfig,
            float areaMu = 0f,
            SoilState finalSoil = null,
            int committedCount = -1,
            float pricePerKg = 0f,
            float totalCost = 0f,
            FertilizerApplication? cumulativeInputPerMu = null)
        {
            if (!MathX.IsFinite(areaMu) || areaMu < 0f) throw new System.ArgumentOutOfRangeException(nameof(areaMu));
            if (!MathX.IsFinite(pricePerKg) || pricePerKg < 0f) throw new System.ArgumentOutOfRangeException(nameof(pricePerKg));
            if (!MathX.IsFinite(totalCost) || totalCost < 0f) throw new System.ArgumentOutOfRangeException(nameof(totalCost));
            // 只把已结算的阶段交给公式：未结算阶段若按 0 摊进平均，Ffert / Y 会被凭白拉低
            int committed = committedCount < 0 ? Stages.Count : committedCount;
            if (committed > Stages.Count) committed = Stages.Count;
            if (committed < 0) committed = 0;

            var breakdown = YieldModel.Compute(SliceStages(stageFertility, committed),
                SliceStages(stageWaterFactors, committed), yieldConfig);

            return new RevenueReport
            {
                // 快照存完整四个阶段：界面要列 S1~S4，未结算的由界面显示"—"
                StageFertility = stageFertility != null
                    ? (float[])stageFertility.Clone()
                    : new float[Stages.Count],
                StageWaterFactors = stageWaterFactors != null ? (float[])stageWaterFactors.Clone() : null,

                // 公式结果：一律来自 YieldModel，不在这里重算
                OverallFertility = breakdown.Ffert,
                WaterFactor = breakdown.Fwater,
                StaticFactor = breakdown.Sstatic,
                Yield = breakdown.Y,
                YmaxGeo = breakdown.YmaxGeo,
                IsWaterSimplified = breakdown.WaterSimplified,

                FinalSoil = finalSoil != null ? finalSoil.Clone() : null,
                Area = areaMu,
                HasInputSummary = cumulativeInputPerMu.HasValue,
                CumulativeInputPerMu = cumulativeInputPerMu ?? FertilizerApplication.None,
                CommittedCount = committed,

                // 经济层：数据（单价/成本）待策划提供，为 0 时界面自动隐藏这一行
                PricePerKg = pricePerKg,
                TotalCost = totalCost,
            };
        }

        /// <summary>取前 n 个（已结算的）阶段；count 越界会自动钳到合法范围。</summary>
        private static float[] SliceStages(float[] values, int count)
        {
            if (values == null || count <= 0) return new float[0];

            int n = count < values.Length ? count : values.Length;
            var slice = new float[n];
            for (int i = 0; i < n; i++) slice[i] = values[i];
            return slice;
        }

        // ---------------- 以下为后续"经济层"预留 ----------------

        /// <summary>【预留】玉米单价 元/kg。为 0 表示经济层未启用。</summary>
        public float PricePerKg;

        /// <summary>【预留】总成本（肥料 + 灌溉）元。为 0 表示未启用。</summary>
        public float TotalCost;

        /// <summary>是否有经济层数据（能算钱）。</summary>
        public bool HasEconomics => HasArea && PricePerKg > 0f;

        /// <summary>面积缺失时不把单位面积产量冒充总产量。</summary>
        public bool HasArea => MathX.IsFinite(Area) && Area > 0f;

        /// <summary>总产量 = 单位面积产量 × 土地面积。</summary>
        public float TotalYield => HasArea ? Yield * Area : 0f;

        /// <summary>毛收入（元）。经济层未启用时返回 0。</summary>
        public float GrossIncome => HasEconomics ? TotalYield * PricePerKg : 0f;

        /// <summary>净收益（元）。经济层未启用时返回 0。</summary>
        public float NetIncome => HasEconomics ? GrossIncome - TotalCost : 0f;

        /// <summary>产量占理论上限的百分比，界面上的"达成率"。</summary>
        public float AchievementRate => YmaxGeo > 0f ? Yield / YmaxGeo : 0f;

        /// <summary>
        /// 水分系数那一行的补充说明（界面直接显示，别再在 UI 里写死这句话）。
        /// v1 简化时解释"为什么 Fwater = 1"；将来接了 [挂起项 D2] 后自动改成列各阶段 Fw,s。
        /// </summary>
        public string WaterNote => IsWaterSimplified
            ? "（本版暂按无水分胁迫计算，水分系数为 1）"
            : "（Fw,s = " + FormatStageWaterFactors() + "，按 α 权重合成）";

        /// <summary>把各阶段水分因子拼成 "1 / 0.9 / 1 / 1" 这样的一行。</summary>
        private string FormatStageWaterFactors()
        {
            if (StageWaterFactors == null || StageWaterFactors.Length == 0) return "—";

            var sb = new System.Text.StringBuilder();
            int n = StageWaterFactors.Length < Stages.Count ? StageWaterFactors.Length : Stages.Count;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(StageWaterFactors[i].ToString("0.###"));
            }
            return sb.ToString();
        }

        public override string ToString()
            => $"Ffert={OverallFertility:F4} Fwater={WaterFactor:F2} Sstatic={StaticFactor:F2} " +
               $"Y={Yield:F2}/{YmaxGeo:F0} ({AchievementRate:P1})" +
               (IsPartial ? $" [已结算 {CommittedCount}/{Stages.Count} 阶段]" : string.Empty);
    }
}
