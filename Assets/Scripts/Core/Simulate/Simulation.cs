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
    public sealed class Simulation
    {
        private readonly SimConfig _config;
        private readonly PlanTable _planTable;

        /// <summary>四个阶段各自的肥力系数，下标 0=S1。收益计算取它的平均。</summary>
        private readonly float[] _stageFertility = new float[Stages.Count];

        /// <summary>四个阶段的完整结果，便于回看与导出。</summary>
        private readonly StageResult[] _history = new StageResult[Stages.Count];

        private SoilState _soil;
        private Stage _stage = Stage.S1;
        private int _committedCount;

        public Simulation(SimConfig config)
        {
            _config = config ?? SimConfig.CreateDefault();
            _planTable = new PlanTable(_config);
        }

        // ===================================================================
        //  对外只读状态
        // ===================================================================

        /// <summary>数值配置。</summary>
        public SimConfig Config => _config;

        /// <summary>推荐方案表。</summary>
        public PlanTable PlanTable => _planTable;

        /// <summary>当前阶段。Done 表示四阶段已全部结算。</summary>
        public Stage CurrentStage => _stage;

        /// <summary>当前土壤状态（只读语义，请勿直接改）。</summary>
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

        /// <summary>全部历史结果（按顺序）。收益界面用来列四阶段参数。</summary>
        public StageResult[] History => _history;

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
            // 不传随机源时自动新建，保证静态指标真的随机
            if (rng == null) rng = new System.Random();

            _soil = SoilGenerator.Generate(userInput, _config, rng);
            _stage = Stage.S1;
            _committedCount = 0;

            for (int i = 0; i < _stageFertility.Length; i++) _stageFertility[i] = 0f;
            for (int i = 0; i < _history.Length; i++) _history[i] = null;
        }

        /// <summary>仅重置流程，保留同一套配置。用于"开始新的模拟"。</summary>
        public void Reset()
        {
            _soil = null;
            _stage = Stage.S1;
            _committedCount = 0;
            for (int i = 0; i < _stageFertility.Length; i++) _stageFertility[i] = 0f;
            for (int i = 0; i < _history.Length; i++) _history[i] = null;
        }

        // ===================================================================
        //  查询：当前阶段的适宜度与建议
        // ===================================================================

        /// <summary>计算当前土壤的五个适宜度。UI 每次进入阶段时调一次即可。</summary>
        public SuitabilityResult Evaluate()
        {
            return SuitabilityResult.From(_soil, _config);
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
        /// </summary>
        /// <param name="applied">玩家填的水肥量</param>
        /// <returns>本阶段的完整结果；已结束时返回 null</returns>
        public StageResult Commit(FertilizerApplication applied)
        {
            if (_soil == null || IsFinished) return null;

            var stage = _stage;
            var soilBefore = _soil;

            // 1) 用当前土壤算适宜度与肥力系数
            var fit = SuitabilityResult.From(soilBefore, _config);
            float stageFertility = fit.StageFertility();
            _stageFertility[Stages.ToIndex(stage)] = stageFertility;

            // 2) 取本阶段建议（演算需要它的基准灌溉量作分母）
            var rec = _planTable.Match(stage, fit);

            // 3) 演算下一阶段土壤
            var soilAfter = SoilAdvance.Advance(soilBefore, stage, applied, rec, _config);

            // 4) 长势判定
            var growth = _config.Growth != null
                ? _config.Growth.Evaluate(fit)
                : GrowthLevel.Normal;

            // 5) 组装结果
            var result = new StageResult
            {
                Stage = stage,
                SoilBefore = soilBefore,
                SoilAfter = soilAfter,
                Fit = fit,
                StageFertility = stageFertility,
                Applied = applied,
                Recommended = rec,
                Growth = growth,
            };

            _history[Stages.ToIndex(stage)] = result;
            _committedCount++;

            // 6) 推进
            _soil = soilAfter;
            _stage = (stage == Stage.S4) ? Stage.Done : Stages.FromIndex(Stages.ToIndex(stage) + 1);

            return result;
        }

        // ===================================================================
        //  收益
        // ===================================================================

        /// <summary>产出收益报告。四阶段未结算完时按已结算的部分计算，并在日志中体现。</summary>
        public RevenueReport Revenue(float area = 0f)
        {
            float fert = YieldModel.OverallFertility(_stageFertility);
            float water = 1f;                                        // v1：Fwater = 1
            float stat = _config.Yield != null ? _config.Yield.SStatic : 1f;
            float y = YieldModel.Yield(fert, water, stat, _config.Yield);

            return new RevenueReport
            {
                StageFertility = (float[])_stageFertility.Clone(),
                OverallFertility = fert,
                WaterFactor = water,
                StaticFactor = stat,
                Yield = y,
                YmaxGeo = _config.Yield != null ? _config.Yield.YmaxGeo : 0f,
                FinalSoil = _soil != null ? _soil.Clone() : null,
                Area = area,
            };
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
