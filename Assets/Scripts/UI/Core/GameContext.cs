using System;
using UnityEngine;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 跨屏共享的数据。
    ///
    /// 【两类数据要分清】
    ///   1. <b>玩家输入</b>：土地类型、植物类型、六项基础数据 —— 这些是纯数据字段，
    ///      模拟开始前由各选择/输入界面写入。
    ///   2. <b>内核状态</b>：九项指标、阶段、长势、收益 —— 这些**不在本类里存**，
    ///      一律转发给 <see cref="GameSession"/>（它包着 Engineer.Core 的 Simulation）。
    ///
    ///   为什么要这样分：如果本类自己再存一份指标，就会出现"两份数据谁是真的"的问题 ——
    ///   土壤演变之后两边不同步，界面上显示的和实际算的不一致。
    ///   所以指标一律**实时问内核**，本类只保留"还没开始模拟时"的输入值。
    ///
    /// 【兼容性说明】公开字段名（Ph / OrganicMatter / StageDosage ...）刻意保持不变，
    ///   因为 UI.unity 场景的 YAML 里引用了这些序列化名，改名会导致 Inspector 赋值丢失。
    /// </summary>
    [Serializable]
    public class GameContext
    {
        /// <summary>阶段总数：S1 播种苗期 → S5 收获期。</summary>
        public const int MaxStage = 5;

        /// <summary>五个阶段的名称，下标 0 对应 S1。</summary>
        public static readonly string[] StageNames =
        {
            "播种苗期",
            "营养旺长期",
            "生殖关键期",
            "灌浆成熟期",
            "收获期",
        };

        // ------------------------------------------------------------------
        // 桥接层（由 GameFlowController 在 Awake 时注入）
        // ------------------------------------------------------------------

        [NonSerialized] private GameSession session;

        /// <summary>数值内核桥接层。未注入时为 null（编辑期预览用）。</summary>
        public GameSession Session
        {
            get { return session; }
        }

        /// <summary>是否已经接上数值内核。</summary>
        public bool HasSession
        {
            get { return session != null; }
        }

        /// <summary>模拟是否已经开始（玩家已填完六项基础数据）。</summary>
        public bool IsSimulationStarted
        {
            get { return session != null && session.IsStarted; }
        }

        /// <summary>由 <see cref="GameFlowController"/> 注入桥接层。</summary>
        public void BindSession(GameSession value)
        {
            session = value;
        }

        // ------------------------------------------------------------------
        // 选择结果
        // ------------------------------------------------------------------

        /// <summary>土地类型 Id（Demo 只开放「平川台」）。</summary>
        public string LandTypeId = "pingchuantai";

        /// <summary>土地类型显示名。</summary>
        public string LandTypeName = "平川台";

        /// <summary>植物类型 Id。</summary>
        public string PlantTypeId = "corn";

        /// <summary>植物类型显示名。</summary>
        public string PlantTypeName = "玉米";

        // ------------------------------------------------------------------
        // 土地基础数据设置（六项）—— 玩家输入
        // ------------------------------------------------------------------

        /// <summary>土地面积（亩）。</summary>
        public float LandAreaMu = 10f;

        /// <summary>1 酸衡值 (pH)。默认取平川台区间中值 6.9。</summary>
        public float Ph = 6.9f;

        /// <summary>2 腐殖丰度。默认取区间中值 21.5。</summary>
        public float OrganicMatter = 21.5f;

        /// <summary>3 速效氮元。默认取区间中值 100。</summary>
        public float AvailableNitrogen = 100f;

        /// <summary>4 磷活度。默认取区间中值 16.5。</summary>
        public float LabilePhosphorus = 16.5f;

        /// <summary>5 钾储量。默认取区间中值 145。</summary>
        public float Potassium = 145f;

        /// <summary>
        /// 静态指标（氧还势 / 土骨离子 / 毒砷潜势 / 汞积量）。
        /// 【注意】模拟开始后这四个值由内核随机生成并保持不变，
        /// 本字段只用于"模拟还没开始时"的界面占位显示。
        /// 要取真实值请用 <see cref="GetIndicator"/>。
        /// </summary>
        public float[] StaticIndicators = { 470f, 11f, 9f, 0.07f };

        // ------------------------------------------------------------------
        // 流程
        // ------------------------------------------------------------------

        /// <summary>
        /// 当前阶段，1~5。
        /// 接了内核之后，这个值应当跟随 <see cref="GameSession.UiStage"/> 走；
        /// 保留字段是为了兼容场景里已有的序列化引用。
        /// </summary>
        public int StageIndex = 1;

        /// <summary>
        /// 本阶段水肥施用量（顺序：灌溉量、有机肥、钾肥、氮肥、磷肥）。
        /// 【注意】钾在氮前面，构造 FertilizerApplication 时别接错。
        /// </summary>
        public readonly float[] StageDosage = new float[5];

        /// <summary>当前阶段名。</summary>
        public string CurrentStageName
        {
            get
            {
                // 接上内核后以内核阶段为准，避免两边不同步
                if (session != null && session.IsStarted) return StageMap.DisplayName(session.UiStage);
                return StageName(StageIndex);
            }
        }

        /// <summary>是否是最后一个阶段（此时农场界面的按钮变成「收益计算」）。</summary>
        public bool IsLastStage
        {
            get
            {
                if (session != null && session.IsStarted) return StageMap.IsHarvest(session.UiStage);
                return StageIndex >= MaxStage;
            }
        }

        public static string StageName(int stageIndex)
        {
            var index = Mathf.Clamp(stageIndex, 1, MaxStage) - 1;
            return StageNames[index];
        }

        // ------------------------------------------------------------------
        // 指标读写
        // ------------------------------------------------------------------

        /// <summary>
        /// 按 <see cref="SoilIndicators"/> 的下标读取指标值。
        ///
        /// 【行为】模拟已开始 → 从数值内核读真实值（会随水肥演变）；
        ///         模拟未开始 → 读本类的玩家输入字段。
        /// </summary>
        public float GetIndicator(int indicatorIndex)
        {
            if (session != null && session.IsStarted)
                return session.GetIndicator(indicatorIndex, ReadInputIndicator(indicatorIndex));
            return ReadInputIndicator(indicatorIndex);
        }

        /// <summary>
        /// 按 <see cref="SoilIndicators"/> 的下标写入指标值。
        ///
        /// 【重要】模拟开始后**不要**用这个方法改土壤 —— 土壤只能由
        /// <see cref="GameSession.Commit"/> 按水肥演算推进，否则内核状态与界面会不一致。
        /// 本方法现在的用途是：模拟未开始时由玩家输入界面写入初始值；
        /// 已开始时调用会被忽略并给出警告。
        /// </summary>
        public void SetIndicator(int indicatorIndex, float value)
        {
            if (session != null && session.IsStarted)
            {
                Debug.LogWarning("[GameContext] 模拟已开始，SetIndicator 被忽略。" +
                                 "土壤只能通过 GameSession.Commit 按水肥演算推进。");
                return;
            }

            switch (indicatorIndex)
            {
                case 0: Ph = value; break;
                case 1: OrganicMatter = value; break;
                case 2: AvailableNitrogen = value; break;
                case 3: LabilePhosphorus = value; break;
                case 4: Potassium = value; break;
                default:
                    var staticIndex = indicatorIndex - SoilIndicators.DynamicCount;
                    if (staticIndex >= 0 && staticIndex < StaticIndicators.Length)
                        StaticIndicators[staticIndex] = value;
                    break;
            }
        }

        /// <summary>只读玩家输入字段，不做内核转发。</summary>
        private float ReadInputIndicator(int indicatorIndex)
        {
            switch (indicatorIndex)
            {
                case 0: return Ph;
                case 1: return OrganicMatter;
                case 2: return AvailableNitrogen;
                case 3: return LabilePhosphorus;
                case 4: return Potassium;
                default:
                    var staticIndex = indicatorIndex - SoilIndicators.DynamicCount;
                    if (staticIndex >= 0 && staticIndex < StaticIndicators.Length)
                        return StaticIndicators[staticIndex];
                    return 0f;
            }
        }

        // ------------------------------------------------------------------
        // 模拟启动
        // ------------------------------------------------------------------

        /// <summary>
        /// 用本类里的六项输入启动数值内核。
        /// 由「土地基础数据设置」界面在玩家点下一步时调用。
        /// </summary>
        /// <returns>是否成功启动（没有接上桥接层时返回 false）</returns>
        public bool BeginSimulation(System.Random rng = null)
        {
            if (session == null)
            {
                Debug.LogWarning("[GameContext] 没有注入 GameSession，无法启动数值内核。");
                return false;
            }

            var input = GameSession.BuildInput(Ph, OrganicMatter, AvailableNitrogen,
                                               LabilePhosphorus, Potassium);
            session.Begin(input, rng);
            StageIndex = 1;
            return true;
        }

        /// <summary>把当前内核阶段同步到 <see cref="StageIndex"/>（界面切换时调用）。</summary>
        public void SyncStageFromSession()
        {
            if (session != null && session.IsStarted) StageIndex = session.UiStage;
        }

        // ------------------------------------------------------------------
        // 重置
        // ------------------------------------------------------------------

        /// <summary>恢复到「开始新的模拟」时的初始值。</summary>
        public void Reset()
        {
            LandTypeId = "pingchuantai";
            LandTypeName = "平川台";
            PlantTypeId = "corn";
            PlantTypeName = "玉米";

            LandAreaMu = 10f;
            Ph = 6.9f;
            OrganicMatter = 21.5f;
            AvailableNitrogen = 100f;
            LabilePhosphorus = 16.5f;
            Potassium = 145f;
            StaticIndicators = new float[] { 470f, 11f, 9f, 0.07f };

            StageIndex = 1;
            for (var i = 0; i < StageDosage.Length; i++) StageDosage[i] = 0f;

            // 内核也一起复位，否则「开始新的模拟」后土壤还是上一局的
            if (session != null) session.Restart();
        }
    }
}
