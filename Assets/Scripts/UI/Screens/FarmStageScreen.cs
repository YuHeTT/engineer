using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 农场主界面（文档 image7 / image11，S1–S5 五个阶段共用同一个屏）。
    ///
    /// 白盒范围：
    ///   * 左上：操作提示（不同阶段提示不同，见文档 9 个阶段的玩法描述）；
    ///   * 右上：当前阶段 + 状态；
    ///   * 下方：「所有数据」面板，9 项指标全部展示，静态 4 项灰显；
    ///   * 右下：机器人检测 / 开始分析；进入 S5 后主按钮自动变成「收益计算」。
    /// </summary>
    public class FarmStageScreen : PlaceholderScreen
    {
        [Header("农场 HUD")]
        [Tooltip("左上角操作提示。")]
        public TMP_Text hintText;

        [Tooltip("右上角：当前阶段标题，例如「S1 播种苗期」。")]
        public TMP_Text stageTitleText;

        [Tooltip("右上角：状态说明，例如「正常生长」。")]
        public TMP_Text stageStatusText;

        [Tooltip("下方「所有数据」面板里的 9 个数值文本，顺序与 SoilIndicators 一致。")]
        public TMP_Text[] indicatorValueTexts = new TMP_Text[SoilIndicators.Count];

        [Tooltip("右下角主按钮：S1–S4 是「开始分析」，S5 是「收益计算」。")]
        public Button primaryButton;

        [Tooltip("主按钮上的文字。")]
        public TMP_Text primaryButtonLabel;

        private const string PrimaryLabelAnalyze = "开始分析";
        private const string PrimaryLabelRevenue = "收益计算";

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            primaryButtonLabel = ResolveLabel(primaryButtonLabel, primaryButton);
            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveListener(OnPrimaryClicked);
                primaryButton.onClick.AddListener(OnPrimaryClicked);
            }

            RefreshFromContext();
        }

        /// <summary>用 GameContext 里的数据刷新「当前阶段 / 所有数据」两处文案。</summary>
        public void RefreshFromContext()
        {
            var context = Flow != null ? Flow.Context : null;
            if (context == null) return;

            if (stageTitleText != null) stageTitleText.text = "S" + context.StageIndex + " " + context.CurrentStageName;
            if (stageStatusText != null) stageStatusText.text = context.IsLastStage ? "已成熟，可以结算" : "白盒占位：正常生长";
            if (hintText != null) hintText.text = BuildHint(context);
            if (primaryButtonLabel != null)
                primaryButtonLabel.text = context.IsLastStage ? PrimaryLabelRevenue : PrimaryLabelAnalyze;

            if (indicatorValueTexts == null) return;
            for (var i = 0; i < indicatorValueTexts.Length && i < SoilIndicators.Count; i++)
            {
                if (indicatorValueTexts[i] == null) continue;
                indicatorValueTexts[i].text = SoilIndicators.Format(i, context.GetIndicator(i));
            }
        }

        private void OnPrimaryClicked()
        {
            if (Flow == null) return;
            Flow.GoTo(Flow.Context.IsLastStage ? GameState.Revenue : GameState.Analyze);
        }

        private static string BuildHint(GameContext context)
        {
            switch (context.StageIndex)
            {
                case 1: return "S1 播种苗期：本阶段无需施肥，先让机器人巡航收集土壤数据。";
                case 2: return "S2 营养旺长期：根据上一阶段结果确定本阶段水肥量。";
                case 3: return "S3 生殖关键期：注意氮肥不要过量，避免植株徒长。";
                case 4: return "S4 灌浆成熟期：关注钾储量与磷活度，保证灌浆质量。";
                default: return "S5 收获期：本阶段结束即可计算收益。";
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
