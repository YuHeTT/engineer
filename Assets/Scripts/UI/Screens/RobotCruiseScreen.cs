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
    ///   手动巡航 → 停留在巡航进度页，让玩家驾驶小车
    ///   自动巡航 → 停留在巡航进度页，小车自己走遍全田
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

        [Tooltip("自动巡航的实时进度与剩余时间（可选，留空就不显示）。")]
        public TMP_Text autoProgressText;

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
        [Range(0f, 1f)] public float analyzeThreshold = 1f;

        [Tooltip("达到这个覆盖率算「完整数据」，误差收敛到最小。")]
        [Range(0f, 1f)] public float completeThreshold = 1.0f;

        [Header("巡航请求")]
        // 兼容旧场景序列化；巡航固定显示 RobotCruise 进度页。
        [HideInInspector] public bool switchToFarmOnCruise = false;

        [Tooltip("自动巡航在农场场景里跑完之后，是否自动切回本屏。")]
        public bool returnAfterAutoCruise = true;

        private FarmFieldGrid grid;
        private FarmCruiseController cruise;

        private float displayedCoverage = -1f;

        /// <summary>当前覆盖率 0~1（来自 3D 巡航网格）。</summary>
        public float Coverage
        {
            get { return grid != null ? grid.Coverage : 0f; }
        }

        protected override bool OwnsButton(Button button) => button != null
            && (button == manualButton || button == autoButton || button == updateButton);

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            Resolve();
            analyzeThreshold = FarmProbeState.RequiredCoverage;
            completeThreshold = FarmProbeState.RequiredCoverage;

            // 每次进本屏都解除按钮禁用状态。
            //
            // 【注意】**不要**在这里调 cruise.ResetToStart()。
            //   那会做两件坏事：
            //     ① 进入本屏时里程/覆盖率被清零 —— 玩家来回切页面就丢进度
            //     ② 如果此刻巡航正在进行，Mode 会被打回 Idle，巡航当场结束
            //   真正的重置放在玩家**主动点击**「手动/自动巡航」的时候。

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

            displayedCoverage = -1f;
            RefreshSourceValues();
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        public override void OnExit()
        {
            // 中途离开出舱页时取消开场，防止其它页面被镜头接管。
            if (cruise != null && cruise.IsDeploying) cruise.Stop();
            base.OnExit();
        }

        private void Resolve()
        {
            // 【注意】FindObjectOfType 开销不小，而这里每帧都会被调，
            //   所以只在**还没找到**的时候才去查（找到就缓存住）。
            if (grid == null) grid = FindObjectOfType<FarmFieldGrid>();
            if (cruise == null) cruise = FindObjectOfType<FarmCruiseController>();
            if (!resolved) { resolved = true; EnsureResolved(); }
        }

        /// <summary>字段自动绑定只做一次，避免每帧走 transform.Find。</summary>
        private bool resolved;

        /// <summary>
        /// 按节点名补齐未绑定的字段。
        /// 【为什么】场景里的 `RobotCruiseScreen` 组件是早先配好的，
        /// 后加的字段（如 `autoProgressText`）在场景里是空的 —— 靠这里自动找，
        /// 就不必手工再拖一次。找不到就跳过，不影响其它功能。
        /// </summary>
        public void EnsureResolved()
        {
            if (autoProgressText == null)
            {
                var t = transform.Find("HUD/TopBand/ProgressGroup/AutoProgress");
                if (t != null) autoProgressText = t.GetComponent<TMP_Text>();
            }
            if (coverageText == null)
            {
                var t = transform.Find("HUD/TopBand/ProgressGroup/CoverageText");
                if (t != null) coverageText = t.GetComponent<TMP_Text>();
            }
            if (coverageFill == null)
            {
                var t = transform.Find("HUD/TopBand/ProgressGroup/CoverageBar/Fill");
                if (t != null) coverageFill = t.GetComponent<Image>();
            }
            if (uncoveredText == null)
            {
                var t = transform.Find("HUD/TopBand/ProgressGroup/UncoveredText");
                if (t != null) uncoveredText = t.GetComponent<TMP_Text>();
            }
            if (hintText == null)
            {
                var t = transform.Find("HUD/TopBand/Hint");
                if (t != null) hintText = t.GetComponent<TMP_Text>();
            }

            ConfigureProgressBar();
            // 移除白盒中心说明，保留实际 3D 视野。
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
                if (label.text != null && label.text.Contains("3D") && label.text.Contains("占位"))
                    label.gameObject.SetActive(false);

            // 9 项原数据 / 9 项探测数据
            if (sourceValueTexts == null || sourceValueTexts.Length < SoilIndicators.Count)
                sourceValueTexts = new TMP_Text[SoilIndicators.Count];
            if (probeValueTexts == null || probeValueTexts.Length < SoilIndicators.Count)
                probeValueTexts = new TMP_Text[SoilIndicators.Count];

            for (var i = 0; i < SoilIndicators.Count; i++)
            {
                if (sourceValueTexts[i] == null)
                {
                    var t = transform.Find("HUD/SourcePanel/Values/Value_" + (i + 1) + "/Value");
                    if (t != null) sourceValueTexts[i] = t.GetComponent<TMP_Text>();
                }
                if (probeValueTexts[i] == null)
                {
                    var t = transform.Find("HUD/ProbePanel/Values/Value_" + (i + 1) + "/Value");
                    if (t != null) probeValueTexts[i] = t.GetComponent<TMP_Text>();
                }
            }

            if (updateButton == null)
            {
                var t = transform.Find("HUD/ActionRow/Button_UpdateData");
                if (t != null) updateButton = t.GetComponent<Button>();
            }
            if (manualButton == null)
            {
                var t = transform.Find("HUD/ActionRow/Button_ManualCruise");
                if (t != null) manualButton = t.GetComponent<Button>();
            }
            if (autoButton == null)
            {
                var t = transform.Find("HUD/ActionRow/Button_AutoCruise");
                if (t != null) autoButton = t.GetComponent<Button>();
            }
        }

        private void ConfigureProgressBar()
        {
            if (coverageFill == null) return;
            // 没有 Sprite 的 Image 不会按 fillAmount 裁切，因此用真实 RectTransform 宽度。
            coverageFill.type = Image.Type.Simple;
            coverageFill.raycastTarget = false;
            coverageFill.color = new Color(0.12f, 0.78f, 0.95f, 1f);
            var background = coverageFill.transform.parent.GetComponent<Image>();
            if (background != null)
            {
                background.color = new Color(0.035f, 0.09f, 0.15f, 0.95f);
                background.raycastTarget = false;
            }
            if (coverageText != null) coverageText.color = Color.white;
            if (uncoveredText != null) uncoveredText.color = new Color(.8f, .9f, 1f);
            if (autoProgressText != null) autoProgressText.color = Color.white;
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
            probe.SetCoverage(coverage);
            if (Mathf.Approximately(displayedCoverage, probe.Coverage)) return;
            displayedCoverage = probe.Coverage;

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

        /// <summary>手动巡航：显示巡航进度页，同时驾驶小车。</summary>
        public void OnManualCruise()
        {
            // 不切屏的按钮：只出声 + 闪一下，不收起、不排队。
            UiClickFeedback.Play(manualButton, false, null);

            Resolve();
            if (cruise != null && cruise.IsDeploying) return;

            if (cruise == null || grid == null)
            {
                Debug.LogWarning("[UI] 当前场景里没有农场巡航组件，" +
                                 "请等待 Farm 场景加载；不会生成虚假探测数据。", this);
                RunInstantFallback();
                return;
            }

            if (Flow != null) { Flow.Probe.Reset(); Flow.Probe.Begin(Flow.Session); }
            cruise.ResetToStart();
            cruise.StartManual();
            Debug.Log("[巡航] StartManual 完成，mode=" + cruise.Mode, this);

            if (Flow != null) Flow.GoTo(GameState.RobotCruise);
            displayedCoverage = -1f;
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        /// <summary>自动巡航：显示巡航进度页，小车自己走完。</summary>
        public void OnAutoCruise()
        {
            UiClickFeedback.Play(autoButton, false, null);

            Resolve();
            if (cruise != null && cruise.IsDeploying) return;

            if (cruise == null || grid == null)
            {
                Debug.LogWarning("[UI] 当前场景里没有农场巡航组件，" +
                                 "请等待 Farm 场景加载；不会生成虚假探测数据。", this);
                RunInstantFallback();
                return;
            }

            if (Flow != null) { Flow.Probe.Reset(); Flow.Probe.Begin(Flow.Session); }
            cruise.ResetToStart();
            cruise.StartAuto();
            Debug.Log("[巡航] StartAuto 完成，mode=" + cruise.Mode +
                      "，路点数=" + cruise.WaypointCount, this);

            if (Flow != null) Flow.GoTo(GameState.RobotCruise);
            displayedCoverage = -1f;
            SyncProbeFromGrid();
            RefreshCruiseState();
        }

        /// <summary>没有农场场景时的兜底：直接把覆盖标记补齐。</summary>
        private void RunInstantFallback()
        {
            if (hintText != null) hintText.text = "农场巡航组件未加载，请返回农场等待加载后重试。";
            Debug.LogWarning("[巡航] 缺少巡航组件，不能伪造覆盖率或进入分析。", this);
        }

        // ==================================================================
        //  更新数据
        // ==================================================================

        /// <summary>
        /// 「更新数据」：确认探测快照并留在本页，玩家自行返回农场后开始分析。
        ///
        /// 【注意】**不写回内核土壤**。内核里的土壤是权威数据，只能由水肥演算推进。
        /// 探测值保存在 <see cref="FarmProbeState"/> 里，分析界面读它。
        /// 这样"探测"这一步既有效果（分析用带误差的值），又不会污染数值内核。
        /// </summary>
        public void OnUpdateData()
        {
            UiClickFeedback.Play(updateButton, false, null);

            var probe = Flow != null ? Flow.Probe : null;

            if (Flow == null || !Flow.ConfirmProbe())
            {
                if (hintText != null) hintText.text = "请完成本阶段 100% 巡航后再确认数据。";
                return;
            }


            Debug.Log(string.Format("[UI] 数据已确认。覆盖率 {0:0.0}%，" +
                                    "探测误差幅度 {1:0.#}%~{2:0.#}%。\n" +
                                    "  探测值已存入 FarmProbeState，分析适宜度与水肥建议将使用这份已确认的探测数据。",
                (probe != null ? probe.Coverage : 0f) * 100f,
                FarmProbeState.MinError * 100f, FarmProbeState.MaxError * 100f), this);

            if (cruise != null) cruise.Stop();
            RefreshCruiseState();
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

            progress = Mathf.Clamp01(progress);
            if (coverageFill != null)
            {
                coverageFill.fillAmount = progress;
                var rect = coverageFill.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = new Vector2(progress, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                coverageFill.color = progress >= FarmProbeState.RequiredCoverage
                    ? new Color(.2f, .85f, .45f, 1f) : new Color(.12f, .78f, .95f, 1f);
            }
            if (coverageText != null)
                coverageText.text = "巡航进度 " + (progress >= FarmProbeState.RequiredCoverage ? "100" : Mathf.FloorToInt(progress * 100f).ToString()) + "%";

            var context = Flow != null ? Flow.Context : null;
            var area = context != null ? context.LandAreaMu : 0f;
            if (uncoveredText != null)
                uncoveredText.text = "未覆盖面积 " + (area * (1f - progress)).ToString("0.0") + " 亩";

            var canAnalyze = progress >= FarmProbeState.RequiredCoverage;
            var confirmed = Flow != null && Flow.CanAnalyze;
            if (updateButton != null) updateButton.interactable = canAnalyze && !confirmed;
            // 更新之后仍可开始新一轮巡航；主动重扫会清空覆盖并撤销上一份确认。
            if (manualButton != null) manualButton.interactable = cruise == null || !cruise.IsDeploying;
            if (autoButton != null) autoButton.interactable = cruise == null || !cruise.IsDeploying;

            if (hintText != null)
            {
                if (cruise != null && cruise.IsDeploying)
                {
                    hintText.text = "房车正在打开机器人舱，小车出舱中，请稍候……";
                }
                else if (confirmed)
                {
                    hintText.text = "数据已更新。点击「返回农场」，再点击「开始分析」查看结果。\n" +
                        "也可以重新手动或自动巡航，采集新一轮数据；重新采集后需再次完成覆盖并更新数据。";
                }
                else if (!canAnalyze)
                {
                    hintText.text =
                        (cruise != null && cruise.IsCruising ? "机器人探测：巡航中，右上角显示实际采集进度。\n" : "机器人探测：请选择手动或者自动巡航进行数据收集。\n") +
                        "· 手动巡航：Q/E 转向、W/S 前进后退（也能用方向键）\n" +
                        "· 自动巡航：小车按弓字形自己走遍全田\n" +
                        string.Format("覆盖率到 {0:0}% 即可「更新数据」。当前 {1:0}%",
                            analyzeThreshold * 100f, progress * 100f);
                }
                else
                {
                    hintText.text = "巡航完成：请确认探测数据后点击「更新数据」。";
                }
            }

            RefreshAutoProgress();
        }

        /// <summary>
        /// 显示自动巡航的实时进度与预计剩余时间。
        ///
        /// 【为什么需要】原来自动巡航速度是 4 m/s，全田 136 m 要走 **34 秒**，
        /// 而且界面上没有任何"还剩多久"的提示，体验上就是"卡住了"。
        /// 现在速度提到 15 m/s（约 9 秒），并显示剩余时间。
        /// </summary>
        private void RefreshAutoProgress()
        {
            if (autoProgressText == null) return;
            Resolve();

            if (cruise == null || cruise.Mode != FarmCruiseController.CruiseMode.Auto)
            {
                autoProgressText.text = string.Empty;
                return;
            }

            autoProgressText.text = string.Format("自动巡航中　剩余约 {0:0} 秒", cruise.EstimateRemainingSeconds());
        }
    }
}
