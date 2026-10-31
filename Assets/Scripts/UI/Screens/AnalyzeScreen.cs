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

        private SoilState analysisSoil;
        private SuitabilityResult analysisFit;
        private bool observationFallback;
        private RecommendedPlan analysisPlan;

        protected override bool OwnsButton(Button button) => button != null && button == confirmButton;

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

            // 旧场景里的固定文案是「玉米生理说明」；四种作物都开放后改成通用叫法。
            var labels = GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].text == "玉米生理说明")
                    labels[i].text = "作物生理说明";
            }

            analysisSoil = Flow != null ? Flow.Probe.AnalysisSoil(session) : null;
            analysisFit = analysisSoil != null && session != null
                ? SuitabilityResult.From(analysisSoil, session.Config, session.ActiveCurves)
                : null;
            analysisPlan = session != null ? session.RecommendFromObservation(analysisSoil, out observationFallback) : null;
            if (confirmButton != null) confirmButton.interactable = analysisSoil != null;
            if (stageTitleText != null) stageTitleText.text += analysisSoil != null
                ? " · 探测数据分析（100%）" : " · 请先完成本阶段巡航并确认数据";
            RefreshSuggestions(session);
        }

        /// <summary>诊断对比：建议/适宜度采用确认探测值，真实土壤仅用于演变与产量。</summary>
        private void ReportProbeGap(GameSession session)
        {
            if (!logSuggestionRefresh) return;
            if (session == null || !session.IsStarted) return;

            var probe = Flow != null ? Flow.Probe : null;

            // 【诊断】以前这里是静默 return，导致"为什么没有探测值"无从判断。
            //   现在每种跳过原因都打一行说明。
            if (probe == null)
            {
                Debug.LogWarning("[Analyze] 没有 FarmProbeState（GameFlowController 上缺 Probe）。" +
                                 "探测值对比无法输出。", this);
                return;
            }
            if (!probe.CanAnalyze(session))
            {
                Debug.LogWarning("[Analyze] 本阶段探测数据尚未确认，禁止分析与提交。", this);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendFormat("[Analyze] 探测值 vs 真值（覆盖率 {0:0.0}%，误差幅度 {1:0.#}%~{2:0.#}%）：\n",
                probe.Coverage * 100f,
                Engineer.UI.FarmProbeState.MinError * 100f,
                Engineer.UI.FarmProbeState.MaxError * 100f);

            for (var i = 0; i < SoilIndicators.Count; i++)
            {
                var truth = session.GetIndicator(i, 0f);
                if (!probe.IsRevealed(i))
                {
                    sb.AppendFormat("    {0,-12} 真值 {1,-10} 未探测\n",
                        SoilIndicators.Names[i], SoilIndicators.Format(i, truth));
                    continue;
                }
                var pv = probe.GetProbe(i);
                var diff = truth != 0f ? (pv - truth) / truth * 100f : 0f;
                sb.AppendFormat("    {0,-12} 真值 {1,-10} 探测 {2,-10} 偏差 {3,6:0.0}%\n",
                    SoilIndicators.Names[i], SoilIndicators.Format(i, truth),
                    SoilIndicators.Format(i, pv), diff);
            }

            Debug.Log(sb.ToString(), this);
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
            if (UiClickFeedback.Busy) return;

            var flow = Flow;
            var context = flow != null ? flow.Context : null;
            if (flow == null || context == null)
            {
                LogPlaceholder("确认本阶段方案（没有拿到状态机）");
                return;
            }

            // 1) 读输入框 → GameContext.StageDosage（顺序：灌溉 / 有机肥 / 钾 / 氮 / 磷）
            if (flow.Current != GameState.Analyze || !flow.CanAnalyze || !ReadDosageInputs(context)) return;

            // 2) 交给内核演算并推进。
            //    注意：CommitStageDosage 内部会同步 StageIndex 并清空输入槽。
            var result = flow.CommitStageDosage();

            if (result == null)
            {
                if (stageTitleText != null) stageTitleText.text = "提交失败，请检查模拟是否已启动";
                Debug.LogWarning("[Analyze] 未结算成功，保留本阶段和输入，不推进 UI。", this);
                return;
            }

            // 3) 判断去农场还是去收益
            //    S4 提交完之后内核进入 Done，此时应该直接看收益
            var target = flow.Session != null && (flow.Session.IsFinished || StageMap.IsHarvest(flow.Session.UiStage))
                ? GameState.Revenue
                : GameState.FarmStage;

            // 先播按钮反馈，再切屏：否则新界面会立刻盖住消散动画。
            ClickThen(confirmButton, true, delegate { flow.GoTo(target); });
        }

        /// <summary>刷新左侧 4 项建议 + 5 项适宜度。</summary>
        public void RefreshSuggestions(GameSession session)
        {
            // 适宜度是独立的一组文本，先刷新它
            RefreshSuitability(session);

            // 探测值 vs 内核真值的差距（见方法说明）
            ReportProbeGap(session);

            if (suggestionValues == null) return;

            if (session == null || !session.IsStarted)
            {
                for (var i = 0; i < suggestionValues.Length; i++)
                    if (suggestionValues[i] != null) suggestionValues[i].text = NotConnected;

                if (stageTitleText != null && session == null)
                    stageTitleText.text += "（未接入数值内核）";
                return;
            }

            var plan = analysisPlan;
            if (plan == null)
            {
                for (var i = 0; i < suggestionValues.Length; i++)
                    if (suggestionValues[i] != null) suggestionValues[i].text = "请先完成巡航并确认探测数据";

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
                    plan.Label, observationFallback,
                    amounts.Organic, amounts.N, amounts.P, amounts.K, plan.BaseIrrigation));
            }

            SetText(0, plan.Label);
            SetText(1, BuildFertilizerText(amounts));
            SetText(2, plan.BaseIrrigation.ToString("0.#") + " m³/亩");
            var sourceText = "分析依据：本阶段确认探测值（覆盖100%）"
                + "\npH " + analysisSoil.pH.ToString("0.##") + " / OM " + analysisSoil.OM.ToString("0.##")
                + "\nN " + analysisSoil.N.ToString("0.##") + " / P " + analysisSoil.P.ToString("0.##")
                + " / K " + analysisSoil.K.ToString("0.##")
                + "\n土壤演变与产量结算使用真实土壤及实际投入。";
            var noteNode = transform.Find("Body/DosagePanel/Note");
            var note = noteNode != null ? noteNode.GetComponent<TMP_Text>() : null;
            if (note != null)
            {
                note.text = sourceText;
                note.enableAutoSizing = true;
                note.fontSizeMin = 12f;
                note.fontSizeMax = 18f;
            }
            SetText(3, plan.Physiology + (note == null ? "\n" + sourceText : ""));

            // 文档规则空洞提示（见 Docs/漏洞与风险清单.md G2）
            if (warnOnFallbackRecommendation && observationFallback
                && stageTitleText != null)
            {
                stageTitleText.text += "（建议采用兜底规则）";
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

            var fit = analysisFit;
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
        private bool ReadDosageInputs(GameContext context)
        {
            var values = new float[5];
            for (var i = 0; i < values.Length; i++)
            {
                var input = dosageInputs != null && i < dosageInputs.Length ? dosageInputs[i] : null;
                if (input == null || !MathX.TryParseInput(input.text, out values[i]) || values[i] < 0f)
                {
                    if (stageTitleText != null) stageTitleText.text = "第 " + (i + 1) + " 项：请输入非负的有效数字";
                    if (input != null) input.Select();
                    return false;
                }
            }
            System.Array.Copy(values, context.StageDosage, values.Length);
            return true;
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
