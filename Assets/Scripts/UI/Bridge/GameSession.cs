// ---------------------------------------------------------------------------
//  GameSession.cs —— UI 与数值内核之间的唯一桥梁
// ---------------------------------------------------------------------------
//  作用：把 Engineer.Core 的 Simulation 包起来，对外只暴露"界面视角"的方法。
//
//  【为什么需要这一层，而不是让 UI 直接用 Simulation】
//    1. 阶段编号不一样：UI 用 1~5，内核用 S1~S4+Done → 换算集中在这里和 StageMap
//    2. UI 不应该拿到 Initialize / Reset 之外的写入口，也不该看到 PlanTable 之类的内核细节
//    3. 将来换内核实现（或加存档）时，只要这一层的接口不变，界面代码不用动
//    4. 界面测试可以塞一个假的 GameSession
//
//  【数据流】
//
//    LandParamsScreen 填六项
//          │  Begin(input)
//          ▼
//      GameSession ──持有──> Simulation（Engineer.Core）
//          │                        │
//          │  Soil / Fit /          │  Commit(applied)
//          │  Recommend() /         │  ← AnalyzeScreen 提交水肥
//          │  Growth()              │
//          ▼                        ▼
//      FarmStageScreen        土壤演变到下一阶段
//      AnalyzeScreen
//      RobotCruiseScreen
//      RevenueScreen ── Revenue(area) ──> RevenueReport
//
//  【注意】本类不引用 UnityEngine —— 它是纯数据转发层，
//  这样即使将来把 UI 换成别的框架，这一层也能留用。
// ---------------------------------------------------------------------------

