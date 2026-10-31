using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>点击时的消散模式。</summary>
    public enum ButtonShredMode
    {
        /// <summary>不消散，只播音效和闪烁。</summary>
        None = 0,

        /// <summary>碎裂后留在消失状态（会离开当前屏的按钮）。</summary>
        Exit = 1,

        /// <summary>碎裂后快速重组回来（不换页的按钮）。</summary>
        Recover = 2,
    }

    /// <summary>
    /// 按钮点击的"科技感"反馈，把一串容易写散的动作串成一条时间轴：
    ///
    ///   0ms        音效 + 变色（按钮整体染成青色）
    ///   tintTime   闪烁（青色冲白、再淡出）
    ///   shredDelay 横向切片消散：按钮被切成 N 条横线，各自随机偏移、淡出
    ///   之后       换页的按钮停在消失状态并执行动作；不换页的按钮快速重组回原样
    ///
    /// 【两种消散模式】见 <see cref="ButtonShredMode"/>：Exit（切屏）/ Recover（原地碎裂重组）。
    ///
    /// 【为什么做成单例而不是挂在每个按钮上】
    ///   1. 音效只需要一个 AudioSource，避免每个按钮一个；
    ///   2. 点击必须"先消散、再切屏"，所以调用方要把导航放进回调里排队执行；
    ///   3. 同一时间只允许一次点击，防止 0.4 秒内连点触发两次跳转。
    ///
    /// 【和转场的关系】
    ///   本组件只管"按钮怎么消失"，不负责换页。回调里调用 GameFlowController.GoTo，
    ///   切屏动画由 ScreenTransitionPlayer 播放，两段首尾相接，视觉上是一条完整动效。
    ///
    /// 【按钮怎么恢复】
    ///   切屏的按钮消散后刻意不立刻弹回来（否则会和新界面叠在一起闪一下），
    ///   由 PlaceholderScreen 在 OnExit / OnEnter 时统一还原；原地碎裂的会自己回到原样。
    /// </summary>
    [AddComponentMenu("Engineer/UI/Button Click Feedback")]
    [DisallowMultipleComponent]
    public class UiClickFeedback : MonoBehaviour
    {
        /// <summary>点击音效在工程里的固定路径，构建器按它自动挂引用。</summary>
        public const string ClipPath = "Assets/Audio/OnClick/015-short-futuristic-ui-confirm-beep.wav";

        private static UiClickFeedback instance;

        /// <summary>场景里的唯一实例；没有就是 null（调用会自动退化成"直接执行"）。</summary>
        public static UiClickFeedback Instance
        {
            get { return instance; }
        }

        /// <summary>是否正在播反馈。连点保护用。</summary>
        public static bool Busy
        {
            get { return instance != null && instance.busy != null; }
        }

        [Header("音效")]
        [Tooltip("点击音效。留空则不发声，其它效果照常。")]
        public AudioClip clickClip;

        [Range(0f, 1f)]
        [Tooltip("音量。0.8 左右比较克制，不会盖过场景音。")]
        public float volume = 0.85f;

        [Tooltip("留空会自动创建并配置好（2D、不循环、不自动播放）。")]
        public AudioSource audioSource;

        [Header("时序（秒）")]
        [Tooltip("变色（染成青色）的时长。")]
        [Min(0f)] public float tintTime = 0.08f;

        [Tooltip("闪烁（冲白再淡出）的时长。")]
        [Min(0f)] public float flashTime = 0.12f;

        [Tooltip("变色开始后多久开始把按钮切成横线。")]
        [Min(0f)] public float shredDelay = 0.22f;

        [Tooltip("整段消散的时长。")]
        [Min(0.05f)] public float shredDuration = 0.45f;

        [Tooltip("横线高度（px）。越小条数越多、越像信号撕裂。")]
        [Min(1f)] public float shredStripHeight = 5f;

        [Tooltip("每条横线最大横向偏移（px）。")]
        [Min(0f)] public float shredSpread = 90f;

        [Tooltip("条数上限，避免特别高的按钮生成太多副本。")]
        [Range(4, 40)] public int shredMaxStrips = 24;

        [Tooltip("不换页的按钮：碎裂后多久重组回来。")]
        [Min(0.02f)] public float recoverTime = 0.14f;

        [Tooltip("切屏失败（例如 Back 已经在栈底）时，多久之后把按钮恢复原样。")]
        [Min(0.1f)] public float recoverDelay = 1.2f;

        [Header("观感")]
        [Tooltip("按下时按钮染成的颜色（半透明，叠在按钮上）。")]
        public Color tintColor = new Color(0.25f, 0.85f, 1f, 0.45f);

        [Tooltip("闪烁时的颜色，一般是接近白的高亮色。")]
        public Color flashColor = new Color(0.85f, 0.97f, 1f, 0.95f);

        [Tooltip("关掉后只播音效和闪烁，不做切片消散。")]
        public bool shredOnNavigate = true;

        private readonly Dictionary<Button, Vector3> baseScales = new Dictionary<Button, Vector3>();
        private readonly Dictionary<Button, float> baseAlphas = new Dictionary<Button, float>();
        private readonly Dictionary<Button, Image> overlays = new Dictionary<Button, Image>();

        private Button busy;
        private long token;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[UI] 场景里有多个 UiClickFeedback，后出现的会被忽略。", this);
                enabled = false;
                return;
            }

            instance = this;
            EnsureSource();

            // 音效导入设置是"首次使用时加载"，开局先解压好，避免第一次点击卡一下。
            if (clickClip != null) clickClip.LoadAudioData();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------
        // 对外接口
        // ------------------------------------------------------------------

        /// <summary>
        /// 播放一次点击反馈。
        /// </summary>
        /// <param name="navigate">
        /// true = 这次点击会离开当前屏：碎裂后停在消失状态，回调在动画结束后执行（切屏要等它）；
        /// false = 不换页：碎裂后**重组回来**，回调立刻执行，不挡后面的点击。
        /// </param>
        public static void Play(Button button, bool navigate, Action onComplete)
        {
            Play(button, navigate ? ButtonShredMode.Exit : ButtonShredMode.Recover, onComplete);
        }

        /// <summary>同上，显式指定模式。</summary>
        public static void Play(Button button, ButtonShredMode mode, Action onComplete)
        {
            var owner = instance;
            if (owner == null || !owner.isActiveAndEnabled)
            {
                if (onComplete != null) onComplete();
                return;
            }

            owner.PlayInternal(button, mode, onComplete);
        }

        /// <summary>把某个按钮恢复成"没被点过"的样子（位置/缩放/覆盖层）。</summary>
        public static void Restore(Button button)
        {
            if (instance != null) instance.RestoreInternal(button);
        }

        /// <summary>把一整屏里的按钮都恢复原样，切屏前后各调一次最稳。</summary>
        public static void RestoreAll(Component root)
        {
            if (instance == null || root == null) return;
            instance.RestoreAllInternal(root.transform);
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private void PlayInternal(Button button, ButtonShredMode mode, Action onComplete)
        {
            if (button == null)
            {
                if (onComplete != null) onComplete();
                return;
            }

            // 0.4 秒内连点：直接吞掉，既不重复播音效也不重复跳转。
            if (busy != null) return;

            if (!shredOnNavigate) mode = ButtonShredMode.None;

            // 只有"会离开当前屏"的点击才上锁；原地碎裂不能挡住后面的返回和其它按钮。
            if (mode == ButtonShredMode.Exit) busy = button;

            // 不换页的动作立刻执行，别等消散播完再响应。
            if (mode == ButtonShredMode.Recover && onComplete != null)
            {
                var immediate = onComplete;
                onComplete = null;
                immediate();
            }

            if (!baseScales.ContainsKey(button)) baseScales.Add(button, button.transform.localScale);

            var group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            if (!baseAlphas.ContainsKey(button)) baseAlphas.Add(button, group.alpha);

            var overlay = EnsureOverlay(button);

            if (clickClip != null)
            {
                EnsureSource();
                if (audioSource != null) audioSource.PlayOneShot(clickClip, volume);
            }

            token++;
            StartCoroutine(Sequence(button, group, overlay, mode, onComplete, token));
        }

        private IEnumerator Sequence(Button button, CanvasGroup group, Image overlay, ButtonShredMode mode,
                                     Action onComplete, long myToken)
        {
            if (button == null)
            {
                busy = null;
                if (onComplete != null) onComplete();
                yield break;
            }

            var shred = mode != ButtonShredMode.None;
            var recover = mode == ButtonShredMode.Recover;

            // 原地碎裂比切屏碎裂短一些，免得按钮"消失"太久。
            var duration = recover ? shredDuration * 0.6f : shredDuration;
            var shredEnd = shredDelay + duration;

            var baseAlpha = baseAlphas.ContainsKey(button) ? baseAlphas[button] : 1f;
            var baseBlocks = group == null || group.blocksRaycasts;

            var animateEnd = tintTime + flashTime;
            if (shred) animateEnd = Mathf.Max(animateEnd, shredEnd + (recover ? recoverTime : 0f));

            ButtonShredEffect effect = null;
            var rect = button.transform as RectTransform;

            var t = 0f;
            var wallStart = Time.realtimeSinceStartup;
            while (t < animateEnd)
            {
                // 兜底：如果 unscaledDeltaTime 被外部卡成 0（暂停/断点/异常时间源），
                // 不能让协程永远停在这里，否则按钮永远不恢复、也不切屏。
                if (Time.realtimeSinceStartup - wallStart > animateEnd + 2f) break;

                yield return null;
                t += Time.unscaledDeltaTime;

                if (overlay != null) overlay.color = EvaluateOverlay(t);

                if (shred && t >= shredDelay)
                {
                    if (effect == null)
                    {
                        // 先把按钮藏起来，再把它的样子交给切片——否则会看到"按钮 + 切片"重叠。
                        effect = new ButtonShredEffect();
                        effect.Begin(rect, shredStripHeight, shredSpread, shredMaxStrips);

                        if (group != null)
                        {
                            group.alpha = 0f;
                            group.blocksRaycasts = false;
                        }
                    }

                    effect.Tick(t - shredDelay, duration);

                    if (recover && t >= shredEnd)
                    {
                        var k = Mathf.Clamp01((t - shredEnd) / Mathf.Max(0.02f, recoverTime));
                        if (group != null) group.alpha = baseAlpha * Mathf.SmoothStep(0f, 1f, k);
                    }
                }
            }

            if (overlay != null) overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

            if (effect != null)
            {
                effect.End();
                effect = null;
            }

            if (group != null)
            {
                group.blocksRaycasts = baseBlocks;

                // 原地碎裂的最后一定回到原样；切屏那条留到 OnExit 再还原。
                if (recover) group.alpha = baseAlpha;
            }

            var callback = onComplete;
            if (busy == button) busy = null;

            if (callback != null) callback();

            if (mode != ButtonShredMode.Exit) yield break;

            // 切屏正常的话，这里按钮所属的屏已经被隐藏并在 OnExit 里还原了；
            // 只有"点了但没切走"（比如 Back 已经在栈底）才需要自己收拾。
            // 用 token 判断：期间又点过一次的话，这次收拾就作废，别把新动画的按钮掰回去。
            yield return new WaitForSecondsRealtime(recoverDelay);
            if (myToken == token && button != null && IsStillShowing(button)) RestoreInternal(button);
        }

        /// <summary>覆盖层的颜色：先青色、再冲白、最后淡出。</summary>
        private Color EvaluateOverlay(float t)
        {
            var tint = Mathf.Max(0.0001f, tintTime);
            var flash = Mathf.Max(0.0001f, flashTime);

            if (t < tint)
            {
                var k = Mathf.Clamp01(t / tint);
                return new Color(tintColor.r, tintColor.g, tintColor.b, tintColor.a * k);
            }

            var flashT = t - tint;
            if (flashT < flash)
            {
                var k = flashT / flash;
                if (k < 0.35f)
                {
                    var rise = k / 0.35f;
                    return Color.Lerp(tintColor, flashColor, rise);
                }

                var fade = (k - 0.35f) / 0.65f;
                return new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * (1f - fade));
            }

            return new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }

        /// <summary>按钮所属的屏还是当前屏吗？（用于判断"点了却没切走"）</summary>
        private static bool IsStillShowing(Button button)
        {
            if (!button.gameObject.activeInHierarchy) return false;

            var screen = button.GetComponentInParent<PlaceholderScreen>(true);
            if (screen == null) return true;

            var flow = screen.Flow;
            return flow == null || flow.Current == screen.State;
        }

        private void RestoreInternal(Button button)
        {
            if (button == null) return;

            Vector3 scale;
            if (baseScales.TryGetValue(button, out scale)) button.transform.localScale = scale;

            float alpha;
            var group = button.GetComponent<CanvasGroup>();
            if (group != null && baseAlphas.TryGetValue(button, out alpha)) group.alpha = alpha;

            Image overlay;
            if (overlays.TryGetValue(button, out overlay) && overlay != null)
                overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);

            if (busy == button) busy = null;
        }

        private void RestoreAllInternal(Transform root)
        {
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++) RestoreInternal(buttons[i]);
        }

        private void EnsureSource()
        {
            if (audioSource != null)
            {
                Configure(audioSource);
                return;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            Configure(audioSource);
        }

        private static void Configure(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f; // 2D，不受相机位置影响
            source.volume = 1f;       // 实际音量在 PlayOneShot 里给
        }

        /// <summary>
        /// 给按钮铺一层"效果覆盖层"：不接收点击、不参与布局、形状尽量跟随按钮的图。
        /// 用覆盖层而不是直接改按钮底色，是为了不动皮肤套上来的 sprite / 颜色。
        /// </summary>
        private Image EnsureOverlay(Button button)
        {
            Image overlay;
            if (overlays.TryGetValue(button, out overlay) && overlay != null) return overlay;

            var go = new GameObject("ClickFX", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(button.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            // 按钮可能挂在 LayoutGroup 里：不忽略布局的话这层会挤动别的元素。
            var layout = go.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            overlay = go.GetComponent<Image>();
            overlay.raycastTarget = false;

            // 跟随按钮底图的圆角/九宫格，避免效果层是硬邦邦的直角。
            var source = button.targetGraphic as Image;
            if (source != null && source.sprite != null)
            {
                overlay.sprite = source.sprite;
                overlay.type = source.type;
                overlay.preserveAspect = source.preserveAspect;
                overlay.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            }

            overlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            overlays[button] = overlay;
            return overlay;
        }
    }
}
