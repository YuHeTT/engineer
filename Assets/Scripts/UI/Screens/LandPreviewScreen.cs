using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 地形观察页（LandSelect → 这里 → LandParams）。
    ///
    /// 目前只保留 UI 壳：填地形文案、管「返回选地 / 构建农场」、构建时拉起
    /// <see cref="LoadingOverlay"/>。地形生成已经移出 UI 层——等确定用
    /// Unity Terrain 画板后，再由场景/后端把相机和地形接进来。
    /// </summary>
    public class LandPreviewScreen : PlaceholderScreen
    {
        [Header("文案")]
        [Tooltip("左上角小字：「地形观察 · 平川台」。")]
        public TMP_Text badgeText;

        [Tooltip("地形名。")]
        public TMP_Text titleText;

        [Tooltip("地形介绍，最多 3 行。")]
        public TMP_Text introText;

        [Tooltip("一行关键指标：坡度 / 土层 / 排水。")]
        public TMP_Text factsText;

        [Tooltip("右上角面包屑：「土地选择 › 地形观察」。")]
        public TMP_Text breadcrumbText;

        [Tooltip("底部操作提示。")]
        public TMP_Text controlHintText;

        [Tooltip("生成地形时的「正在生成地形…」，默认隐藏。")]
        public TMP_Text generatingHint;

        [Header("按钮")]
        public Button backButton;
        public Button buildButton;

        [Tooltip("构建按钮上的文字（会改成「构建农场 / 继续」）。")]
        public TMP_Text buildLabel;

        [Header("协作对象（可空）")]
        [Tooltip("构建时的全屏加载层。留空则不显示加载动画，只等时长。")]
        public LoadingOverlay loadingOverlay;

        [Header("构建")]
        [Tooltip("构建动画时长（秒）。")]
        [Min(0.1f)] public float buildDuration = 1.6f;

        private bool building;
        private bool built;
        private int builtLandIndex = -1;
        private int landIndex = -1;

        /// <summary>是否已经构建过（返回本页时按钮显示「继续」）。</summary>
        public bool Built
        {
            get { return built; }
        }

        // ------------------------------------------------------------------
        // 状态机
        // ------------------------------------------------------------------

        public override void OnEnter(object args)
        {
            base.OnEnter(args);

            FillTexts();
            RefreshBuildLabel();
            WireButtons();

            if (generatingHint != null) generatingHint.gameObject.SetActive(false);
        }

        public override void OnExit()
        {
            StopAllCoroutines();
            building = false;
            base.OnExit();
        }

        /// <summary>「开始新的模拟」会走到这里：把"已构建"状态清掉，否则新一局还显示「继续」。</summary>
        public override void ResetPlaceholderState()
        {
            base.ResetPlaceholderState();

            building = false;
            built = false;
            builtLandIndex = -1;
            RefreshBuildLabel();
        }

        protected override bool OwnsButton(Button button)
        {
            return (buildButton != null && button == buildButton)
                   || (backButton != null && button == backButton);
        }

        // ------------------------------------------------------------------
        // 按钮
        // ------------------------------------------------------------------

        private void WireButtons()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }

            if (buildButton != null)
            {
                buildButton.onClick.RemoveListener(OnBuildClicked);
                buildButton.onClick.AddListener(OnBuildClicked);
            }
        }

        private void OnBackClicked()
        {
            if (building) return;

            ClickThen(backButton, true, delegate
            {
                if (Flow != null) Flow.Back();
            });
        }

        private void OnBuildClicked()
        {
            if (building) return;

            // 已经构建过：这次只是从参数页返回，直接「继续」，不再播一遍加载动画。
            if (built)
            {
                ClickThen(buildButton, true, delegate
                {
                    if (Flow != null) Flow.GoTo(GameState.LandParams);
                });
                return;
            }

            ClickThen(buildButton, true, StartBuild);
        }

        private void StartBuild()
        {
            if (building) return;
            building = true;
            StartCoroutine(BuildRoutine());
        }

        private IEnumerator BuildRoutine()
        {
            var context = Flow != null ? Flow.Context : null;
            var landName = context != null && !string.IsNullOrEmpty(context.LandTypeName)
                ? context.LandTypeName
                : LandPreviewContent.SafeName(Mathf.Max(0, landIndex));

            if (loadingOverlay != null) loadingOverlay.Show(landName);

            var total = Mathf.Max(0.4f, buildDuration);
            var t = 0f;

            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                var k = Mathf.Clamp01(t / total);
                if (loadingOverlay != null) loadingOverlay.SetProgress(k * 0.9f);
                yield return null;
            }

            if (loadingOverlay != null)
            {
                loadingOverlay.SetProgress(1f,
                    LandPreviewContent.LoadingStatus[LandPreviewContent.LoadingStatus.Length - 1],
                    string.Empty);
            }

            yield return null;

            if (loadingOverlay != null) loadingOverlay.Hide();

            built = true;
            builtLandIndex = landIndex;
            building = false;

            if (Flow != null) Flow.GoTo(GameState.LandParams);
        }

        // ------------------------------------------------------------------
        // 文案
        // ------------------------------------------------------------------

        private void FillTexts()
        {
            var context = Flow != null ? Flow.Context : null;

            landIndex = LandPreviewContent.IndexFor(context != null ? context.LandTypeId : null);
            if (landIndex < 0) landIndex = 0;

            // 换了一块地：之前那次"已构建"就作废，按钮回到「构建农场」。
            if (built && builtLandIndex >= 0 && builtLandIndex != landIndex) built = false;

            var landName = context != null && !string.IsNullOrEmpty(context.LandTypeName)
                ? context.LandTypeName
                : LandPreviewContent.SafeName(landIndex);

            if (badgeText != null) badgeText.text = "地形观察 · " + landName;
            if (titleText != null) titleText.text = landName;
            if (introText != null) introText.text = LandPreviewContent.SafeIntro(landIndex);
            if (factsText != null) factsText.text = LandPreviewContent.SafeFact(landIndex);
            if (breadcrumbText != null) breadcrumbText.text = LandPreviewContent.Breadcrumb;
            if (controlHintText != null) controlHintText.text = LandPreviewContent.ControlHint;
            if (generatingHint != null) generatingHint.text = LandPreviewContent.GeneratingHint;
        }

        private void RefreshBuildLabel()
        {
            if (buildLabel != null)
                buildLabel.text = built ? LandPreviewContent.BuildAgainLabel : LandPreviewContent.BuildLabel;
        }
    }
}
