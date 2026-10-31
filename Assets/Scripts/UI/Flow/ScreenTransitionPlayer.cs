using System;
using System.Collections;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>界面切换的样式，对应 PPT「切换」选项卡里最常用的几种。</summary>
    public enum ScreenTransitionStyle
    {
        /// <summary>推入：旧屏向左滑出、新屏从右侧同时滑入，两屏像一张长卷一样平移。</summary>
        Push = 0,

        /// <summary>覆盖：新屏从右侧盖到旧屏上，旧屏被顶开一点点（视差），层次感更强。</summary>
        Cover = 1,

        /// <summary>揭开：旧屏滑走，新屏原地不动，像把旧的一页"抽"出去。</summary>
        Reveal = 2,

        /// <summary>淡入淡出：不位移，靠 alpha + 轻微缩放过渡，最稳、最不容易出错。</summary>
        Fade = 3,

        /// <summary>缩放：新屏从略小放大进来，旧屏放大淡出。</summary>
        Zoom = 4,

        /// <summary>
        /// 全息闪烁：不位移，旧屏的每个元素按扫描顺序逐个明灭消失，新屏的元素再逐个闪出来。
        /// 见 <see cref="HologramFlicker"/>。
        /// </summary>
        Hologram = 5,
    }

    /// <summary>缓动曲线。PPT 的"平滑"一半来自时长，一半来自先快后慢的曲线。</summary>
    public enum ScreenTransitionEase
    {
        /// <summary>先快后慢，最接近 PPT 默认观感。</summary>
        OutCubic = 0,

        /// <summary>两头慢中间快，适合"翻页"这种有明确起终点的动作。</summary>
        InOutCubic = 1,

        /// <summary>收尾更"急"，显得干净利落。</summary>
        OutQuint = 2,

        /// <summary>末尾轻微回弹，活泼但别用在严肃的数据界面上。</summary>
        OutBack = 3,
    }

    /// <summary>
    /// 界面切换动效播放器：把"旧屏退场 + 新屏入场"做成一段可打断的补间动画。
    ///
    /// 【为什么不给每个屏挂 Animator】
    ///   Animator 适合"播完就结束"的演出；界面切换是**可交互**的——用户很可能在动画
    ///   播到一半时又点了一次按钮。这里的做法是每帧从当前数值继续算，新一次 Play 会先
    ///   把上一段收尾，不会出现两段动画同时改同一个 RectTransform 而抖动。
    ///   补间用 unscaledDeltaTime，所以暂停（timeScale = 0）时也能正常播。
    ///
    /// 【用法】
    ///   1. 挂在 UI 根节点上（通常就是挂 GameFlowController 的 UI_Flow），全场景一个；
    ///   2. 状态机切换时调用 <see cref="Play"/>，并把"隐藏旧屏"写进 onComplete；
    ///   3. 屏的根节点必须铺满父级（stretch），动的是 anchoredPosition / localScale / alpha。
    ///
    /// 【要注意的边界】
    ///   * 两屏必须是同一个父节点的兄弟，否则 SetAsLastSibling 的层级关系没有意义；
    ///   * 如果画布不是全屏 Overlay（比如嵌在 3D 里的世界空间画布），请在父级加 RectMask2D，
    ///     否则滑出画面的部分会露在画布外面；
    ///   * 这个脚本只管"整屏"的进出。屏内部元素的错峰入场（标题先来、卡片后到）属于第二层
    ///     动效，一般做法是在新屏 OnEnter 里给子元素排一个 delay 序列，不要和整屏动画混在一起。
    /// </summary>
    [AddComponentMenu("Engineer/UI/Screen Transition Player")]
    [DisallowMultipleComponent]
    public class ScreenTransitionPlayer : MonoBehaviour
    {
        [Header("样式")]
        [Tooltip("默认全息闪烁（逐元素明灭）。想要 PPT 那种整屏平移，改成 Push / Cover。")]
        [SerializeField] private ScreenTransitionStyle style = ScreenTransitionStyle.Hologram;

        [Tooltip("缓动曲线。只对平移类样式生效；全息闪烁是离散开关，不走曲线。")]
        [SerializeField] private ScreenTransitionEase easing = ScreenTransitionEase.OutCubic;

        [Tooltip("一次切换的总时长（秒）。全息样式下的 0.5~0.6 比较像扫描一遍。")]
        [SerializeField, Range(0.05f, 2f)] private float duration = 0.55f;

        [Header("参数")]
        [Tooltip("Cover / Reveal 时旧屏的位移比例：0 = 完全不动，1 = 与新屏同速。")]
        [SerializeField, Range(0f, 1f)] private float parallax = 0.25f;

        [Tooltip("Fade / Zoom 时新旧屏的缩放幅度（1 - 该值）。")]
        [SerializeField, Range(0f, 0.5f)] private float zoomAmount = 0.06f;

        [Header("全息闪烁（Hologram 专用）")]
        [Tooltip("每秒明灭次数。26 左右像全息投影；低于 12 会退化成慢速淡出，高于 45 像噪点。")]
        [SerializeField, Range(4f, 60f)] private float hologramFlickerRate = 26f;

        [Tooltip("明灭到最暗时的透明度下限。0 = 完全黑掉；0.06 左右更有'扫描'感。")]
        [SerializeField, Range(0f, 0.5f)] private float hologramMinAlpha = 0.06f;

        // ------------------------------------------------------------------
        // 运行时状态
        // ------------------------------------------------------------------

        private Coroutine routine;
        private Action completed;

        private RectTransform fromRt;
        private RectTransform toRt;
        private CanvasGroup fromGroup;
        private CanvasGroup toGroup;

        private Vector2 fromPos;
        private Vector2 toPos;
        private Vector3 fromScale = Vector3.one;
        private Vector3 toScale = Vector3.one;
        private float fromAlpha = 1f;
        private float toAlpha = 1f;
        private bool fromBlocks;
        private bool toBlocks;

        private Pose fromStart;
        private Pose fromEnd;
        private Pose toStart;
        private Pose toEnd;

        /// <summary>用于把"一个屏宽"换算成画布单位。</summary>
        private float width = 1920f;

        // 全息样式用：两屏各自的元素表
        private HologramFlicker hologramOut;
        private HologramFlicker hologramIn;
        private bool hologramReverse;

        /// <summary>是否正在播。</summary>
        public bool IsPlaying
        {
            get { return routine != null; }
        }

        /// <summary>当前样式（运行时切样式做对比时用）。</summary>
        public ScreenTransitionStyle Style
        {
            get { return style; }
            set { style = value; }
        }

        private void OnDisable()
        {
            // 被禁用时协程会被 Unity 掐断，这里补一次收尾，避免屏停在半路的位置上。
            if (routine != null) FinishImmediately();
        }

        // ------------------------------------------------------------------
        // 对外接口
        // ------------------------------------------------------------------

        /// <summary>
        /// 播一次切换。
        /// </summary>
        /// <param name="outgoing">旧屏（会被移动到画面外）。</param>
        /// <param name="incoming">新屏（会被显示出来）。</param>
        /// <param name="forward">true = 前进（新屏从右侧来），false = 返回（新屏从左侧来）。</param>
        /// <param name="onComplete">动画播完后的回调，通常传旧屏的 OnExit，让它在动画结束后才真正隐藏。</param>
        public void Play(Component outgoing, Component incoming, bool forward, Action onComplete = null)
        {
            // 上一段还没播完：先收尾（并补上它的回调），否则两段动画会同时改同一批 RectTransform。
            if (routine != null) FinishImmediately();

            var inRt = incoming != null ? incoming.transform as RectTransform : null;
            if (inRt == null)
            {
                if (onComplete != null) onComplete();
                return;
            }

            var outRt = outgoing != null ? outgoing.transform as RectTransform : null;
            if (outRt == inRt) outRt = null;

            if (!inRt.gameObject.activeSelf) inRt.gameObject.SetActive(true);

            // 两屏重叠时谁在上层决定成败：覆盖/缩放要让新屏浮在上面；
            // 揭开则相反——旧屏必须压在新屏上面，否则新屏会先一步把旧屏盖住。
            if (style == ScreenTransitionStyle.Reveal && outRt != null) outRt.SetAsLastSibling();
            else inRt.SetAsLastSibling();

            Capture(outRt, inRt);

            completed = onComplete;

            // 时长为 0 或组件没启用时直接出结果（比如编辑器里关掉了脚本）。
            if (duration <= 0f || !isActiveAndEnabled)
            {
                Complete();
                return;
            }

            if (style == ScreenTransitionStyle.Hologram)
            {
                StartHologram(forward);
                return;
            }

            BuildPoses(forward ? 1f : -1f);
            ApplyAt(0f); // 先把新屏摆到"场外"，避免第一帧闪一下全屏内容
            routine = StartCoroutine(Run());
        }

        /// <summary>立刻结束当前动画并跳到终点（用于快速连点、关闭界面等场景）。</summary>
        public void FinishImmediately()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            if (toRt == null && fromRt == null) return;

            // 全息样式没有"姿态"可跳，直接把元素透明度还原即可，Complete 里会做。
            if (hologramOut == null && hologramIn == null) ApplyAt(1f);
            Complete();
        }

        // ------------------------------------------------------------------
        // 补间
        // ------------------------------------------------------------------

        private IEnumerator Run()
        {
            var t = 0f;
            while (t < 1f)
            {
                yield return null;
                t += Time.unscaledDeltaTime / Mathf.Max(0.0001f, duration);
                ApplyAt(t);
            }

            ApplyAt(1f);
            routine = null;
            Complete();
        }

        /// <summary>按 0~1 的线性进度，把两屏摆到对应位置。</summary>
        private void ApplyAt(float linear)
        {
            var k = Ease(Mathf.Clamp01(linear), easing);
            ApplyPose(fromRt, fromGroup, fromStart, fromEnd, k, fromPos, fromScale, fromAlpha);
            ApplyPose(toRt, toGroup, toStart, toEnd, k, toPos, toScale, toAlpha);
        }

        // ------------------------------------------------------------------
        // 全息闪烁
        // ------------------------------------------------------------------

        private void StartHologram(bool forward)
        {
            hologramReverse = !forward;

            hologramOut = fromRt != null ? new HologramFlicker() : null;
            if (hologramOut != null) hologramOut.Capture(fromRt);

            hologramIn = new HologramFlicker();
            hologramIn.Capture(toRt);

            ApplyHologram(0f);
            routine = StartCoroutine(RunHologram());
        }

        private IEnumerator RunHologram()
        {
            var total = Mathf.Max(0.05f, duration);
            var t = 0f;

            while (t < total)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                ApplyHologram(t);
            }

            ApplyHologram(total);
            routine = null;
            Complete();
        }

        /// <summary>
        /// 时间轴换算：旧屏占前 40%，新屏从 42% 开始、到 100% 结束。
        /// 两段几乎不重叠——全息屏换页是"旧的散掉、新的重组成形"，叠着闪会糊成一片。
        /// </summary>
        private void ApplyHologram(float time)
        {
            var total = Mathf.Max(0.05f, duration);
            var outTime = total * 0.40f;
            var inDelay = total * 0.42f;
            var inTime = total * 0.50f;
            var stagger = total * 0.08f;

            if (hologramOut != null)
                hologramOut.Apply(time, false, 0f, outTime, stagger, hologramFlickerRate, hologramMinAlpha, total, hologramReverse);

            if (hologramIn != null)
                hologramIn.Apply(time, true, inDelay, inTime, stagger, hologramFlickerRate, hologramMinAlpha, total, hologramReverse);
        }

        /// <summary>把一屏摆到某个姿态。位移以"屏宽"为单位，和分辨率无关。</summary>
        private void ApplyPose(RectTransform rt, CanvasGroup group, Pose a, Pose b, float t,
                               Vector2 basePos, Vector3 baseScale, float baseAlpha)
        {
            if (rt == null) return;

            var pose = Pose.Lerp(a, b, t);
            rt.anchoredPosition = basePos + new Vector2(pose.offset * width, 0f);
            rt.localScale = baseScale * pose.scale;
            if (group != null) group.alpha = baseAlpha * Mathf.Clamp01(pose.alpha);
        }

        /// <summary>根据样式算出旧屏/新屏的起点与终点。</summary>
        private void BuildPoses(float dir)
        {
            fromStart = Pose.Rest();
            fromEnd = Pose.Rest();
            toStart = Pose.Rest();
            toEnd = Pose.Rest();

            switch (style)
            {
                case ScreenTransitionStyle.Cover:
                    // 旧屏被"顶"开一小段，新屏整屏盖上去。
                    fromEnd.offset = -dir * parallax * 0.25f;
                    fromEnd.alpha = 0.88f;
                    toStart.offset = dir;
                    break;

                case ScreenTransitionStyle.Reveal:
                    // 旧屏整屏滑走，新屏原地等着被揭开。
                    fromEnd.offset = -dir;
                    break;

                case ScreenTransitionStyle.Fade:
                    fromEnd.alpha = 0f;
                    toStart.alpha = 0f;
                    toStart.scale = 1f - zoomAmount;
                    break;

                case ScreenTransitionStyle.Zoom:
                    fromEnd.alpha = 0f;
                    fromEnd.scale = 1f + zoomAmount * 2f;
                    toStart.alpha = 0f;
                    toStart.scale = 1f - zoomAmount * 2f;
                    break;

                case ScreenTransitionStyle.Push:
                default:
                    // 两屏同速平移：t=0.5 时旧屏占左半、新屏占右半，正好拼满，中间不露底。
                    fromEnd.offset = -dir;
                    toStart.offset = dir;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // 记录 / 还原
        // ------------------------------------------------------------------

        private void Capture(RectTransform outRt, RectTransform inRt)
        {
            fromRt = outRt;
            toRt = inRt;
            fromGroup = outRt != null ? EnsureGroup(outRt) : null;
            toGroup = EnsureGroup(inRt);

            fromPos = outRt != null ? outRt.anchoredPosition : Vector2.zero;
            toPos = inRt.anchoredPosition;
            fromScale = outRt != null ? outRt.localScale : Vector3.one;
            toScale = inRt.localScale;
            fromAlpha = fromGroup != null ? fromGroup.alpha : 1f;
            toAlpha = toGroup != null ? toGroup.alpha : 1f;
            fromBlocks = fromGroup == null || fromGroup.blocksRaycasts;
            toBlocks = toGroup == null || toGroup.blocksRaycasts;

            width = inRt.rect.width;
            if (width <= 1f && inRt.parent is RectTransform parent) width = parent.rect.width;
            if (width <= 1f) width = 1920f;

            // 动画期间两屏都不吃点击：否则新屏的按钮还没停稳就被点中，容易误触。
            if (fromGroup != null) fromGroup.blocksRaycasts = false;
            if (toGroup != null) toGroup.blocksRaycasts = false;
        }

        /// <summary>动画结束：把数值还原到"静止态"，再回调（回调里通常 SetActive(false) 旧屏）。</summary>
        private void Complete()
        {
            var callback = completed;
            completed = null;
            routine = null;

            // 全息样式：把所有元素的 CanvasGroup.alpha 还原，否则屏会一直停在被闪没的状态。
            if (hologramOut != null)
            {
                hologramOut.Restore();
                hologramOut = null;
            }
            if (hologramIn != null)
            {
                hologramIn.Restore();
                hologramIn = null;
            }

            if (fromRt != null)
            {
                fromRt.anchoredPosition = fromPos;
                fromRt.localScale = fromScale;
                if (fromGroup != null)
                {
                    fromGroup.alpha = fromAlpha;
                    fromGroup.blocksRaycasts = fromBlocks;
                }
            }

            if (toRt != null)
            {
                toRt.anchoredPosition = toPos;
                toRt.localScale = toScale;
                if (toGroup != null)
                {
                    toGroup.alpha = toAlpha;
                    toGroup.blocksRaycasts = toBlocks;
                }
            }

            fromRt = null;
            toRt = null;
            fromGroup = null;
            toGroup = null;

            if (callback != null) callback();
        }

        private static CanvasGroup EnsureGroup(RectTransform rt)
        {
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        // ------------------------------------------------------------------
        // 缓动 / 姿态
        // ------------------------------------------------------------------

        private static float Ease(float t, ScreenTransitionEase kind)
        {
            switch (kind)
            {
                case ScreenTransitionEase.InOutCubic:
                    return t < 0.5f
                        ? 4f * t * t * t
                        : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

                case ScreenTransitionEase.OutQuint:
                    return 1f - Mathf.Pow(1f - t, 5f);

                case ScreenTransitionEase.OutBack:
                    {
                        const float c1 = 1.70158f;
                        const float c3 = c1 + 1f;
                        var u = t - 1f;
                        return 1f + c3 * u * u * u + c1 * u * u;
                    }

                case ScreenTransitionEase.OutCubic:
                default:
                    return 1f - Mathf.Pow(1f - t, 3f);
            }
        }

        /// <summary>一屏在某时刻的姿态。offset 以屏宽为单位，方便跨分辨率复用。</summary>
        private struct Pose
        {
            public float offset;
            public float alpha;
            public float scale;

            public static Pose Rest()
            {
                return new Pose { offset = 0f, alpha = 1f, scale = 1f };
            }

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                Pose pose;
                pose.offset = Mathf.LerpUnclamped(a.offset, b.offset, t);
                pose.alpha = Mathf.LerpUnclamped(a.alpha, b.alpha, t);
                pose.scale = Mathf.LerpUnclamped(a.scale, b.scale, t);
                return pose;
            }
        }
    }
}
