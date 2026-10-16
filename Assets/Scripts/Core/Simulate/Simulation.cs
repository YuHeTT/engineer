// ---------------------------------------------------------------------------
//  Simulation.cs —— 数值内核总入口（状态机）
// ---------------------------------------------------------------------------
//  作用：把前面所有零件串起来，对外只暴露 5 个方法。
//        UI 层只跟这个类打交道，不直接碰 SuitabilityCurve / SoilAdvance。
//
//  【设计要点：能单步推进，而不是一次跑完】
//    因为每个阶段之间都要插入玩家的操作（机器人巡航 -> 看建议 -> 填水肥），
//    所以不能设计成 RunAll()。正确形态是：
//
//        sim.Initialize(土壤);                    // 选定地形与基础数据后
//        while (!sim.IsFinished) {
//            var fit = sim.Evaluate();             // 当前适宜度
//            var rec = sim.Recommend();            // 当前建议
//            ...UI 显示，等玩家填水肥...
//            var result = sim.Commit(玩家输入);     // 结算并推进到下一阶段
//        }
//        var report = sim.Revenue();               // 出收益报告
//
//  【谁能改土壤】
//    只有两处：Initialize（写入初始值）和 Commit（演算推进）。
//    对外只暴露只读的 CurrentSoil，避免 UI 层误改导致状态不一致。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>五阶段模拟的状态机。</summary>
    public sealed class Simulation : ISimulationView
    {
        private readonly SimConfig _config;
        private readonly PlanTable _planTable;

        /// <summary>四个阶段各自的肥力系数，下标 0=S1。收益计算取它的平均。</summary>
        private readonly float[] _stageFertility = new float[Stages.Count];

        /// <summary>
        /// 四个阶段各自的水分因子 Fw,s，下标 0=S1。收益计算按 α 权重合成 Fwater。
        /// v1 由 <see cref="ResetWaterFactors"/> 填成全 1（见挂起项 D2）。
        /// </summary>
        private readonly float[] _stageWaterFactors = new float[Stages.Count];

        /// <summary>四个阶段的完整结果，便于回看与导出。</summary>
        private readonly StageResult[] _history = new StageResult[Stages.Count];

        private SoilState _soil;
        private Stage _stage = Stage.S1;
        private int _committedCount;

        /// <summary>
        /// 当前阶段适宜度的缓存。土壤或阶段一变就失效。
        ///
        /// 【为什么需要】UI 一屏里会连调 Evaluate() / Recommend() / CurrentGrowth()，
        /// 这三个内部都要算适宜度。不缓存的话同一阶段要算三遍，
        /// 而且如果将来有任何非确定性因素，三次结果还可能不一致。
        /// </summary>
        private SuitabilityResult _cachedFit;

        public Simulation(SimConfig config)
        {
            _config = config ?? SimConfig.CreateDefault();
            _planTable = new PlanTable(_config);
            ResetWaterFactors();          // v1：Fw,s 全为 1（挂起项 D2 见 StageWaterFactors()）
        }

        /// <summary>清掉适宜度缓存。任何改变土壤或阶段的操作都必须调它。</summary>
        private void InvalidateCache() => _cachedFit = null;

        // ===================================================================
        //  对外只读状态
        // ===================================================================

        /// <summary>数值配置。</summary>
        public SimConfig Config => _config;

        /// <summary>推荐方案表。</summary>
        public PlanTable PlanTable => _planTable;

        /// <summary>当前阶段。Done 表示四阶段已全部结算。</summary>
        public Stage CurrentStage => _stage;

        /// <summary>
        /// 当前土壤状态。
        /// 【只读语义】返回的是内部对象的引用，请勿直接修改。
        /// 需要修改请先 Clone()。UI 层只读展示不受影响。
        /// </summary>
        public SoilState CurrentSoil => _soil;

        /// <summary>四阶段是否已全部结束。</summary>
        public bool IsFinished => _stage == Stage.Done;

        /// <summary>已完成结算的阶段数（0~4）。</summary>
        public int CommittedCount => _committedCount;

        /// <summary>取某阶段的历史结果。未结算返回 null。</summary>
        public StageResult GetHistory(Stage s)
        {
            int i = Stages.ToIndex(s);
            if (i < 0 || i >= _history.Length) return null;
            return _history[i];
        }

        /// <summary>
        /// 全部历史结果（按顺序）。
        /// 【只读语义】返回的是内部数组，请勿修改其元素指向。
        /// </summary>
        public StageResult[] History => _history;

        /// <summary>只读视图。UI 层应依赖这个接口而不是具体类，方便写假数据测试。</summary>
        public ISimulationView View => this;

        // ===================================================================
        //  生命周期
        // ===================================================================

        /// <summary>
        /// 初始化。把玩家输入的动态指标与随机生成的静态指标合成完整土壤。
        /// </summary>
        /// <param name="userInput">玩家在"土地基础数据设置"填的土壤（静态四项会被覆盖）</param>
        /// <param name="rng">
        /// 随机源。传了 → 结果可复现（测试、批量验证用）；不传或传 null → 自动新建，每次结果都不同。
        ///
        /// 【重要】早期实现在 rng 为 null 时会取静态指标范围的"下界"，
        /// 导致 Eh/BaseIon/As/Hg 每局都一样、文档的"随机生成"完全失效，
        /// 而且不报错、看不出来。现已改为自动新建随机源。
        /// 确实需要确定性取值时，请显式传 new System.Random(固定种子)。
        /// </param>
        public void Initialize(SoilState userInput, System.Random rng = null)
        {
            if (userInput != null && (!MathX.IsFinite(userInput.pH) || !MathX.IsFinite(userInput.OM)
                || !MathX.IsFinite(userInput.N) || !MathX.IsFinite(userInput.P) || !MathX.IsFinite(userInput.K)))
                throw new System.ArgumentException("土壤输入必须是有限数字。", nameof(userInput));
            // 不传随机源时自动新建，保证静态指标真的随机
            if (rng == null) rng = new System.Random();

            _soil = SoilGenerator.Generate(userInput, _config, rng);
            _stage = Stage.S1;
            _committedCount = 0;
            InvalidateCache();

            for (int i = 0; i < _stageFertility.Length; i++) _stageFertility[i] = 0f;
            for (int i = 0; i < _history.Length; i++) _history[i] = null;
            ResetWaterFactors();
        }

        /// <summary>仅重置流程，保留同一套配置。用于"开始新的模拟"。</summary>
        public void Reset()
        {
            _soil = null;
            _stage = Stage.S1;
            _committedCount = 0;
            InvalidateCache();
            for (int i = 0; i < _stageFertility.Length; i++) _stageFertility[i] = 0f;
            for (int i = 0; i < _history.Length; i++) _history[i] = null;
            ResetWaterFactors();
        }

        // ===================================================================
        //  查询：当前阶段的适宜度与建议
        // ===================================================================

        /// <summary>
        /// 计算当前土壤的五个适宜度。
        /// 结果会缓存，同一个阶段内重复调用不会重算（土壤或阶段变了自动失效）。
        /// </summary>
        public SuitabilityResult Evaluate()
        {
            if (_cachedFit == null)
                _cachedFit = SuitabilityResult.From(_soil, _config);
            return _cachedFit;
        }

        /// <summary>
        /// 取当前阶段的推荐方案。
        /// 若返回成功但 PlanTable.LastMatchUsedFallback 为 true，
        /// 说明该适宜度组合在文档里没有定义，走的是兜底规则 —— UI 可以提示出来。
        /// </summary>
        public RecommendedPlan Recommend()
        {
            if (_soil == null || IsFinished) return null;
            return _planTable.Match(_stage, Evaluate());
        }

        /// <summary>取当前阶段的长势等级（决定玉米贴图）。</summary>
        public GrowthLevel CurrentGrowth()
            => _config.Growth != null ? _config.Growth.Evaluate(Evaluate()) : GrowthLevel.Normal;

        // ===================================================================
        //  推进：提交本阶段水肥
        // ===================================================================

        /// <summary>
        /// 提交本阶段的水肥投入，结算并推进到下一阶段。
        /// 内部顺序很重要：先算适宜度（用旧土壤），再演算（得新土壤）。
        /// 顺序反了会导致"本阶段的成绩被本阶段的操作影响"，与文档不符。
        ///
        /// 【输入会被钳制】玩家填的数值会先过 SimConfig.Inputs 的边界（默认：下限 0、
        /// 上限 = 建议量 × 5），挡住负数施肥、超大灌溉这类会让指标爆表的输入。
        /// 返回的 StageResult.Applied 是【钳制后】的实际投入。
        /// </summary>
        /// <param name="applied">玩家填的水肥量（会被钳制进合法范围）</param>
        /// <returns>本阶段的完整结果；已结束时返回 null</returns>
        public StageResult Commit(FertilizerApplication applied)
        {
            if (_soil == null || IsFinished) return null;

            if (!MathX.IsFinite(applied.Irrigation) || !MathX.IsFinite(applied.Organic)
                || !MathX.IsFinite(applied.N) || !MathX.IsFinite(applied.P) || !MathX.IsFinite(applied.K))
                return null; // 拒绝整个事务，不写历史、不推进阶段。

            var stage = _stage;

            // 【快照】必须克隆。_soil 是引用，虽然 Advance 内部会 new 一个，
            // 但直接把引用存进 result 会留下"外部改了 result.SoilBefore 就改了
            // 内部状态"的隐患。这里明确存副本。
            var soilBefore = _soil.Clone();

            // 1) 用当前土壤算适宜度与肥力系数（走缓存，避免重复计算）
            var fit = Evaluate();
            float stageFertility = fit.StageFertility();
            _stageFertility[Stages.ToIndex(stage)] = stageFertility;

            // 2) 取本阶段建议（演算需要它的基准灌溉量作分母）
            var rec = _planTable.Match(stage, fit);

            // 3) 钳制玩家输入，挡住荒谬值
            var appliedSafe = _config.Inputs != null
                ? _config.Inputs.Clamp(applied, rec)
                : applied;

            // 4) 演算下一阶段土壤（用钳制后的投入）
            var soilAfter = SoilAdvance.Advance(soilBefore, stage, appliedSafe, rec, _config);

            // 5) 长势判定
            var growth = _config.Growth != null
                ? _config.Growth.Evaluate(fit)
                : GrowthLevel.Normal;

            // 6) 组装结果
            var result = new StageResult
            {
                Stage = stage,
                SoilBefore = soilBefore,
                SoilAfter = soilAfter,
                Fit = fit,
                StageFertility = stageFertility,
                Applied = appliedSafe,
                Recommended = rec,
                Growth = growth,
            };

            _history[Stages.ToIndex(stage)] = result;
            _committedCount++;

            // 7) 推进，并让适宜度缓存失效（土壤和阶段都变了）
            _soil = soilAfter;
            _stage = (stage == Stage.S4) ? Stage.Done : Stages.FromIndex(Stages.ToIndex(stage) + 1);
            InvalidateCache();

            return result;
        }

        // ===================================================================
        //  收益
        // ===================================================================

        /// <summary>
        /// 【挂起项 D2 的唯一改动点】四个阶段的水分因子 Fw,s（下标 0=S1）。
        ///
        /// v1：按文档 [248]/[249] —— 玩家照抄系统推荐灌溉量 ⇒ Fw,s 全为 1 ⇒ Fwater = 1。
        /// 公式确定后只要改 <see cref="YieldModel.SimplifiedWaterFactors"/>，
        /// 或在这里按实际灌溉量（可参考 StageResult.EffectiveWaterRatio()）写入 _stageWaterFactors，
        /// 收益报告与收益界面都不用动。
        /// 【只读语义】返回内部数组，请勿修改。
        /// </summary>
        public float[] StageWaterFactors() => _stageWaterFactors;

        /// <summary>把水分因子重置为 v1 的取值（全 1）。</summary>
        private void ResetWaterFactors()
        {
            for (int i = 0; i < _stageWaterFactors.Length; i++)
                _stageWaterFactors[i] = YieldModel.SimplifiedWaterFactor;
        }

        /// <summary>
        /// 产出收益报告。
        ///
        /// 【统一入口】这里不再自己拼公式，只把两样输入交给 <see cref="RevenueReport.Create"/>：
        ///   · 已结算阶段的肥力系数（_stageFertility 的前 _committedCount 个）
        ///   · 阶段水分因子 _stageWaterFactors
        /// 公式实现全工程只有一份（YieldModel）。
        ///
        /// 四阶段未结算完时按【已结算的那几个阶段】求平均，报告的 IsPartial = true ——
        /// 未结算阶段不会被按 0 摊进平均。
        /// </summary>
        /// <param name="area">土地面积（亩），只用于算总产量；0 表示未设置</param>
        public RevenueReport Revenue(float area = 0f)
        {
            var inputs = FertilizerApplication.None;
            for (int i = 0; i < _committedCount; i++)
            {
                var applied = _history[i].Applied;
                inputs.Irrigation += applied.Irrigation;
                inputs.Add(applied.Organic, applied.N, applied.P, applied.K);
            }
            return RevenueReport.Create(
                _stageFertility,
                _stageWaterFactors,
                _config.Yield,
                area,
                _soil,
                _committedCount,
                cumulativeInputPerMu: inputs);
        }

        // ===================================================================
        //  调试辅助
        // ===================================================================

        /// <summary>把四阶段过程导成一行行文本，用于日志与策划评审。</summary>
        public string DumpHistory()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("阶段 | 起始土壤 | 适宜度 | Ffert | 实际投入 | 建议方案 | 长势");
            sb.AppendLine(new string('-', 100));
            foreach (var s in Stages.All)
            {
                var h = GetHistory(s);
                if (h == null) { sb.AppendLine($"{Stages.ShortName(s)} | 未结算"); continue; }
                sb.AppendLine($"{Stages.ShortName(s)} | {h.SoilBefore}");
                sb.AppendLine($"     | {h.Fit}");
                sb.AppendLine($"     | 投入 {h.Applied}");
                sb.AppendLine($"     | 建议 {h.Recommended}");
                sb.AppendLine($"     | 长势 {GrowthLevels.DisplayName(h.Growth)} | " +
                              $"有效水比 {h.EffectiveWaterRatio():F3} 超灌比 {h.OverWaterRatio():F3}");
            }
            var r = Revenue();
            sb.AppendLine(new string('-', 100));
            sb.AppendLine($"收益：{r}");
            return sb.ToString();
        }
    }
}
