// ---------------------------------------------------------------------------
//  YieldModel.cs —— 产量与收益模型【全工程唯一的公式实现】
// ---------------------------------------------------------------------------
//  作用：把四个阶段的肥力系数与水分因子聚合成一个最终产量。
//
//  【这个文件是唯一的公式源】
//    收益链路只允许有一条：
//        Simulation.Revenue() → RevenueReport.Create() → YieldModel.Compute()
//    别处（UI 屏、批量验证窗口、测试）都不许再拼一遍公式 ——
//    否则改一次公式要动几处，还容易出现两处算出两个数。
//
//  【公式来自文档"收益计算"段】
//      Ffert,stage = fPH × fOM × fN × fP × fK          （阶段肥力系数）
//      Ffert       = (S1 + S2 + S3 + S4) / 4            （全生育期平均肥力系数）
//      Fwater      = (α1·Fw,s1 + α2·Fw,s2 + α3·Fw,s3 + α4·Fw,s4) / 4
//      Sstatic     = 1
//      Y           = Ymax,geo × Ffert × Fwater × Sstatic
//
//  【v1 的简化：Fwater = 1】
//    文档 [248] 说玩家照抄系统推荐灌溉量时 Fw,s 全为 1，[249] 由此得出结论 Fwater = 1。
//    v1 就停在这个结论上：SimplifiedWaterFactors() 返回全 1。
//    【挂起项 D2】Fw,s 的公式（策划文档里没有）一旦确定，**只改这一个方法**，
//    下游（RevenueReport / UI）一行都不用动。
//
//  【一处文档不一致（已按"意图"实现）】
//    文档 [246] 写 Fwater = Σ(α_i·Fw,s_i) / 4，而 [247] 的 α = 0.2/0.2/0.4/0.2 **和为 1**。
//    照字面把 Fw,s = 1 代进去会得到 (0.2+0.2+0.4+0.2)/4 = 0.25，
//    与 [249] 的 Fwater = 1 自相矛盾 —— 说明 α 是"相对权重"，÷4 是笔误。
//    本实现按 Σα 归一化（权重和恰为 1 时等价于 Σα_i·Fw,s_i），
//    这样 Fw,s 全 1 时 Fwater 恰好等于 1，与文档结论一致。
//    若策划确认要按字面 ÷4，改 WaterFactor() 的最后一行即可。
//
//  【必须知道的数值特性】
//    五个因子连乘 => 要拿到 90% 产量，五个适宜度的几何平均需要达到 0.979。
//    也就是说任何一处小瑕疵都会被放大五倍惩罚。
//    这符合农学上的"木桶效应"，但会让分数集中在一个偏低的区间。
//    策划已知，后续优化。详见 Docs/数值模拟验证记录.md
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>产量模型。</summary>
    public static class YieldModel
    {
        /// <summary>
        /// v1 的单阶段水分因子 Fw,s = 1（= 无水分胁迫）。
        /// 文档 [248]：玩家照抄系统推荐灌溉量时的取值。
        /// </summary>
        public const float SimplifiedWaterFactor = 1f;

        /// <summary>
        /// 一次聚合算出的全部中间量。
        ///
        /// 【为什么要有这个结构】收益界面要把 Ffert / Fwater / Sstatic / Y 全显示出来，
        /// 若调用方各自再算一遍，很容易出现"界面显示的 Fwater"和"算 Y 用的 Fwater"不一样。
        /// 统一由 <see cref="Compute"/> 一次算完，调用方只读结果。
        /// </summary>
        public readonly struct YieldBreakdown
        {
            /// <summary>全生育期平均肥力系数 Ffert。</summary>
            public readonly float Ffert;

            /// <summary>水分系数 Fwater。</summary>
            public readonly float Fwater;

            /// <summary>静态因素系数 Sstatic。</summary>
            public readonly float Sstatic;

            /// <summary>单位面积实际产量 Y = Ymax,geo × Ffert × Fwater × Sstatic。</summary>
            public readonly float Y;

            /// <summary>本次使用的 Ymax,geo。</summary>
            public readonly float YmaxGeo;

            /// <summary>水分因子是否仍按 v1 简化（全为 1）处理。</summary>
            public readonly bool WaterSimplified;

            /// <summary>
            /// 参与本次计算的阶段水分因子（下标 0=S1）。
            /// 【只读语义】与传入的是同一个数组引用，请勿修改。
            /// </summary>
            public readonly float[] StageWaterFactors;

            public YieldBreakdown(float ffert, float fwater, float sstatic, float y, float ymaxGeo,
                bool waterSimplified, float[] stageWaterFactors)
            {
                Ffert = ffert;
                Fwater = fwater;
                Sstatic = sstatic;
                Y = y;
                YmaxGeo = ymaxGeo;
                WaterSimplified = waterSimplified;
                StageWaterFactors = stageWaterFactors;
            }

            /// <summary>产量占理论上限的百分比（界面上的"达成率"）。</summary>
            public float AchievementRate => YmaxGeo > 0f ? Y / YmaxGeo : 0f;

            public override string ToString()
                => $"Ffert={Ffert:F4} Fwater={Fwater:F2} Sstatic={Sstatic:F2} " +
                   $"Y={Y:F2}/{YmaxGeo:F0} ({AchievementRate:P1})";
        }

        /// <summary>
        /// 全生育期平均肥力系数 Ffert = (S1+S2+S3+S4)/4。
        ///
        /// 【注意】分母是**传进来的阶段数**，不是固定 4。
        /// 因此"只结算了 N 个阶段"时，调用方应传入长度为 N 的数组 ——
        /// 见 <see cref="RevenueReport.Create"/> 里的处理，这样未结算阶段不会被按 0 摊进去。
        /// </summary>
        /// <param name="stageFertility">各阶段的肥力系数，下标 0=S1</param>
        public static float OverallFertility(float[] stageFertility)
        {
            if (stageFertility == null || stageFertility.Length == 0) return 0f;

            float sum = 0f;
            int n = stageFertility.Length < Stages.Count ? stageFertility.Length : Stages.Count;
            for (int i = 0; i < n; i++) sum += stageFertility[i];
            return n > 0 ? sum / n : 0f;
        }

        // ===================================================================
        //  唯一入口
        // ===================================================================

        /// <summary>
        /// 【唯一公式入口】由各阶段肥力系数 + 各阶段水分因子算出 Ffert / Fwater / Sstatic / Y。
        ///
        /// 调用方一律走这里（或更上层的 <see cref="RevenueReport.Create"/>），
        /// 不要自己再写一遍 Y = Ymax × Ffert × Fwater × Sstatic。
        /// </summary>
        /// <param name="stageFertility">各阶段肥力系数 Ffert,stage，下标 0=S1；只挑前若干项参与平均由调用方决定</param>
        /// <param name="stageWaterFactors">各阶段水分因子 Fw,s，下标 0=S1；传 null 或全为 1 即视为 v1 简化</param>
        /// <param name="cfg">产量配置（含 YmaxGeo / Sstatic / α 权重）</param>
        public static YieldBreakdown Compute(float[] stageFertility, float[] stageWaterFactors, YieldConfig cfg)
        {
            ValidateFactors(stageFertility, nameof(stageFertility));
            ValidateFactors(stageWaterFactors, nameof(stageWaterFactors));
            if (cfg != null && (!MathX.IsFinite(cfg.YmaxGeo) || cfg.YmaxGeo < 0f
                || !MathX.IsFinite(cfg.SStatic) || cfg.SStatic < 0f))
                throw new System.ArgumentException("产量配置必须为非负有限数字。", nameof(cfg));
            float fert = OverallFertility(stageFertility);
            bool simplified = IsSimplifiedWater(stageWaterFactors);
            float water = simplified ? SimplifiedWaterFactor : WaterFactor(stageWaterFactors, cfg);
            float stat = cfg != null ? cfg.SStatic : 1f;
            float y = Yield(fert, water, stat, cfg);
            float ymax = cfg != null ? cfg.YmaxGeo : 0f;

            // 【注意】返回值里携带的 StageWaterFactors 与传入的是同一个引用（只读语义）。
            // 需要存下来的调用方请自己 Clone —— RevenueReport.Create 就是这么做的。
            return new YieldBreakdown(fert, water, stat, y, ymax, simplified, stageWaterFactors);
        }

        /// <summary>
        /// 便利重载：没有水分因子数据时按 v1 简化处理（Fw,s 全为 1）。
        /// </summary>
        public static YieldBreakdown ComputeFromStages(float[] stageFertility, YieldConfig cfg)
            => Compute(stageFertility, SimplifiedWaterFactors(), cfg);

        /// <summary>
        /// 水分系数 Fwater（Jensen 乘法水分生产函数，按 α 加权）。
        ///
        /// 【v1 其实用不到它】v1 的 Fw,s 全为 1，<see cref="Compute"/> 会直接取 1 而跳过本方法；
        /// 这里保留实现是为了 [挂起项 D2] 之后不用改调用方。
        ///
        /// 【与文档字面不同的一处】文档 [246] 写 ÷4，但 [247] 的 α 和为 1（÷4 会与 [249] 矛盾），
        /// 所以这里按 **Σα 归一化**。理由见文件头「一处文档不一致」。
        /// </summary>
        /// <param name="stageWaterFactors">各阶段水分因子 Fw,s，下标 0=S1</param>
        public static float WaterFactor(float[] stageWaterFactors, YieldConfig cfg)
        {
            if (stageWaterFactors == null || stageWaterFactors.Length == 0) return SimplifiedWaterFactor;

            int n = stageWaterFactors.Length < Stages.Count ? stageWaterFactors.Length : Stages.Count;

            if (cfg == null || !cfg.UseWeightedWater)
            {
                // 不加权：直接平均
                float sum = 0f;
                for (int i = 0; i < n; i++) sum += stageWaterFactors[i];
                return n > 0 ? sum / n : SimplifiedWaterFactor;
            }

            // 加权：(α1·Fw,s1 + … + α4·Fw,s4) / Σα
            float acc = 0f;
            float weightSum = 0f;
            for (int i = 0; i < n; i++)
            {
                float alpha = (cfg.Alpha != null && i < cfg.Alpha.Length) ? cfg.Alpha[i] : 0.25f;
                if (alpha < 0f) alpha = 0f;                     // 负权重没有意义，按 0 处理
                acc += alpha * stageWaterFactors[i];
                weightSum += alpha;
            }

            // 权重全为 0 / 缺失时退回"无胁迫"，不要返回 0 把产量整个打没
            return weightSum > 0f ? acc / weightSum : SimplifiedWaterFactor;
        }

        /// <summary>
        /// v1 的四阶段水分因子：全为 1。
        ///
        /// 【挂起项 D2 的唯一改动点】
        ///   文档 [245] 说 Fwater 应当"由玩家每阶段灌溉量计算"，
        ///   但 [246] 需要的单阶段公式 Fw,s 在策划文档里根本没有
        ///   （见 Docs/待办工作清单-整合版.md 挂起项 D2）。
        ///   所以 v1 按 [249] 的结论停在 Fwater = 1；
        ///   公式确定后**只需要改这个方法**（例如改成读每阶段的 StageResult.EffectiveWaterRatio()），
        ///   RevenueReport / 收益屏都不用动。
        /// </summary>
        public static float[] SimplifiedWaterFactors()
        {
            var factors = new float[Stages.Count];
            for (int i = 0; i < factors.Length; i++) factors[i] = SimplifiedWaterFactor;
            return factors;
        }

        /// <summary>
        /// 这组水分因子是否仍是 v1 简化（未提供 / 全为 1）。
        /// 收益屏用它决定"本版按最优灌溉简化处理"这句提示要不要显示 ——
        /// 免得将来接了 D2 之后提示还挂在界面上。
        /// </summary>
        private static void ValidateFactors(float[] values, string name)
        {
            if (values == null) return;
            for (int i = 0; i < values.Length && i < Stages.Count; i++)
                if (!MathX.IsFinite(values[i]) || values[i] < 0f)
                    throw new System.ArgumentException("阶段系数必须为非负有限数字。", name);
        }

        public static bool IsSimplifiedWater(float[] stageWaterFactors)
        {
            ValidateFactors(stageWaterFactors, nameof(stageWaterFactors));
            if (stageWaterFactors == null) return true;

            int n = stageWaterFactors.Length < Stages.Count ? stageWaterFactors.Length : Stages.Count;
            if (n <= 0) return true;

            for (int i = 0; i < n; i++)
            {
                if (stageWaterFactors[i] < SimplifiedWaterFactor - 1e-4f ||
                    stageWaterFactors[i] > SimplifiedWaterFactor + 1e-4f) return false;
            }
            return true;
        }

        /// <summary>
        /// 单位面积实际产量 Y = Ymax,geo × Ffert × Fwater × Sstatic。
        /// </summary>
        public static float Yield(float fertility, float waterFactor, float staticFactor, YieldConfig cfg)
        {
            if (cfg == null) return 0f;
            return cfg.YmaxGeo * fertility * waterFactor * staticFactor;
        }

        /// <summary>
        /// 便利重载（历史 API）：直接由各阶段肥力系数算出产量，水分按 v1 简化。
        /// 内部走 <see cref="ComputeFromStages"/>，不再自己拼一遍公式。
        /// </summary>
        public static float YieldFromStages(float[] stageFertility, YieldConfig cfg)
            => ComputeFromStages(stageFertility, cfg).Y;
    }
}
