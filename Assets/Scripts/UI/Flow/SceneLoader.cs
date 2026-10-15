// ---------------------------------------------------------------------------
//  SceneLoader.cs —— 把 UI 与 Farm 两个场景组合起来
// ---------------------------------------------------------------------------
//  作用：这是「Bootstrap」场景上的唯一脚本。它负责：
//    1. 加性加载 UI 场景（10 个界面 + GameFlowController）
//    2. 加性加载 Farm 场景（田块 + 玉米 + 巡航小车）
//    3. 处理**两个相机冲突**（UI 场景有 Main Camera，Farm 场景有 FarmCamera）
//    4. 提供一个跨场景的入口，方便任何一边 FindObjectOfType 拿到另一边的组件
//
//  【为什么不用单场景合并】
//    UI.unity 已经 1.4 MB / 493 个对象，而且布局是精调过的；
//    把农场塞进去风险大（之前出过一次误删事故）。
//    加性加载让两边可以各自独立修改和调试，互不影响。
//
//  【相机冲突怎么处理】
//    两个场景各有一个相机，同时激活会互相覆盖画面。
//    这里的做法：**只保留 FarmCamera 渲染**，把 UI 的 Main Camera 关掉，
//    但把 UI 的 Canvas 设成 Screen Space - Overlay（Overlay 不需要相机）。
//    如果 UI 的 Canvas 是 Screen Space - Camera，那就需要把它的
//    worldCamera 指向 FarmCamera。
// ---------------------------------------------------------------------------

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>Bootstrap 场景的加载器：把 UI 与 Farm 组合成一个可运行的 demo。</summary>
    [DisallowMultipleComponent]
    public class SceneLoader : MonoBehaviour
    {
        [Header("场景名（必须在 Build Settings 里登记）")]
        [SerializeField] private string uiSceneName = "UI";
        [SerializeField] private string farmSceneName = "Farm";

        [Tooltip("是否处理两个场景的相机冲突（强烈建议保持开启）。")]
        [SerializeField] private bool resolveCameraConflict = true;

        [Tooltip("只允许一个 AudioListener。两个同时存在时 Unity 每次都会警告" +
                 "「There are 2 audio listeners in the scene」。")]
        [SerializeField] private bool keepSingleAudioListener = true;

        [Header("UI 相机")]
        [Tooltip("保留 UI 相机的渲染吗？\n" +
                 "★ 默认关掉渲染、但**保持组件启用** —— 因为 uGUI 的点击检测" +
                 "（GraphicRaycaster）需要它，把组件整个禁掉 UI 就点不动了。\n" +
                 "关掉渲染的做法是把 cullingMask 设成 0，而不是 enabled=false。")]
        [SerializeField] private bool keepUiCameraRendering = false;

        [Tooltip("开局默认打开哪个状态。")]
        [SerializeField] private GameState startState = GameState.Welcome;

        [Header("运行时状态（只读）")]
        [SerializeField] private bool uiLoaded;
        [SerializeField] private bool farmLoaded;

        public bool UiLoaded { get { return uiLoaded; } }
        public bool FarmLoaded { get { return farmLoaded; } }

        private IEnumerator Start()
        {
            yield return LoadAdditive(uiSceneName, ok => uiLoaded = ok);
            yield return LoadAdditive(farmSceneName, ok => farmLoaded = ok);

            if (resolveCameraConflict) ResolveCameraConflict();
            if (keepSingleAudioListener) KeepSingleAudioListener();

            BindUiCanvasToFarmCamera();

            // ★ 顺序很重要：**先**让 Farm 场景生效，**再**补齐组件并优化。
            //
            // 【为什么】原实现先 EnsureFarmComponents（里面会 Run 优化）后聚焦场景，
            // 而那时候 FarmFieldBuilder.Awake 还没跑（加性加载的 Awake 在这一帧稍后），
            // 所以优化器面对的是"空场景"——统计出来的渲染器数其实是别的东西。
            ApplyFarmSceneAsActive();

            // ★ 关掉 UI 场景自带的平行光。
            //
            // 【这是"从 Bootstrap 跑就高亮"的真正原因】
            //   UI.unity 里有一个 `Directional Light`（intensity = 1），
            //   Farm.unity 里有一个 `Sun`（FarmLighting 把它设成 0.4）。
            //   加性加载后**两个平行光同时照明** → 场景被照了两遍 → 明显过亮。
            //   直接打开 Farm 场景时没有 UI 场景，所以只有 Sun，亮度就正常。
            DisableForeignDirectionalLights();

            EnsureFarmComponents();
            FocusFlowToStartState();

            Debug.Log(string.Format(
                "[Bootstrap] 加载完成：UI={0} Farm={1}。\n" +
                "操作：\n" +
                "  农场相机 —— Tab 切俯视/第三人称，C 切取景档位（全景/中景/近景），" +
                "V 切天空视角，QE 旋转，滚轮缩放\n" +
                "  巡航小车 —— 手动巡航时 WASD / 方向键移动，Q/E 转向\n" +
                "  亮度太亮/太暗 —— 调 FarmRoot 上 FarmLighting 的太阳与环境光强度",
                uiLoaded, farmLoaded), this);
        }

        private IEnumerator LoadAdditive(string sceneName, System.Action<bool> done)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                if (done != null) done(false);
                yield break;
            }

            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                if (done != null) done(true);
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogWarning("[Bootstrap] 加载不了场景「" + sceneName +
                                 "」。确认它已加入 Build Settings（File ▸ Build Settings）。", this);
                if (done != null) done(false);
                yield break;
            }

            while (!op.isDone) yield return null;
            if (done != null) done(true);
        }

        /// <summary>
        /// 处理两个场景的相机冲突。
        ///
        /// 【为什么不是直接把 UI 相机禁掉】
        ///   uGUI 的点击检测（GraphicRaycaster）**需要相机组件是启用的**。
        ///   如果把 Camera.enabled 设成 false，界面就点不动了。
        ///   正确做法是**保留组件启用、但让它不渲染任何东西**：
        ///   把 cullingMask 设成 0。
        ///
        /// 【之前的问题】原实现把 UI 相机 enabled=false，同时没管 AudioListener，
        ///   于是运行时同时存在两个 AudioListener，Unity 一直警告。
        /// </summary>
        private void ResolveCameraConflict()
        {
            var farmCam = FindFarmCamera();
            var all = FindObjectsOfType<Camera>();

            for (var i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c == farmCam) continue;

                if (keepUiCameraRendering) continue;

                // 保留组件启用（UI 点击要用），但不出图
                c.cullingMask = 0;
                c.enabled = true;
                // depth 压到最低，确保不参与成像顺序
                c.depth = -100;
            }

            if (farmCam != null)
            {
                farmCam.enabled = true;
                // ★ 保证农场相机画所有层
                farmCam.cullingMask = ~0;
                // ★ depth 必须大于其它相机，否则可能被后画的相机盖掉
                farmCam.depth = 10;
                farmCam.clearFlags = CameraClearFlags.Skybox;
                // ★ 农场相机不能排在相机栈之外（否则完全不渲染）
                if (farmCam.targetTexture != null)
                {
                    Debug.LogWarning("[Bootstrap] FarmCamera 上挂了 RenderTexture，画面会输出到贴图而不是屏幕。" +
                                     "已清空 targetTexture。", farmCam);
                    farmCam.targetTexture = null;
                }
            }

            Debug.Log(string.Format(
                "[Bootstrap] 相机处理: FarmCamera={0}（enabled={1}, depth={2}, mask={3}），" +
                "其余 {4} 个相机已置为不出图（组件仍启用，UI 才能点击）。",
                farmCam != null ? farmCam.name : "(没找到!)",
                farmCam != null ? farmCam.enabled.ToString() : "-",
                farmCam != null ? farmCam.depth.ToString() : "-",
                farmCam != null ? farmCam.cullingMask.ToString() : "-",
                Mathf.Max(0, all.Length - 1)), this);
        }

        /// <summary>只保留一个 AudioListener。</summary>
        private void KeepSingleAudioListener()
        {
            var all = FindObjectsOfType<AudioListener>();
            if (all.Length <= 1) return;

            // 优先保留 FarmCamera 上的那个（那才是玩家在听的相机）
            var farmCam = FindFarmCamera();
            AudioListener keep = null;
            if (farmCam != null) keep = farmCam.GetComponent<AudioListener>();

            for (var i = 0; i < all.Length; i++)
            {
                var l = all[i];
                if (l == null || l == keep) continue;
                Destroy(l);
            }

            Debug.Log(string.Format("[Bootstrap] 场景里有 {0} 个 AudioListener，已只保留 1 个。", all.Length), this);
        }

        private static Camera FindFarmCamera()
        {
            var rig = FindObjectOfType<FarmCameraRig>();
            return rig != null ? rig.GetComponent<Camera>() : null;
        }

        /// <summary>
        /// 如果 UI 的 Canvas 是「Screen Space - Camera」，把它指向 FarmCamera；
        /// 如果是 Overlay 则不用管。
        /// </summary>
        private void BindUiCanvasToFarmCamera()
        {
            var farmCam = FindFarmCamera();
            if (farmCam == null) return;

            var canvases = FindObjectsOfType<Canvas>();
            for (var i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera) continue;

                if (canvas.worldCamera == null || !canvas.worldCamera.enabled)
                {
                    canvas.worldCamera = farmCam;
                    Debug.Log("[Bootstrap] 把 Canvas「" + canvas.name + "」的渲染相机指向了 FarmCamera。", canvas);
                }
            }
        }

        /// <summary>
        /// 让 Farm 场景成为**激活场景**，并把它自己的 RenderSettings 应用过来。
        ///
        /// 【为什么必须做这一步 —— 这是"亮度不一致"的根因】
        ///   Unity 里 **RenderSettings（环境光 / 天空盒 / 雾）是"每个场景各存一份"** 的，
        ///   而运行时**只有激活场景的那一份会生效**。
        ///
        ///   于是：
        ///     · 直接打开 Farm 场景 Play  → Farm 是激活场景 → 它的环境光 0.3、天空盒 sky-7、雾生效 ✅
        ///     · 从 Bootstrap 加性加载     → **Bootstrap 才是激活场景**
        ///       → 用的是 Bootstrap 的环境光 1.0、默认天空盒、无雾 ❌
        ///
        ///   Bootstrap 场景是 `NewSceneSetup.EmptyScene` 建的，它的 RenderSettings 是
        ///   Unity 的**默认值**（环境光强度 1.0、默认天空盒、无雾）——
        ///   所以从 Bootstrap 跑就明显更亮，而且天空盒也不对。
        ///
        ///   修法：加载完把 Farm 设成激活场景。
        ///   （Farm 场景自身存的 RenderSettings 就会生效；
        ///     另外 FarmLighting.Apply() 也会在运行时再设一遍并实时响应改动。）
        /// </summary>
        private void ApplyFarmSceneAsActive()
        {
            var farmScene = SceneManager.GetSceneByName(farmSceneName);
            if (!farmScene.IsValid() || !farmScene.isLoaded)
            {
                Debug.LogWarning("[Bootstrap] Farm 场景没加载好，无法设为激活场景。", this);
                return;
            }

            if (SceneManager.GetActiveScene() == farmScene) return;

            var before = SceneManager.GetActiveScene().name;
            SceneManager.SetActiveScene(farmScene);
            Debug.Log(string.Format(
                "[Bootstrap] 激活场景 {0} → {1}。\n" +
                "  这一步很关键：RenderSettings（环境光/天空盒/雾）是【按场景各存一份】的，" +
                "只有激活场景那一份生效。\n" +
                "  不设的话会用 Bootstrap 的默认值（环境光 1.0、默认天空盒），" +
                "画面就会比直接打开 Farm 场景明显更亮。",
                before, farmScene.name), this);

            // 把 Farm 的 RenderSettings 显式抄一份到当前全局状态，
            // 保证即使将来 Unity 改了激活场景语义也不会跑偏。
            CopyFarmRenderSettings(farmScene);
        }

        /// <summary>把 Farm 场景里保存的 RenderSettings 抄成当前生效值。</summary>
        private void CopyFarmRenderSettings(Scene farmScene)
        {
            var lighting = FindObjectOfType<FarmLighting>();
            if (lighting == null) return;

            // FarmLighting 自己知道该用什么值（它的 Apply 会写 RenderSettings）
            lighting.Apply();
        }

        /// <summary>
        /// 只保留农场自己的那个平行光（名字叫 "Sun"，或挂在 Farm 场景里），
        /// 其余场景的平行光一律关掉。
        ///
        /// 【为什么必须做】UI.unity 里带了一个 `Directional Light`。
        /// 加性加载之后，UI 的 1.0 强度光 + 农场的 0.4 强度光会**叠加照明**，
        /// 画面直接翻倍变亮 —— 这就是"从 Bootstrap 跑就高亮、直接开 Farm 就正常"的根因。
        /// </summary>
        private void DisableForeignDirectionalLights()
        {
            var farmScene = SceneManager.GetSceneByName(farmSceneName);
            var lights = FindObjectsOfType<Light>();
            var disabled = 0;

            for (var i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || l.type != LightType.Directional) continue;

                var inFarm = farmScene.IsValid() && l.gameObject.scene == farmScene;
                if (inFarm) continue;      // 农场自己的留着

                // 关掉强度而不是禁用组件 —— 保证它仍然存在（将来要恢复也方便）
                l.intensity = 0f;
                l.enabled = false;
                disabled++;
            }

            if (disabled > 0)
            {
                Debug.Log(string.Format(
                    "[Bootstrap] 关掉了 {0} 个非农场场景的平行光（否则会和农场的 Sun 叠加照明，画面过亮）。" +
                    "被关掉的通常是 UI 场景自带的 Directional Light。", disabled), this);
            }
        }

        /// <summary>
        /// 确保农场场景里**必须有**的两个组件存在：<see cref="FarmLighting"/> 与
        /// <see cref="FarmOptimizer"/>。缺了就现场补上。
        ///
        /// 【为什么必须这么兜底】这是踩过的坑：这两个组件是后来加进
        /// FarmSceneBuilder 的，而已经生成好的 Farm.unity 里没有它们。
        /// 结果就是：调亮度没反应、按 F8 没反应、阴影关不掉 ——
        /// 所有"优化"都跑了个空，白费了很多轮排查时间。
        /// </summary>
        private void EnsureFarmComponents()
        {
            var builder = FindObjectOfType<FarmFieldBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[Bootstrap] 场景里没有 FarmFieldBuilder，" +
                                 "Farm 场景可能没加载成功。", this);
                return;
            }

            var added = new System.Collections.Generic.List<string>();

            // ---- 光照 ----
            if (FindObjectOfType<FarmLighting>() == null)
            {
                // 挂在太阳上，这样 ApplySun() 能直接找到它
                var sun = FindDirectionalLight();
                var host = sun != null ? sun.gameObject : builder.gameObject;
                host.AddComponent<FarmLighting>();
                added.Add("FarmLighting" + (sun != null ? "（挂到 Sun 上）" : "（没找到太阳，挂到 FarmRoot 上）"));
            }

            // ---- 优化器 ----
            FarmOptimizer optimizer = FindObjectOfType<FarmOptimizer>();
            if (optimizer == null)
            {
                optimizer = builder.gameObject.AddComponent<FarmOptimizer>();
                added.Add("FarmOptimizer");
            }

            if (added.Count > 0)
            {
                Debug.LogWarning("[Bootstrap] 场景里缺少 " + added.Count +
                                 " 个农场组件，已运行时补上：" + string.Join("、", added.ToArray()) +
                                 "。\n" +
                                 "  建议重跑一次菜单「Engineer / 农场 / 一键搭建农场场景」，" +
                                 "把这些组件固化进场景，避免每次运行都补。", this);
            }

            // 立刻跑一次优化，让 Instancing / 阴影设置生效
            if (optimizer != null) optimizer.Run();

            // 检测"几何被烘焙进场景"+ "运行时又生成一遍"的重复问题
            CheckForBakedGeometry(builder);
        }

        /// <summary>
        /// 检测场景里是否残留了"编辑期烘焙的几何"。
        ///
        /// 【为什么需要这个检查】这是本项目最严重的坑：
        ///   FarmSceneBuilder 曾经在编辑期调用 Build()，把 20,333 个 GameObject
        ///   （46 MB）序列化进了 Farm.unity。Play 时 Awake 又生成一遍，
        ///   于是场景里同时存在两套几何 —— 渲染器涨到 4 万、draw call 爆炸、
        ///   阴影投影 10 万、画面叠加变亮。排查了好几轮才发现。
        ///
        /// 正确的做法是"只在运行时生成"（各生成器的 Awake/Start 里已加
        /// Application.isPlaying 判断）。这个检查用来兜底报警。
        /// </summary>
        private void CheckForBakedGeometry(FarmFieldBuilder builder)
        {
            if (builder == null) return;

            var soilRoot = builder.transform.Find("Soil");
            var plantRoot = builder.transform.Find("Plants");
            if (soilRoot == null && plantRoot == null) return;

            // 运行时生成的子物体数量是已知的：
            //   土壤 = 块数 × 每块土壤数；玉米 = 株数 × 3 个阶段容器
            // 如果远超这个规模，说明场景里还留着烘焙的旧几何。
            var soilChildren = soilRoot != null ? soilRoot.childCount : 0;
            var plantChildren = plantRoot != null ? plantRoot.childCount : 0;

            // 正常情况下 Plants 下只有 3 个 Stage_* 容器
            if (plantChildren > 10 || soilChildren > 100)
            {
                Debug.LogError(string.Format(
                    "[Bootstrap] 检测到疑似【重复几何】：Plants 下有 {0} 个子物体、" +
                    "Soil 下有 {1} 个（正常应分别 ≤3 与 ＝土壤块数/合并块数）。\n" +
                    "  这几乎可以确定是：Farm.unity 里残留了编辑期烘焙的几何，" +
                    "运行时又生成了一遍。\n" +
                    "  修法：重跑菜单「Engineer / 农场 / 一键搭建农场场景」重新生成场景。\n" +
                    "  （新版本的构建器已经不再烘焙几何，所以重跑一次即可根治。）",
                    plantChildren, soilChildren), builder);
            }
        }

        private static Light FindDirectionalLight()
        {
            var lights = FindObjectsOfType<Light>();
            for (var i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional) return lights[i];
            return null;
        }

        private void FocusFlowToStartState()
        {
            var flow = FindObjectOfType<GameFlowController>();
            if (flow == null) return;

            // GameFlowController 的 Awake 会自己进起始状态，
            // 这里只在起始状态不是它默认值时才强制切一次。
            if (flow.Current != startState) flow.GoTo(startState);
        }
    }
}
