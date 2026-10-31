using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 全屏构建加载层。不参与状态机，挂在 Screen_Canvas 下的 OverlayLayer 上，
    /// 平时 `SetActive(false)`，构建时由 <see cref="LandPreviewScreen"/> 拉起。
    ///
    /// 【为什么单独一层】它是常驻 Overlay，不能是某个屏的子节点——
    /// <see cref="ScreenTransitionPlayer"/> 会把整屏移走 / 逐元素闪没，加载层会跟着消失。
    ///
    /// 进度由调用方用 <see cref="SetProgress"/> 推进；最短显示 1.2 秒，
    /// 避免"一闪而过"，但也不会改变 Time.timeScale（农场动画照常跑）。
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingOverlay : MonoBehaviour
    {
        [Header("引用")]
        public CanvasGroup group;
        public TMP_Text badgeText;
        public TMP_Text titleText;
        public TMP_Text statusText;
        public TMP_Text tipText;
        public TMP_Text percentText;
        public Image progressFill;

        [Header("时序（秒，unscaled）")]
        [Min(0f)] public float fadeIn = 0.25f;
        [Min(0f)] public float fadeOut = 0.3f;

        [Tooltip("最短显示时间，防止一闪而过。")]
        [Min(0f)] public float minShow = 1.2f;

        private Coroutine routine;
        private float shownAt;
        private bool visible;

        /// <summary>加载层是否正在显示（包括淡出中）。</summary>
        public bool IsVisible
        {
            get { return visible; }
        }

        private void Awake()
        {
            EnsureGroup();
        }

        private void EnsureGroup()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
        }

        // ------------------------------------------------------------------
        // 对外
        // ------------------------------------------------------------------

        /// <summary>显示并淡入。会重置进度到 0。</summary>
        public void Show(string landName)
        {
            gameObject.SetActive(true);
            EnsureGroup();

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            shownAt = Time.unscaledTime;
            visible = true;

            var badge = string.IsNullOrEmpty(landName)
                ? LandPreviewContent.LoadingBadgeSuffix
                : landName + " · " + LandPreviewContent.LoadingBadgeSuffix;

            if (badgeText != null) badgeText.text = badge;
            if (titleText != null) titleText.text = LandPreviewContent.LoadingTitle;
            if (tipText != null) tipText.text = LandPreviewContent.TipFor(0, landName);

            SetProgress(0f, LandPreviewContent.LoadingStatus[0], null);

            group.blocksRaycasts = true;
            group.interactable = true;
            group.alpha = 0f;

            if (fadeIn <= 0f)
            {
                group.alpha = 1f;
                return;
            }

            routine = StartCoroutine(FadeInRoutine());
        }

        /// <summary>按 0~1 推进；阶段状态与提示自动按进度取。</summary>
        public void SetProgress(float value)
        {
            var v = Mathf.Clamp01(value);
            var stage = LandPreviewContent.StageFor(v);
            SetProgress(v,
                LandPreviewContent.LoadingStatus[stage],
                LandPreviewContent.TipFor(stage, null));
        }

        /// <summary>按 0~1 推进，并指定状态 / 提示（传 null 则不改）。</summary>
        public void SetProgress(float value, string status, string tip)
        {
            var v = Mathf.Clamp01(value);

            if (progressFill != null) progressFill.fillAmount = v;
            if (percentText != null) percentText.text = Mathf.RoundToInt(v * 100f) + "%";
            if (statusText != null && !string.IsNullOrEmpty(status)) statusText.text = status;
            if (tipText != null && !string.IsNullOrEmpty(tip)) tipText.text = tip;
        }

        /// <summary>淡出并隐藏。会在满足最短显示时间之后才真正开始淡出。</summary>
        public void Hide()
        {
            if (!visible) return;

            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            routine = StartCoroutine(HideRoutine());
        }

        // ------------------------------------------------------------------
        // 协程
        // ------------------------------------------------------------------

        private IEnumerator FadeInRoutine()
        {
            var t = 0f;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Clamp01(t / fadeIn);
                yield return null;
            }

            if (group != null) group.alpha = 1f;
            routine = null;
        }

        private IEnumerator HideRoutine()
        {
            var remain = minShow - (Time.unscaledTime - shownAt);
            if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

            if (group != null) group.blocksRaycasts = false;

            var start = group != null ? group.alpha : 1f;
            var t = 0f;

            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t / fadeOut));
                yield return null;
            }

            if (group != null) group.alpha = 0f;

            visible = false;
            routine = null;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // 被外部关掉（或 Hide 自己关掉）时，别让协程挂在半路。
            StopAllCoroutines();
            routine = null;
            visible = false;
            if (group != null) group.alpha = 0f;
        }
    }
}
