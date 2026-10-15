using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 机器人操控界面（文档 image8 手动巡航 / image9 自动巡航）。
    ///
    /// 【本版已接入真实 3D 巡航】
    ///   覆盖率**不再是"点一次涨一格"的模拟**，而是直接读
    ///   <see cref="FarmFieldGrid.Coverage"/> —— 也就是小车在农场场景里
    ///   真实走过、探测过的面积比例。
    ///
    ///   探测数据来自 <see cref="FarmProbeState"/>：
    ///     · 原数据  = 进入本屏时抄一份的"玩家以为的值"
    ///     · 探测数据 = 带误差的实测值，随覆盖率逐项揭示，未揭示显示「无」
    ///
    /// 【两个按钮做什么】
    ///   手动巡航 → 切到农场场景，让玩家用 WASD 开小车
    ///   自动巡航 → 切到农场场景，小车自己按弓字形走完
    ///   巡航过程中本屏的覆盖率条会实时涨（因为读的是同一个 FarmFieldGrid）
    ///
    /// 【误差模型】见 <see cref="FarmProbeState"/>：幅度随覆盖率从 10% 收敛到 5%。
    /// </summary>
    public class RobotCruiseScreen : PlaceholderScreen
    {
        [Header("机器人 HUD")]
        [Tooltip("顶部操作提示。")]
        public TMP_Text hintText;

        [Tooltip("巡航进度填充条（Image 的 Filled 模式）。")]
        public Image coverageFill;

        [Tooltip("巡航覆盖率文案。")]
        public TMP_Text coverageText;

        [Tooltip("未覆盖面积文案。")]
        public TMP_Text uncoveredText;

        [Tooltip("左侧「原数据」的 9 个数值文本。")]
        public TMP_Text[] sourceValueTexts = new TMP_Text[SoilIndicators.Count];

        [Tooltip("右侧「探测数据」的 9 个数值文本，未探测到之前显示「无」。")]
        public TMP_Text[] probeValueTexts = new TMP_Text[SoilIndicators.Count];

        [Tooltip("「更新数据」按钮：巡航覆盖率达标后才可点。")]
        public Button updateButton;

        [Tooltip("手动巡航按钮。")]
        public Button manualButton;

        [Tooltip("自动巡航按钮。")]
        public Button autoButton;

        [Header("覆盖率阈值（与策划约定）")]
        [Tooltip("达到这个覆盖率就可以更新数据、进入分析。")]
        [Range(0f, 1f)] public float analyzeThreshold = 0.8f;

        [Tooltip("达到这个覆盖率算「完整数据」，误差收敛到最小。")]
        [Range(0f, 1f)] public float completeThreshold = 1.0f;

        [Header("巡航请求")]
        [Tooltip("点「手动巡航」时是否切到农场场景让玩家开车。\n" +
                 "关掉的话就留在本屏，只靠「一键跑完」推进（白盒模式）。")]
        public bool switchToFarmOnCruise = true;

        [Tooltip("自动巡航在农场场景里跑完之后，是否自动切回本屏。")]
        public bool returnAfterAutoCruise = true;

        private FarmFieldGrid grid;
        private FarmCruiseController cruise;

        /// <summary>当前覆盖率 0~1（来自 3D 巡航网格）。</summary>
        public float Coverage
        {
            get { return grid != null ? grid.Coverage : 0f; }
        }

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            Resolve();

            // 开始一次新的探测：抄一份"原数据"
            var probe = Flow != null ? Flow.Probe : null;
            if (probe != null)
            {
                var session = Flow.Context != null ? Flow.Context.Session : null;
                probe.Begin(session);
            }

            if (manualButton != null)
            {
                manualButton.onClick.RemoveListener(OnManualCruise);
                manualButton.onClick.AddListener(OnManualCruise);
            }

            if (autoButton != null)
            {
                autoButton.onClick.RemoveListener(OnAutoCruise);
                autoButton.onClick.AddListener(OnAutoCruise);
            }

            if (updateButton != null)
            {
                updateButton.onClick.RemoveListener(OnUpdateData);
                updateButton.onClick.AddListener(OnUpdateData);
            }

            RefreshSourceValues();
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        private void Resolve()
        {
            if (grid == null) grid = FindObjectOfType<FarmFieldGrid>();
            if (cruise == null) cruise = FindObjectOfType<FarmCruiseController>();
        }

        private void Update()
        {
            // 覆盖率会随小车移动实时变化，所以每帧同步一下（代价很低）
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        /// <summary>把 3D 网格的覆盖率同步进探测状态，并刷新探测数据列。</summary>
        private void SyncProbeFromGrid()
        {
            var probe = Flow != null ? Flow.Probe : null;
            if (probe == null || !probe.IsActive) return;

            Resolve();
            var coverage = grid != null ? grid.Coverage : 0f;
            if (Mathf.Approximately(coverage, probe.Coverage)) return;

            probe.SetCoverage(coverage);

            if (probeValueTexts == null) return;
            for (var i = 0; i < probeValueTexts.Length && i < SoilIndicators.Count; i++)
            {
                if (probeValueTexts[i] == null) continue;
                probeValueTexts[i].text = probe.IsRevealed(i)
                    ? SoilIndicators.Format(i, probe.GetProbe(i))
                    : SoilIndicators.NoDataText;
            }
        }

        // ==================================================================
        //  巡航
        // ==================================================================

        /// <summary>手动巡航：切到农场场景，玩家用 WASD 开小车。</summary>
        public void OnManualCruise()
        {
            Resolve();
            if (cruise == null || grid == null)
            {
                Debug.LogWarning("[UI] 当前场景里没有农场巡航组件，" +
                                 "改用「一键跑完」占位。载入 Farm 场景后即可真正巡航。", this);
                RunInstantFallback();
                return;
            }

            cruise.ResetToStart();
            cruise.StartManual();

            if (switchToFarmOnCruise && Flow != null)
                Flow.GoTo(GameState.FarmStage);
        }

        /// <summary>自动巡航：切到农场场景，小车自己走完。</summary>
        public void OnAutoCruise()
        {
            Resolve();
            if (cruise == null || grid == null)
            {
                Debug.LogWarning("[UI] 当前场景里没有农场巡航组件，" +
                                 "改用「一键跑完」占位。载入 Farm 场景后即可真正巡航。", this);
                RunInstantFallback();
                return;
            }

            cruise.ResetToStart();
            cruise.StartAuto();

            if (switchToFarmOnCruise && Flow != null)
                Flow.GoTo(GameState.FarmStage);
        }

        /// <summary>没有农场场景时的兜底：直接把覆盖标记补齐。</summary>
        private void RunInstantFallback()
        {
            var probe = Flow != null ? Flow.Probe : null;
            if (probe == null) return;

            probe.SetCoverage(1f);
            probe.RevealAll();
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        // ==================================================================
        //  更新数据
        // ==================================================================

        /// <summary>
        /// 「更新数据」：把探测结果确认下来，回到农场。
        ///
        /// 【注意】**不写回内核土壤**。内核里的土壤是权威数据，只能由水肥演算推进。
        /// 探测值保存在 <see cref="FarmProbeState"/> 里，分析界面读它。
        /// 这样"探测"这一步既有效果（分析用带误差的值），又不会污染数值内核。
        /// </summary>
        public void OnUpdateData()
        {
            var probe = Flow != null ? Flow.Probe : null;

            if (probe != null && probe.Coverage < analyzeThreshold)
            {
                Debug.Log(string.Format("[UI] 覆盖率只有 {0:0.0}%，还没到 {1:0}%，暂时不能更新数据。",
                    probe.Coverage * 100f, analyzeThreshold * 100f), this);
                return;
            }

            // 达标后把静态四项也揭示出来（它们不随覆盖率逐项揭示，跟着"确认"一起给）
            if (probe != null) probe.RevealAll();

            Debug.Log(string.Format("[UI] 数据已确认。覆盖率 {0:0.0}%，" +
                                    "探测误差幅度 {1:0.#}%~{2:0.#}%。",
                (probe != null ? probe.Coverage : 0f) * 100f,
                FarmProbeState.MinError * 100f, FarmProbeState.MaxError * 100f), this);

            if (Flow != null) Flow.Back();
        }

        // ==================================================================
        //  显示
        // ==================================================================

        /// <summary>左侧「原数据」。</summary>
        public void RefreshSourceValues()
        {
            var probe = Flow != null ? Flow.Probe : null;
            var context = Flow != null ? Flow.Context : null;

            for (var i = 0; i < sourceValueTexts.Length && i < SoilIndicators.Count; i++)
            {
                if (sourceValueTexts[i] == null) continue;

                float v;
                if (probe != null && probe.IsActive) v = probe.GetSource(i);
                else if (context != null) v = context.GetIndicator(i);
                else { sourceValueTexts[i].text = SoilIndicators.NoDataText; continue; }

                sourceValueTexts[i].text = SoilIndicators.Format(i, v);
            }
        }

        private void RefreshCruiseState()
        {
            var probe = Flow != null ? Flow.Probe : null;
            var progress = probe != null && probe.IsActive
                ? probe.Coverage
                : (grid != null ? grid.Coverage : 0f);

            if (coverageFill != null) coverageFill.fillAmount = progress;
            if (coverageText != null)
                coverageText.text = "已覆盖 " + Mathf.RoundToInt(progress * 100f) + "%";

            var context = Flow != null ? Flow.Context : null;
            var area = context != null ? context.LandAreaMu : 0f;
            if (uncoveredText != null)
                uncoveredText.text = "未覆盖面积 " + (area * (1f - progress)).ToString("0.0") + " 亩";

            var canAnalyze = progress >= analyzeThreshold;
            if (updateButton != null) updateButton.interactable = canAnalyze;

            // 自动巡航跑完（或没有 3D 场景时）就不再允许重复点
            var autoDone = cruise != null &&
                           (cruise.Mode == FarmCruiseController.CruiseMode.Auto ||
                            cruise.Mode == FarmCruiseController.CruiseMode.Finished);
            if (manualButton != null) manualButton.interactable = !autoDone || !canAnalyze;
            if (autoButton != null) autoButton.interactable = !autoDone || !canAnalyze;

            if (hintText != null)
            {
                if (!canAnalyze)
                {
                    hintText.text =
                        "机器人探测：请选择手动或者自动巡航进行数据收集。\n" +
                        "· 手动巡航：切到农场，WASD / 方向键开小车，Q/E 转向\n" +
                        "· 自动巡航：小车按弓字形自己走遍全田\n" +
                        string.Format("覆盖率到 {0:0}% 即可「更新数据」。当前 {1:0}%",
                            analyzeThreshold * 100f, progress * 100f);
                }
                else
                {
                    hintText.text = "巡航完成：请确认探测数据后点击「更新数据」。";
                }
            }
        }
    }
}
