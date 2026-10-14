using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 机器人操控界面（文档 image8 手动巡航 / image9 自动巡航）。
    ///
    /// 白盒行为（全部是假的、只用来验证交互骨架，不参与任何数值计算）：
    ///   * 左侧「原数据」：进入时从 GameContext 抄一份当前值；
    ///   * 右侧「探测数据」：默认显示「无」，随巡航推进逐项揭示；
    ///   * 光标/进度：手动巡航每点一次推进一格（5 格），自动巡航一次填满；
    ///   * 「更新数据」：把探测结果写回 GameContext，供后续「开始分析」使用。
    /// </summary>
    public class RobotCruiseScreen : PlaceholderScreen
    {
        private const int ProbeSteps = 5;

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

        [Tooltip("「更新数据」按钮：巡航未完成前不可点。")]
        public Button updateButton;

        [Tooltip("手动巡航按钮。")]
        public Button manualButton;

        [Tooltip("自动巡航按钮。")]
        public Button autoButton;

        private int revealedCount;
        private readonly float[] probeValues = new float[SoilIndicators.Count];

        /// <summary>当前已探测到的指标个数。</summary>
        public int RevealedCount
        {
            get { return revealedCount; }
        }

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            revealedCount = 0;
            for (var i = 0; i < probeValues.Length; i++) probeValues[i] = 0f;

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
            RefreshCruiseState();
        }

        /// <summary>手动巡航：每点一次向前推进一格，并揭示下一个指标。</summary>
        public void OnManualCruise()
        {
            if (revealedCount >= SoilIndicators.Count)
            {
                LogPlaceholder("巡航已经跑完");
                return;
            }

            RevealNext();
            RefreshCruiseState();
        }

        /// <summary>自动巡航：一次跑完全部指标。</summary>
        public void OnAutoCruise()
        {
            while (revealedCount < SoilIndicators.Count) RevealNext();
            RefreshCruiseState();
        }

        /// <summary>把探测结果写回共享数据，并回到农场界面。</summary>
        public void OnUpdateData()
        {
            if (revealedCount < SoilIndicators.Count)
            {
                LogPlaceholder("巡航还没跑完，暂时不更新数据");
                return;
            }

            var context = Flow != null ? Flow.Context : null;
            if (context != null)
            {
                for (var i = 0; i < SoilIndicators.Count; i++) context.SetIndicator(i, probeValues[i]);
            }

            if (Flow != null) Flow.Back();
        }

        /// <summary>左侧「原数据」取当前共享值。</summary>
        public void RefreshSourceValues()
        {
            var context = Flow != null ? Flow.Context : null;

            for (var i = 0; i < sourceValueTexts.Length && i < SoilIndicators.Count; i++)
            {
                if (sourceValueTexts[i] == null) continue;
                sourceValueTexts[i].text = context != null
                    ? SoilIndicators.Format(i, context.GetIndicator(i))
                    : SoilIndicators.NoDataText;
            }
        }

        private void RevealNext()
        {
            var index = revealedCount;
            revealedCount++;

            // 白盒阶段的假数据：以共享值为基准做 ±3% 的抖动，只为让「探测」看得出变化。
            var context = Flow != null ? Flow.Context : null;
            var basis = context != null ? context.GetIndicator(index) : 10f;
            var jitter = 0.97f + 0.06f * index / Mathf.Max(1, SoilIndicators.Count - 1);
            probeValues[index] = basis * jitter;

            if (probeValueTexts[index] != null)
                probeValueTexts[index].text = SoilIndicators.Format(index, probeValues[index]);
        }

        private void RefreshCruiseState()
        {
            var progress = SoilIndicators.Count == 0 ? 0f : (float)revealedCount / SoilIndicators.Count;

            if (coverageFill != null) coverageFill.fillAmount = progress;
            if (coverageText != null) coverageText.text = "已覆盖 " + Mathf.RoundToInt(progress * 100f) + "%";

            var context = Flow != null ? Flow.Context : null;
            var area = context != null ? context.LandAreaMu : 0f;
            if (uncoveredText != null) uncoveredText.text = "未覆盖面积 " + (area * (1f - progress)).ToString("0.0") + " 亩";

            if (updateButton != null) updateButton.interactable = revealedCount >= SoilIndicators.Count;
            if (manualButton != null) manualButton.interactable = revealedCount < SoilIndicators.Count;
            // 自动巡航一次跑完：跑完后按钮置灰，避免重复点击（正式版这里是「正在检测…」的运行态）。
            if (autoButton != null) autoButton.interactable = revealedCount < SoilIndicators.Count;

            if (hintText != null)
            {
                hintText.text = revealedCount >= SoilIndicators.Count
                    ? "巡航完成：请确认探测数据后点击「更新数据」。"
                    : "长按或点击画面移动机器人；点「手动巡航」逐格推进，点「自动巡航」一次跑完。";
            }
        }
    }
}
