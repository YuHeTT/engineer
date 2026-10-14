using UnityEngine;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒主题：颜色、字号、间距全部集中在这里。
    /// 文档《Unity界面.docx》开头写明「图片中颜色只做区分，不作为标准」，
    /// 所以这里只用一组**暖橙褐色系**把区域区分开，正式配色等美术出稿后整体替换这一处即可。
    /// </summary>
    public static class UiTheme
    {
        // ---------- 颜色 ----------
        /// <summary>正式版是 3D 农场画面，白盒阶段用这个底色占位。</summary>
        public static readonly Color ViewBackdrop = new Color(0.086f, 0.063f, 0.043f, 1f);

        /// <summary>弹窗/对话框背后压暗的一层。</summary>
        public static readonly Color ScreenDim = new Color(0.043f, 0.035f, 0.027f, 0.80f);

        /// <summary>主面板底色。</summary>
        public static readonly Color PanelBg = new Color(0.161f, 0.110f, 0.071f, 0.94f);

        /// <summary>次级面板底色（半透明，HUD 用）。</summary>
        public static readonly Color PanelBgSoft = new Color(0.129f, 0.086f, 0.055f, 0.82f);

        /// <summary>卡片底色。</summary>
        public static readonly Color CardBg = new Color(0.220f, 0.157f, 0.102f, 0.96f);

        /// <summary>卡片选中态。</summary>
        public static readonly Color CardBgSelected = new Color(0.451f, 0.275f, 0.110f, 0.98f);

        /// <summary>高亮橙（对应文档里的按钮按下色 #9c511d）。</summary>
        public static readonly Color Accent = new Color(0.612f, 0.318f, 0.114f, 1f);

        /// <summary>按钮常态（文档里的 #593114）。</summary>
        public static readonly Color ButtonNormal = new Color(0.349f, 0.192f, 0.078f, 1f);
        public static readonly Color ButtonHighlighted = new Color(0.420f, 0.251f, 0.118f, 1f);
        public static readonly Color ButtonPressed = new Color(0.612f, 0.318f, 0.114f, 1f);
        public static readonly Color ButtonDisabled = new Color(0.220f, 0.196f, 0.176f, 0.60f);

        /// <summary>主按钮（每屏的主行动）。</summary>
        public static readonly Color ButtonPrimaryNormal = new Color(0.722f, 0.361f, 0.098f, 1f);
        public static readonly Color ButtonPrimaryHighlighted = new Color(0.804f, 0.443f, 0.153f, 1f);
        public static readonly Color ButtonPrimaryPressed = new Color(0.612f, 0.318f, 0.114f, 1f);

        public static readonly Color TextPrimary = new Color(0.961f, 0.929f, 0.878f, 1f);
        public static readonly Color TextSecondary = new Color(0.749f, 0.702f, 0.627f, 1f);
        public static readonly Color TextMuted = new Color(0.549f, 0.502f, 0.443f, 1f);
        public static readonly Color TextAccent = new Color(0.976f, 0.729f, 0.427f, 1f);

        /// <summary>分隔线 / 进度条底槽。</summary>
        public static readonly Color Divider = new Color(1f, 1f, 1f, 0.10f);

        /// <summary>输入框底色与占位文字色。</summary>
        public static readonly Color InputBg = new Color(0.106f, 0.078f, 0.055f, 0.98f);
        public static readonly Color InputPlaceholder = new Color(0.482f, 0.435f, 0.396f, 1f);

        /// <summary>「无数据」这类灰显。</summary>
        public static readonly Color ValueMuted = new Color(0.478f, 0.443f, 0.404f, 1f);

        /// <summary>静态指标（氧还势/土骨离子/毒砷潜势/汞积量）的标记色，用来一眼区分「不可改」。</summary>
        public static readonly Color StaticTagBg = new Color(0.278f, 0.255f, 0.220f, 0.85f);

        // ---------- 字号（1920×1080 参考分辨率）----------
        public const int FontDisplay = 64;
        public const int FontTitle = 44;
        public const int FontSection = 30;
        public const int FontBody = 24;
        public const int FontSmall = 20;
        public const int FontButton = 26;

        // ---------- 尺寸 ----------
        public const float CanvasWidth = 1920f;
        public const float CanvasHeight = 1080f;
        public const float SideMargin = 72f;
        public const float TopMargin = 44f;
        public const float BottomMargin = 44f;
        public const float HeaderHeight = 108f;
        public const float FooterHeight = 64f;
        public const float Gap = 24f;
        public const float ButtonHeight = 64f;
        public const float BackButtonWidth = 240f;
        public const float PrimaryButtonWidth = 300f;
    }
}
