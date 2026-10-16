// ---------------------------------------------------------------------------
//  YieldModel.cs —— 产量与收益模型
// ---------------------------------------------------------------------------
//  作用：把四个阶段的肥力系数聚合成一个最终产量。
//
//  【公式来自文档"收益计算"段】
//      Ffert,stage = fPH × fOM × fN × fP × fK          （阶段肥力系数）
//      Ffert       = (S1 + S2 + S3 + S4) / 4            （全生育期平均肥力系数）
//      Fwater      = (α1·Fw,s1 + α2·Fw,s2 + α3·Fw,s3 + α4·Fw,s4) / 4
//      Sstatic     = 1
//      Y           = Ymax,geo × Ffert × Fwater × Sstatic
//
//  【v1 的简化】
//    文档说明"因系统每次推荐最优灌溉量，Fwater = 1"。
//    所以 v1 里 Fwater 和 Sstatic 都是 1，Y = Ymax,geo × Ffert。
//    但参数保留、不写成常量 1 —— 后续要接"水分胁迫"时不用改函数签名。
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
        /// 全生育期平均肥力系数 Ffert = (S1+S2+S3+S4)/4。
        /// </summary>
        /// <param name="stageFertility">四个阶段的肥力系数，下标 0=S1</param>
        public static float OverallFertility(float[] stageFertility)
        {
            if (stageFertility == null || stageFertility.Length == 0) return 0f;

            float sum = 0f;
            int n = stageFertility.Length < Stages.Count ? stageFertility.Length : Stages.Count;
            for (int i = 0; i < n; i++) sum += stageFertility[i];
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>
        /// 水分系数。v1 恒为 1（系统总给最优灌溉）。
        /// 保留实现是为了后续接水分胁迫时不用改调用方。
        /// </summary>
        /// <param name="stageWaterFactors">四个阶段的水分因子，下标 0=S1</param>
        public static float WaterFactor(float[] stageWaterFactors, YieldConfig cfg)
        {
            if (stageWaterFactors == null || stageWaterFactors.Length == 0) return 1f;
            if (cfg == null || !cfg.UseWeightedWater)
            {
                // 不加权：直接平均
                float sum = 0f;
                int n = stageWaterFactors.Length < Stages.Count ? stageWaterFactors.Length : Stages.Count;
                for (int i = 0; i < n; i++) sum += stageWaterFactors[i];
                return n > 0 ? sum / n : 1f;
            }

            // 加权：(α1·Fw1 + … + α4·Fw4) / 4
            float acc = 0f;
            int m = stageWaterFactors.Length < Stages.Count ? stageWaterFactors.Length : Stages.Count;
            for (int i = 0; i < m; i++)
            {
                float alpha = (cfg.Alpha != null && i < cfg.Alpha.Length) ? cfg.Alpha[i] : 0.25f;
                acc += alpha * stageWaterFactors[i];
            }
            return acc / 4f;
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
        /// 便利重载：直接由四阶段肥力系数算出产量。
        /// </summary>
        public static float YieldFromStages(float[] stageFertility, YieldConfig cfg)
        {
            float fert = OverallFertility(stageFertility);
            // Fwater 暂时仍取常量 1 —— 因为"由灌溉量算 Fw 阶段值"的具体规则尚未敲定
            // （见待办 C1）。但 Alpha=[0.2,0.2,0.4,0.2] 与 UseWeightedWater 已按文档就位，
            // 规则一旦确定，只需把这里换成按灌溉量计算的实现。
            float water = 1f;
            float stat = cfg != null ? cfg.SStatic : 1f;       // 文档：暂定不参与产量
            return Yield(fert, water, stat, cfg);
        }
    }
}