using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>UI 与数值内核之间的桥梁。由 GameFlowController 创建并注入 GameContext。</summary>
    public sealed class GameSession
    {
        private readonly SimConfig config;
        private readonly Simulation simulation;

        public GameSession(SimConfig config)
        {
            this.config = config ?? SimConfig.CreateDefault();
            simulation = new Simulation(this.config);
        }

        // ==================================================================
        //  配置与只读视图
        // ==================================================================

        /// <summary>数值配置。界面需要读输入区间、作物名等。</summary>
        public SimConfig Config
        {
            get { return config; }
        }

        /// <summary>
        /// 只读视图。界面若要直接读内核状态，走这个接口，
        /// 这样编译期就调不到 Commit / Initialize 之类的写操作。
        /// </summary>
        public ISimulationView View
        {
            get { return simulation.View; }
        }

        // ==================================================================
        //  生命周期
        // ==================================================================

        /// <summary>是否已经开始模拟（玩家填完六项基础数据后为 true）。</summary>
        public bool IsStarted
        {
            get { return simulation.CurrentSoil != null; }
        }

        /// <summary>是否已经全部结束（四阶段结算完）。</summary>
        public bool IsFinished
        {
            get { return simulation.IsFinished; }
        }

        /// <summary>
        /// 开始模拟。由「土地基础数据设置」界面在玩家填完六项后调用。
        /// 静态四项（氧还势 / 土骨离子 / 毒砷潜势 / 汞积量）由内核按配置随机生成，
        /// 所以传进来的 input 只需要填动态五项。
        /// </summary>
        /// <param name="input">玩家输入的土壤（动态五项）</param>
        /// <param name="rng">可选随机源；不传则自动新建（每局不同）</param>
        public void Begin(SoilState input, System.Random rng = null)
        {
            simulation.Initialize(input, rng);
        }

        /// <summary>把当前 UI 的六项输入合成一份 SoilState。"土地基础数据设置"用。</summary>
        public static SoilState BuildInput(float ph, float organicMatter,
                                           float availableNitrogen, float labilePhosphorus,
                                           float potassium)
        {
            return new SoilState
            {
                pH = ph,
                OM = organicMatter,
                N = availableNitrogen,
                P = labilePhosphorus,
                K = potassium,
                // 静态四项留 0，Initialize 会按 SimConfig.StaticRanges 覆盖
            };
        }

        /// <summary>重置到"还没开始"的状态。用于「开始新的模拟」。</summary>
        public void Restart()
        {
            simulation.Reset();
        }

        // ==================================================================
        //  当前状态查询（给界面显示）
        // ==================================================================

        /// <summary>当前阶段（UI 编号 1~5）。</summary>
        public int UiStage
        {
            get { return StageMap.ToUi(simulation.CurrentStage); }
        }

        /// <summary>当前阶段（内核枚举）。收获期返回 Stage.Done。</summary>
        public Stage CoreStage
        {
            get { return simulation.CurrentStage; }
        }

        /// <summary>当前土壤。未开始时返回 null。</summary>
        public SoilState Soil
        {
            get { return simulation.CurrentSoil; }
        }

        /// <summary>取某项指标当前值。未开始时返回 <paramref name="fallback"/>。</summary>
        public float GetIndicator(int indicatorIndex, float fallback)
        {
            var soil = simulation.CurrentSoil;
            if (soil == null) return fallback;
            return soil[StageMap.ToMetric(indicatorIndex)];
        }

        /// <summary>当前五个适宜度。未开始时返回 null。</summary>
        public SuitabilityResult Fit
        {
            get { return simulation.CurrentSoil != null ? simulation.Evaluate() : null; }
        }

        /// <summary>当前阶段的长势等级（决定玉米贴图）。未开始时返回 Normal。</summary>
        public GrowthLevel Growth
        {
            get
            {
                return simulation.CurrentSoil != null
                    ? simulation.CurrentGrowth()
                    : GrowthLevel.Normal;
            }
        }

        /// <summary>
        /// 当前阶段的推荐方案。未开始或已结束时返回 null。
        /// 【注意】调用后 <see cref="RecommendUsedFallback"/> 才有意义。
        /// </summary>
        public RecommendedPlan Recommend()
        {
            return simulation.CurrentSoil != null && !simulation.IsFinished
                ? simulation.Recommend()
                : null;
        }

        /// <summary>
        /// 上一次 <see cref="Recommend"/> 是否走了兜底规则。
        /// true 表示这个适宜度组合在策划文档里没有定义 —— 界面可以据此提示。
        /// 见 Docs/漏洞与风险清单.md 的 G2。
        /// </summary>
        public bool RecommendUsedFallback
        {
            get { return simulation.PlanTable.LastMatchUsedFallback; }
        }

        // ==================================================================
        //  推进
        // ==================================================================

        /// <summary>
        /// 提交本阶段水肥，结算并推进到下一阶段。
        /// 输入会被 <see cref="SimConfig.Inputs"/> 的边界钳制（负数归零、上限=建议量×倍数）。
        /// </summary>
        /// <returns>本阶段完整结果；已结束或未开始时返回 null</returns>
        public StageResult Commit(FertilizerApplication applied)
        {
            return simulation.Commit(applied);
        }

        /// <summary>按 UI 的输入框顺序（灌溉 / 有机肥 / 钾 / 氮 / 磷）提交。</summary>
        /// <remarks>
        /// 【字段顺序提醒】GameContext.StageDosage 的顺序是
        ///   0 灌溉量、1 有机肥、2 钾肥、3 氮肥、4 磷肥
        /// —— **钾在氮前面**。构造 FertilizerApplication 时别接错，
        /// 接错不会编译报错，只是结果不对。
        /// </remarks>
        public StageResult CommitFromDosage(float[] dosage)
        {
            if (dosage == null || dosage.Length < 5) return null;

            var applied = new FertilizerApplication(
                irrigation: dosage[0],
                organic: dosage[1],
                k: dosage[2],       // 钾
                n: dosage[3],       // 氮
                p: dosage[4]);      // 磷

            return Commit(applied);
        }

        // ==================================================================
        //  收益
        // ==================================================================

        /// <summary>产出收益报告。</summary>
        /// <param name="areaMu">土地面积（亩），用于算总产量</param>
        public RevenueReport Revenue(float areaMu)
        {
            return simulation.Revenue(areaMu);
        }

        // ==================================================================
        //  调试
        // ==================================================================

        /// <summary>导出全过程文本，排查用。</summary>
        public string DumpHistory()
        {
            return simulation.DumpHistory();
        }
    }
}
