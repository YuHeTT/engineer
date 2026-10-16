using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 农场主界面（文档 image7 / image11，S1–S5 五个阶段共用同一个屏）。
    ///
    /// 【已接入数值内核】
    ///   * 左上「操作提示」：仍按阶段走文案（策划文案待补），但会在建议走了兜底规则时提示
    ///   * 右上「当前阶段」：跟随内核阶段（S1~S4 / 收获期）
    ///   * 右上「状态」：由 <see cref="GrowthLevel"/> 决定（长势良好 / 正常 / 较差），
    ///     不再是"白盒占位：正常生长"
    ///   * 下方「所有数据」：9 项指标**从内核读**，会随每阶段水肥投入变化。
    ///     静态 4 项由内核在第一阶段随机生成后保持不变。
    ///   * 右下主按钮：S1–S4 是「开始分析」，收获期是「收益计算」
    /// </summary>
    public class FarmStageScreen : PlaceholderScreen
    {
        [Header("农场 HUD")]
        [Tooltip("左上角操作提示。")]
        public TMP_Text hintText;

        [Tooltip("右上角：当前阶段标题，例如「S1 播种苗期」。")]
        public TMP_Text stageTitleText;

        [Tooltip("右上角：状态说明，例如「长势良好」。")]
        public TMP_Text stageStatusText;

        [Tooltip("下方「所有数据」面板里的 9 个数值文本，顺序与 SoilIndicators 一致。")]
        public TMP_Text[] indicatorValueTexts = new TMP_Text[SoilIndicators.Count];

        [Tooltip("右下角主按钮：S1–S4 是「开始分析」，收获期是「收益计算」。")]
        public Button primaryButton;

        [Tooltip("主按钮上的文字。")]
        public TMP_Text primaryButtonLabel;

        [Header("选项")]
        [Tooltip("是否在数据面板上标出「相对上一阶段的变化」（用 ▲▼ 表示）。")]
        public bool showDelta = false;

        private const string PrimaryLabelAnalyze = "开始分析";
        private const string PrimaryLabelRevenue = "收益计算";

        /// <summary>上一阶段显示过的数值，用于显示变化量。下标与 SoilIndicators 一致。</summary>
        private readonly float[] lastValues = new float[SoilIndicators.Count];
        private readonly bool[] hasLast = new bool[SoilIndicators.Count];

        protected override bool OwnsButton(Button button) => button != null && button == primaryButton;
        private float nextRefresh;
        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.15f;
            if (Flow != null) Flow.SyncProbe();
            RefreshFromContext();
        }

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            primaryButtonLabel = ResolveLabel(primaryButtonLabel, primaryButton);
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(OnPrimaryClicked);
                primaryButton.onClick.AddListener(OnPrimaryClicked);
            }

            foreach (var value in indicatorValueTexts)
            {
                if (value == null) continue;
                value.enableWordWrapping = false;
                value.enableAutoSizing = true;
                value.fontSizeMin = 10f;
                value.fontSizeMax = 18f;
                value.alignment = TextAlignmentOptions.MidlineRight;
            }
            RefreshFromContext();
        }

        /// <summary>用当前内核状态刷新「当前阶段 / 状态 / 所有数据」。</summary>
        public void RefreshFromContext()
        {
            var context = Flow != null ? Flow.Context : null;
            if (context == null) return;

            var session = context.Session;
            var started = session != null && session.IsStarted;

            // 阶段编号：接上内核后以内核为准
            var uiStage = started ? session.UiStage : context.StageIndex;
            var isHarvest = started ? StageMap.IsHarvest(uiStage) : context.IsLastStage;

            if (stageTitleText != null)
                stageTitleText.text = "S" + uiStage + " " + StageMap.DisplayName(uiStage);

            // 状态：由长势决定
            if (stageStatusText != null)
            {
                stageStatusText.text = started
                    ? GrowthLevels.DisplayName(session.Growth)
                    : "尚未开始模拟";
            }

            var probe = Flow != null ? Flow.Probe : null;
            if (hintText != null)
                hintText.text = BuildHint(uiStage, session) + (isHarvest ? "" :
                    "\n本阶段巡航 " + ((probe != null ? probe.Coverage : 0f) * 100f).ToString("0")
                    + "% · " + (Flow.CanAnalyze ? "探测数据已确认，可开始分析" : "完成100%巡航后确认数据；未探测项不参与分析"));

            if (primaryButtonLabel != null)
                primaryButtonLabel.text = isHarvest ? PrimaryLabelRevenue : PrimaryLabelAnalyze;
            if (primaryButton != null)
                primaryButton.interactable = isHarvest || Flow.CanAnalyze;

            RefreshIndicators(context, session, started);
        }

        /// <summary>刷新 9 项指标。已开始模拟时全部从内核读。</summary>
        private void RefreshIndicators(GameContext context, GameSession session, bool started)
        {
            if (indicatorValueTexts == null) return;

            for (var i = 0; i < indicatorValueTexts.Length && i < SoilIndicators.Count; i++)
            {
                if (indicatorValueTexts[i] == null) continue;

                // GetIndicator 内部已经做了"开始了读内核、没开始读输入"的分支
                var probe = Flow != null ? Flow.Probe : null;
                bool observed = probe != null && probe.IsCurrent(session) && probe.IsRevealed(i);
                var value = observed ? probe.GetProbe(i) : context.GetIndicator(i);
                var text = started && !session.IsFinished
                    ? (observed ? SoilIndicators.Format(i, value) + "（探测）" : "待巡航采集")
                    : SoilIndicators.Format(i, value);

                if (showDelta && started && hasLast[i])
                {
                    var delta = value - lastValues[i];
                    if (Mathf.Abs(delta) > 0.005f)
                        text += delta > 0f ? "  ▲" : "  ▼";
                }

                indicatorValueTexts[i].text = text;

                // 静态四项不参与演变，不需要变化量
                if (!SoilIndicators.IsStatic[i])
                {
                    lastValues[i] = value;
                    hasLast[i] = true;
                }
            }
        }

        private void OnPrimaryClicked()
        {
            if (Flow == null) return;

            var context = Flow.Context;
            var session = context != null ? context.Session : null;
            var isHarvest = session != null && session.IsStarted
                ? StageMap.IsHarvest(session.UiStage)
                : context != null && context.IsLastStage;

            Flow.SyncProbe();
            // 右侧只负责分析；未确认采集时不再充当第二个“机器人检测”入口。
            if (!isHarvest && !Flow.CanAnalyze) return;
            Flow.GoTo(isHarvest ? GameState.Revenue : GameState.Analyze);
        }

        /// <summary>
        /// 操作提示。
        /// 【待策划】下面的文案是按文档各阶段的玩法描述写的临时版本，
        /// 策划文案定稿后替换这里即可（不影响其它逻辑）。
        /// </summary>
        private static string BuildHint(int uiStage, GameSession session)
        {
            var hint = HintForStage(uiStage);

            // 建议走了兜底规则时提示（见 Docs/漏洞与风险清单.md G2）
            if (session != null && session.IsStarted && session.RecommendUsedFallback)
                hint += "\n（注意：当前土壤状况在策划文档里没有对应的建议规则，系统使用了兜底方案。）";

            return hint;
        }

        private static string HintForStage(int uiStage)
        {
            switch (uiStage)
            {
                case 1: return "S1 播种苗期：请先让机器人巡航采集土壤数据，再进入「开始分析」查看水肥建议。";
                case 2: return "S2 营养旺长期：氮需求进入高峰。可先巡航更新数据，再确定本阶段水肥量。";
                case 3: return "S3 生殖关键期：对水分胁迫最敏感，注意不要过量灌溉；钾肥促穗粒。";
                case 4: return "S4 灌浆成熟期：以钾为主、控制氮肥，避免贪青倒伏。";
                default: return "收获期：本阶段无需操作，点「收益计算」查看最终产量与收益。";
            }
        }

        private static TMP_Text ResolveLabel(TMP_Text assigned, Button button)
        {
            if (assigned != null) return assigned;
            if (button == null) return null;
            return button.GetComponentInChildren<TMP_Text>(true);
        }
    }
}
