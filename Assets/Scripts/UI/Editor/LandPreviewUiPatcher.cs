using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 地形观察页的一次性场景补丁（幂等，可重复执行）。
    ///
    /// 【为什么不重跑白盒构建】现有 UI.unity 在构建后有人工改动（套过皮肤、删过
    /// FarmStage/ViewPlaceholder、精调过布局），UiWhiteboxBuilder.Build 会先删掉整个
    /// UI_Root 再重建，这些都会丢。所以这里只按名字查找现有节点，缺什么补什么。
    ///
    /// 菜单：Tools ▸ UI ▸ 地形观察：应用 UI 补丁
    ///
    /// 做的事（对应 Docs/地形观察UI改动清单.md 第 3~8 节）：
    ///   1. Screen_LandSelect：副标题 / 备注文案、四张卡全部开放、挂 OptionCardButton、
    ///      删掉旧的 ScreenAction（否则两个监听抢一次点击）、加「● 已选」标记；
    ///   2. 新建 Screen_LandPreview：左上介绍面板 + 右上面包屑 + 底部提示 + 返回/构建；
    ///   3. 新建 OverlayLayer/LoadingOverlay 并关联到观察页；
    ///   4. 把新屏注册进 GameFlowController.screens；
    ///   5. 对新元素套一次 EPS 皮肤，保存场景。
    /// </summary>
    public static class LandPreviewUiPatcher
    {
        private const string ScenePath = "Assets/Scenes/UI.unity";
        private const string RootName = "UI_Root";

        private const string LandSelectSubtitle = "四块地横排；点「选择」进入地形观察，左下角返回上一步。";
        private const string LandSelectNote = "备注：四块地都可观察；选好地形后点「构建农场」，再进入参数设置。";

        [MenuItem("Tools/UI/地形观察：应用 UI 补丁", false, 20)]
        public static void ApplyPatch()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("地形观察 UI 补丁", "请先退出 Play 模式再执行。", "好");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var previousScenePath = EditorSceneManager.GetActiveScene().path;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = FindRoot(scene);
            if (root == null)
            {
                Debug.LogError("[UI] 补丁失败：在 " + ScenePath + " 里找不到 " + RootName + "。");
                return;
            }

            var canvas = FindDeep(root.transform, "Screen_Canvas");
            var screens = FindDeep(canvas, "Screens");
            var landSelect = FindDeep(screens, "Screen_LandSelect");

            if (canvas == null || screens == null || landSelect == null)
            {
                Debug.LogError("[UI] 补丁失败：找不到 Screen_Canvas / Screens / Screen_LandSelect 中的某一个。");
                return;
            }

            // 先记录现有界面的文字配色（暖橙 / 青蓝），新元素要跟它一致，
            // 而不是按映射表重刷一遍——那会把手工调过的配色盖掉。
            var coolText = UiSkin.IsCoolPalette(root);

            // 先把既有元素的材质 / 配色拍个快照，补丁结束时原样恢复。
            // 这样即使编辑器里有别的钩子中途重刷了皮肤，也不会把老界面改掉。
            var baseline = CaptureVisuals(root);

            var cards = PatchLandSelect(landSelect, coolText);
            PatchLandParams(canvas, screens);
            PatchConfirm(canvas);

            var preview = EnsureLandPreview(screens, coolText);
            var previewScreen = preview.GetComponent<LandPreviewScreen>();
            var overlay = EnsureLoadingOverlay(canvas, coolText);

            if (previewScreen != null && overlay != null)
            {
                previewScreen.loadingOverlay = overlay;
                EditorUtility.SetDirty(previewScreen);
            }

            var flow = root.GetComponentInChildren<GameFlowController>(true);
            EnsureMotionComponents(flow);
            RegisterScreen(flow, previewScreen);

            var restored = RestoreVisuals(baseline);
            if (restored > 0)
            {
                Debug.LogWarning("[UI] 补丁过程中有 " + restored + " 处既有元素的材质/配色被改动，已恢复原值。" +
                                 "如果反复出现，检查是不是还有别的工具在自动套皮。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 补丁是"顺手改一下现有场景"，别把人从原来的场景里踢出去。
            if (!string.IsNullOrEmpty(previousScenePath)
                && previousScenePath != ScenePath
                && System.IO.File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            Debug.Log(string.Format(
                "[UI] 地形观察补丁已应用：卡片 {0} 张，新屏 {1}，加载层 {2}。既有元素的材质与配色未改动。",
                cards, preview != null ? "已就绪" : "缺失", overlay != null ? "已就绪" : "缺失"));
        }

        // ------------------------------------------------------------------
        // Screen_LandSelect
        // ------------------------------------------------------------------

        private static int PatchLandSelect(Transform landSelect, bool coolText)
        {
            SetText(landSelect, "Header/Subtitle", LandSelectSubtitle);
            SetText(landSelect, "Body/Note", LandSelectNote);

            var screen = landSelect.GetComponent<PlaceholderScreen>();
            if (screen == null)
            {
                Debug.LogWarning("[UI] Screen_LandSelect 上没有 PlaceholderScreen，跳过按钮改接线。", landSelect);
                return 0;
            }

            var tags = new TMP_Text[LandPreviewContent.Ids.Length];
            var patched = 0;

            for (var i = 0; i < LandPreviewContent.Ids.Length; i++)
            {
                var card = FindChild(landSelect,
                    "Body/LandCards/Cell_Land" + (i + 1) + "/Card_Land" + (i + 1));
                if (card == null)
                {
                    Debug.LogWarning("[UI] 找不到第 " + (i + 1) + " 张地形卡，跳过。", landSelect);
                    continue;
                }

                var buttonTransform = FindChild(card, "Button_Select");
                if (buttonTransform == null) buttonTransform = FindChild(card, "Button_Locked");
                if (buttonTransform == null)
                {
                    Debug.LogWarning("[UI] " + card.name + " 下没有选择按钮，跳过。", card);
                    continue;
                }

                buttonTransform.name = "Button_Select";

                var button = buttonTransform.GetComponent<Button>();
                if (button != null) button.interactable = true;

                var label = FindChild(buttonTransform, "Label");
                if (label != null)
                {
                    var text = label.GetComponent<TMP_Text>();
                    if (text != null) text.text = "选择";
                }

                var option = buttonTransform.GetComponent<OptionCardButton>();
                if (option == null) option = buttonTransform.gameObject.AddComponent<OptionCardButton>();

                option.category = OptionCategory.Land;
                option.optionId = LandPreviewContent.Ids[i];
                option.displayName = LandPreviewContent.Names[i];
                option.targetState = GameState.LandPreview;
                option.description = LandPreviewContent.Intros[i];

                EditorUtility.SetDirty(option);
                if (button != null) EditorUtility.SetDirty(button);

                // 关键：旧动作必须删掉，否则 PlaceholderScreen 和 OptionCardButton 会各跳一次。
                RemoveActionFor(screen, button);

                tags[i] = EnsureSelectedTag(card, coolText);
                patched++;
            }

            var highlighter = landSelect.GetComponent<LandSelectHighlighter>();
            if (highlighter == null) highlighter = landSelect.gameObject.AddComponent<LandSelectHighlighter>();

            highlighter.selectedTags = tags;
            EditorUtility.SetDirty(highlighter);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(landSelect.gameObject);

            return patched;
        }

        /// <summary>从 PlaceholderScreen.actions 里摘掉指向这颗按钮的条目（保留返回键）。</summary>
        private static void RemoveActionFor(PlaceholderScreen screen, Button button)
        {
            if (screen == null || button == null) return;

            var so = new SerializedObject(screen);
            var actions = so.FindProperty("actions");
            if (actions == null) return;

            var removed = 0;
            for (var i = actions.arraySize - 1; i >= 0; i--)
            {
                var element = actions.GetArrayElementAtIndex(i);
                var buttonProperty = element.FindPropertyRelative("button");
                if (buttonProperty == null || buttonProperty.objectReferenceValue != button) continue;

                // 先清引用再删，避免对象引用数组"删成 null 占位"的坑。
                buttonProperty.objectReferenceValue = null;
                actions.DeleteArrayElementAtIndex(i);
                removed++;
            }

            if (removed > 0) so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text EnsureSelectedTag(Transform card, bool coolText)
        {
            var existing = FindChild(card, "SelectedTag");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                return existing.GetComponent<TMP_Text>();
            }

            var tag = UiFactory.MakeText(card, "SelectedTag", "● 已选",
                UiTheme.FontSmall, UiTheme.TextAccent, TextAlignmentOptions.MidlineRight);
            UiLayout.TopBand(tag.rectTransform, 24f, 20f, 24f, 28f);

            // 跟着现有界面的文字配色走（青蓝 / 暖橙）。
            if (coolText) UiSkin.SwapTextPalette(tag.gameObject, true);

            // 默认隐藏：只有"已经选过"的那张才亮，见 LandSelectHighlighter。
            tag.gameObject.SetActive(false);
            return tag;
        }

        // ------------------------------------------------------------------
        // Screen_LandPreview
        // ------------------------------------------------------------------

        private static GameObject EnsureLandPreview(Transform screens, bool coolText)
        {
            var existing = FindChild(screens, "Screen_LandPreview");
            if (existing != null) return existing.gameObject;

            var screenRoot = UiFactory.MakeGroup(screens, "Screen_LandPreview");
            var screen = screenRoot.gameObject.AddComponent<LandPreviewScreen>();
            screen.SetState(GameState.LandPreview);

            // ---- 左上介绍面板 ----
            var panel = UiFactory.MakePanel(screenRoot, "IntroPanel", UiTheme.PanelBg, UiSkinRole.Panel);
            UiLayout.TopLeft(panel.rectTransform, 72f, 44f, 620f, 340f);
            panel.raycastTarget = true;

            // 材质从场景里已有的同类面板复制，不套映射表。
            MatchImage(panel, screens, UiSkinRole.Panel);

            var badge = UiFactory.MakeText(panel.rectTransform, "Badge", LandPreviewContent.Badge(0),
                UiTheme.FontSmall, UiTheme.TextAccent, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(badge.rectTransform, 24f, 20f, 24f, 28f);

            var title = UiFactory.MakeText(panel.rectTransform, "Title", LandPreviewContent.Names[0],
                UiTheme.FontTitle, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(title.rectTransform, 24f, 52f, 24f, 60f);

            var intro = UiFactory.MakeText(panel.rectTransform, "Intro", LandPreviewContent.Intros[0],
                UiTheme.FontBody, UiTheme.TextPrimary, TextAlignmentOptions.TopLeft);
            UiLayout.TopBand(intro.rectTransform, 24f, 136f, 24f, 120f);
            intro.lineSpacing = 6f;

            var facts = UiFactory.MakeText(panel.rectTransform, "Facts", LandPreviewContent.Facts[0],
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UiLayout.TopBand(facts.rectTransform, 24f, 272f, 24f, 48f);

            // ---- 右上面包屑 ----
            var breadcrumb = UiFactory.MakeText(screenRoot, "Breadcrumb", LandPreviewContent.Breadcrumb,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.MidlineRight);
            UiLayout.TopRight(breadcrumb.rectTransform, 72f, 44f, 420f, 40f);

            // ---- 底部操作提示（底部居中，避免窄屏偏移）----
            var hint = UiFactory.MakeText(screenRoot, "ControlHint", LandPreviewContent.ControlHint,
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.Center);
            UiLayout.Place(hint.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-380f, 64f), new Vector2(380f, 104f));

            // ---- 底部按钮 ----
            var footer = UiFactory.MakeGroup(screenRoot, "Footer");
            UiLayout.Stretch(footer, 0f, 0f, 0f, 0f);

            TMP_Text backLabel;
            var back = UiFactory.MakeButton(footer, "Button_Back", "返回选地", out backLabel);
            UiLayout.BottomLeft((RectTransform)back.transform, 72f, 44f, 240f, 64f);
            MatchButton(back, screens, UiSkinRole.Button);

            var buildGroup = UiFactory.MakeGroup(footer, "BuildGroup");
            UiLayout.Stretch(buildGroup, 0f, 0f, 0f, 0f);

            var buildHint = UiFactory.MakeText(buildGroup, "BuildHint", LandPreviewContent.BuildHint,
                18, UiTheme.TextSecondary, TextAlignmentOptions.MidlineRight);
            UiLayout.BottomRight(buildHint.rectTransform, 72f, 124f, 340f, 28f);

            TMP_Text buildLabel;
            var build = UiFactory.MakeButton(buildGroup, "Button_Build", LandPreviewContent.BuildLabel,
                out buildLabel, true);
            UiLayout.BottomRight((RectTransform)build.transform, 72f, 44f, 340f, 72f);
            MatchButton(build, screens, UiSkinRole.ButtonPrimary);

            // ---- 生成提示（默认隐藏）----
            var generating = UiFactory.MakeText(screenRoot, "GeneratingHint", LandPreviewContent.GeneratingHint,
                UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.Center);
            UiLayout.Center(generating.rectTransform, 0f, 0f, 420f, 40f);
            generating.gameObject.SetActive(false);

            // ---- 接线 ----
            screen.badgeText = badge;
            screen.titleText = title;
            screen.introText = intro;
            screen.factsText = facts;
            screen.breadcrumbText = breadcrumb;
            screen.controlHintText = hint;
            screen.generatingHint = generating;
            screen.backButton = back;
            screen.buildButton = build;
            screen.buildLabel = buildLabel;

            EditorUtility.SetDirty(screen);

            // 文字配色跟随现有界面（青蓝 / 暖橙），只换这几个主题色，手调过的颜色不动。
            if (coolText) UiSkin.SwapTextPalette(screenRoot.gameObject, true);

            // 与其它非起始屏一致：场景里默认隐藏，进 Play 由状态机负责显示。
            screenRoot.gameObject.SetActive(false);
            return screenRoot.gameObject;
        }

        // ------------------------------------------------------------------
        // LoadingOverlay
        // ------------------------------------------------------------------

        private static LoadingOverlay EnsureLoadingOverlay(Transform canvas, bool coolText)
        {
            var layer = FindChild(canvas, "OverlayLayer");
            if (layer == null)
            {
                var layerRect = UiFactory.MakeGroup(canvas, "OverlayLayer");
                UiLayout.Stretch(layerRect, 0f, 0f, 0f, 0f);
                layerRect.SetAsLastSibling();
                layer = layerRect;
            }

            var existing = FindChild(layer, "LoadingOverlay");
            if (existing != null)
            {
                var found = existing.GetComponent<LoadingOverlay>();
                if (found != null)
                {
                    existing.gameObject.SetActive(false);
                    return found;
                }
            }

            var overlayGo = new GameObject("LoadingOverlay", typeof(RectTransform));
            var rt = (RectTransform)overlayGo.transform;
            rt.SetParent(layer, false);
            UiLayout.Stretch(rt, 0f, 0f, 0f, 0f);

            var group = overlayGo.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            var backdrop = UiFactory.MakeImage(rt, "Backdrop", new Color(0f, 0f, 0f, 0.96f));
            UiLayout.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);
            backdrop.raycastTarget = true;

            var badge = UiFactory.MakeText(rt, "LandBadge", string.Empty,
                UiTheme.FontSmall, UiTheme.TextAccent, TextAlignmentOptions.Center);
            UiLayout.Center(badge.rectTransform, 0f, 160f, 500f, 28f);

            var title = UiFactory.MakeText(rt, "Title", LandPreviewContent.LoadingTitle,
                UiTheme.FontTitle, UiTheme.TextPrimary, TextAlignmentOptions.Center);
            UiLayout.Center(title.rectTransform, 0f, 100f, 800f, 60f);

            var status = UiFactory.MakeText(rt, "Status", LandPreviewContent.LoadingStatus[0],
                UiTheme.FontBody, UiTheme.TextSecondary, TextAlignmentOptions.Center);
            UiLayout.Center(status.rectTransform, 0f, 30f, 800f, 40f);

            Image fill;
            var track = UiFactory.MakeProgressBar(rt, "ProgressTrack", out fill);
            UiLayout.Center(track.rectTransform, 0f, -30f, 640f, 8f);
            MatchImage(track, canvas, UiSkinRole.BarTrack);
            MatchImage(fill, canvas, UiSkinRole.BarFill);

            var percent = UiFactory.MakeText(rt, "Percent", "0%",
                UiTheme.FontSmall, UiTheme.TextMuted, TextAlignmentOptions.Center);
            UiLayout.Center(percent.rectTransform, 0f, -60f, 200f, 28f);

            var tip = UiFactory.MakeText(rt, "Tip", string.Empty,
                18, UiTheme.TextMuted, TextAlignmentOptions.Center);
            UiLayout.Center(tip.rectTransform, 0f, -120f, 900f, 60f);

            var overlay = overlayGo.AddComponent<LoadingOverlay>();
            overlay.group = group;
            overlay.badgeText = badge;
            overlay.titleText = title;
            overlay.statusText = status;
            overlay.tipText = tip;
            overlay.percentText = percent;
            overlay.progressFill = fill;

            EditorUtility.SetDirty(overlay);

            if (coolText) UiSkin.SwapTextPalette(overlayGo, true);

            overlayGo.SetActive(false);
            return overlay;
        }

        // ------------------------------------------------------------------
        // 状态机注册
        // ------------------------------------------------------------------

        private static void RegisterScreen(GameFlowController flow, LandPreviewScreen screen)
        {
            if (flow == null || screen == null)
            {
                Debug.LogWarning("[UI] 没能注册 LandPreview 屏（缺少 GameFlowController 或屏脚本）。");
                return;
            }

            var list = flow.GetScreens();
            if (list == null) list = new List<PlaceholderScreen>();

            // 去重：同状态的旧屏（重复执行补丁时）先摘掉，避免 screenMap 里出现两份。
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null) { list.RemoveAt(i); continue; }
                if (list[i] != screen && list[i].State == GameState.LandPreview) list.RemoveAt(i);
            }

            if (!list.Contains(screen)) list.Add(screen);

            flow.SetScreens(list);
            EditorUtility.SetDirty(flow);
        }

        // ------------------------------------------------------------------
        // 后续屏的连带改动
        // ------------------------------------------------------------------

        /// <summary>LandParams 现在盖在明亮的 3D 农场上，补一块半透明底板保证表单可读。</summary>
        private static void PatchLandParams(Transform canvas, Transform screens)
        {
            var landParams = FindDeep(canvas, "Screen_LandParams");
            if (landParams == null) return;

            var body = FindChild(landParams, "Body");
            if (body == null) return;

            var existing = FindChild(body, "FormBackdrop");
            Image backdrop = existing != null ? existing.GetComponent<Image>() : null;

            if (backdrop == null)
            {
                backdrop = UiFactory.MakeImage(body, "FormBackdrop", UiTheme.PanelBg, UiSkinRole.Panel);
                UiLayout.Center(backdrop.rectTransform, 0f, 0f, 1280f, 640f);

                // 必须压在 ParamForm 下面，否则会挡住输入框。
                backdrop.rectTransform.SetAsFirstSibling();

                MatchImage(backdrop, screens, UiSkinRole.Panel);
            }

            backdrop.raycastTarget = true;
            EditorUtility.SetDirty(backdrop);
        }

        /// <summary>旧文案还在说「确认后进入第三人称视角农场」，农场其实在构建时已经出现了。</summary>
        private static void PatchConfirm(Transform canvas)
        {
            var confirm = FindDeep(canvas, "Screen_Confirm");
            if (confirm == null) return;

            SetText(confirm, "Header/Subtitle",
                "农场已构建完成。确认后开始第一阶段（S1 播种苗期），视角中心是玉米田，旁边停着智能房车。");
        }

        // ------------------------------------------------------------------
        // 材质：从场景里同类元素复制（绝不改既有元素）
        // ------------------------------------------------------------------

        /// <summary>
        /// 把场景里**已有**同类元素的贴图 / 材质 / 颜色 / 九宫格参数复制到新元素上。
        ///
        /// 【不要再用 UiSkin.ApplyAll(整棵界面)】那会按映射表把每个 UiSkinSlot 重写一遍，
        /// 之前手工挑过的贴图、颜色和文字配色会被一起覆盖。新元素只"抄"现有元素的参数。
        /// </summary>
        private static void MatchImage(Image target, Transform searchRoot, UiSkinRole role)
        {
            if (target == null) return;

            var source = FindRoleImage(searchRoot, role);
            if (source == null) return;

            target.sprite = source.sprite;
            target.type = source.type;
            target.material = source.material;
            target.color = source.color;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;

            EditorUtility.SetDirty(target);
        }

        private static void MatchButton(Button target, Transform searchRoot, UiSkinRole role)
        {
            if (target == null) return;

            MatchImage(target.targetGraphic as Image, searchRoot, role);

            var source = FindRoleButton(searchRoot, role);
            if (source == null) return;

            target.transition = source.transition;
            target.colors = source.colors;
            target.spriteState = source.spriteState;

            EditorUtility.SetDirty(target);
        }

        /// <summary>在现有界面里找第一个"已经套过皮"（有 sprite）的同角色图片。</summary>
        private static Image FindRoleImage(Transform searchRoot, UiSkinRole role)
        {
            if (searchRoot == null) return null;

            var slots = searchRoot.GetComponentsInChildren<UiSkinSlot>(true);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].role != role) continue;

                var image = slots[i].GetComponent<Image>();
                if (image != null && image.sprite != null) return image;
            }
            return null;
        }

        private static Button FindRoleButton(Transform searchRoot, UiSkinRole role)
        {
            if (searchRoot == null) return null;

            var slots = searchRoot.GetComponentsInChildren<UiSkinSlot>(true);
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i].role != role) continue;

                var button = slots[i].GetComponent<Button>();
                if (button != null) return button;
            }
            return null;
        }

        /// <summary>转场 + 点击反馈组件（重复执行不会重复添加）。</summary>
        private static void EnsureMotionComponents(GameFlowController flow)
        {
            if (flow == null) return;

            if (flow.GetComponent<ScreenTransitionPlayer>() == null)
                flow.gameObject.AddComponent<ScreenTransitionPlayer>();

            var feedback = flow.GetComponent<UiClickFeedback>();
            if (feedback == null) feedback = flow.gameObject.AddComponent<UiClickFeedback>();

            if (feedback.clickClip == null)
                feedback.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiClickFeedback.ClipPath);

            EditorUtility.SetDirty(feedback);
        }

        // ------------------------------------------------------------------
        // 既有元素护栏：拍快照 → 补丁结束原样恢复
        // ------------------------------------------------------------------

        private sealed class VisualState
        {
            public Image image;
            public Sprite sprite;
            public Material material;
            public Color color;
            public Image.Type type;
            public float pixelsPerUnitMultiplier;
            public bool preserveAspect;
            public bool fillCenter;

            public TMP_Text text;
            public Color textColor;
        }

        private static List<VisualState> CaptureVisuals(GameObject root)
        {
            var list = new List<VisualState>();
            if (root == null) return list;

            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                list.Add(new VisualState
                {
                    image = image,
                    sprite = image.sprite,
                    material = image.material,
                    color = image.color,
                    type = image.type,
                    pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier,
                    preserveAspect = image.preserveAspect,
                    fillCenter = image.fillCenter,
                });
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                list.Add(new VisualState { text = text, textColor = text.color });
            }

            return list;
        }

        /// <summary>把既有元素恢复成补丁开始前的样子，返回实际改动处数。</summary>
        private static int RestoreVisuals(List<VisualState> states)
        {
            var fixedCount = 0;
            if (states == null) return fixedCount;

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];

                if (state.image != null)
                {
                    var image = state.image;
                    var dirty = false;

                    if (image.sprite != state.sprite) { image.sprite = state.sprite; dirty = true; }
                    if (image.material != state.material) { image.material = state.material; dirty = true; }
                    if (image.color != state.color) { image.color = state.color; dirty = true; }
                    if (image.type != state.type) { image.type = state.type; dirty = true; }
                    if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, state.pixelsPerUnitMultiplier))
                    {
                        image.pixelsPerUnitMultiplier = state.pixelsPerUnitMultiplier;
                        dirty = true;
                    }
                    if (image.preserveAspect != state.preserveAspect) { image.preserveAspect = state.preserveAspect; dirty = true; }
                    if (image.fillCenter != state.fillCenter) { image.fillCenter = state.fillCenter; dirty = true; }

                    if (dirty)
                    {
                        EditorUtility.SetDirty(image);
                        fixedCount++;
                    }
                }

                if (state.text != null && state.text.color != state.textColor)
                {
                    state.text.color = state.textColor;
                    EditorUtility.SetDirty(state.text);
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        // ------------------------------------------------------------------
        // 查找 / 文本
        // ------------------------------------------------------------------

        private static GameObject FindRoot(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == RootName) return roots[i];
            }
            return null;
        }

        /// <summary>按名字递归查找（包含未激活节点）。</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>按 "A/B/C" 路径查找直接子级链（Transform.Find，支持未激活）。</summary>
        private static Transform FindChild(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;

            var current = parent;
            var parts = path.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                current = current.Find(parts[i]);
                if (current == null) return null;
            }
            return current;
        }

        private static void SetText(Transform root, string path, string content)
        {
            var node = FindChild(root, path);
            if (node == null)
            {
                Debug.LogWarning("[UI] 找不到 " + path + "，文案未更新。", root);
                return;
            }

            var text = node.GetComponent<TMP_Text>();
            if (text == null)
            {
                Debug.LogWarning("[UI] " + path + " 上没有 TMP 文本，文案未更新。", root);
                return;
            }

            text.text = content;
            EditorUtility.SetDirty(text);
        }
    }
}
