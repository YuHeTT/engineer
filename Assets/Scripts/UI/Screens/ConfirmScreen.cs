using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 确认选择界面（文档 image6）。
    ///
    /// 【这一屏之前的问题】
    ///   它原本挂的是通用 <see cref="PlaceholderScreen"/>，12 行汇总数据全是
    ///   白盒构建器写死的默认值（平川台 / 玉米 / 10 / 6.5 / 22 / 95 / 17 / 150 …），
    ///   **没有任何脚本把玩家实际选择的数据填进去**。
    ///   所以会出现"确认界面显示的数据和实际选择的不一致"，
    ///   而进入农场后又是对的（农场屏已经接了 GameContext）。
    ///
    /// 【本脚本做的事】
    ///   进入本屏时，把 <see cref="GameContext"/> 与数值内核的真实数据填进 12 行。
    ///   行结构固定为 `SummaryGrid/Row_N/{Label,Value}`，所以**不需要在 Inspector 里
    ///   手工绑定 12 行** —— 运行时按节点名自动查找。
    ///
    /// 【行的含义（与策划文档"所有数据"一致）】
    ///   1 土地类型   2 植物类型   3 土地面积   4 酸衡值 pH   5 腐殖丰度
    ///   6 速效氮元   7 磷活度     8 钾储量    9 氧还势 Eh   10 土骨离子
    ///   11 毒砷潜势  12 汞积量
    /// </summary>
    public class ConfirmScreen : PlaceholderScreen
    {
        private const int RowCount = 12;

        [Header("汇总表格")]
        [Tooltip("12 行的数值文本。留空会在运行时按 SummaryGrid/Row_N/Value 自动查找。")]
        public TMP_Text[] rowValues = new TMP_Text[RowCount];

        [Tooltip("12 行的标题文本。留空会自动查找；只有需要改文案时才手动填。")]
        public TMP_Text[] rowLabels = new TMP_Text[RowCount];

        [Header("其它")]
        [Tooltip("底部说明文本。")]
        public TMP_Text noteText;

        [Header("选项")]
        [Tooltip("是否用代码里的标准文案覆盖行标题（避免场景里的标题与内核顺序不一致）。")]
        public bool overwriteLabels = true;

        private bool resolved;

        public override void OnEnter(object args)
        {
            base.OnEnter(args);
            EnsureResolved();
            Refresh();
        }

        /// <summary>把当前选择与内核数据填进 12 行。</summary>
        public void Refresh()
        {
            var context = Flow != null ? Flow.Context : null;
            if (context == null) return;

            EnsureResolved();

            for (var i = 0; i < RowCount; i++)
            {
                if (overwriteLabels) SetLabel(i, RowLabel(i));
                SetValue(i, RowValue(i, context));
            }

            if (noteText != null) noteText.text = BuildNote(context);
        }

        /// <summary>第 i 行（0 基）的标题文案。</summary>
        private static string RowLabel(int i)
        {
            switch (i)
            {
                case 0: return "土地类型";
                case 1: return "植物类型";
                case 2: return "土地面积（亩）";
                case 3: return "酸衡值（pH）";
                case 4: return "腐殖丰度";
                case 5: return "速效氮元";
                case 6: return "磷活度";
                case 7: return "钾储量";
                case 8: return "氧还势 (Eh)";
                case 9: return "土骨离子";
                case 10: return "毒砷潜势";
                case 11: return "汞积量";
                default: return "参数 " + (i + 1);
            }
        }

        /// <summary>第 i 行（0 基）的数值文案。</summary>
        private static string RowValue(int i, GameContext context)
        {
            switch (i)
            {
                case 0: return context.LandTypeName;
                case 1: return context.PlantTypeName;
                case 2: return context.LandAreaMu.ToString("0.##");
                case 3: return context.Ph.ToString("0.##");
                case 4: return context.OrganicMatter.ToString("0.##");
                case 5: return context.AvailableNitrogen.ToString("0.##");
                case 6: return context.LabilePhosphorus.ToString("0.##");
                case 7: return context.Potassium.ToString("0.##");
                default:
                {
                    // 静态四项：GetIndicator 会在模拟已开始时返回内核的随机值，
                    // 未开始时返回 GameContext 里的输入占位值。
                    var index = i - 3;      // 0..3 -> 指标下标 5..8
                    if (index < 0 || index >= SoilMetrics.StaticCount) return "—";
                    var indicatorIndex = SoilIndicators.DynamicCount + index;
                    return SoilIndicators.Format(indicatorIndex, context.GetIndicator(indicatorIndex));
                }
            }
        }

        private static string BuildNote(GameContext context)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("确认以上配置后进入农场。");

            var session = context.Session;
            if (session == null) return sb.ToString();

            if (session.IsStarted)
            {
                sb.Append("（模拟已启动：静态四项为系统随机生成，后续阶段保持不变）");
            }
            else
            {
                sb.Append("（点「进入农场」后系统会为静态四项生成随机值）");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // 自动查找
        // ------------------------------------------------------------------

        /// <summary>按节点名补齐未绑定的字段，避免引用丢失导致本屏失效。</summary>
        public void EnsureResolved()
        {
            if (resolved) return;
            resolved = true;

            if (rowValues == null || rowValues.Length < RowCount) rowValues = new TMP_Text[RowCount];
            if (rowLabels == null || rowLabels.Length < RowCount) rowLabels = new TMP_Text[RowCount];

            for (var i = 0; i < RowCount; i++)
            {
                var rowPath = "Body/SummaryPanel/SummaryGrid/Row_" + (i + 1);
                var row = transform.Find(rowPath);
                if (row == null) continue;

                if (rowValues[i] == null)
                {
                    var t = row.Find("Value");
                    if (t != null) rowValues[i] = t.GetComponent<TMP_Text>();
                }
                if (rowLabels[i] == null)
                {
                    var t = row.Find("Label");
                    if (t != null) rowLabels[i] = t.GetComponent<TMP_Text>();
                }
            }

            if (noteText == null)
            {
                var t = transform.Find("Body/Note");
                if (t != null) noteText = t.GetComponent<TMP_Text>();
            }
        }

        private void SetValue(int i, string text)
        {
            if (rowValues == null || i < 0 || i >= rowValues.Length) return;
            if (rowValues[i] != null) rowValues[i].text = text;
        }

        private void SetLabel(int i, string text)
        {
            if (rowLabels == null || i < 0 || i >= rowLabels.Length) return;
            if (rowLabels[i] != null) rowLabels[i].text = text;
        }
    }
}
