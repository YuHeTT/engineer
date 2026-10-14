using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 机器人操控界面（文档 image8 手动巡航 / image9 自动巡航）。
    ///
    /// 【已接入数值内核（部分）】
    ///   * 左侧「原数据」：进入时从内核抄一份当前土壤（含静态四项的随机结果）；
    ///   * 右侧「探测数据」：随巡航推进逐项揭示，**揭示的是内核里的真实值**；
    ///   * 「更新数据」：把探测结果写回 —— 见下面的重要说明；
    ///   * 光标/进度：手动巡航每点一次推进一格，自动巡航一次填满。
    ///
    /// 【重要：这个屏只完成了一半，另一半要等策划】
    ///   按文档设计意图，探测值应当带误差（"原数据可能不准，所以要探测"）。
    ///   但误差模型属于**策划未定义的漏洞**（见 Docs/漏洞与风险清单.md 的 I2），
    ///   现阶段按"探测值 = 真实值"占位。
    ///
    ///   后果：探测值等于真值时，"更新数据"这一步没有信息增益。
    ///   而且由于内核里的土壤是权威数据（界面不能直接改），
    ///   本屏的「更新数据」目前**只做界面反馈**，不写回内核 —— 因为写回真值等于没改，
    ///   写回带误差的假值则会污染数值内核。
    ///
    ///   误差模型定下来之后，需要改的地方只有 <see cref="ComputeProbeValue"/> 一处。
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

        [Header("选项（策划定下误差模型前请保持 0）")]
        [Tooltip("探测误差幅度。0 = 探测值等于真值（当前占位）。" +
                 "将来策划定下模型后，建议改为随覆盖率收敛：覆盖越多误差越小。")]
        [Range(0f, 0.5f)]
        public float probeErrorAmplitude = 0f;

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

            // 进入时清空「探测数据」列
            if (probeValueTexts != null)
                for (var i = 0; i < probeValueTexts.Length; i++)
                    if (probeValueTexts[i] != null) probeValueTexts[i].text = SoilIndicators.NoDataText;

            RefreshSourceValues();
            RefreshCruiseState();
        }

        /// <summary>手动巡航：每点一次向前推进一格，并揭示下一个指标。</summary>
        public void OnManualCruise()
        {
            if (revealedCount >= SoilIndicators.Count)
            {
                Debug.Log("[UI] " + name + " ：巡航已经跑完。", this);
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

        /// <summary>
        /// 「更新数据」。
        ///
        /// 【当前行为】只给界面反馈，不写回内核土壤。
        /// 原因：内核里的土壤是权威数据，只能由水肥演算推进。
        /// 在"探测值 = 真值"的占位阶段，写回与否结果一样；等误差模型定下来之后，
        /// 正确的做法是让**界面显示的土壤**与**内核的真实土壤**分离
        /// （内核用真值算分，界面显示玩家以为的值），而不是直接改内核。
        /// </summary>
        public void OnUpdateData()
        {
            if (revealedCount < SoilIndicators.Count)
            {
                Debug.Log("[UI] " + name + " ：巡航还没跑完，暂时不更新数据。", this);
                return;
            }

            var context = Flow != null ? Flow.Context : null;
            var session = context != null ? context.Session : null;

            if (session == null || !session.IsStarted)
            {
                // 没接内核：退回旧行为（写进 GameContext 的输入字段），保证白盒流程能走
                if (context != null)
                    for (var i = 0; i < SoilIndicators.Count; i++)
                        context.SetIndicator(i, probeValues[i]);
            }
            else
            {
                // 已接内核：土壤以内核为准，本步骤只作确认动作
                Debug.Log("[UI] 数据已确认。当前探测值等于真实值（误差模型待策划定义，见 I2）。", this);
            }

            if (Flow != null) Flow.Back();
        }

        /// <summary>左侧「原数据」取当前真实土壤（模拟已开始时走内核）。</summary>
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

            var context = Flow != null ? Flow.Context : null;
            var truth = context != null ? context.GetIndicator(index) : 0f;

            probeValues[index] = ComputeProbeValue(index, truth);

            if (probeValueTexts[index] != null)
                probeValueTexts[index].text = SoilIndicators.Format(index, probeValues[index]);
        }

        /// <summary>
        /// 由真实值算出探测值。
        ///
        /// 【这是唯一需要改的地方】等策划定下误差模型后，把这里改成
        /// "误差随覆盖率收敛"即可，例如：
        ///     var progress = (float)revealedCount / SoilIndicators.Count;
        ///     var amplitude = probeErrorAmplitude * (1f - progress);
        ///     return truth * (1f + Random.Range(-amplitude, amplitude));
        /// </summary>
        private float ComputeProbeValue(int index, float truth)
        {
            if (probeErrorAmplitude <= 0f) return truth;   // 当前占位：探测值 = 真值

            // 误差随覆盖率线性收敛到 0
            var progress = SoilIndicators.Count <= 1
                ? 1f
                : (float)(revealedCount - 1) / (SoilIndicators.Count - 1);
            var amplitude = probeErrorAmplitude * (1f - progress);

            return truth * (1f + Random.Range(-amplitude, amplitude));
        }

        private void RefreshCruiseState()
        {
            var progress = SoilIndicators.Count == 0 ? 0f : (float)revealedCount / SoilIndicators.Count;

            if (coverageFill != null) coverageFill.fillAmount = progress;
            if (coverageText != null) coverageText.text = "已覆盖 " + Mathf.RoundToInt(progress * 100f) + "%";

            var context = Flow != null ? Flow.Context : null;
            var area = context != null ? context.LandAreaMu : 0f;
            if (uncoveredText != null) uncoveredText.text = "未覆盖面积 " + (area * (1f - progress)).ToString("0.0") + " 亩";

            var done = revealedCount >= SoilIndicators.Count;
            if (updateButton != null) updateButton.interactable = done;
            if (manualButton != null) manualButton.interactable = !done;
            // 自动巡航一次跑完：跑完后按钮置灰（正式版这里应显示「正在检测…」的运行态）
            if (autoButton != null) autoButton.interactable = !done;

            if (hintText != null)
            {
                hintText.text = done
                    ? "巡航完成：请确认探测数据后点击「更新数据」。"
                    : "点「手动巡航」逐格推进，点「自动巡航」一次跑完。";
            }
        }
    }
}
