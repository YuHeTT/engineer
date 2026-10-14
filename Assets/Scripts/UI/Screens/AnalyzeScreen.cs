using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 「开始分析」界面（文档 image10）。
    ///
    /// 【已接入数值内核】
    ///   * 左侧「本阶段灌溉量施肥量建议」：4 项全部由 <see cref="RecommendedPlan"/> 填充，
    ///     不再是"待接入"占位。若该适宜度组合在策划文档里没有定义（走了兜底规则），
    ///     会在标题上给出提示。
    ///   * 右侧「本阶段水肥量」：5 个输入框，顺序与 <see cref="GameContext.StageDosage"/>
    ///     完全一致（灌溉量 / 有机肥 / 钾肥 / 氮肥 / 磷肥）。进入本屏时会**预填系统建议值**，
    ///     玩家可以改 —— 这就是"采纳建议 / 自主调整"两种玩法。
    ///   * 点「确认本阶段方案」：把输入交给数值内核演算（<see cref="GameSession.CommitFromDosage"/>），
    ///     土壤随之演变到下一阶段，然后回到农场主界面。
    /// </summary>
    public class AnalyzeScreen : PlaceholderScreen
    {
        [Header("开始分析")]
        [Tooltip("右上角本阶段标题，例如「S2 营养旺长期」。")]
        public TMP_Text stageTitleText;

        [Tooltip("右侧「本阶段水肥量」5 个输入框，顺序与 GameContext.StageDosage 一致。")]
        public TMP_InputField[] dosageInputs = new TMP_InputField[5];

        [Tooltip("左侧 4 项建议的数值文本（推荐肥料类型 / 单亩施肥量 / 单亩基准灌溉 / 玉米生理说明）。")]
        public TMP_Text[] suggestionValues = new TMP_Text[4];

        [Tooltip("左侧 5 项适宜度的数值文本，顺序：fPH / fOM / fN / fP / fK。")]
        public TMP_Text[] suitabilityValues = new TMP_Text[5];

        [Tooltip("右下角「确认本阶段方案」。")]
        public Button confirmButton;

        [Tooltip("主按钮上的文字。")]
        public TMP_Text confirmLabel;

        [Header("选项")]
        [Tooltip("进入本屏时是否把系统建议值预填进 5 个输入框。")]
        public bool prefillWithRecommendation = true;

        [Tooltip("是否在标题后追加「文档未定义」提示（当建议走了兜底规则时）。")]
        public bool warnOnFallbackRecommendation = true;

        [Header("诊断")]
        [Tooltip("打印建议栏的填充过程到 Console，用于排查「建议栏没更新」这类问题。")]
        public bool logSuggestionRefresh = true;

        /// <summary>没有接上内核时的兜底文案（理论上不该出现）。</summary>
        private const string NotConnected = "未接入数值内核";

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            EnsureResolved();

            var context = Flow != null ? Flow.Context : null;
            var session = context != null ? context.Session : null;

            if (logSuggestionRefresh)
            {
                Debug.Log(string.Format(
                    "[Analyze] OnEnter: context={0} session={1} IsStarted={2} " +
                    "suggestionValues=[{3}] suitabilityValues=[{4}] dosageInputs=[{5}] stageTitleText={6}",
                    context != null, session != null,
                    session != null && session.IsStarted,
                    DescribeTexts(suggestionValues), DescribeTexts(suitabilityValues),
                    DescribeInputs(dosageInputs), stageTitleText != null));
            }

            if (stageTitleText != null)
            {
                stageTitleText.text = context != null
                    ? "S" + context.StageIndex + " " + context.CurrentStageName
                    : "本阶段水肥量";
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmDosage);
                confirmButton.onClick.AddListener(OnConfirmDosage);
            }

            RefreshSuggestions(session);
        }

        /// <summary>按节点名补齐未绑定的字段。万一场景重建导致引用丢失，本屏仍能工作。</summary>
        public void EnsureResolved()
        {
            if (suggestionValues == null || suggestionValues.Length < 4)
                suggestionValues = new TMP_Text[4];

            for (var i = 0; i < 4; i++)
            {
                if (suggestionValues[i] != null) continue;
                var t = transform.Find("Body/SuggestionPanel/SuggestionList/Suggestion_" + (i + 1) + "/Value");
                if (t != null) suggestionValues[i] = t.GetComponent<TMP_Text>();
            }

            // 5 项适宜度是**另一组**文本（SuitabilityList），与上面的建议列表是分开的
            if (suitabilityValues == null || suitabilityValues.Length < 5)
                suitabilityValues = new TMP_Text[5];

            for (var i = 0; i < 5; i++)
            {
                if (suitabilityValues[i] != null) continue;
                var t = transform.Find("Body/SuggestionPanel/SuitabilityList/Suitability_" + (i + 1) + "/Value");
                if (t != null) suitabilityValues[i] = t.GetComponent<TMP_Text>();
            }

            if (stageTitleText == null)
            {
                var t = transform.Find("Body/DosagePanel/StageTitle");
                if (t != null) stageTitleText = t.GetComponent<TMP_Text>();
            }

            if (confirmButton == null)
            {
                var t = transform.Find("Footer/Button_ConfirmDosage");
                if (t != null) confirmButton = t.GetComponent<Button>();
            }
        }

        private static string DescribeTexts(TMP_Text[] arr)
        {
            if (arr == null) return "null";
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(arr[i] == null ? "null" : "\"" + arr[i].text + "\"");
            }
            return sb.ToString();
        }

        private static string DescribeInputs(TMP_InputField[] arr)
        {
            if (arr == null) return "null";
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < arr.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(arr[i] == null ? "null" : "ok");
            }
            return sb.ToString();
        }

        /// <summary>把输入框里的数值交给内核演算，然后进入下一阶段并返回农场主界面。</summary>
        public void OnConfirmDosage()
        {
            var flow = Flow;
            var context = flow != null ? flow.Context : null;
            if (flow == null || context == null)
            {
                LogPlaceholder("确认本阶段方案（没有拿到状态机）");
                return;
            }

            // 1) 读输入框 → GameContext.StageDosage（顺序：灌溉 / 有机肥 / 钾 / 氮 / 磷）
            ReadDosageInputs(context);

            // 2) 交给内核演算并推进。
            //    注意：CommitStageDosage 内部会同步 StageIndex 并清空输入槽。
            var result = flow.CommitStageDosage();

            if (result == null)
            {
                // 没接内核（纯白盒走查）或已经结束：退回旧的推进方式，保证流程不断
                Debug.LogWarning("[Analyze] 数值内核未提交成功，改用纯 UI 推进。"
                                 + "检查 GameFlowController 是否成功创建了 GameSession。");
                if (context.IsLastStage)
                {
                    flow.GoTo(GameState.Revenue);
                    return;
                }

                flow.AdvanceStage();
                flow.GoTo(GameState.FarmStage);
                return;
            }

            // 3) 判断去农场还是去收益
            //    S4 提交完之后内核进入 Done，此时应该直接看收益
            if (flow.Session != null && (flow.Session.IsFinished || StageMap.IsHarvest(flow.Session.UiStage)))
                flow.GoTo(GameState.Revenue);
            else
                flow.GoTo(GameState.FarmStage);
        }

        /// <summary>刷新左侧 4 项建议 + 5 项适宜度。</summary>
        public void RefreshSuggestions(GameSession session)
        {
            // 适宜度是独立的一组文本，先刷新它
            RefreshSuitability(session);

            if (suggestionValues == null) return;

            if (session == null || !session.IsStarted)
            {
                for (var i = 0; i < suggestionValues.Length; i++)
                    if (suggestionValues[i] != null) suggestionValues[i].text = NotConnected;

                if (stageTitleText != null && session == null)
                    stageTitleText.text += "（未接入数值内核）";
                return;
            }

            var plan = session.Recommend();
            if (plan == null)
            {
                for (var i = 0; i < suggestionValues.Length; i++)
                    if (suggestionValues[i] != null) suggestionValues[i].text = "本阶段无需操作";

                if (logSuggestionRefresh)
                    Debug.Log("[Analyze] 本阶段没有推荐方案（已结束或未开始），4 项建议显示占位。");

                return;
            }

            // 复合肥在这里已按 SimConfig.CompoundRatio 折算成氮磷钾
            var amounts = plan.ResolvedAmounts(session.Config);

            if (logSuggestionRefresh)
            {
                Debug.Log(string.Format(
                    "[Analyze] 写入建议：plan=「{0}」 走兜底={1} | 有机肥={2:0.#} 氮={3:0.#} 磷={4:0.#} 钾={5:0.#} 水={6:0.#}",
                    plan.Label, session.RecommendUsedFallback,
                    amounts.Organic, amounts.N, amounts.P, amounts.K, plan.BaseIrrigation));
            }

            SetText(0, plan.Label);
            SetText(1, BuildFertilizerText(amounts));
            SetText(2, plan.BaseIrrigation.ToString("0.#") + " m³/亩");
            SetText(3, plan.Physiology);

            // 文档规则空洞提示（见 Docs/漏洞与风险清单.md G2）
            if (warnOnFallbackRecommendation && session.RecommendUsedFallback
                && stageTitleText != null)
            {
                stageTitleText.text += "（该情况文档未定义，使用兜底建议）";
            }

            if (prefillWithRecommendation)
            {
                var applied = plan.ToApplication(session.Config);
                PrefillDosageInputs(applied.Irrigation, applied.Organic,
                                    applied.K, applied.N, applied.P);
            }
        }

        /// <summary>
        /// 刷新左侧 5 项适宜度（SuitabilityList/Suitability_1..5/Value）。
        /// 面板上的 Range 已经说明了"值域 [0.2, 1]（结果得 1 即 100%）"，
        /// 所以这里给出"数值（百分比）"最直观。
        /// </summary>
        public void RefreshSuitability(GameSession session)
        {
            if (suitabilityValues == null) return;

            // 还没启动内核：给出可操作的提示，而不是留一个"待接入"
            if (session == null || !session.IsStarted)
            {
                for (var i = 0; i < suitabilityValues.Length; i++)
                    if (suitabilityValues[i] != null)
                        suitabilityValues[i].text = session == null ? NotConnected : "请先启动模拟";

                if (logSuggestionRefresh)
                {
                    Debug.Log(string.Format("[Analyze] RefreshSuitability: session={0} IsStarted={1} -> 显示提示",
                        session != null, session != null && session.IsStarted));
                }
                return;
            }

            var fit = session.Fit;
            if (fit == null)
            {
                for (var i = 0; i < suitabilityValues.Length; i++)
                    if (suitabilityValues[i] != null) suitabilityValues[i].text = "—";
                return;
            }

            var values = new[] { fit.fpH, fit.fOM, fit.fN, fit.fP, fit.fK };
            for (var i = 0; i < suitabilityValues.Length && i < values.Length; i++)
            {
                if (suitabilityValues[i] == null) continue;
                suitabilityValues[i].text = string.Format("{0:0.00}（{1:0}%）",
                    values[i], values[i] * 100f);
            }

            if (logSuggestionRefresh)
            {
                Debug.Log(string.Format(
                    "[Analyze] RefreshSuitability: fPH={0:0.###} fOM={1:0.###} fN={2:0.###} fP={3:0.###} fK={4:0.###}（已写入 5 个适宜度单元格）",
                    fit.fpH, fit.fOM, fit.fN, fit.fP, fit.fK));
            }
        }

        /// <summary>把建议值预填进 5 个输入框（顺序：灌溉 / 有机肥 / 钾 / 氮 / 磷）。</summary>
        private void PrefillDosageInputs(float irrigation, float organic, float k, float n, float p)
        {
            var values = new[] { irrigation, organic, k, n, p };
            for (var i = 0; i < dosageInputs.Length && i < values.Length; i++)
            {
                if (dosageInputs[i] == null) continue;
                dosageInputs[i].SetTextWithoutNotify(values[i].ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        /// <summary>读 5 个输入框写回 GameContext.StageDosage。</summary>
        private void ReadDosageInputs(GameContext context)
        {
            for (var i = 0; i < dosageInputs.Length && i < context.StageDosage.Length; i++)
            {
                var input = dosageInputs[i];
                if (input == null) continue;

                float value;
                context.StageDosage[i] = float.TryParse(input.text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) ? value : 0f;
            }
        }

        /// <summary>把四项肥料用量拼成一行可读文案。</summary>
        private static string BuildFertilizerText(FertilizerAmounts a)
        {
            var sb = new System.Text.StringBuilder();
            Append(sb, "有机肥", a.Organic);
            Append(sb, "氮", a.N);
            Append(sb, "磷", a.P);
            Append(sb, "钾", a.K);

            if (sb.Length == 0) return "不施肥";
            sb.Append(" kg/亩");
            return sb.ToString();
        }

        private static void Append(System.Text.StringBuilder sb, string label, float value)
        {
            if (value <= 0f) return;
            if (sb.Length > 0) sb.Append("、");
            sb.Append(label).Append(' ').Append(value.ToString("0.##"));
        }

        private void SetText(int index, string text)
        {
            if (suggestionValues == null || index < 0 || index >= suggestionValues.Length) return;
            if (suggestionValues[index] != null) suggestionValues[index].text = text;
        }
    }
}
