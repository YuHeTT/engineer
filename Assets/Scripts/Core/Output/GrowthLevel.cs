// ---------------------------------------------------------------------------
//  GrowthLevel.cs —— 玉米长势三档
// ---------------------------------------------------------------------------
//  作用：把"五个适宜度"归纳成一个 UI 能显示的长势等级，用来切贴图。
//
//  【文档原文】
//      好    : 所有适宜度 > 0.6
//      正常  : 3 个适宜度 > 0.6
//      差    : 所有适宜度 < 0.6
//
//  【已确认的修订（策划回复）】
//      除了"好"和"差"之外，其余一律算"正常"。
//      即第三档从"3 个 >0.6"放宽为"其余全部"。
//
//  【注意判定用的是严格不等号】
//      好 = 全部 > 0.6      （f == 0.6 不算好）
//      差 = 全部 < 0.6      （f == 0.6 不算差）
//      所以五项都恰好等于 0.6 时 -> 正常。这是有意为之，不是漏洞。
//
//  【文档的贴图说明】
//      第一阶段只看得见土地（不显示玉米）
//      第二、三、四阶段按本等级切贴图
//      第五阶段（收获期）与第四阶段共用贴图
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>玉米长势等级。用于切换植株贴图/模型。</summary>
    public enum GrowthLevel
    {
        /// <summary>好：全部适宜度 &gt; 0.6</summary>
        Good = 0,

        /// <summary>正常：其余全部情况（含恰好 0.6）</summary>
        Normal = 1,

        /// <summary>差：全部适宜度 &lt; 0.6</summary>
        Bad = 2,
    }

    /// <summary>长势判定参数与工具。</summary>
    public sealed class GrowthRule
    {
        /// <summary>"好"的门槛：全部适宜度必须严格大于它。</summary>
        public float GoodAllAbove = 0.6f;

        /// <summary>"差"的门槛：全部适宜度必须严格小于它。</summary>
        public float BadAllBelow = 0.6f;

        /// <summary>
        /// 按适宜度判定长势。
        /// </summary>
        /// <param name="fit">本阶段的五个适宜度</param>
        public GrowthLevel Evaluate(SuitabilityResult fit)
        {
            if (fit == null) return GrowthLevel.Normal;

            bool allGood = true;
            bool allBad = true;

            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                float f = fit[SoilMetrics.Dynamic[i]];
                if (!(f > GoodAllAbove)) allGood = false;
                if (!(f < BadAllBelow)) allBad = false;
            }

            if (allGood) return GrowthLevel.Good;
            if (allBad) return GrowthLevel.Bad;
            return GrowthLevel.Normal;
        }
    }

    /// <summary>长势等级的中文名与资源名约定。</summary>
    public static class GrowthLevels
    {
        public static readonly GrowthLevel[] All =
        {
            GrowthLevel.Good, GrowthLevel.Normal, GrowthLevel.Bad,
        };

        /// <summary>UI 显示名。</summary>
        public static string DisplayName(GrowthLevel g)
        {
            switch (g)
            {
                case GrowthLevel.Good:   return "长势良好";
                case GrowthLevel.Bad:    return "长势较差";
                default:                 return "长势正常";
            }
        }

        /// <summary>
        /// 资源命名后缀约定。美术按这个命名，代码按这个拼路径，避免对不上。
        /// 例：玉米_成熟_好 / 玉米_成长_正常 / 玉米_幼苗_差
        /// </summary>
        public static string AssetSuffix(GrowthLevel g)
        {
            switch (g)
            {
                case GrowthLevel.Good:   return "好";
                case GrowthLevel.Bad:    return "差";
                default:                 return "正常";
            }
        }
    }
}
