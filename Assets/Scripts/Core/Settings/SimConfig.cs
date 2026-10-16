// ---------------------------------------------------------------------------
//  SimConfig.cs —— 数值总配置（单一数据源）
// ---------------------------------------------------------------------------
//  作用：把文档里所有表格化的数值集中到一个对象里。
//        数值内核的每一处计算都从这里取值，绝不硬编码常数。
//
//  【为什么这很重要】
//    策划后续要反复调数值（Ymax、pH 系数、推荐方案…）。
//    只要所有数值都在这里，调整就变成"改配置"，工程侧零成本。
//    如果常数散落在各个方法里，每改一次都要重新编译 + 全流程回归。
//
//  【这同时是一份"配置规格说明"】
//    每个字段的注释都标了它来自文档哪张表，方便策划对照。
//
//  使用方式：
//      var cfg = SimConfig.CreateDefault();     // 拿到带文档全部数值的配置
//      cfg.Yield.YmaxGeo = 1200f;               // 改任意一项
//      var sim = new Simulation(cfg);
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>数值总配置。所有数值设定的唯一来源。</summary>
    public sealed class SimConfig
    {
        // ===================================================================
        //  一、适宜度曲线（文档 TABLE 3~7）
        // ===================================================================

        /// <summary>
        /// 五个动态指标的适宜度曲线。
        /// 下标与 SoilMetric 一致：0=pH, 1=OM, 2=N, 3=P, 4=K。
        /// 用 CurveOf(metric) 安全取值。
        /// </summary>
        public SuitabilityCurve[] Curves = new SuitabilityCurve[SoilMetrics.DynamicCount];

        /// <summary>按指标取曲线。静态指标返回 null。</summary>
        public SuitabilityCurve CurveOf(SoilMetric m)
        {
            int i = (int)m;
            if (i < 0 || i >= SoilMetrics.DynamicCount) return null;
            if (Curves == null || i >= Curves.Length) return null;
            return Curves[i];
        }

        // ===================================================================
        //  二、初始土壤范围（策划设定）
        // ===================================================================

        /// <summary>
        /// 动态五项的可输入范围 [min, max]。玩家在"土地基础数据设置"界面里调。
        /// 顺序与 SoilMetrics.Dynamic 一致。
        /// 策划给的平川台初始区间：
        ///   pH 5.8~8 / 腐殖丰度 15~28 / 速效氮 70~130 / 磷活度 8~25 / 钾储量 90~200
        /// 【注意】这些范围目前只有数值、没有单位。策划已确认 demo 阶段不定义单位。
        /// </summary>
        public StaticMetricRange[] DynamicInputRanges = new StaticMetricRange[SoilMetrics.DynamicCount];

        /// <summary>静态四项的随机范围（文档 TABLE 2）。</summary>
        public StaticMetricRange[] StaticRanges = new StaticMetricRange[SoilMetrics.StaticCount];

        /// <summary>按指标取输入范围。静态指标走 StaticRanges。</summary>
        public StaticMetricRange RangeOf(SoilMetric m)
        {
            if (SoilMetrics.IsDynamic(m))
            {
                int i = (int)m;
                if (DynamicInputRanges != null && i < DynamicInputRanges.Length)
                    return DynamicInputRanges[i];
            }
            else
            {
                int i = (int)m - SoilMetrics.DynamicCount;
                if (StaticRanges != null && i >= 0 && i < StaticRanges.Length)
                    return StaticRanges[i];
            }
            return null;
        }

        // ===================================================================
        //  三、地形与作物（文档 TABLE 0 / TABLE 1）
        // ===================================================================

        /// <summary>四套地形。v1 只有平川台可玩。</summary>
        public TerrainDef[] Terrains;

        /// <summary>四种作物。v1 只有玉米可玩。</summary>
        public CropDef[] Crops;

        // ===================================================================
        //  四、推荐水肥方案（文档 TABLE 13）
        // ===================================================================

        /// <summary>
        /// 推荐方案规则集合（11 条 + 可能的兜底行）。
        /// 【必须带 Priority】文档的表是有先后顺序的分支，不是互斥条件表。
        /// </summary>
        public PlanRule[] PlanRules;

        /// <summary>
        /// 判定条件里"任意项/全部"的默认阈值。文档统一是 0.6 和 0.8。
        /// 抽出来是为了万一策划要改"0.6/0.8 这条线"，只改这里。
        /// </summary>
        public float ThresholdLow = 0.6f;

        /// <summary>判定条件里的高档阈值。文档：0.8。</summary>
        public float ThresholdHigh = 0.8f;

        // ===================================================================
        //  五、阶段消耗（文档 TABLE 18）
        // ===================================================================

        /// <summary>四个阶段的养分消耗量，下标 0=S1。</summary>
        public StageConsume[] StageConsumes = new StageConsume[Stages.Count];

        /// <summary>取某阶段消耗量。</summary>
        public StageConsume ConsumeOf(Stage s)
        {
            int i = Stages.ToIndex(s);
            if (StageConsumes == null || i < 0 || i >= StageConsumes.Length) return null;
            return StageConsumes[i];
        }

        // ===================================================================
        //  六、演算系数（文档"补充"节）
        // ===================================================================

        /// <summary>指标演算系数。含已知有问题的 pH 系数，见 AdvanceCoef 注释。</summary>
        public AdvanceCoef Advance = new AdvanceCoef();

        /// <summary>
        /// 复合肥折算比例：每单位复合肥折出多少氮（=多少磷=多少钾）。
        /// 已确认 氮:磷:钾 = 1:1:1，所以每项是 1/3。
        /// 若改成 "复合肥 14 就是氮 14 磷 14 钾 14"，把这里改成 1.0。
        /// </summary>
        public float CompoundRatio = 1f / 3f;

        // ===================================================================
        //  七、长势与产量
        // ===================================================================

        /// <summary>长势判定规则（好 / 正常 / 差）。</summary>
        public GrowthRule Growth = new GrowthRule();

        /// <summary>产量模型参数（含占位 YmaxGeo）。</summary>
        public YieldConfig Yield = new YieldConfig();

        // ===================================================================
        //  七之二、玩家输入边界（工程健壮性，非设计约束）
        // ===================================================================

        /// <summary>
        /// 玩家输入的水肥边界。默认下限 0、上限取"建议量 × 5"。
        /// 用来挡住负数施肥、超大灌溉这类会让数值爆表的输入。
        /// 想关掉（比如做对比调试）就把 Enabled 设为 false。
        /// </summary>
        public InputLimits Inputs = new InputLimits();

        // ===================================================================
        //  八、工厂方法：带文档全部数值的默认配置
        // ===================================================================

        /// <summary>
        /// 生成一份按文档填好全部数值的配置。
        /// 这是唯一一处集中出现字面数值的地方 —— 改数值优先改这里或改配置资产。
        /// </summary>
        public static SimConfig CreateDefault()
        {
            var c = new SimConfig();

            // ---------- 1. 适宜度曲线（文档 TABLE 3~7）----------
            //  每行：B0(保底下界), B1(平台起), B2(平台止), B3(保底上界), Floor, Peak
            c.Curves[(int)SoilMetric.pH] = new SuitabilityCurve(4.5f, 5.5f,   7.0f,   8.5f);
            c.Curves[(int)SoilMetric.OM] = new SuitabilityCurve(10f,  20f,    35f,    50f);
            c.Curves[(int)SoilMetric.N]  = new SuitabilityCurve(20f,  70f,    110f,   150f);
            c.Curves[(int)SoilMetric.P]  = new SuitabilityCurve(2f,   10f,    25f,    40f);
            c.Curves[(int)SoilMetric.K]  = new SuitabilityCurve(40f,  100f,   200f,   280f);

            // ---------- 2. 动态指标输入范围（策划给的平川台区间）----------
            c.DynamicInputRanges[(int)SoilMetric.pH] = Range(SoilMetric.pH, 5.8f,  8.0f);
            c.DynamicInputRanges[(int)SoilMetric.OM] = Range(SoilMetric.OM, 15f,   28f);
            c.DynamicInputRanges[(int)SoilMetric.N]  = Range(SoilMetric.N,  70f,   130f);
            c.DynamicInputRanges[(int)SoilMetric.P]  = Range(SoilMetric.P,  8f,    25f);
            c.DynamicInputRanges[(int)SoilMetric.K]  = Range(SoilMetric.K,  90f,   200f);

            // ---------- 3. 静态指标随机范围（文档 TABLE 2）----------
            c.StaticRanges[0] = Range(SoilMetric.Eh,      400f,  550f);
            c.StaticRanges[1] = Range(SoilMetric.BaseIon, 7f,    16f);
            c.StaticRanges[2] = Range(SoilMetric.As,      5f,    15f);
            c.StaticRanges[3] = Range(SoilMetric.Hg,      0.03f, 0.12f);

            // ---------- 4. 阶段消耗量（文档 TABLE 18）----------
            c.StageConsumes[0] = Consume(Stage.S1,  3.33f, 2.25f, 0f);
            c.StageConsumes[1] = Consume(Stage.S2, 10f,    3.75f, 0f);
            c.StageConsumes[2] = Consume(Stage.S3,  6.67f, 3f,   25f);
            c.StageConsumes[3] = Consume(Stage.S4,  0f,    0f,   22.5f);

            // ---------- 5. 演算系数（文档"补充"节，全部为文档原值）----------
            c.Advance = new AdvanceCoef();   // 字段默认值就是文档值

            // ---------- 6. 复合肥折算（已确认 1:1:1）----------
            c.CompoundRatio = 1f / 3f;

            // ---------- 7. 地形（文档 TABLE 0）----------
            c.Terrains = new[]
            {
                new TerrainDef { Id = "flat_plain", DisplayName = "平川台", PlayableInV1 = true,
                    Description = "地势平缓开阔，土层较厚，排水条件适中，地下水位适宜，便于农业利用。" },
                new TerrainDef { Id = "low_wetland", DisplayName = "泽洼域",
                    Description = "地势低洼，地下水位较高，雨季容易积水，土壤湿度偏大，需做好排水防涝。" },
                new TerrainDef { Id = "steep_ridge", DisplayName = "崇岭阶",
                    Description = "坡度过大，土层较薄，地表径流较强，易发生水土流失，需加强植被保护。" },
                new TerrainDef { Id = "plateau", DisplayName = "高原坪",
                    Description = "海拔高，气压较低，昼夜温差较大，地势相对平缓，气候较凉爽。" },
            };

            // ---------- 8. 作物（文档 TABLE 1）----------
            c.Crops = new[]
            {
                new CropDef { Id = "corn", DisplayName = "玉米", PlayableInV1 = true,
                    Curves = c.Curves,
                    Description = "玉米适宜在地势平缓、土层较厚的地块生长，喜温喜光，生长期需要充足水分。平川台排水适中，有利于根系发育和籽粒成熟。" },
                new CropDef { Id = "beet", DisplayName = "甜菜",
                    Curves = c.Curves,   // TODO 后续为每种作物配独立曲线
                    Description = "甜菜属于根用作物，喜凉爽气候和疏松土壤，块根可用于制糖。泽洼域需改善排水，避免雨季积水影响根系生长和糖分积累。" },
                new CropDef { Id = "sweet_potato", DisplayName = "红薯",
                    Curves = c.Curves,
                    Description = "红薯喜温暖、光照充足的环境，块根可作粮食和加工原料。崇岭阶坡度较大，种植应选择梯田，覆盖并控制径流，减轻水土流失。" },
                new CropDef { Id = "potato", DisplayName = "土豆",
                    Curves = c.Curves,
                    Description = "土豆喜凉爽气候和疏松、排水良好的土壤，地下块茎可食用。高原坪昼夜温差较大，种植时应注意防霜和补水，保障块茎发育。" },
            };

            // ---------- 9. 推荐方案规则（文档 TABLE 13，共 11 条）----------
            c.PlanRules = BuildDefaultPlanRules();

            // ---------- 10. 长势与产量 ----------
            c.Growth = new GrowthRule { GoodAllAbove = 0.6f, BadAllBelow = 0.6f };
            // Ymax,geo = 733（文档：平川台+玉米），水分系数走 Jensen 加权（α3=0.4）
            c.Yield  = new YieldConfig { YmaxGeo = 733f, UseWeightedWater = true, SStatic = 1f };
            c.Inputs = new InputLimits();      // 下限 0 / 上限=建议量×5，具体见 InputLimits 注释

            return c;
        }

        // ===================================================================
        //  九、默认推荐方案表
        // ===================================================================

        /// <summary>
        /// 按文档 TABLE 13 构建 11 条规则。
        /// 判定顺序（Priority 越小越先）见 Docs/数值内核与设计表实现方案.md 第 4.2 节。
        /// </summary>
        public static PlanRule[] BuildDefaultPlanRules()
        {
            var list = new System.Collections.Generic.List<PlanRule>();

            // ---------------- S1 播种苗期（文档行 1~2）----------------
            list.Add(Rule(Stage.S1, 1,
                new PlanCondition { Type = PlanCondType.AnyBelow, Threshold = 0.6f },
                "有机肥 + 少量氮磷肥",
                "苗期需水低；有机质决定出苗，少量氮磷促根，不施钾肥",
                new FertilizerAmounts { Organic = 80f, N = 4f, P = 3f }, 25f, isFallback: true));

            list.Add(Rule(Stage.S1, 2,
                new PlanCondition { Type = PlanCondType.AllAboveAndCount,
                                    Threshold = 0.6f, Count = 3, CountThreshold = 0.8f },
                "少量有机肥",
                "本底土壤优，控水防烧苗",
                new FertilizerAmounts { Organic = 40f, N = 1.5f }, 20f));

            // ---------------- S2 营养旺长期（文档行 3~5）----------------
            list.Add(Rule(Stage.S2, 1,
                new PlanCondition { Type = PlanCondType.Below, Scope = new[]{ SoilMetric.N }, Threshold = 0.6f },
                "氮肥为主 + 磷肥",
                "茎叶快长，氮需求高峰；磷促根系",
                new FertilizerAmounts { N = 12f, P = 5f }, 35f, isFallback: true));

            list.Add(Rule(Stage.S2, 2,
                new PlanCondition { Type = PlanCondType.Below, Scope = new[]{ SoilMetric.N }, Threshold = 0.8f },
                "中量氮肥 + 少量磷",
                "轻度缺氮，防徒长",
                new FertilizerAmounts { N = 7f, P = 3f }, 30f));

            list.Add(Rule(Stage.S2, 3,
                new PlanCondition { Type = PlanCondType.AllAbove, Threshold = 0.8f },
                "少量氮肥",
                "少追肥，控旺防倒伏",
                new FertilizerAmounts { N = 3f }, 25f));

            // ---------------- S3 生殖关键期（文档行 6~8）----------------
            //  文档条件写"fN 或 fP 或 fK＜0.6" -> 限定指标集合的 AnyBelow
            list.Add(Rule(Stage.S3, 1,
                new PlanCondition { Type = PlanCondType.AnyBelow,
                                    Scope = new[]{ SoilMetric.N, SoilMetric.P, SoilMetric.K },
                                    Threshold = 0.6f },
                "氮磷钾协同追肥",
                "水分权重 0.4，胁迫最敏感；钾促穗粒、抗逆",
                new FertilizerAmounts { N = 8f, P = 4f, K = 10f }, 45f, isFallback: true));

            //  文档条件写"任意项 0.6~0.8" -> 已确认"任意项 = 至少一项"，区间半开 [0.6, 0.8)
            list.Add(Rule(Stage.S3, 2,
                new PlanCondition { Type = PlanCondType.AnyInRange, Lo = 0.6f, Hi = 0.8f },
                "氮磷钾复合肥",
                "轻度缺素，维持水分，忌积水",
                new FertilizerAmounts { Compound = 14f }, 40f));

            list.Add(Rule(Stage.S3, 3,
                new PlanCondition { Type = PlanCondType.AllAbove, Threshold = 0.8f },
                "仅少量钾肥",
                "只补钾，防贪青",
                new FertilizerAmounts { K = 5f }, 32f));

            // ---------------- S4 灌浆成熟期（文档行 9~11）----------------
            list.Add(Rule(Stage.S4, 1,
                new PlanCondition { Type = PlanCondType.Below, Scope = new[]{ SoilMetric.K }, Threshold = 0.6f },
                "钾肥为主，禁大量氮",
                "光合产物转籽粒；氮多致贪青倒伏",
                new FertilizerAmounts { K = 9f }, 22f, isFallback: true));

            list.Add(Rule(Stage.S4, 2,
                new PlanCondition { Type = PlanCondType.Below, Scope = new[]{ SoilMetric.K }, Threshold = 0.8f },
                "少量钾肥",
                "促进籽粒灌浆成熟",
                new FertilizerAmounts { K = 5f }, 16f));

            list.Add(Rule(Stage.S4, 3,
                new PlanCondition { Type = PlanCondType.AllAbove, Threshold = 0.8f },
                "不施肥",
                "控水促成熟",
                FertilizerAmounts.None, 12f));

            return list.ToArray();
        }

        // ===================================================================
        //  十、构造小工具
        // ===================================================================

        private static StaticMetricRange Range(SoilMetric m, float min, float max)
            => new StaticMetricRange { Metric = m, Min = min, Max = max };

        private static StageConsume Consume(Stage s, float n, float p, float k)
            => new StageConsume { Stage = s, N = n, P = p, K = k };

        private static PlanRule Rule(Stage stage, int priority, PlanCondition cond,
                                     string label, string physiology,
                                     FertilizerAmounts amounts, float irrigation,
                                     bool isFallback = false)
        {
            return new PlanRule
            {
                Stage = stage,
                Priority = priority,
                Condition = cond,
                Label = label,
                Physiology = physiology,
                Amounts = amounts,
                BaseIrrigation = irrigation,
                IsFallback = isFallback,
            };
        }

        // ===================================================================
        //  十一、合法性校验
        // ===================================================================

        /// <summary>
        /// 校验配置是否自洽。导入 CSV 后必须调用，能挡住绝大多数低级错误。
        ///
        /// 【errors 与 warnings 的区别】
        ///   errors   —— 配置坏了，必须修（边界写反、缺表、值为负等）
        ///   warnings —— 配置能用，但文档规则表不完备（存在判定空洞）。
        ///               这些空洞由兜底规则接管，不会崩，但应该反馈给策划补规则。
        /// </summary>
        /// <param name="errors">收集到的错误</param>
        /// <param name="warnings">收集到的警告（可为 null）</param>
        /// <returns>errors 为空则返回 true</returns>
        public bool Validate(System.Collections.Generic.List<string> errors,
                             System.Collections.Generic.List<string> warnings = null)
        {
            if (errors == null) errors = new System.Collections.Generic.List<string>();

            // 曲线边界
            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                var curve = c_Curve(i);
                if (curve == null) { errors.Add($"缺少 {SoilMetrics.Dynamic[i]} 的适宜度曲线"); continue; }
                if (!curve.Validate(out string err))
                    errors.Add($"{SoilMetrics.Dynamic[i]} 曲线非法：{err}");
            }

            // 静态范围
            for (int i = 0; i < SoilMetrics.StaticCount; i++)
            {
                var r = (StaticRanges != null && i < StaticRanges.Length) ? StaticRanges[i] : null;
                if (r == null) { errors.Add($"缺少 {SoilMetrics.Static[i]} 的随机范围"); continue; }
                if (!(r.Min < r.Max)) errors.Add($"{SoilMetrics.Static[i]} 范围非法：[{r.Min}, {r.Max}]");
            }

            // 阶段消耗
            for (int i = 0; i < Stages.Count; i++)
            {
                if (StageConsumes == null || i >= StageConsumes.Length || StageConsumes[i] == null)
                    errors.Add($"缺少 {Stages.All[i]} 的阶段消耗量");
            }

            // 推荐方案
            if (PlanRules == null || PlanRules.Length == 0)
            {
                errors.Add("推荐方案规则为空");
            }
            else
            {
                foreach (var stage in Stages.All)
                {
                    bool hasRule = false, hasFallback = false;
                    foreach (var r in PlanRules)
                    {
                        if (r == null || r.Stage != stage) continue;
                        hasRule = true;
                        if (r.IsFallback) hasFallback = true;
                    }
                    if (!hasRule) errors.Add($"{Stages.ShortName(stage)} 没有任何推荐方案规则");
                    else if (!hasFallback)
                        warnings?.Add($"{Stages.ShortName(stage)} 没有标记兜底规则，" +
                                      "当规则表出现空洞时会退回最高优先级规则（可能给出不合适的建议）");
                }

                // 覆盖性：规则表本身是否完备（空洞由兜底接管，故记为警告）
                if (warnings != null)
                {
                    var table = new PlanTable(this);
                    // 用 8 档降采样：8^5 ≈ 3.3 万组，够发现空洞，又不会让编辑器卡住
                    if (!table.CheckCoverage(8, out var gaps))
                    {
                        foreach (var g in gaps) warnings.Add(g);
                    }
                }
            }

            // 复合肥折算比例
            if (CompoundRatio <= 0f) errors.Add("CompoundRatio 必须大于 0");

            // 输入边界
            if (Inputs != null)
            {
                if (Inputs.IrrigationMultiple <= 0f)
                    errors.Add("Inputs.IrrigationMultiple 必须大于 0");
                if (Inputs.FertilizerMultiple <= 0f)
                    errors.Add("Inputs.FertilizerMultiple 必须大于 0");
                if (Inputs.AbsoluteMaxIrrigation <= 0f)
                    errors.Add("Inputs.AbsoluteMaxIrrigation 必须大于 0");
                if (Inputs.AbsoluteMaxFertilizer <= 0f)
                    errors.Add("Inputs.AbsoluteMaxFertilizer 必须大于 0");
            }

            // 产量参数
            if (Yield == null) errors.Add("缺少 YieldConfig");
            else if (Yield.YmaxGeo <= 0f) errors.Add("YmaxGeo 必须大于 0");

            return errors.Count == 0;
        }

        private SuitabilityCurve c_Curve(int i)
            => (Curves != null && i < Curves.Length) ? Curves[i] : null;
    }
}
