using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒界面工厂：只做「创建 + 上色」，摆放统一交给 <see cref="UiLayout"/>。
    /// </summary>
    public static class UiFactory
    {
        /// <summary>中文字体：项目自带的微软雅黑 SDF。</summary>
        private const string FontAssetPath = "Assets/fonts/msyh SDF.asset";

        private static TMP_FontAsset cachedFont;

        /// <summary>全工程统一字体。找不到时回退到 TMP 默认字体并给出警告。</summary>
        public static TMP_FontAsset Font
        {
            get
            {
                if (cachedFont != null) return cachedFont;

                cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
                if (cachedFont == null)
                {
                    Debug.LogWarning("[UI] 找不到中文字体 " + FontAssetPath + "，本次回退到 TMP 默认字体。");
                    var settings = TMP_Settings.instance;
                    if (settings != null) cachedFont = TMP_Settings.defaultFontAsset;
                }

                return cachedFont;
            }
        }

        // ==================================================================
        // 创建
        // ==================================================================

        /// <summary>创建空容器（默认铺满父级）。</summary>
        public static RectTransform MakeGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            UiLayout.Stretch(rt, 0f, 0f, 0f, 0f);
            return rt;
        }

        /// <summary>创建纯色图片（默认不接收射线，避免挡住按钮）。</summary>
        public static Image MakeImage(Transform parent, string name, Color color)
        {
            return MakeImage(parent, name, color, UiSkinRole.None);
        }

        /// <summary>
        /// 同上，并登记「皮肤角色」：工具 Tools ▸ UI ▸ 皮肤：套用 EPS 素材 时不看名字、只看这个角色，
        /// 所以界面重建后素材也能一键套回来（见 <see cref="UiSkinSlot"/>）。
        /// </summary>
        public static Image MakeImage(Transform parent, string name, Color color, UiSkinRole role)
        {
            var rt = MakeGroup(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            UiSkinSlot.Set(rt.gameObject, role);
            return image;
        }

        /// <summary>创建面板（纯色图片 + 会挡住点击穿透），默认登记为「主面板」角色。</summary>
        public static Image MakePanel(Transform parent, string name, Color color)
        {
            return MakePanel(parent, name, color, UiSkinRole.Panel);
        }

        /// <summary>同上，但指定角色：HUD 数据面板用 PanelSoft、选项卡用 Card、全屏底图用 ScreenBackdrop。</summary>
        public static Image MakePanel(Transform parent, string name, Color color, UiSkinRole role)
        {
            var image = MakeImage(parent, name, color, role);
            image.raycastTarget = true;
            return image;
        }

        /// <summary>创建文本。</summary>
        public static TMP_Text MakeText(Transform parent, string name, string content, int fontSize, Color color)
        {
            return MakeText(parent, name, content, fontSize, color, TextAlignmentOptions.TopLeft);
        }

        public static TMP_Text MakeText(Transform parent, string name, string content, int fontSize, Color color, TextAlignmentOptions alignment)
        {
            var rt = MakeGroup(parent, name);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.text = content;
            text.richText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>水平分隔线。</summary>
        public static Image MakeDivider(Transform parent, string name)
        {
            return MakeImage(parent, name, UiTheme.Divider, UiSkinRole.Divider);
        }

        /// <summary>按钮：底图 + 居中文字。</summary>
        public static Button MakeButton(Transform parent, string name, string label, out TMP_Text labelText)
        {
            return MakeButton(parent, name, label, out labelText, false);
        }

        /// <summary>按钮：primary=true 时用更亮的主行动色。</summary>
        public static Button MakeButton(Transform parent, string name, string label, out TMP_Text labelText, bool primary)
        {
            var rt = MakeGroup(parent, name);

            var background = rt.gameObject.AddComponent<Image>();
            background.color = primary ? UiTheme.ButtonPrimaryNormal : UiTheme.ButtonNormal;
            background.raycastTarget = true;
            UiSkinSlot.Set(rt.gameObject, primary ? UiSkinRole.ButtonPrimary : UiSkinRole.Button);

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = BuildColorBlock(primary);

            labelText = MakeText(rt, "Label", label, UiTheme.FontButton, UiTheme.TextPrimary, TextAlignmentOptions.Center);
            UiLayout.Stretch(labelText.rectTransform, 12f, 4f, 12f, 4f);

            return button;
        }

        /// <summary>进度条：返回底槽，fill 为可按 fillAmount 控制的填充。</summary>
        public static Image MakeProgressBar(Transform parent, string name, out Image fill)
        {
            var track = MakeImage(parent, name, UiTheme.Divider, UiSkinRole.BarTrack);
            fill = MakeImage(track.rectTransform, "Fill", UiTheme.Accent, UiSkinRole.BarFill);
            UiLayout.Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            return track;
        }

        /// <summary>
        /// 数值输入框（TMP_InputField）。
        /// 层级照抄 TMP 官方预制体：InputField → Text Area(RectMask2D) → Placeholder / Text。
        /// </summary>
        public static TMP_InputField MakeInputField(Transform parent, string name, string placeholder, string initialText)
        {
            var rt = MakeGroup(parent, name);

            var background = rt.gameObject.AddComponent<Image>();
            background.color = UiTheme.InputBg;
            background.raycastTarget = true;
            UiSkinSlot.Set(rt.gameObject, UiSkinRole.InputField);

            var field = rt.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = background;

            var area = MakeGroup(rt, "Text Area");
            UiLayout.Stretch(area, 16f, 8f, 16f, 8f);
            area.gameObject.AddComponent<RectMask2D>();

            var text = MakeText(area, "Text", initialText, UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);

            var placeholderText = MakeText(area, "Placeholder", placeholder, UiTheme.FontBody, UiTheme.InputPlaceholder, TextAlignmentOptions.MidlineLeft);
            UiLayout.Stretch(placeholderText.rectTransform, 0f, 0f, 0f, 0f);

            field.textViewport = area;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.DecimalNumber;
            field.characterLimit = 8;
            field.onFocusSelectAll = true;
            field.restoreOriginalTextOnEscape = true;
            field.caretBlinkRate = 0.85f;

            return field;
        }

        private static ColorBlock BuildColorBlock(bool primary)
        {
            return new ColorBlock
            {
                normalColor = primary ? UiTheme.ButtonPrimaryNormal : UiTheme.ButtonNormal,
                highlightedColor = primary ? UiTheme.ButtonPrimaryHighlighted : UiTheme.ButtonHighlighted,
                pressedColor = primary ? UiTheme.ButtonPrimaryPressed : UiTheme.ButtonPressed,
                selectedColor = primary ? UiTheme.ButtonPrimaryNormal : UiTheme.ButtonNormal,
                disabledColor = UiTheme.ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
        }
    }
}
