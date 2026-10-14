using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒构建器 · 农场与机器人阶段：image7 / image11 农场主界面、image8 / image9 机器人操控。
    /// 这两屏都是 HUD（盖在 3D 画面上），白盒阶段用深色底 + 占位说明代替模型。
    /// </summary>
    public static partial class UiWhiteboxBuilder
    {
        // ==================================================================
        // image7 / image11 农场主界面（S1–S5 共用）
        // ==================================================================

        private static PlaceholderScreen BuildFarmStage(Transform parent)
        {
            RectTransform hud, bar;
            var screen = NewHudScreen<FarmStageScreen>(parent, GameState.FarmStage,
                "3D 农场场景占位：第三人称视角，视角中心是玉米田，旁边停着智能房车。\n" +
                "需要采集土壤数据时，点右下角「机器人检测」进入机器人操控界面。", out hud, out bar);

            // ---------- 顶部：左提示 / 右阶段 ----------
            var topBand = UiFactory.MakeGroup(hud, "TopBand");
            UiLayout.TopBand(topBand, 0f, 0f, 0f, 132f);

            var hintText = UiFactory.MakeText(topBand, "Hint", UiContent.FarmHint,
                UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            UiLayout.SplitLeft(hintText.rectTransform, 0.56f, 32f, 0f, 0f, 0f, 0f);

            var stageGroup = UiFactory.MakeGroup(topBand, "StageGroup");
            UiLayout.SplitRight(stageGroup, 0.56f, 32f, 0f, 0f, 0f, 0f);

            var stageTitle = UiFactory.MakeText(stageGroup, "StageTitle", "S1 播种苗期",
                UiTheme.FontSection, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(stageTitle.rectTransform, 0f, 8f, 0f, 48f);

            var stageStatus = UiFactory.MakeText(stageGroup, "StageStatus", "白盒占位：正常生长",
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(stageStatus.rectTransform, 0f, 64f, 0f, 32f);

            // ---------- 左下：所有数据（9 项，3×3）----------
            var dataPanel = UiFactory.MakePanel(hud, "DataPanel", UiTheme.PanelBgSoft);
            UiLayout.BottomLeft(dataPanel.rectTransform, 0f, 92f, 900f, 360f);

            var dataTitle = UiFactory.MakeText(dataPanel.rectTransform, "Title", UiContent.DataPanelTitle,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(dataTitle.rectTransform, 24f, 18f, 24f, 40f);

            var dataTag = UiFactory.MakeText(dataPanel.rectTransform, "Tag", "静态 4 项灰显，动态 5 项随水肥变化",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineRight);
            UiLayout.TopBand(dataTag.rectTransform, 24f, 24f, 24f, 28f);

            var indicatorValues = BuildIndicatorGrid(dataPanel.rectTransform);

            // ---------- 右下：机器人检测 / 主按钮 ----------
            TMP_Text robotLabel;
            var robotButton = UiFactory.MakeButton(bar, "Button_RobotCruise", "机器人检测", out robotLabel);
            UiLayout.BottomRight(GetRect(robotButton), UiTheme.PrimaryButtonWidth + 24f, 0f, 260f, UiTheme.ButtonHeight);
            screen.AddAction(robotButton, ScreenActionKind.GoTo, GameState.RobotCruise);

            TMP_Text primaryLabel;
            var primaryButton = UiFactory.MakeButton(bar, "Button_Primary", "开始分析", out primaryLabel, true);
            UiLayout.BottomRight(GetRect(primaryButton), 0f, 0f, UiTheme.PrimaryButtonWidth, UiTheme.ButtonHeight);

            // 注意：主按钮**故意不在这里接线**。FarmStageScreen 会在进入本屏时自己挂监听，
            // S1–S4 跳「开始分析」、S5 跳「收益计算」；这里再加一条会互相打架。
            var farm = (FarmStageScreen)screen;
            farm.hintText = hintText;
            farm.stageTitleText = stageTitle;
            farm.stageStatusText = stageStatus;
            farm.indicatorValueTexts = indicatorValues;
            farm.primaryButton = primaryButton;
            farm.primaryButtonLabel = primaryLabel;

            return screen;
        }

        /// <summary>「所有数据」3 列 × 3 行的 9 项指标，返回数值文本数组供运行时刷新。</summary>
        private static TMP_Text[] BuildIndicatorGrid(RectTransform panel)
        {
            var grid = UiFactory.MakeGroup(panel, "Indicators");
            UiLayout.Stretch(grid, 24f, 72f, 24f, 24f);

            var values = new TMP_Text[SoilIndicators.Count];
            for (var i = 0; i < SoilIndicators.Count; i++)
            {
                var isStatic = SoilIndicators.IsStatic[i];
                var cell = UiFactory.MakeGroup(grid, "Indicator_" + (i + 1));
                UiLayout.GridCell(cell, i % 3, i / 3, 3, 3, 0f, 0f, 0f, 0f, 32f, 8f);

                var label = UiFactory.MakeText(cell, "Name", SoilIndicators.Names[i],
                    UiTheme.FontSmall, isStatic ? UiTheme.ValueMuted : UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
                UiLayout.SplitLeft(label.rectTransform, 0.62f, 12f, 0f, 0f, 0f, 0f);

                var value = UiFactory.MakeText(cell, "Value", SoilIndicators.Format(i, 0f),
                    UiTheme.FontBody, isStatic ? UiTheme.ValueMuted : UiTheme.TextAccent, TextAlignmentOptions.MidlineRight);
                UiLayout.SplitRight(value.rectTransform, 0.62f, 12f, 0f, 0f, 0f, 0f);

                values[i] = value;
            }

            return values;
        }

        // ==================================================================
        // image8 / image9 机器人操控（手动巡航 / 自动巡航）
        // ==================================================================

        private static PlaceholderScreen BuildRobotCruise(Transform parent)
        {
            RectTransform hud;
            var screen = NewHudScreen<RobotCruiseScreen>(parent, GameState.RobotCruise,
                "3D 农场场景占位：机器人视角看向未覆盖的玉米田（手动巡航用 W / A / S / D 移动）。",
                out hud, out _);

            // ---------- 顶部：左提示 / 右巡航进度 ----------
            var topBand = UiFactory.MakeGroup(hud, "TopBand");
            UiLayout.TopBand(topBand, 0f, 0f, 0f, 190f);

            var hintText = UiFactory.MakeText(topBand, "Hint", UiContent.CruiseHint,
                UiTheme.FontSmall, UiTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            UiLayout.SplitLeft(hintText.rectTransform, 0.62f, 32f, 0f, 0f, 0f, 0f);

            var progress = UiFactory.MakeGroup(topBand, "ProgressGroup");
            UiLayout.SplitRight(progress, 0.62f, 32f, 0f, 0f, 0f, 0f);

            var coverageTitle = UiFactory.MakeText(progress, "CoverageTitle", "巡航覆盖率",
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(coverageTitle.rectTransform, 0f, 8f, 0f, 28f);

            Image fill;
            var track = UiFactory.MakeProgressBar(progress, "CoverageBar", out fill);
            UiLayout.TopBand(track.rectTransform, 0f, 40f, 0f, 18f);

            var coverageText = UiFactory.MakeText(progress, "CoverageText", "已覆盖 0%",
                UiTheme.FontBody, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(coverageText.rectTransform, 0f, 68f, 0f, 32f);

            var uncoveredText = UiFactory.MakeText(progress, "UncoveredText", "未覆盖面积 -- 亩",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(uncoveredText.rectTransform, 0f, 106f, 0f, 28f);

            var legend = UiFactory.MakeText(progress, "Legend",
                "巡航推进方式：手动逐格 / 自动一次跑完；探测数据全部显示后才能「更新数据」。",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.TopLeft);
            UiLayout.TopBand(legend.rectTransform, 0f, 138f, 0f, 44f);

            // ---------- 左下：原数据 / 右下：探测数据 ----------
            TMP_Text[] sourceValues;
            BuildValuePanel(hud, "SourcePanel", UiContent.CruiseSourceTitle, "进入本屏时从共享数据抄一份",
                true, false, out sourceValues);

            TMP_Text[] probeValues;
            BuildValuePanel(hud, "ProbePanel", UiContent.CruiseProbeTitle, "探测到才会显示，未探测显示「无」",
                false, true, out probeValues);

            // ---------- 底部中间：巡航操作（4 颗）----------
            var actions = UiFactory.MakeGroup(hud, "ActionRow");
            UiLayout.Place(actions, new Vector2(0.26f, 0f), new Vector2(0.74f, 0f),
                new Vector2(0f, 16f), new Vector2(0f, 80f));

            TMP_Text manualLabel;
            var manualButton = UiFactory.MakeButton(actions, "Button_ManualCruise", UiContent.CruiseManual, out manualLabel);
            UiLayout.GridCell(GetRect(manualButton), 0, 0, 4, 1, 0f, 0f, 0f, 0f, 20f, 0f);

            TMP_Text autoLabel;
            var autoButton = UiFactory.MakeButton(actions, "Button_AutoCruise", UiContent.CruiseAuto, out autoLabel);
            UiLayout.GridCell(GetRect(autoButton), 1, 0, 4, 1, 0f, 0f, 0f, 0f, 20f, 0f);

            TMP_Text updateLabel;
            var updateButton = UiFactory.MakeButton(actions, "Button_UpdateData", UiContent.CruiseUpdate, out updateLabel, true);
            UiLayout.GridCell(GetRect(updateButton), 2, 0, 4, 1, 0f, 0f, 0f, 0f, 20f, 0f);

            TMP_Text backLabel;
            var backButton = UiFactory.MakeButton(actions, "Button_BackToFarm", "返回农场", out backLabel);
            UiLayout.GridCell(GetRect(backButton), 3, 0, 4, 1, 0f, 0f, 0f, 0f, 20f, 0f);
            screen.AddAction(backButton, ScreenActionKind.Back, GameState.FarmStage);

            // 手动 / 自动 / 更新三颗按钮由 RobotCruiseScreen.OnEnter 自己接线（含 Step 推进与「正在检测」状态）。
            var cruise = (RobotCruiseScreen)screen;
            cruise.hintText = hintText;
            cruise.coverageFill = fill;
            cruise.coverageText = coverageText;
            cruise.uncoveredText = uncoveredText;
            cruise.sourceValueTexts = sourceValues;
            cruise.probeValueTexts = probeValues;
            cruise.manualButton = manualButton;
            cruise.autoButton = autoButton;
            cruise.updateButton = updateButton;

            return screen;
        }

        /// <summary>左右两块「9 项指标」面板：<paramref name="probe"/> = true 时初值显示「无」。</summary>
        private static void BuildValuePanel(RectTransform hud, string objectName, string title, string subtitle,
            bool alignLeft, bool probe, out TMP_Text[] values)
        {
            var panel = UiFactory.MakePanel(hud, objectName, UiTheme.PanelBgSoft);
            if (alignLeft) UiLayout.BottomLeft(panel.rectTransform, 0f, 92f, 460f, 430f);
            else UiLayout.BottomRight(panel.rectTransform, 0f, 92f, 460f, 430f);

            var heading = UiFactory.MakeText(panel.rectTransform, "Title", title,
                UiTheme.FontSection, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(heading.rectTransform, 20f, 16f, 20f, 40f);

            var sub = UiFactory.MakeText(panel.rectTransform, "Subtitle", subtitle,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(sub.rectTransform, 20f, 58f, 20f, 28f);

            var list = UiFactory.MakeGroup(panel.rectTransform, "Values");
            UiLayout.Stretch(list, 20f, 92f, 20f, 20f);

            values = new TMP_Text[SoilIndicators.Count];
            for (var i = 0; i < SoilIndicators.Count; i++)
            {
                var isStatic = SoilIndicators.IsStatic[i];
                var cell = UiFactory.MakeGroup(list, "Value_" + (i + 1));
                UiLayout.GridCell(cell, 0, i, 1, SoilIndicators.Count, 0f, 0f, 0f, 0f, 0f, 2f);

                var label = UiFactory.MakeText(cell, "Name", SoilIndicators.Names[i], UiTheme.FontSmall,
                    isStatic ? UiTheme.ValueMuted : UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
                UiLayout.SplitLeft(label.rectTransform, 0.6f, 12f, 0f, 0f, 0f, 0f);

                var value = UiFactory.MakeText(cell, "Value",
                    probe ? SoilIndicators.NoDataText : SoilIndicators.Format(i, 0f),
                    UiTheme.FontBody, probe ? UiTheme.ValueMuted : UiTheme.TextAccent, TextAlignmentOptions.MidlineRight);
                UiLayout.SplitRight(value.rectTransform, 0.6f, 12f, 0f, 0f, 0f, 0f);

                values[i] = value;
            }
        }
    }
}
