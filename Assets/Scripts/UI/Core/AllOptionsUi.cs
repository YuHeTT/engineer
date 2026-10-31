using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 把土地/植物选择页的四张卡全部开放。
    ///
    /// 现有 UI.unity 是白盒构建后手工调过的，不能重跑构建；所以这里按节点名
    /// 找到卡片按钮，补上 <see cref="OptionCardButton"/> 并改按钮文案。
    /// 运行时由 <see cref="GameFlowController.Awake"/> 调一次，保证即使没执行
    /// 编辑器补丁菜单，Play 时四张卡也能用。
    ///
    /// 编辑器菜单 Tools ▸ UI ▸ 开放全部土地与植物 会调用同一份逻辑，并额外
    /// 删掉旧的 ScreenAction，保证编辑模式也显示「选择」。
    /// </summary>
    public static class AllOptionsUi
    {
        public static readonly string[] LandIds = { "flat_plain", "low_wetland", "steep_ridge", "plateau" };
        public static readonly string[] LandNames = { "平川台", "泽洼域", "崇岭阶", "高原坪" };
        public static readonly string[] PlantIds = { "corn", "beet", "sweet_potato", "potato" };
        public static readonly string[] PlantNames = { "玉米", "甜菜", "红薯", "土豆" };

        private const string LandSubtitle = "四块地横排；点「选择」进入参数设置，左下角返回上一步。";
        private const string LandNote = "备注：四块地均可选择。";
        private const string PlantSubtitle = "四种作物横排；点「选择」进入确认界面，左下角返回上一步。";
        private const string PlantNote = "备注：四种作物均可选择。";

        /// <summary>在 UI 根节点下开放全部土地/植物卡片；可重复调用。</summary>
        public static void Apply(Transform uiRoot)
        {
            if (uiRoot == null)
            {
                var flow = Object.FindObjectOfType<GameFlowController>();
                if (flow != null) uiRoot = flow.transform.root;
            }

            if (uiRoot == null) return;

            var land = FindDeep(uiRoot, "Screen_LandSelect");
            if (land != null)
            {
                Patch(land, "Card_Land", LandIds, LandNames,
                    OptionCategory.Land, GameState.LandParams,
                    LandSubtitle, LandNote);
            }

            var plant = FindDeep(uiRoot, "Screen_PlantSelect");
            if (plant != null)
            {
                Patch(plant, "Card_Plant", PlantIds, PlantNames,
                    OptionCategory.Plant, GameState.Confirm,
                    PlantSubtitle, PlantNote);
            }
        }

        private static void Patch(Transform screen, string cardPrefix, string[] ids, string[] names,
            OptionCategory category, GameState target, string subtitle, string note)
        {
            for (var i = 0; i < ids.Length; i++)
            {
                var card = FindDeep(screen, cardPrefix + (i + 1));
                if (card == null) continue;

                var buttonTransform = card.Find("Button_Select");
                if (buttonTransform == null) buttonTransform = card.Find("Button_Locked");
                if (buttonTransform == null) continue;

                var button = buttonTransform.GetComponent<Button>();
                if (button == null) continue;

                button.interactable = true;
                button.name = "Button_Select";

                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "选择";

                var option = button.GetComponent<OptionCardButton>();
                if (option == null) option = button.gameObject.AddComponent<OptionCardButton>();

                option.optionId = ids[i];
                option.displayName = names[i];
                option.category = category;
                option.targetState = target;
                option.description = category == OptionCategory.Land && i < LandPreviewContent.Intros.Length
                    ? LandPreviewContent.Intros[i]
                    : string.Empty;
            }

            var subtitleText = FindDeep(screen, "Subtitle");
            if (subtitleText != null)
            {
                var tmp = subtitleText.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = subtitle;
            }

            var noteText = FindDeep(screen, "Note");
            if (noteText != null)
            {
                var tmp = noteText.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = note;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }

            return null;
        }
    }
}
