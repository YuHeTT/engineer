using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒界面一键构建器。
    ///
    /// 用法（二选一）：
    ///   1) 编辑器菜单：Tools ▸ UI ▸ 构建白盒界面（12 屏），构建完直接进 Play 模式点着看；
    ///   2) 命令行：
    ///      "&lt;Unity 安装目录&gt;\Editor\Unity.exe" -batchmode -quit -projectPath "&lt;工程根目录&gt;"
    ///          -executeMethod Engineer.UI.EditorTools.UiWhiteboxBuilder.BuildFromCommandLine -logFile ui-build.log
    ///
    /// 特点：
    ///   * 幂等：每次构建先删掉旧的 UI_Root，再整棵重建，不会出现半新半旧的层级；
    ///   * 不依赖任何手动拖拽引用，全部由代码按《Unity界面.docx》的 12 张图摆放；
    ///   * 只生成界面骨架（矩形 + 文字 + 按钮 + 输入框），几何/材质/动效留给后续阶段；
    ///   * 换皮：构建时只给元素贴「角色」标签（<see cref="UiSkinSlot"/>），素材由
    ///     Tools ▸ UI ▸ 皮肤：套用 EPS 素材 一键填（见 <see cref="UiSkin"/>），重建后能再套回来。
    /// </summary>
    public static partial class UiWhiteboxBuilder
    {
        /// <summary>白盒界面所在场景（工程里已有，不新建）。</summary>
        public const string TargetScenePath = "Assets/Scenes/UI.unity";

        /// <summary>整棵界面树的根节点名，重建时会先删掉同名节点。</summary>
        public const string RootName = "UI_Root";

        /// <summary>TMP 基础资源是否存在，用于判断有没有跑过 TMP 首次导入。</summary>
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // ==================================================================
        // 菜单入口
        // ==================================================================

        [MenuItem("Tools/UI/构建白盒界面（12 屏）", false, 10)]
        public static void BuildFromMenu()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("UI 白盒构建器", "请先退出 Play 模式再构建。", "好");
                return;
            }

            // TMP 基础资源（着色器 / 默认样式）没导入时，中文会显示成粉色方块。
            // 这里先导入，让用户看清 Console 提示后再点一次构建，避免建出「字体材质缺着色器」的场景。
            if (EnsureTmpEssentials(true))
            {
                EditorUtility.DisplayDialog("UI 白盒构建器",
                    "TMP 基础资源正在导入（中文显示必需）。\n\n" +
                    "等 Console 里导入与编译结束后，再点一次「Tools ▸ UI ▸ 构建白盒界面（12 屏）」。",
                    "好");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                EditorUtility.DisplayDialog("UI 白盒构建器", "当前没有打开任何场景。", "好");
                return;
            }

            if (scene.path != TargetScenePath)
            {
                var switchScene = EditorUtility.DisplayDialog("UI 白盒构建器",
                    "当前场景是：\n" + scene.path + "\n\n白盒界面要构建在：\n" + TargetScenePath + "\n\n是否切换过去再构建？",
                    "切换并构建", "取消");
                if (!switchScene) return;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            }

            Build(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[UI] 白盒界面构建完成，共 " + ((int)GameState.Revenue + 1) + " 个屏。直接进入 Play 模式即可点击走流程。");
        }

        [MenuItem("Tools/UI/导入 TMP 基础资源（中文显示所需）", false, 11)]
        public static void ImportTmpEssentialsMenu()
        {
            if (EnsureTmpEssentials(true))
                EditorUtility.DisplayDialog("UI 白盒构建器", "已发起 TMP 基础资源导入。", "好");
            else
                EditorUtility.DisplayDialog("UI 白盒构建器", "TMP 基础资源已存在，无需重复导入。", "好");
        }

        // ==================================================================
        // 命令行入口
        // ==================================================================

        /// <summary>供 -executeMethod 调用；构建完成后直接退出编辑器。</summary>
        public static void BuildFromCommandLine()
        {
            var scene = SceneManager.GetActiveScene();

            if (!scene.IsValid() || !scene.isLoaded || scene.path != TargetScenePath)
            {
                if (!File.Exists(TargetScenePath))
                {
                    Debug.LogError("[UI] 找不到场景 " + TargetScenePath + "，命令行构建中止。");
                    EditorApplication.Exit(1);
                    return;
                }

                scene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
            }

            Build(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[UI] 命令行白盒构建完成：" + TargetScenePath);
            EditorApplication.Exit(0);
        }

        // ==================================================================
        // 基础设施
        // ==================================================================

        /// <summary>没有 TMP Settings 时自动导入 TMP 基础资源（中文字体材质依赖它提供的着色器）。</summary>
        private static bool EnsureTmpEssentials(bool verbose)
        {
            if (File.Exists(TmpSettingsPath))
            {
                if (verbose) Debug.Log("[UI] 已存在 " + TmpSettingsPath + "，跳过 TMP 基础资源导入。");
                return false;
            }

            Debug.Log("[UI] 未检测到 TMP 基础资源，开始导入 TMP Essential Resources…");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();

            if (!File.Exists(TmpSettingsPath))
                Debug.LogWarning("[UI] TMP 基础资源可能仍在后台导入。若中文显示为粉色或方块，请手动执行 " +
                                 "Window ▸ TextMeshPro ▸ Import TMP Essential Resources 后重新构建。");

            return true;
        }

        /// <summary>确保场景里有一台用于看白盒的相机（深色纯色底），没有就补一台。</summary>
        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null) camera = UnityEngine.Object.FindObjectOfType<Camera>();

            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.transform.position = new Vector3(0f, 1.6f, -8f);
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = UiTheme.ViewBackdrop;
        }

        /// <summary>没有 EventSystem 就补一个（uGUI 按钮、输入框必需）。</summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform GetRect(Component component)
        {
            return component.GetComponent<RectTransform>();
        }

        // ==================================================================
        // 白盒屏母版
        // ==================================================================

        /// <summary>
        /// 概念屏（1~6、10~12）：顶部标题 + 中间内容 + 底部按钮条 + 可选压暗层。
        /// </summary>
        private static PlaceholderScreen NewScreen(Transform parent, GameState state, string title, string subtitle,
            bool dim, out RectTransform body, out RectTransform footer)
        {
            return NewScreen<PlaceholderScreen>(parent, state, title, subtitle, dim, out body, out footer);
        }

        /// <summary>同上，但挂指定的屏脚本（农场 / 巡航 / 分析屏各有子类）。</summary>
        private static T NewScreen<T>(Transform parent, GameState state, string title, string subtitle,
            bool dim, out RectTransform body, out RectTransform footer) where T : PlaceholderScreen
        {
            var screenRoot = UiFactory.MakeGroup(parent, GameStateInfo.ScreenObjectName(state));
            var screen = screenRoot.gameObject.AddComponent<T>();
            screen.SetState(state);

            if (dim)
            {
                var backdrop = UiFactory.MakeImage(screenRoot, "Backdrop", UiTheme.ScreenDim, UiSkinRole.ScreenDim);
                backdrop.raycastTarget = true;
            }

            var header = UiFactory.MakeGroup(screenRoot, "Header");
            UiLayout.TopBand(header, UiTheme.SideMargin, UiTheme.TopMargin, UiTheme.SideMargin, UiTheme.HeaderHeight);

            var titleText = UiFactory.MakeText(header, "Title", title, UiTheme.FontTitle, UiTheme.TextPrimary, TextAlignmentOptions.MidlineLeft);
            UiLayout.Stretch(titleText.rectTransform, 0f, 0f, 0f, 44f);

            var subtitleText = UiFactory.MakeText(header, "Subtitle", subtitle, UiTheme.FontSmall, UiTheme.TextSecondary, TextAlignmentOptions.MidlineLeft);
            UiLayout.Stretch(subtitleText.rectTransform, 2f, 72f, 0f, 4f);

            var divider = UiFactory.MakeDivider(header, "Divider");
            UiLayout.BottomBand(divider.rectTransform, 0f, 0f, 0f, 2f);

            footer = UiFactory.MakeGroup(screenRoot, "Footer");
            UiLayout.BottomBand(footer, UiTheme.SideMargin, UiTheme.BottomMargin, UiTheme.SideMargin, UiTheme.FooterHeight);

            body = UiFactory.MakeGroup(screenRoot, "Body");
            UiLayout.Stretch(body, UiTheme.SideMargin, UiTheme.TopMargin + UiTheme.HeaderHeight + 16f,
                UiTheme.SideMargin, UiTheme.BottomMargin + UiTheme.FooterHeight + 20f);

            return screen;
        }

        /// <summary>
        /// HUD 屏（7 农场主界面、8 机器人操控）：没有大标题，四周是可点击的仪表层，
        /// 盖在 3D 视角之上（白盒阶段用一块深色底 + 占位说明代替 3D 画面）。
        /// </summary>
        private static PlaceholderScreen NewHudScreen(Transform parent, GameState state, string viewNote,
            out RectTransform hud, out RectTransform bottomBar)
        {
            return NewHudScreen<PlaceholderScreen>(parent, state, viewNote, out hud, out bottomBar);
        }

        /// <summary>同上，但挂指定的屏脚本。</summary>
        private static T NewHudScreen<T>(Transform parent, GameState state, string viewNote,
            out RectTransform hud, out RectTransform bottomBar) where T : PlaceholderScreen
        {
            var screenRoot = UiFactory.MakeGroup(parent, GameStateInfo.ScreenObjectName(state));
            var screen = screenRoot.gameObject.AddComponent<T>();
            screen.SetState(state);

            var view = UiFactory.MakeImage(screenRoot, "ViewPlaceholder", UiTheme.ViewBackdrop, UiSkinRole.ScreenBackdrop);
            view.raycastTarget = true;

            var note = UiFactory.MakeText(screenRoot, "ViewPlaceholder_Note", viewNote,
                UiTheme.FontSection, UiTheme.TextMuted, TextAlignmentOptions.Center);
            UiLayout.Center(note.rectTransform, 0f, -40f, 1100f, 120f);

            hud = UiFactory.MakeGroup(screenRoot, "HUD");
            UiLayout.Stretch(hud, UiTheme.SideMargin, UiTheme.TopMargin, UiTheme.SideMargin, UiTheme.BottomMargin);

            bottomBar = UiFactory.MakeGroup(screenRoot, "BottomBar");
            UiLayout.BottomBand(bottomBar, UiTheme.SideMargin, UiTheme.BottomMargin, UiTheme.SideMargin, UiTheme.ButtonHeight);

            return screen;
        }

        // ==================================================================
        // 常用按钮
        // ==================================================================

        /// <summary>「上一步返回」按钮（左下角，对应文档里"点击左下角返回上一步"）。</summary>
        private static Button FooterBack(RectTransform footer, PlaceholderScreen screen)
        {
            TMP_Text label;
            var button = UiFactory.MakeButton(footer, "Button_Back", "上一步返回", out label);
            UiLayout.BottomLeft(GetRect(button), 0f, 0f, UiTheme.BackButtonWidth, UiTheme.ButtonHeight);
            screen.AddAction(button, ScreenActionKind.Back, GameState.Welcome);
            return button;
        }

        /// <summary>主行动按钮（右下角）。</summary>
        private static Button FooterPrimary(RectTransform footer, PlaceholderScreen screen, string label, GameState target, bool primary = true)
        {
            TMP_Text labelText;
            var button = UiFactory.MakeButton(footer, "Button_" + target, label, out labelText, primary);
            UiLayout.BottomRight(GetRect(button), 0f, 0f, UiTheme.PrimaryButtonWidth, UiTheme.ButtonHeight);
            screen.AddAction(button, ScreenActionKind.GoTo, target);
            return button;
        }

        /// <summary>还没有实现动作的按钮（例如未开放的地形 / 作物）。</summary>
        private static Button PlaceholderButton(RectTransform parent, PlaceholderScreen screen, string name, string label,
            string note, out TMP_Text labelText, bool primary = false)
        {
            var button = UiFactory.MakeButton(parent, name, label, out labelText, primary);
            screen.AddAction(button, ScreenActionKind.LogOnly, GameState.Welcome, note);
            return button;
        }

        // ==================================================================
        // 主构建流程
        // ==================================================================

        private static void Build(Scene scene)
        {
            EnsureTmpEssentials(false);
            EnsureCamera();
            EnsureEventSystem();

            // 幂等：先删掉上一次的整棵树（包括被隐藏的对象）。
            foreach (var existingRoot in scene.GetRootGameObjects())
            {
                if (existingRoot.name == RootName) UnityEngine.Object.DestroyImmediate(existingRoot);
            }

            var rootObject = new GameObject(RootName);
            var root = rootObject.transform;

            // ---------- 画布：1920×1080 参考分辨率 ----------
            var canvasObject = new GameObject("Screen_Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiTheme.CanvasWidth, UiTheme.CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            var screensRoot = UiFactory.MakeGroup(canvasObject.transform, "Screens");

            // ---------- 12 张界面图 → 10 个屏（image7/image11 共用农场屏，image8/image9 共用巡航屏）----------
            var screens = new List<PlaceholderScreen>
            {
                BuildWelcome(screensRoot),
                BuildIntro(screensRoot),
                BuildLandSelect(screensRoot),
                BuildLandParams(screensRoot),
                BuildPlantSelect(screensRoot),
                BuildConfirm(screensRoot),
                BuildFarmStage(screensRoot),
                BuildRobotCruise(screensRoot),
                BuildAnalyze(screensRoot),
                BuildRevenue(screensRoot),
            };

            // ---------- 状态机 ----------
            var flowObject = new GameObject("UI_Flow");
            flowObject.transform.SetParent(root, false);
            var flow = flowObject.AddComponent<GameFlowController>();
            flow.SetScreens(screens);

            // ---------- 动效：整屏转场 + 点击反馈 ----------
            // 两者都和状态机挂在同一个对象上，运行时互相按 GetComponent 找得到，不用拖引用。
            var transition = flowObject.AddComponent<ScreenTransitionPlayer>();
            transition.Style = ScreenTransitionStyle.Hologram;

            var feedback = flowObject.AddComponent<UiClickFeedback>();
            feedback.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>(UiClickFeedback.ClipPath);
            if (feedback.clickClip == null)
                Debug.LogWarning("[UI] 找不到点击音效 " + UiClickFeedback.ClipPath + "，点击将静音。");

            // 除起始屏外全部隐藏（Play 后状态机还会再确认一次）。
            foreach (var screen in screens)
            {
                if (screen != null) screen.gameObject.SetActive(screen.State == GameState.Welcome);
            }

            // ---------- 可选：构建完直接把 EPS 素材套上 ----------
            // 开关：Tools ▸ UI ▸ 皮肤：构建时自动套用；纯色白盒阶段保持关闭即可。
            UiSkin.ApplyAfterBuild(rootObject);

            Debug.Log("[UI] 已构建 " + screens.Count + " 个屏，场景：" + TargetScenePath + "。按 Play 即可点击走通全流程。");
        }
    }
}
