// ---------------------------------------------------------------------------
//  RevenueReport.cs —— 收益报告
// ---------------------------------------------------------------------------
//  作用：最终界面的数据源。对应文档"收益计算"那一屏。
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

        /// <summary>水分系数 Fwater。v1 恒为 1。</summary>
        public float WaterFactor = 1f;

        /// <summary>静态因素系数 Sstatic。v1 恒为 1。</summary>
        public float StaticFactor = 1f;

        /// <summary>单位面积实际产量 Y = Ymax,geo × Ffert × Fwater × Sstatic。</summary>
        public float Yield;

        /// <summary>使用的 Ymax,geo，便于界面说明"满分是多少"。</summary>
        public float YmaxGeo;

        /// <summary>最终土壤状态（含四个静态指标），用于界面展示。</summary>
        public SoilState FinalSoil;

        /// <summary>土地面积（亩）。为 0 表示未设置。总产量 = Yield × Area。</summary>
        public float Area;

        // ---------------- 以下为后续"经济层"预留 ----------------

        /// <summary>【预留】玉米单价 元/kg。为 0 表示经济层未启用。</summary>
        public float PricePerKg;

        /// <summary>【预留】总成本（肥料 + 灌溉）元。为 0 表示未启用。</summary>
        public float TotalCost;

        /// <summary>是否有经济层数据（能算钱）。</summary>
        public bool HasEconomics => PricePerKg > 0f;

        /// <summary>总产量 = 单位面积产量 × 土地面积。</summary>
        public float TotalYield => Area > 0f ? Yield * Area : Yield;

        /// <summary>毛收入（元）。经济层未启用时返回 0。</summary>
        public float GrossIncome => HasEconomics ? TotalYield * PricePerKg : 0f;

        /// <summary>净收益（元）。经济层未启用时返回 0。</summary>
        public float NetIncome => HasEconomics ? GrossIncome - TotalCost : 0f;

        /// <summary>产量占理论上限的百分比，界面上的"达成率"。</summary>
        public float AchievementRate => YmaxGeo > 0f ? Yield / YmaxGeo : 0f;

        public override string ToString()
            => $"Ffert={OverallFertility:F4} Fwater={WaterFactor:F2} Sstatic={StaticFactor:F2} " +
               $"Y={Yield:F2}/{YmaxGeo:F0} ({AchievementRate:P1})";
    }
}
