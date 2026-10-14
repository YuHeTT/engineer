// ---------------------------------------------------------------------------
//  PlanTable.cs —— 推荐水肥方案表（有序匹配）
// ---------------------------------------------------------------------------
//  作用：给定"阶段 + 五个适宜度"，从 11 条规则里挑出唯一一条推荐方案。
//
//  【为什么需要这么个类，而不是在 SimConfig 里遍历】
//    因为"按优先级匹配 + 兜底 + 覆盖性告警"这段逻辑容易写错，
//    集中在一个地方，只写一次、只测一次。
//
//  【判定顺序 —— 实现依据，改这里必须同步文档】
//       S1:  行1(任意<0.6) → 行2(全部≥0.6 且 ≥3项≥0.8) → 兜底=行1
//       S2:  行3(fN<0.6)   → 行4(fN<0.8)              → 行5(全部≥0.8)
//       S3:  行6(N/P/K任一<0.6) → 行7(任意∈[0.6,0.8)) → 行8(全部≥0.8)
//       S4:  行9(fK<0.6)   → 行10(fK<0.8)             → 行11(全部≥0.8)
//
//  【为什么 S1 需要兜底】
//    文档 S1 只有两条规则：
//        行1 = 任意项 < 0.6
//        行2 = 全部 ≥ 0.6 且 ≥3 项 ≥ 0.8
//    "全部 ≥ 0.6 但只有 2 项 ≥ 0.8" 落不进任何一条。
//    已确认处理方式：归入行 1（偏肥分支，更保守，不会让作物缺肥）。
//
//  【边界语义 —— 与 PlanCondition 保持一致】
//    "＜0.6" 用严格小于；"≥0.8" 用大于等于。
//    所以 fN = 0.8 会落到"少量氮肥"那条，fN = 0.6 会落到"中量氮肥"那条。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>推荐方案表。构造一次，之后只读。</summary>
    public sealed class PlanTable
    {
        private readonly SimConfig _config;

        /// <summary>按阶段分组的规则，组内已按 Priority 升序排好。</summary>
        private readonly System.Collections.Generic.List<PlanRule>[] _byStage;

        /// <summary>各阶段的兜底规则（文档没定义、由工程补的那条）。</summary>
        private readonly PlanRule[] _fallback = new PlanRule[Stages.Count];

        /// <summary>
        /// 最近一次匹配是否走了兜底。UI 可以据此提示"该情况文档未定义"，
        /// 方便演示时发现问题，而不是悄悄用一条错的规则。
        /// </summary>
        public bool LastMatchUsedFallback { get; private set; }

        /// <summary>
        /// 构造方案表。
        ///
        /// 【重要契约：构造时快照配置，之后不再感知配置变化】
        ///   构造函数会把 config.PlanRules 按阶段分组并排序，之后就一直用这份副本。
        ///   如果构造完再去改 SimConfig.PlanRules（增删规则、改优先级），
        ///   本对象【不会】跟着变 —— 这是一个刻意的设计（避免每帧排序），
        ///   但也是个容易踩的坑。
        ///
        ///   正确做法：配置定型之后再构造 PlanTable。
        ///   Simulation 在构造函数里创建 PlanTable，所以"改配置要重新 new Simulation"。
        ///   如果你的场景需要在运行中热改规则，就重新 new 一个 PlanTable。
        /// </summary>
        public PlanTable(SimConfig config)
        {
            _config = config;
            _byStage = new System.Collections.Generic.List<PlanRule>[Stages.Count];
            for (int i = 0; i < _byStage.Length; i++)
                _byStage[i] = new System.Collections.Generic.List<PlanRule>();

            if (config != null && config.PlanRules != null)
            {
                foreach (var r in config.PlanRules)
                {
                    if (r == null || !Stages.IsPlayable(r.Stage)) continue;
                    _byStage[Stages.ToIndex(r.Stage)].Add(r);
                }
                for (int i = 0; i < _byStage.Length; i++)
                    _byStage[i].Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }

            // 兜底规则：取该阶段优先级最高的那条
            for (int i = 0; i < Stages.Count; i++)
            {
                if (_byStage[i].Count > 0) _fallback[i] = _byStage[i][0];
            }
        }

        /// <summary>
        /// 按优先级匹配推荐方案。永远返回非 null。
        ///
        /// 【三级匹配 —— 因为文档的规则表并不完备】
        ///   文档 TABLE 13 的规则之间存在真实的空洞。例如：
        ///     S2 只有三条规则，分别判定 fN&lt;0.6 / fN&lt;0.8 / 全部≥0.8。
        ///     当「fN=1.0 但 fP=0.6」时，三条都不成立 —— 缺磷却没有对应建议。
        ///     S4 同理（只判定 fK）。
        ///   所以匹配分三级：
        ///     1) 按 Priority 找精确匹配
        ///     2) 没有精确匹配时，用标记为 IsFallback 的规则（"缺什么补什么"那条）
        ///     3) 连兜底规则都没有，才退回优先级最高的规则，并置 LastMatchUsedFallback
        /// </summary>
        public RecommendedPlan Match(Stage stage, SuitabilityResult fit)
        {
            LastMatchUsedFallback = false;
            if (!Stages.IsPlayable(stage)) return FallbackPlan(stage);

            var list = _byStage[Stages.ToIndex(stage)];

            // 1) 精确匹配
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Matches(fit)) return list[i].ToPlan();
            }

            // 2) 缺什么补什么：用标记为兜底的规则
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].IsFallback) return list[i].ToPlan();
            }

            // 3) 最后才退回最高优先级，并记录"文档未覆盖"
            LastMatchUsedFallback = true;
            return FallbackPlan(stage);
        }

        /// <summary>取某阶段的兜底方案。</summary>
        public RecommendedPlan FallbackPlan(Stage stage)
        {
            if (!Stages.IsPlayable(stage)) return null;
            var rule = _fallback[Stages.ToIndex(stage)];
            return rule != null ? rule.ToPlan() : null;
        }

        /// <summary>取某阶段的全部规则，供 UI 展示"完整方案表"。</summary>
        public System.Collections.Generic.IList<PlanRule> RulesOf(Stage stage)
        {
            if (!Stages.IsPlayable(stage)) return new PlanRule[0];
            return _byStage[Stages.ToIndex(stage)];
        }

        /// <summary>
        /// 覆盖性自检：在阈值相关的采样点上枚举全部组合，检查是否都能匹配到规则。
        ///
        /// 采样点特意选在判定阈值附近（0.59 / 0.6 / 0.79 / 0.8），
        /// 因为规则的分支边界都在这里。等距采样（如 0.2,0.29,0.38…）
        /// 反而会漏掉边界附近的问题。
        ///
        /// 【性能】完整 12 档是 12^5 ≈ 25 万组 × 4 阶段，
        /// 编辑器里同步跑约 0.1~0.3 秒。想更快就传 steps=8。
        /// </summary>
        /// <param name="steps">每个维度的采样档数。0 或负数表示用完整 12 档。</param>
        /// <param name="report">命中的空洞描述</param>
        public bool CheckCoverage(int steps, out System.Collections.Generic.List<string> report)
        {
            report = new System.Collections.Generic.List<string>();

            // 阈值相关采样点：保底值、阈值两侧、阈值、满分
            float[] full =
            {
                0.20f, 0.30f, 0.50f, 0.59f, 0.60f, 0.65f, 0.70f,
                0.79f, 0.80f, 0.85f, 0.90f, 1.00f,
            };

            float[] values;
            if (steps <= 0 || steps >= full.Length)
            {
                values = full;
            }
            else
            {
                // 降采样：始终保留阈值本身（0.6 / 0.8），其余等距取
                var list = new System.Collections.Generic.List<float>();
                int interior = steps - 3;
                if (interior < 1) interior = 1;
                list.Add(0.20f);
                for (int i = 1; i <= interior; i++)
                    list.Add(0.20f + 0.80f * i / (interior + 1f));
                list.Add(0.60f);
                list.Add(0.80f);
                list.Add(1.00f);
                values = list.ToArray();
            }

            return CheckCoverage(values, out report);
        }

        /// <summary>用完整 12 档采样做覆盖性自检。</summary>
        public bool CheckCoverage(out System.Collections.Generic.List<string> report)
            => CheckCoverage(0, out report);

        /// <summary>
        /// 覆盖性自检（自定义采样点）。会枚举全部组合，复杂度是 O(n^5)。
        /// </summary>
        /// <param name="values">每个维度的采样值</param>
        /// <param name="report">命中的空洞描述</param>
        public bool CheckCoverage(float[] values, out System.Collections.Generic.List<string> report)
        {
            report = new System.Collections.Generic.List<string>();
            if (values == null || values.Length == 0) return true;

            int steps = values.Length;
            foreach (var stage in Stages.All)
            {
                int gaps = 0;
                var list = _byStage[Stages.ToIndex(stage)];

                for (int a = 0; a < steps; a++)
                for (int b = 0; b < steps; b++)
                for (int c = 0; c < steps; c++)
                for (int d = 0; d < steps; d++)
                for (int e = 0; e < steps; e++)
                {
                    var fit = new SuitabilityResult(values[a], values[b], values[c], values[d], values[e]);

                    bool hit = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i].Matches(fit)) { hit = true; break; }
                    }
                    if (!hit)
                    {
                        gaps++;
                        if (gaps <= 5) report.Add($"{Stages.ShortName(stage)} 无精确匹配：{fit}");
                    }
                }

                if (gaps > 0)
                    report.Add($"{Stages.ShortName(stage)} 共 {gaps} 组输入在文档规则里无精确匹配" +
                               $"（已由兜底规则处理，但规则表本身不完备）");
            }

            return report.Count == 0;
        }
    }
}
