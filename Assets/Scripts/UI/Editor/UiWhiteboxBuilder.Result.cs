using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒构建器 · 结算阶段：image10 开始分析、image12 收益计算。
    /// </summary>
    public static partial class UiWhiteboxBuilder
    {
        // ==================================================================
        // image10 开始分析
        // ==================================================================

        private static PlaceholderScreen BuildAnalyze(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen<AnalyzeScreen>(parent, GameState.Analyze, "开始分析", UiContent.AnalyzeHint,
                false, out body, out footer);

            // ---------- 左：本阶段灌溉量施肥量建议 ----------
            var suggestionPanel = UiFactory.MakePanel(body, "SuggestionPanel", UiTheme.PanelBg);
            UiLayout.SplitLeft(suggestionPanel.rectTransform, 0.5f, 24f, 0f, 0f, 0f, 0f);

            var suggestionTitle = UiFactory.MakeText(suggestionPanel.rectTransform, "Title", UiContent.AnalyzeSuggestionTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(suggestionTitle.rectTransform, 24f, 20f, 24f, 40f);

            var rangeText = UiFactory.MakeText(suggestionPanel.rectTransform, "Range", UiContent.SuitabilityRange,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(rangeText.rectTransform, 24f, 62f, 24f, 28f);

            // 5 项适宜度
            var suitabilityList = UiFactory.MakeGroup(suggestionPanel.rectTransform, "SuitabilityList");
            UiLayout.TopBand(suitabilityList, 24f, 100f, 24f, 320f);
            for (var i = 0; i < UiContent.SuitabilityLabels.Length; i++)
            {
                var cell = UiFactory.MakeGroup(suitabilityList, "Suitability_" + (i + 1));
                UiLayout.GridCell(cell, 0, i, 1, UiContent.SuitabilityLabels.Length, 0f, 0f, 0f, 0f, 0f, 2f);
                BuildInfoRow(cell, UiContent.SuitabilityLabels[i], UiContent.AnalyzePending, true);
            }

            // 4 项建议（值由 AnalyzeScreen 在进入时写入）
            var suggestionList = UiFactory.MakeGroup(suggestionPanel.rectTransform, "SuggestionList");
            UiLayout.BottomBand(suggestionList, 24f, 24f, 24f, 260f);

            var suggestionValues = new TMP_Text[UiContent.SuggestionLabels.Length];
            for (var i = 0; i < UiContent.SuggestionLabels.Length; i++)
            {
                var cell = UiFactory.MakeGroup(suggestionList, "Suggestion_" + (i + 1));
                UiLayout.GridCell(cell, 0, i, 1, UiContent.SuggestionLabels.Length, 0f, 0f, 0f, 0f, 0f, 4f);
                suggestionValues[i] = BuildInfoRow(cell, UiContent.SuggestionLabels[i], UiContent.AnalyzePending, true);
            }

            // ---------- 右：本阶段水肥量 ----------
            var dosagePanel = UiFactory.MakePanel(body, "DosagePanel", UiTheme.PanelBg);
            UiLayout.SplitRight(dosagePanel.rectTransform, 0.5f, 24f, 0f, 0f, 0f, 0f);

            var dosageTitle = UiFactory.MakeText(dosagePanel.rectTransform, "Title", UiContent.AnalyzeDosageTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(dosageTitle.rectTransform, 24f, 20f, 24f, 40f);

            var stageTitle = UiFactory.MakeText(dosagePanel.rectTransform, "StageTitle", "S1 播种苗期",
                UiTheme.FontBody, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(stageTitle.rectTransform, 24f, 62f, 24f, 32f);

            var dosageList = UiFactory.MakeGroup(dosagePanel.rectTransform, "DosageList");
            UiLayout.TopBand(dosageList, 24f, 110f, 24f, 340f);

            var dosageInputs = new TMP_InputField[UiContent.DosageLabels.Length];
            for (var i = 0; i < UiContent.DosageLabels.Length; i++)
            {
                var cell = UiFactory.MakeGroup(dosageList, "Dosage_" + (i + 1));
                UiLayout.GridCell(cell, 0, i, 1, UiContent.DosageLabels.Length, 0f, 0f, 0f, 0f, 0f, 8f);

                var label = UiFactory.MakeText(cell, "Label", UiContent.DosageLabels[i],
                    UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UiLayout.SplitLeft(label.rectTransform, 0.52f, 16f, 0f, 0f, 0f, 0f);

                dosageInputs[i] = UiFactory.MakeInputField(cell, "Input_" + (i + 1), "0", "0");
                UiLayout.SplitRight(GetRect(dosageInputs[i]), 0.52f, 16f, 0f, 4f, 0f, 4f);
            }

            var dosageNote = UiFactory.MakeText(dosagePanel.rectTransform, "Note",
                "填写后点右下角「确认本阶段方案」：数据写回共享数据，并进入下一阶段。" +
                "灌溉/施肥建议由「适宜度公式 + 推荐水肥方案总表」驱动，数值内核接入后自动填充左栏。",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.TopLeft);
            UiLayout.Row(dosageNote.rectTransform, 24f, 466f, 24f, 96f, 0f, 0);

            // ---------- 底部 ----------
            FooterBack(footer, screen);

            TMP_Text confirmLabel;
            var confirmButton = UiFactory.MakeButton(footer, "Button_ConfirmDosage", "确认本阶段方案", out confirmLabel, true);
            UiLayout.BottomRight(GetRect(confirmButton), 0f, 0f, UiTheme.PrimaryButtonWidth, UiTheme.ButtonHeight);
            // 这颗按钮由 AnalyzeScreen.OnEnter 接线（写回 StageDosage → AdvanceStage → 回农场）。

            var analyze = (AnalyzeScreen)screen;
            analyze.stageTitleText = stageTitle;
            analyze.suggestionValues = suggestionValues;
            analyze.dosageInputs = dosageInputs;
            analyze.confirmButton = confirmButton;
            analyze.confirmLabel = confirmLabel;

            return screen;
        }

        // ==================================================================
        // image12 收益计算
        // ==================================================================

        private static PlaceholderScreen BuildRevenue(Transform parent)
        {
            RectTransform body, footer;
            // 挂专用脚本 RevenueScreen：它按 RevenueReport 刷新公式展开与四阶段明细。
            // 挂通用 PlaceholderScreen 时，页面显示的只是 UiContent.FormulaLines 里的静态文案。

            var screen = NewScreen<RevenueScreen>(parent, GameState.Revenue, UiContent.RevenueTitle, UiContent.RevenueNote,
                false, out body, out footer);

            // ---------- 左：计算公式 + 最终收益 Y ----------
            var formulaPanel = UiFactory.MakePanel(body, "FormulaPanel", UiTheme.PanelBg);
            UiLayout.SplitLeft(formulaPanel.rectTransform, 0.5f, UiTheme.Gap, 0f, 0f, 0f, 0f);

            var formulaTitle = UiFactory.MakeText(formulaPanel.rectTransform, "Title", UiContent.RevenueFormulaTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(formulaTitle.rectTransform, 24f, 20f, 24f, 40f);

            for (var i = 0; i < UiContent.FormulaLines.Length; i++)
            {
                var line = UiFactory.MakeText(formulaPanel.rectTransform, "Formula_" + (i + 1), UiContent.FormulaLines[i],
                    UiTheme.FontBody, i == UiContent.FormulaLines.Length - 1 ? UiTheme.TextAccent : UiTheme.TextSecondary,
                    TextAlignmentOptions.MidlineLeft);
                UiLayout.Row(line.rectTransform, 24f, 72f, 24f, 56f, 8f, i);
            }

            var resultBox = UiFactory.MakePanel(formulaPanel.rectTransform, "ResultBox", UiTheme.PanelBgSoft, UiSkinRole.PanelSoft);
            UiLayout.BottomBand(resultBox.rectTransform, 24f, 24f, 24f, 240f);

            var resultTitle = UiFactory.MakeText(resultBox.rectTransform, "Title", UiContent.RevenueResultTitle,
                UiTheme.FontSection, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(resultTitle.rectTransform, 24f, 20f, 24f, 40f);

            var resultValue = UiFactory.MakeText(resultBox.rectTransform, "Value", UiContent.RevenueResultPlaceholder,
                UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(resultValue.rectTransform, 24f, 64f, 24f, 36f);

            var resultNote = UiFactory.MakeText(resultBox.rectTransform, "Note",
                "公式全部来自文档：F<sub>fert</sub> 取四阶段施肥适宜度均值，F<sub>water</sub> 带阶段权重 α，" +
                "S<sub>static</sub> 恒等于 1；Y<sub>max,geo</sub> 为品种 × 地理潜在产量。白盒阶段先摆位置，数值内核接入后自动填。",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.TopLeft);
            UiLayout.Stretch(resultNote.rectTransform, 24f, 108f, 24f, 20f);

            // ---------- 右：各阶段参数 + 四个不变参数 ----------
            var dataPanel = UiFactory.MakePanel(body, "DataPanel", UiTheme.PanelBg);
            UiLayout.SplitRight(dataPanel.rectTransform, 0.5f, UiTheme.Gap, 0f, 0f, 0f, 0f);

            var tableTitle = UiFactory.MakeText(dataPanel.rectTransform, "Title", UiContent.RevenueStageTableTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(tableTitle.rectTransform, 24f, 20f, 24f, 40f);

            var table = UiFactory.MakeGroup(dataPanel.rectTransform, "StageTable");
            UiLayout.TopBand(table, 24f, 72f, 24f, 300f);
            BuildRevenueStageTable(table);

            var staticTitle = UiFactory.MakeText(dataPanel.rectTransform, "StaticTitle", UiContent.RevenueStaticTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.BottomBand(staticTitle.rectTransform, 24f, 232f, 24f, 36f);

            var staticList = UiFactory.MakeGroup(dataPanel.rectTransform, "StaticList");
            UiLayout.BottomBand(staticList, 24f, 24f, 24f, 196f);
            for (var i = 0; i < UiContent.StaticParamNames.Length; i++)
            {
                var cell = UiFactory.MakeGroup(staticList, "Static_" + (i + 1));
                UiLayout.GridCell(cell, i % 2, i / 2, 2, 2, 0f, 0f, 0f, 0f, UiTheme.Gap, 8f);
                BuildInfoRow(cell, UiContent.StaticParamNames[i],
                    i < UiContent.StaticParamDefaults.Length ? UiContent.StaticParamDefaults[i] : UiContent.AnalyzePending,
                    true);
            }

            // ---------- 底部 ----------
            FooterBack(footer, screen);

            TMP_Text restartLabel;
            var restartButton = UiFactory.MakeButton(footer, "Button_Restart", UiContent.RevenueRestart, out restartLabel, true);
            UiLayout.BottomRight(GetRect(restartButton), 0f, 0f, UiTheme.PrimaryButtonWidth, UiTheme.ButtonHeight);
            screen.AddAction(restartButton, ScreenActionKind.Restart, GameState.Welcome);

            return screen;
        }

        /// <summary>收益计算页「各阶段参数」表：表头 + S2~S5 四行（文档要求列出第二至第五阶段的参数）。</summary>
        private static void BuildRevenueStageTable(RectTransform table)
        {
            const int rows = GameContext.MaxStage; // 表头 + S2~S5 = 5 行
            for (var r = 0; r < rows; r++)
            {
                var isHeader = r == 0;
                var row = UiFactory.MakeGroup(table, isHeader ? "HeaderRow" : "StageRow_S" + (r + 1));
                UiLayout.GridCell(row, 0, r, 1, rows, 0f, 0f, 0f, 0f, 0f, 6f);

                var stage = isHeader ? UiContent.StageTableHeaders[0] : "S" + (r + 1) + "　" + GameContext.StageName(r + 1);
                var dosage = isHeader ? UiContent.StageTableHeaders[1] : UiContent.AnalyzePending;
                var suitability = isHeader ? UiContent.StageTableHeaders[2] : UiContent.AnalyzePending;

                MakeTableText(row, 0, "Stage", stage, isHeader, TextAlignmentOptions.MidlineLeft);
                MakeTableText(row, 1, "Dosage", dosage, isHeader, TextAlignmentOptions.Center);
                MakeTableText(row, 2, "Suitability", suitability, isHeader, TextAlignmentOptions.Center);
            }
        }

        /// <summary>表格里的一格（3 等分列，列间留 16px）。</summary>
        private static void MakeTableText(RectTransform row, int column, string name, string content,
            bool isHeader, TextAlignmentOptions alignment)
        {
            const int columns = 3;

            var cell = UiFactory.MakeGroup(row, name);
            UiLayout.GridCell(cell, column, 0, columns, 1, 0f, 0f, 0f, 0f, 16f, 0f);

            UiFactory.MakeText(cell, "Text", content,
                isHeader ? UiTheme.FontSmall : UiTheme.FontBody,
                isHeader ? UiTheme.TextMuted : (column == 0 ? UiTheme.TextPrimary : UiTheme.TextAccent),
                alignment);
        }
    }
}
