// ---------------------------------------------------------------------------
//  PlanCondition.cs —— 推荐方案表的判定条件
// ---------------------------------------------------------------------------
//  作用：把文档 TABLE 13 的"适宜度判定条件"列，变成可以求值的数据结构。
//
//  【文档的条件形态】
//      "任意项＜0.6（优先 fOM、fN）"       -> AnyBelow
//      "全部≥0.6 且≥3 项≥0.8"             -> AllAboveAndCount
//      "fN＜0.6（其次 fP）"                -> Below (限定指标 N)
//      "fN 或 fP 或 fK＜0.6"               -> AnyBelowAmong (限定指标集合)
//      "任意项 0.6~0.8"                    -> AnyInRange
//      "全部≥0.8"                          -> AllAbove
//      "其余情况"                          -> Always（必须放在同阶段最后一条）
//
//  【边界语义 —— 全表统一，实现时必须一致】
//      "＜0.6"  ->  value <  0.6         （严格小于才触发低档）
//      "≥0.8"   ->  value >= 0.8         （等号归高档）
//      推论：fN = 0.8 不触发 "fN<0.8"，会落到 "全部≥0.8" 那条。
//            fN = 0.6 不触发 "fN<0.6"，会落到 "fN<0.8" 那条。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>条件类型。对应文档 TABLE 13 判定条件列的写法。</summary>
    public enum PlanCondType
    {
        /// <summary>某个指定指标 &lt; 阈值。如 S2 的 "fN＜0.6"。</summary>
        Below = 0,

        /// <summary>指定指标集合里，任意一项 &lt; 阈值。如 S3 的 "fN 或 fP 或 fK＜0.6"。</summary>
        AnyBelow = 1,

        /// <summary>指定指标集合里，任意一项落在 [Lo, Hi)。如 S3 的 "任意项 0.6~0.8"。</summary>
        AnyInRange = 2,

        /// <summary>指定指标集合里，全部 ≥ 阈值。如 "全部≥0.8"。</summary>
        AllAbove = 3,

        /// <summary>全部 ≥ 阈值，且至少有 Count 项 ≥ CountThreshold。如 S1 行 2。</summary>
        AllAboveAndCount = 4,

        /// <summary>
        /// 其余情况（兜底分支）。只应放在同阶段规则的最后一条；
        /// 对应 2026-10-16 策划方案里的「其余情况」。
        /// </summary>
        Always = 5,
    }

    /// <summary>一条判定条件。</summary>
    public sealed class PlanCondition
    {
        /// <summary>条件类型。</summary>
        public PlanCondType Type = PlanCondType.AnyBelow;

        /// <summary>
        /// 参与判定的指标集合。
        /// null 或空数组 = 全部五个动态指标（文档里的"任意项/全部"）。
        /// </summary>
        public SoilMetric[] Scope;

        /// <summary>主阈值。对应文档里的 0.6 / 0.8。</summary>
        public float Threshold = 0.6f;

        /// <summary>区间下界。仅 AnyInRange 用。</summary>
        public float Lo;

        /// <summary>区间上界。仅 AnyInRange 用。</summary>
        public float Hi;

        /// <summary>计数阈值。仅 AllAboveAndCount 用。</summary>
        public int Count;

        /// <summary>计数用阈值。仅 AllAboveAndCount 用（S1 行 2 的 0.8）。</summary>
        public float CountThreshold = 0.8f;

        /// <summary>取实际参与判定的指标数组，空则返回全部动态五项。</summary>
        public SoilMetric[] ResolvedScope()
            => (Scope == null || Scope.Length == 0) ? SoilMetrics.Dynamic : Scope;

        /// <summary>对给定的适宜度结果求值。</summary>
        public bool Evaluate(SuitabilityResult fit)
        {
            if (fit == null) return false;
            var scope = ResolvedScope();

            switch (Type)
            {
                case PlanCondType.Below:
                    // 只取集合的第一个指标
                    return scope.Length > 0 && fit[scope[0]] < Threshold;

                case PlanCondType.AnyBelow:
                {
                    for (int i = 0; i < scope.Length; i++)
                        if (fit[scope[i]] < Threshold) return true;
                    return false;
                }

                case PlanCondType.AnyInRange:
                {
                    for (int i = 0; i < scope.Length; i++)
                    {
                        float v = fit[scope[i]];
                        if (v >= Lo && v < Hi) return true;
                    }
                    return false;
                }

                case PlanCondType.AllAbove:
                {
                    for (int i = 0; i < scope.Length; i++)
                        if (!(fit[scope[i]] >= Threshold)) return false;
                    return true;
                }

                case PlanCondType.AllAboveAndCount:
                {
                    int cnt = 0;
                    for (int i = 0; i < scope.Length; i++)
                    {
                        float v = fit[scope[i]];
                        if (!(v >= Threshold)) return false;      // 任一未达标即整体不成立
                        if (v >= CountThreshold) cnt++;
                    }
                    return cnt >= Count;
                }

                case PlanCondType.Always:
                    return true;
            }
            return false;
        }

        /// <summary>生成可读描述，用于日志和调试窗口。</summary>
        public string Describe()
        {
            string scopeName = (Scope == null || Scope.Length == 0)
                ? "任意项" : string.Join("/", System.Array.ConvertAll(ResolvedScope(), m => m.ToString()));

            switch (Type)
            {
                case PlanCondType.Below:            return $"{scopeName} < {Threshold}";
                case PlanCondType.AnyBelow:         return $"{scopeName} 任意 < {Threshold}";
                case PlanCondType.AnyInRange:       return $"{scopeName} 任意 ∈ [{Lo}, {Hi})";
                case PlanCondType.AllAbove:         return $"{scopeName} 全部 ≥ {Threshold}";
                case PlanCondType.AllAboveAndCount: return $"{scopeName} 全部 ≥ {Threshold} 且 ≥{Count} 项 ≥ {CountThreshold}";
                case PlanCondType.Always:           return "其余情况";
                default:                            return Type.ToString();
            }
        }
    }
}
