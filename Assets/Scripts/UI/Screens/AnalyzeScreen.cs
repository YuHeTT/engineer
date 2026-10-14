using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 「开始分析」界面（文档 image10）。
    ///
    /// 白盒范围：
    ///   * 左侧「本阶段灌溉量施肥量建议」：5 项适宜度 + 4 项建议，数值内核没接进来前统一显示占位；
    ///   * 右侧「本阶段水肥量」：5 个输入框，顺序与 <see cref="GameContext.StageDosage"/> 完全一致
    ///     （灌溉量 / 有机肥 / 钾肥 / 氮肥 / 磷肥）；
    ///   * 点「确认本阶段方案」：把输入写回共享数据 → 推进到下一阶段 → 回到农场主界面。
    ///     走到 S5（收获期）时，农场界面的主按钮变成「收益计算」，不会再进入本屏。
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

        [Tooltip("右下角「确认本阶段方案」。")]
        public Button confirmButton;

        [Tooltip("主按钮上的文字。")]
        public TMP_Text confirmLabel;

        /// <summary>4 项建议的占位值，接入数值内核后由公式填充。</summary>
        private static readonly string[] SuggestionPlaceholders =
        {
            "待接入",
            "待接入 kg/亩",
            "待接入 m³/亩",
            "待接入（玉米四阶段生长模型）",
        };

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            var context = Flow != null ? Flow.Context : null;
            if (stageTitleText != null)
            {
                stageTitleText.text = context != null
                    ? "S" + context.StageIndex + " " + context.CurrentStageName
                    : "本阶段水肥量";
            }

            RefreshSuggestions();

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(OnConfirmDosage);
                confirmButton.onClick.AddListener(OnConfirmDosage);
            }
        }

        /// <summary>把输入框里的数值写回共享数据，然后进入下一阶段并返回农场主界面。</summary>
        public void OnConfirmDosage()
        {
            var context = Flow != null ? Flow.Context : null;
            if (Flow == null || context == null)
            {
                LogPlaceholder("确认本阶段方案（没有拿到状态机）");
                return;
            }

            for (var i = 0; i < dosageInputs.Length && i < context.StageDosage.Length; i++)
            {
                var input = dosageInputs[i];
                if (input == null) continue;

                float value;
                context.StageDosage[i] = float.TryParse(input.text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) ? value : 0f;
            }

            // 防御：正常流程下 S5 不会进入本屏（农场主按钮已变成「收益计算」）。
            if (context.IsLastStage)
            {
                Flow.GoTo(GameState.Revenue);
                return;
            }

            Flow.AdvanceStage();
            Flow.GoTo(GameState.FarmStage);
        }

        /// <summary>刷新左侧 4 项建议（白盒阶段是占位文案）。</summary>
        public void RefreshSuggestions()
        {
            if (suggestionValues == null) return;

            for (var i = 0; i < suggestionValues.Length; i++)
            {
                if (suggestionValues[i] == null) continue;
                suggestionValues[i].text = i < SuggestionPlaceholders.Length
                    ? SuggestionPlaceholders[i]
                    : "待接入";
            }
        }
    }
}
