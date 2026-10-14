using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒构建器 · 准备阶段：初始界面 / 项目简介 / 土地类型选择 / 土地基础数据设置 /
    /// 植物类型选择 / 确认选择（对应《Unity界面.docx》image1~image6）。
    /// </summary>
    public static partial class UiWhiteboxBuilder
    {
        // ==================================================================
        // image1 初始界面
        // ==================================================================

        private static PlaceholderScreen BuildWelcome(Transform parent)
        {
            var screenRoot = UiFactory.MakeGroup(parent, GameStateInfo.ScreenObjectName(GameState.Welcome));
            var screen = screenRoot.gameObject.AddComponent<PlaceholderScreen>();
            screen.SetState(GameState.Welcome);

            // 主视觉占位（正式版是项目主视觉 / 3D 农田）
            UiFactory.MakePanel(screenRoot, "HeroBackdrop", UiTheme.ViewBackdrop, UiSkinRole.ScreenBackdrop);

            var title = UiFactory.MakeText(screenRoot, "AppTitle", UiContent.AppTitle,
                UiTheme.FontDisplay, UiTheme.TextPrimary, TextAlignmentOptions.Center);
            UiLayout.Center(title.rectTransform, 0f, 240f, 1500f, 100f);

            var subtitle = UiFactory.MakeText(screenRoot, "AppSubtitle", UiContent.AppSubtitle,
                UiTheme.FontBody, UiTheme.TextSecondary, TextAlignmentOptions.Center);
            UiLayout.Center(subtitle.rectTransform, 0f, 150f, 1400f, 48f);

            var divider = UiFactory.MakeDivider(screenRoot, "TitleDivider");
            UiLayout.Center(divider.rectTransform, 0f, 104f, 900f, 2f);

            // 两颗入口：项目简介 / 开始体验
            TMP_Text introLabel;
            var introButton = UiFactory.MakeButton(screenRoot, "Button_Intro", "项目简介", out introLabel);
            UiLayout.Center(GetRect(introButton), -240f, -60f, 400f, 96f);
            screen.AddAction(introButton, ScreenActionKind.GoTo, GameState.Intro);

            TMP_Text startLabel;
            var startButton = UiFactory.MakeButton(screenRoot, "Button_StartExperience", "开始体验", out startLabel, true);
            UiLayout.Center(GetRect(startButton), 240f, -60f, 400f, 96f);
            screen.AddAction(startButton, ScreenActionKind.GoTo, GameState.LandSelect);

            var version = UiFactory.MakeText(screenRoot, "Version", UiContent.AppFooter,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiLayout.BottomLeft(version.rectTransform, UiTheme.SideMargin, UiTheme.BottomMargin, 1200f, 32f);

            TMP_Text quitLabel;
            var quitButton = PlaceholderButton(screenRoot, screen, "Button_Quit", "退出", "退出应用", out quitLabel);
            UiLayout.BottomRight(GetRect(quitButton), UiTheme.SideMargin, UiTheme.BottomMargin, 200f, 56f);

            return screen;
        }

        // ==================================================================
        // image2 项目简介（弹窗）
        // ==================================================================

        private static PlaceholderScreen BuildIntro(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen(parent, GameState.Intro, UiContent.IntroTitle,
                "由初始界面的「项目简介」弹出；点「知道了」或左下角返回初始界面。", true, out body, out footer);

            var panel = UiFactory.MakePanel(body, "IntroPanel", UiTheme.PanelBg);
            UiLayout.Center(panel.rectTransform, 0f, 0f, 1240f, 620f);

            var bodyText = UiFactory.MakeText(panel.rectTransform, "IntroBody", UiContent.IntroBody,
                UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            UiLayout.Stretch(bodyText.rectTransform, 48f, 48f, 48f, 48f);

            FooterBack(footer, screen);
            FooterPrimary(footer, screen, "知道了", GameState.Welcome);

            return screen;
        }

        // ==================================================================
        // 通用：选项卡片
        // ==================================================================

        /// <summary>一张选项卡：编号 + 名称 + 简介 + 按钮。土地选择 / 植物选择共用。</summary>
        private static Button BuildSelectCard(RectTransform cell, PlaceholderScreen screen, string objectName,
            string badge, string title, string description, string actionLabel, GameState target, bool enabled)
        {
            var card = UiFactory.MakePanel(cell, objectName, enabled ? UiTheme.CardBgSelected : UiTheme.CardBg, UiSkinRole.Card);

            var badgeText = UiFactory.MakeText(card.rectTransform, "Badge", badge,
                UiTheme.FontSmall, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(badgeText.rectTransform, 24f, 20f, 24f, 28f);

            var titleText = UiFactory.MakeText(card.rectTransform, "Title", title,
                UiTheme.FontTitle, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(titleText.rectTransform, 24f, 52f, 24f, 60f);

            var descText = UiFactory.MakeText(card.rectTransform, "Description", description,
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.TopLeft);
            UiLayout.Stretch(descText.rectTransform, 24f, 128f, 24f, 104f);

            TMP_Text buttonLabel;
            var button = UiFactory.MakeButton(card.rectTransform, enabled ? "Button_Select" : "Button_Locked",
                actionLabel, out buttonLabel, enabled);
            UiLayout.BottomBand(GetRect(button), 24f, 24f, 24f, 60f);

            // 未开放的地形 / 作物：只打日志，方便验收时确认「哪张图还没做」。
            if (enabled) screen.AddAction(button, ScreenActionKind.GoTo, target);
            else screen.AddAction(button, ScreenActionKind.LogOnly, GameState.Welcome, title);

            return button;
        }

        // ==================================================================
        // image3 土地类型选择
        // ==================================================================

        private static PlaceholderScreen BuildLandSelect(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen(parent, GameState.LandSelect, "土地类型选择",
                "四块地横排；点卡片下方按钮选择，左下角返回上一步。", false, out body, out footer);

            var cards = UiFactory.MakeGroup(body, "LandCards");
            UiLayout.TopBand(cards, 0f, 0f, 0f, 470f);

            for (var i = 0; i < UiContent.LandNames.Length; i++)
            {
                var cell = UiFactory.MakeGroup(cards, "Cell_Land" + (i + 1));
                UiLayout.GridCell(cell, i, 0, UiContent.LandNames.Length, 1, 0f, 0f, 0f, 0f, 32f, 0f);

                BuildSelectCard(cell, screen, "Card_Land" + (i + 1), "土地类型 " + (i + 1),
                    UiContent.LandNames[i], UiContent.LandIntros[i],
                    i == 0 ? "选择" : "暂未开放", GameState.LandParams, i == 0);
            }

            var note = UiFactory.MakeText(body, "Note", UiContent.LandSelectNote,
                UiTheme.FontBody, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.Row(note.rectTransform, 0f, 496f, 0f, 40f, 0f, 0);

            FooterBack(footer, screen);
            return screen;
        }

        // ==================================================================
        // image5 植物类型选择
        // ==================================================================

        private static PlaceholderScreen BuildPlantSelect(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen(parent, GameState.PlantSelect, "植物类型选择",
                "四种作物横排；点卡片下方按钮选择，左下角返回上一步。", false, out body, out footer);

            var cards = UiFactory.MakeGroup(body, "PlantCards");
            UiLayout.TopBand(cards, 0f, 0f, 0f, 470f);

            for (var i = 0; i < UiContent.PlantNames.Length; i++)
            {
                var cell = UiFactory.MakeGroup(cards, "Cell_Plant" + (i + 1));
                UiLayout.GridCell(cell, i, 0, UiContent.PlantNames.Length, 1, 0f, 0f, 0f, 0f, 32f, 0f);

                BuildSelectCard(cell, screen, "Card_Plant" + (i + 1), "植物类型 " + (i + 1),
                    UiContent.PlantNames[i], UiContent.PlantIntros[i],
                    i == 0 ? "选择" : "暂未开放", GameState.Confirm, i == 0);
            }

            var note = UiFactory.MakeText(body, "Note", UiContent.PlantSelectNote,
                UiTheme.FontBody, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.Row(note.rectTransform, 0f, 496f, 0f, 40f, 0f, 0);

            FooterBack(footer, screen);
            return screen;
        }

        // ==================================================================
        // image4 土地基础数据设置
        // ==================================================================

        private static PlaceholderScreen BuildLandParams(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen(parent, GameState.LandParams, "土地基础数据设置",
                "六项基础数值：土地面积与五项动态指标由玩家输入，四项静态指标在第一阶段随机生成。",
                false, out body, out footer);

            var form = UiFactory.MakeGroup(body, "ParamForm");
            UiLayout.TopBand(form, 0f, 0f, 0f, 536f);

            for (var i = 0; i < UiContent.LandParamLabels.Length; i++)
            {
                var cell = UiFactory.MakeGroup(form, "Row_Param" + (i + 1));
                UiLayout.GridCell(cell, 0, i, 1, UiContent.LandParamLabels.Length, 0f, 0f, 0f, 0f, 0f, 12f);

                var label = UiFactory.MakeText(cell, "Label", UiContent.LandParamLabels[i],
                    UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
                UiLayout.SplitLeft(label.rectTransform, 0.44f, 24f, 8f, 0f, 0f, 0f);

                var field = UiFactory.MakeInputField(cell, "Input_" + (i + 1), "请输入…", UiContent.LandParamDefaults[i]);
                UiLayout.SplitRight(GetRect(field), 0.44f, 24f, 0f, 6f, 0f, 6f);
            }

            var note = UiFactory.MakeText(body, "Note", UiContent.LandParamNote,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineLeft);
            UiLayout.Row(note.rectTransform, 4f, 556f, 0f, 32f, 0f, 0);

            FooterBack(footer, screen);
            FooterPrimary(footer, screen, "下一步：选择植物类型", GameState.PlantSelect);
            return screen;
        }

        // ==================================================================
        // image6 确认选择
        // ==================================================================

        private static PlaceholderScreen BuildConfirm(Transform parent)
        {
            RectTransform body, footer;
            var screen = NewScreen(parent, GameState.Confirm, UiContent.ConfirmTitle, UiContent.ConfirmNote,
                false, out body, out footer);

            var panel = UiFactory.MakePanel(body, "SummaryPanel", UiTheme.PanelBg);
            UiLayout.Center(panel.rectTransform, 0f, 20f, 1280f, 560f);

            var heading = UiFactory.MakeText(panel.rectTransform, "Heading", "本局配置一览",
                UiTheme.FontSection, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(heading.rectTransform, 40f, 28f, 40f, 40f);

            // 12 项 = 土地/植物/面积 + 五项动态指标 + 四项静态指标，排成 2 列 × 6 行。
            var labels = new[]
            {
                "土地类型", "植物类型", "土地面积（亩）", "酸衡值（pH）", "腐殖丰度", "速效氮元",
                "磷活度", "钾储量", "氧还势 (Eh)", "土骨离子", "毒砷潜势", "汞积量",
            };
            var values = new[]
            {
                "平川台", "玉米", "10", "6.5", "22", "95",
                "17", "150", "470 mV", "11", "9", "0.07 mg/kg",
            };

            var grid = UiFactory.MakeGroup(panel.rectTransform, "SummaryGrid");
            UiLayout.Stretch(grid, 40f, 96f, 40f, 40f);

            for (var i = 0; i < labels.Length; i++)
            {
                var cell = UiFactory.MakeGroup(grid, "Row_" + (i + 1));
                UiLayout.GridCell(cell, i / 6, i % 6, 2, 6, 0f, 0f, 0f, 0f, 48f, 4f);
                BuildInfoRow(cell, labels[i], values[i], i < 2);
            }

            var note = UiFactory.MakeText(body, "Note",
                "占位数值：接入 GameContext / 数值内核后，这里会自动显示玩家实际填写的值。",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.Center);
            UiLayout.Center(note.rectTransform, 0f, -300f, 1180f, 40f);

            FooterBack(footer, screen);
            FooterPrimary(footer, screen, "进入农场", GameState.FarmStage);
            return screen;
        }

        /// <summary>「标签 —— 数值」一行，确认页 / 分析页 / 收益页共用；返回数值文本便于运行时刷新。</summary>
        private static TMP_Text BuildInfoRow(RectTransform cell, string label, string value, bool highlightValue = false)
        {
            var labelText = UiFactory.MakeText(cell, "Label", label,
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UiLayout.SplitLeft(labelText.rectTransform, 0.55f, 16f, 0f, 0f, 0f, 0f);

            var valueText = UiFactory.MakeText(cell, "Value", value,
                UiTheme.FontBody, highlightValue ? UiTheme.TextAccent : UiTheme.TextPrimary, TextAlignmentOptions.MidlineRight);
            UiLayout.SplitRight(valueText.rectTransform, 0.55f, 16f, 0f, 0f, 0f, 0f);

            return valueText;
        }
    }
}
