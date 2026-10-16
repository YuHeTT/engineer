using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 收益计算界面（文档 image12）。
    ///
    /// 【已接入数值内核】
    ///   全部数据来自 <see cref="RevenueReport"/> 与四阶段的 <see cref="StageResult"/>：
    ///   * FormulaPanel：四个阶段的肥力系数、平均肥力、水分系数、最终产量
    ///   * ResultBox：最终产量与达成率（Y / Ymax,geo）
    ///   * StageTable：S2~S5 四行，每行给出该阶段的肥力系数与土壤指标
    ///   * StaticList：四个静态指标（氧还势 / 土骨离子 / 毒砷潜势 / 汞积量）
    ///
    /// 【为什么表格是 S2~S5 而不是 S1~S4】
    ///   这是文档的写法（"列出第二第三第四第五阶段…的参数"）。
    ///   注意 S5 是收获期，数值上不参与演算，所以它的肥力系数沿用 S4 的值并在备注里说明。
    ///
    /// 【字段绑定】大部分文本字段留空也能工作 —— 会在运行时按节点名自动查找。
    ///   这样即使白盒构建器重新生成场景、Inspector 引用丢失，脚本依然能跑。
    /// </summary>
    public class RevenueScreen : PlaceholderScreen
    {
        [Header("标题")]
        public TMP_Text titleText;
        public TMP_Text subtitleText;

        [Header("公式区（左）")]
        [Tooltip("四行公式说明，顺序：阶段肥力系数 / 全生育期平均 / 水分系数 / 产量公式。")]
        public TMP_Text[] formulaTexts = new TMP_Text[4];

        [Tooltip("结果框标题。")]
        public TMP_Text resultTitle;

        [Tooltip("结果框主数值（最终产量）。")]
        public TMP_Text resultValue;

        [Tooltip("结果框备注（达成率 / 面积 / 总产量）。")]
        public TMP_Text resultNote;

        [Header("数据区（右）")]
        [Tooltip("阶段表格的 4 行（S2~S5），每行 4 列：阶段 / 肥力系数 / 关键指标 / 说明。")]
        public StageRow[] stageRows = new StageRow[4];

        [Tooltip("四个静态指标的文本（氧还势 / 土骨离子 / 毒砷潜势 / 汞积量）。")]
        public TMP_Text[] staticValueTexts = new TMP_Text[4];

        [Header("单位与文案")]
        [Tooltip("产量数值后面拼的单位。文档未定义 Y 的单位（见方案分析 A10），先用 kg/亩。")]
        public string yieldUnit = "kg/亩";

        [Tooltip("是否在公式区额外显示经济层（单价 / 成本 / 净收益）。经济参数为 0 时自动隐藏。")]
        public bool showEconomicsIfAvailable = true;

        /// <summary>阶段表格的一行。</summary>
        [System.Serializable]
        public class StageRow
        {
            [Tooltip("阶段名，例如「S2 营养旺长期」。")]
            public TMP_Text stageLabel;

            [Tooltip("该阶段的肥力系数 Ffert,stage。")]
            public TMP_Text fertilityText;

            [Tooltip("该阶段结束时的关键土壤指标。")]
            public TMP_Text soilText;

            [Tooltip("说明文字。")]
            public TMP_Text noteText;
        }

        private bool resolved;

        public override void OnEnter(object args)
        {
            base.OnEnter(args);
            EnsureResolved();
            Refresh();
        }

        /// <summary>刷新收益界面的全部内容。</summary>
        public void Refresh()
        {
            var context = Flow != null ? Flow.Context : null;
            var session = context != null ? context.Session : null;

            if (session == null || !session.IsStarted)
            {
                ShowNotConnected();
                return;
            }

            var report = session.Revenue(context.LandAreaMu);
            if (report == null)
            {
                ShowNotConnected();
                return;
            }

            if (titleText != null) titleText.text = "收益计算";
            if (subtitleText != null)
                subtitleText.text = "土地 " + context.LandTypeName + " · 作物 " + context.PlantTypeName
                                    + " · " + context.LandAreaMu.ToString("0.#") + " 亩";

            RefreshFormulas(session, report);
            RefreshResult(context, report);
            RefreshStageTable(session, report);
            RefreshStaticList(report);
        }

        // ------------------------------------------------------------------
        // 各区块
        // ------------------------------------------------------------------

        private void RefreshFormulas(GameSession session, RevenueReport report)
        {
            if (formulaTexts == null) return;

            SetText(formulaTexts, 0,
                "阶段肥力系数  Ffert,stage = fPH × fOM × fN × fP × fK\n" + BuildStageFertilityLine(report));

            SetText(formulaTexts, 1,
                (report.IsPartial ? "已结算阶段平均  Ffert\n= " : "全生育期平均  Ffert = (S1+S2+S3+S4) / 4\n= ") + report.OverallFertility.ToString("0.0000")
                + (report.IsPartial
                    ? "（只统计已结算的 " + report.CommittedCount + " 个阶段）"
                    : string.Empty));

            // 水分/静态系数与那句 v1 说明都取 report 里的值 —— UI 不再自己写死
            SetText(formulaTexts, 2,
                "水分系数  Fwater = " + report.WaterFactor.ToString("0.##")
                + "      静态系数  Sstatic = " + report.StaticFactor.ToString("0.##")
                + "\n" + report.WaterNote);

            var sb = new StringBuilder();
            sb.Append("产量  Y = Ymax,geo × Ffert × Fwater × Sstatic\n");
            // 四个因子全列出来：只写前两项会让"界面上的式子"和"算出来的 Y"对不上
            sb.Append("= ").Append(report.YmaxGeo.ToString("0.##"))
              .Append(" × ").Append(report.OverallFertility.ToString("0.0000"))
              .Append(" × ").Append(report.WaterFactor.ToString("0.##"))
              .Append(" × ").Append(report.StaticFactor.ToString("0.##"))
              .Append(" = ").Append(report.Yield.ToString("0.##")).Append(' ').Append(yieldUnit);

            if (report.IsPartial)
                sb.Append("\n（只结算了 ").Append(report.CommittedCount).Append('/')
                  .Append(Stages.Count).Append(" 个阶段，本值是中间结果）");

            if (showEconomicsIfAvailable && report.HasEconomics)
            {
                sb.Append('\n').Append("经济层：单价 ").Append(report.PricePerKg.ToString("0.##"))
                  .Append(" 元/kg，成本 ").Append(report.TotalCost.ToString("0.##"))
                  .Append(" 元，净收益 ").Append(report.NetIncome.ToString("0.##")).Append(" 元");
            }
            else if (showEconomicsIfAvailable)
            {
                sb.Append("\n（当前仅结算产量，暂未启用金额收益）");
            }

            if (report.HasInputSummary && report.HasArea)
            {
                var input = report.TotalInput;
                sb.Append("\n全田累计投入：灌溉 ").Append(input.Irrigation.ToString("0.##")).Append(" m³")
                  .Append("，有机肥 ").Append(input.Organic.ToString("0.##"))
                  .Append(" / 氮 ").Append(input.N.ToString("0.##"))
                  .Append(" / 磷 ").Append(input.P.ToString("0.##"))
                  .Append(" / 钾 ").Append(input.K.ToString("0.##")).Append(" kg");
            }
            SetText(formulaTexts, 3, sb.ToString());
        }

        private void RefreshResult(GameContext context, RevenueReport report)
        {
            if (resultTitle != null) resultTitle.text = "单位面积产量";

            if (resultValue != null)
                resultValue.text = report.Yield.ToString("0.##") + " " + yieldUnit;

            if (resultNote == null) return;

            var sb = new StringBuilder();
            sb.Append("达成率 ").Append((report.AchievementRate * 100f).ToString("0.0")).Append('%');
            sb.Append("（理论上限 ").Append(report.YmaxGeo.ToString("0")).Append(' ').Append(yieldUnit).Append('）');

            if (report.HasArea)
            {
                sb.Append('\n').Append("总产量 ").Append(report.TotalYield.ToString("0.##"))
                  .Append(" kg")
                  .Append("（").Append(report.Area.ToString("0.#")).Append(" 亩）");
            }

            if (report.IsPartial)
                sb.Append('\n').Append("尚未跑完全部 ").Append(Stages.Count)
                  .Append(" 个阶段，这里是按已结算的 ").Append(report.CommittedCount).Append(" 个阶段估算的");

            resultNote.text = sb.ToString();
        }

        private void RefreshStageTable(GameSession session, RevenueReport report)
        {
            if (stageRows == null) return;

            // 文档要求列 S2~S5；S5 是收获期，数值上沿用 S4
            var stages = new[] { Stage.S2, Stage.S3, Stage.S4, Stage.Done };

            for (var i = 0; i < stageRows.Length && i < stages.Length; i++)
            {
                var row = stageRows[i];
                if (row == null) continue;

                var stage = stages[i];
                var uiStage = StageMap.ToUi(stage);

                if (row.stageLabel != null)
                    row.stageLabel.text = "S" + uiStage + " " + StageMap.DisplayName(uiStage);

                // 收获期沿用 S4 的肥力系数
                var sourceStage = stage == Stage.Done ? Stage.S4 : stage;
                var result = session.View.GetHistory(sourceStage);
                var fert = GetFertility(report, sourceStage);

                if (row.fertilityText != null)
                    row.fertilityText.text = result != null ? fert.ToString("0.0000") : "—";

                if (row.soilText != null)
                {
                    var soil = result != null ? result.SoilAfter : null;
                    row.soilText.text = soil != null ? BuildKeySoilLine(soil) : "—";
                }

                if (row.noteText != null)
                {
                    if (stage == Stage.Done)
                        row.noteText.text = "收获期，不参与演算；数值沿用 S4";
                    else if (result != null)
                        row.noteText.text = "长势：" + GrowthLevels.DisplayName(result.Growth);
                    else
                        row.noteText.text = "未结算";
                }
            }
        }

        private void RefreshStaticList(RevenueReport report)
        {
            if (staticValueTexts == null) return;

            var soil = report.FinalSoil;
            var metrics = new[] { SoilMetric.Eh, SoilMetric.BaseIon, SoilMetric.As, SoilMetric.Hg };

            for (var i = 0; i < staticValueTexts.Length && i < metrics.Length; i++)
            {
                if (staticValueTexts[i] == null) continue;

                // 复用 SoilIndicators 的名字与单位，保持与农场界面一致
                var indicatorIndex = SoilIndicators.DynamicCount + i;
                var name = indicatorIndex < SoilIndicators.Names.Length
                    ? SoilIndicators.Names[indicatorIndex]
                    : metrics[i].ToString();

                var value = soil != null ? soil[metrics[i]] : 0f;
                staticValueTexts[i].text = name + "：" + SoilIndicators.Format(indicatorIndex, value);
            }
        }

        // ------------------------------------------------------------------
        // 工具
        // ------------------------------------------------------------------

        private static float GetFertility(RevenueReport report, Stage stage)
        {
            if (report.StageFertility == null) return 0f;
            var i = Stages.ToIndex(stage);
            return (i >= 0 && i < report.StageFertility.Length) ? report.StageFertility[i] : 0f;
        }

        private static string BuildStageFertilityLine(RevenueReport report)
        {
            if (report.StageFertility == null) return string.Empty;

            var sb = new StringBuilder("= ");
            for (var i = 0; i < report.StageFertility.Length && i < Stages.Count; i++)
            {
                if (i > 0) sb.Append(" , ");
                // 报告只统计【已结算】阶段：未结算的显示"—"，
                // 别显示 0.0000 —— 那会被误读成"该阶段肥力为零"
                sb.Append(i < report.CommittedCount
                    ? report.StageFertility[i].ToString("0.0000")
                    : "—");
            }
            return sb.ToString();
        }

        private static string BuildKeySoilLine(SoilState soil)
        {
            return "pH " + soil.pH.ToString("0.##")
                 + " / OM " + soil.OM.ToString("0.##")
                 + " / N " + soil.N.ToString("0.##")
                 + " / P " + soil.P.ToString("0.##")
                 + " / K " + soil.K.ToString("0.##");
        }

        private void ShowNotConnected()
        {
            if (titleText != null) titleText.text = "收益计算";
            if (resultTitle != null) resultTitle.text = "尚未产生结果";
            if (resultValue != null) resultValue.text = "—";
            if (resultNote != null) resultNote.text = "请先完成至少一个阶段的模拟流程。";

            if (formulaTexts != null)
                for (var i = 0; i < formulaTexts.Length; i++)
                    if (formulaTexts[i] != null) formulaTexts[i].text = "—";
        }

        private static void SetText(TMP_Text[] arr, int index, string text)
        {
            if (arr == null || index < 0 || index >= arr.Length) return;
            if (arr[index] != null) arr[index].text = text;
        }

        // ------------------------------------------------------------------
        // 自动查找未绑定的字段
        // ------------------------------------------------------------------

        /// <summary>
        /// 按节点名自动补齐没在 Inspector 里绑定的字段。
        /// 目的：即使白盒构建器重新生成场景导致引用丢失，本脚本仍能工作。
        /// 已手动绑定的字段不会被覆盖。
        /// </summary>
        public void EnsureResolved()
        {
            if (resolved) return;
            resolved = true;

            if (titleText == null) titleText = FindText("Header/Title");
            if (subtitleText == null) subtitleText = FindText("Header/Subtitle");

            if (formulaTexts == null || formulaTexts.Length < 4) formulaTexts = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
                if (formulaTexts[i] == null) formulaTexts[i] = FindText("Body/FormulaPanel/Formula_" + (i + 1));

            if (resultTitle == null) resultTitle = FindText("Body/FormulaPanel/ResultBox/Title");
            if (resultValue == null) resultValue = FindText("Body/FormulaPanel/ResultBox/Value");
            if (resultNote == null) resultNote = FindText("Body/FormulaPanel/ResultBox/Note");

            if (stageRows == null || stageRows.Length < 4) stageRows = new StageRow[4];
            var stageNames = new[] { "S2", "S3", "S4", "S5" };
            for (var i = 0; i < 4; i++)
            {
                if (stageRows[i] == null) stageRows[i] = new StageRow();
                var basePath = "Body/DataPanel/StageTable/StageRow_" + stageNames[i];
                var texts = FindTextsIn(basePath);
                if (stageRows[i].stageLabel == null && texts.Count > 0) stageRows[i].stageLabel = texts[0];
                if (stageRows[i].fertilityText == null && texts.Count > 1) stageRows[i].fertilityText = texts[1];
                if (stageRows[i].soilText == null && texts.Count > 2) stageRows[i].soilText = texts[2];
                if (stageRows[i].noteText == null && texts.Count > 3) stageRows[i].noteText = texts[3];
            }

            if (staticValueTexts == null || staticValueTexts.Length < 4) staticValueTexts = new TMP_Text[4];
            for (var i = 0; i < 4; i++)
                if (staticValueTexts[i] == null)
                    staticValueTexts[i] = FindText("Body/DataPanel/StaticList/Static_" + (i + 1));
        }

        /// <summary>按相对路径找 TMP_Text（找不到就只在那层找第一个）。</summary>
        private TMP_Text FindText(string relativePath)
        {
            var list = FindTextsIn(relativePath);
            return list.Count > 0 ? list[0] : null;
        }

        private List<TMP_Text> FindTextsIn(string relativePath)
        {
            var result = new List<TMP_Text>();
            var t = transform.Find(relativePath);
            if (t == null) return result;

            var own = t.GetComponent<TMP_Text>();
            if (own != null) result.Add(own);
            result.AddRange(t.GetComponentsInChildren<TMP_Text>(true));
            return result;
        }
    }
}
