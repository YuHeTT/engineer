// ---------------------------------------------------------------------------
//  SoilMetric.cs —— 土壤指标枚举
// ---------------------------------------------------------------------------
//  作用：给 9 项土壤指标一个统一编号，全工程用它做数组下标 / 字典键 / 配置字段。
//
//  为什么用枚举而不是字符串：
//    配置表里写 "pH"、"OM" 这种字符串，拼错了要到运行时才发现；
//    枚举拼错编译就不过。而且数组下标可以 O(1) 取适宜度。
//
//  【重要】前 5 项是"动态指标"，后 4 项是"静态指标"，顺序不能改。
//    动态 5 项：每阶段参与适宜度计算，并会被水肥演变到下一阶段。
//    静态 4 项：第一阶段随机生成一次，之后全程不变（v1 只展示，不参与计算）。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>9 项土壤指标。前 5 项动态、后 4 项静态，顺序不可变更。</summary>
    public enum SoilMetric
    {
        // ---- 动态 5 项（参与适宜度与演变）----
        /// <summary>酸衡值 pH。文档：5.8~8（用户输入范围）</summary>
        pH = 0,
        /// <summary>腐殖丰度 OM（有机质）。文档：15~28</summary>
        OM = 1,
        /// <summary>速效氮元 N。文档：70~130</summary>
        N = 2,
        /// <summary>磷活度 P。文档：8~25</summary>
        P = 3,
        /// <summary>钾储量 K。文档：90~200</summary>
        K = 4,

        // ---- 静态 4 项（仅展示）----
        /// <summary>氧还势 Eh。文档随机范围：400~550</summary>
        Eh = 5,
        /// <summary>土骨离子（土壤盐基离子）。文档随机范围：7~16</summary>
        BaseIon = 6,
        /// <summary>毒砷潜势。文档随机范围：5~15</summary>
        As = 7,
        /// <summary>汞积量。文档随机范围：0.03~0.12</summary>
        Hg = 8,
    }

    /// <summary>土壤指标相关的常量与工具。</summary>
    public static class SoilMetrics
    {
        /// <summary>指标总数（9）。</summary>
        public const int Count = 9;

        /// <summary>动态指标数量（5）。</summary>
        public const int DynamicCount = 5;

        /// <summary>静态指标数量（4）。</summary>
        public const int StaticCount = 4;

        /// <summary>全部 9 项，顺序与枚举一致。用于遍历。</summary>
        public static readonly SoilMetric[] All =
        {
            SoilMetric.pH, SoilMetric.OM, SoilMetric.N, SoilMetric.P, SoilMetric.K,
            SoilMetric.Eh, SoilMetric.BaseIon, SoilMetric.As, SoilMetric.Hg,
        };

        /// <summary>动态 5 项，参与适宜度计算与阶段演变。</summary>
        public static readonly SoilMetric[] Dynamic =
        {
            SoilMetric.pH, SoilMetric.OM, SoilMetric.N, SoilMetric.P, SoilMetric.K,
        };

        /// <summary>静态 4 项，第一阶段随机一次后不变。</summary>
        public static readonly SoilMetric[] Static =
        {
            SoilMetric.Eh, SoilMetric.BaseIon, SoilMetric.As, SoilMetric.Hg,
        };

        /// <summary>是否是动态指标（下标 &lt; 5）。</summary>
        public static bool IsDynamic(SoilMetric m) => (int)m < DynamicCount;

        /// <summary>中文显示名，UI 直接用。改文案只改这里。</summary>
        public static string DisplayName(SoilMetric m)
        {
            switch (m)
            {
                case SoilMetric.pH:      return "酸衡值 (pH)";
                case SoilMetric.OM:      return "腐殖丰度";
                case SoilMetric.N:       return "速效氮元";
                case SoilMetric.P:       return "磷活度";
                case SoilMetric.K:       return "钾储量";
                case SoilMetric.Eh:      return "氧还势";
                case SoilMetric.BaseIon: return "土骨离子";
                case SoilMetric.As:      return "毒砷潜势";
                case SoilMetric.Hg:      return "汞积量";
                default:                 return m.ToString();
            }
        }
    }
}
