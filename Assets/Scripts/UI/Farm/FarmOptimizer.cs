// ---------------------------------------------------------------------------
//  FarmOptimizer.cs —— 农场的性能优化总开关
// ---------------------------------------------------------------------------
//  作用：在田块/玉米/地形/房车都生成完之后，统一做三件事：
//    1. 把各部分的零碎网格合并（MeshCombiner）
//    2. 关掉小物体的阴影投射（阴影是 draw call 的最大来源）
//    3. 打印优化前后的对比，方便确认效果
//
//  【为什么阴影是大头】
//    实测（1920×1080，Built-in 管线）：
//      20,059 个渲染器 → 283,776 batches，FPS 5.0
//    约等于每个渲染器被画了 14 次。这 14 次里绝大多数是**阴影级联**：
//    默认 4 档级联 + 方向光阴影距离 150 m，每个投影物体都要按级联重画。
//    把级联降到 1、并把玉米/土壤这种"数量巨大、影子不重要"的物体
//    设成不投影，能立刻去掉大部分重复绘制。
//
//  【执行时机】
//    必须在所有生成器之后。FarmSceneBuilder 按
//      FieldBuilder → Paths → TerrainRing → Equipment → Grid → Lighting → **Optimizer**
//    的顺序添加组件，Awake 顺序与添加顺序一致。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>农场的性能优化：合并网格 + 控制阴影。</summary>
    [DisallowMultipleComponent]
    public class FarmOptimizer : MonoBehaviour
    {
        [Header("网格合并")]
        [Tooltip("是否合并零碎网格。\n" +
                 "★ 默认【关闭】：这批模型数据量太大，合并会因 65535 顶点上限被拆成\n" +
                 "很多 chunk，而且跨 chunk 时几何会被复制（实测 Tris 从 29.7M 涨到 50.2M）。\n" +
                 "改用下面的 GPU Instancing 才对。")]
        [SerializeField] private bool combineMeshes = false;

        [Header("GPU Instancing")]
        [Tooltip("给场景里的材质打开 GPU Instancing。\n" +
                 "★ 这是这批模型最有效的优化：200 株玉米共用同一份网格，\n" +
                 "每株只是换个矩阵再画一次，由 GPU 批处理。")]
        [SerializeField] private bool enableInstancing = true;

        [Header("阴影")]
        [Tooltip("是否关掉玉米的阴影投射。玉米数量最大，影子又不重要。")]
        [SerializeField] private bool disableCornShadows = true;

        [Tooltip("是否关掉土壤块细节（110 个土块）的阴影投射。")]
        [SerializeField] private bool disableSoilShadows = true;

        [Tooltip("是否关掉田埂/小路的阴影投射。")]
        [SerializeField] private bool disablePathShadows = true;

        [Tooltip("是否连外围地形和房车也不投影。开着画面会更平，但能再省一点。")]
        [SerializeField] private bool disableAllShadows = false;

        [Header("运行时开关")]
        [Tooltip("按这个键可以随时开/关全部阴影，用来对比性能。")]
        [SerializeField] private KeyCode toggleShadowsKey = KeyCode.F8;

        [Tooltip("按这个键可以重新跑一次优化（改了参数之后用）。")]
        [SerializeField] private KeyCode rerunKey = KeyCode.F9;

        private void Awake()
        {
            // 把初始阴影设置记下来，F8 可以来回切
            shadowsOn = QualitySettings.shadows != ShadowQuality.Disable;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleShadowsKey)) ToggleAllShadows();
            if (Input.GetKeyDown(rerunKey)) Run();
        }

        /// <summary>一键开/关全部阴影，用来对比性能。</summary>
        public void ToggleAllShadows()
        {
            shadowsOn = !shadowsOn;
            ApplyGlobalShadows(shadowsOn);
            Debug.Log("[农场优化] 阴影总开关：" + (shadowsOn ? "开" : "关") +
                      "（关掉能省掉整个阴影 pass）。", this);
        }

        private void ApplyGlobalShadows(bool on)
        {
            QualitySettings.shadows = on ? ShadowQuality.HardOnly : ShadowQuality.Disable;

            var lights = FindObjectsOfType<Light>();
            for (var i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null || l.type != LightType.Directional) continue;
                l.shadows = on ? LightShadows.Soft : LightShadows.None;
            }
        }

        private bool shadowsOn = true;

        [Header("其它")]
        [Tooltip("是否把相机远裁剪面收紧到田块附近，减少远处绘制。0 = 不改。")]
        [SerializeField] private float cameraFarClip = 300f;

        [Header("运行时统计（只读）")]
        [SerializeField] private int renderersBefore;
        [SerializeField] private int renderersAfter;
        [SerializeField] private int shadowCastersDisabled;
        [SerializeField] private int instancedMaterials;

        public int RenderersBefore { get { return renderersBefore; } }
        public int RenderersAfter { get { return renderersAfter; } }

        private void Start()
        {
            Run();
            DumpRenderState();
        }

        /// <summary>
        /// 把"影响画面亮度的全部运行时状态"打出来。
        ///
        /// 【为什么需要】Bootstrap 加性加载时，**哪个场景是 active 决定了 RenderSettings
        /// 用谁的**。所以"直接打开 Farm 场景"和"从 Bootstrap 加载 Farm"可能亮度完全不同 ——
        /// 这正是我们遇到的现象。与其猜，不如把真实值打出来对比。
        /// </summary>
        private void DumpRenderState()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[农场诊断] ==== 运行时渲染状态 ====");
            sb.AppendLine(string.Format("  当前激活场景        : {0}",
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
            sb.AppendLine(string.Format("  已加载场景          : {0}",
                string.Join("、", LoadedSceneNames())));

            // ★ 列出**全部**平行光并标明所属场景。
            //   之前只报"第一个平行光"，正好抓到 UI 场景自带那个，
            //   于是误报成"太阳 intensity=1"，白白误导了一轮排查。
            var lights = FindObjectsOfType<Light>();
            var directional = 0;
            sb.AppendLine(string.Format("  平行光（共 {0} 个，其中平行光 {1} 个）:",
                lights.Length, CountDirectional(lights)));
            for (var i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                if (l.type != LightType.Directional) continue;
                directional++;
                sb.AppendLine(string.Format(
                    "     {0,-20} 场景={1,-10} enabled={2,-6} intensity={3:0.##} shadows={4}",
                    l.name, l.gameObject.scene.name, l.enabled, l.intensity, l.shadows));
            }
            if (directional > 1)
                sb.AppendLine("     ★★ 有多个平行光在照明 —— 这是画面过亮的典型原因！");

            sb.AppendLine(string.Format("  RenderSettings.ambientMode      : {0}", RenderSettings.ambientMode));
            sb.AppendLine(string.Format("  RenderSettings.ambientIntensity : {0:0.##}", RenderSettings.ambientIntensity));
            sb.AppendLine(string.Format("  RenderSettings.skybox           : {0}",
                RenderSettings.skybox != null ? RenderSettings.skybox.name : "(null)"));
            sb.AppendLine(string.Format("  RenderSettings.fog              : {0}（{1}，{2:0}~{3:0} m）",
                RenderSettings.fog, RenderSettings.fogMode,
                RenderSettings.fogStartDistance, RenderSettings.fogEndDistance));
            sb.AppendLine(string.Format("  QualitySettings.shadows         : {0}（级联 {1}，距离 {2:0} m）",
                QualitySettings.shadows, QualitySettings.shadowCascades, QualitySettings.shadowDistance));

            var cams = FindObjectsOfType<Camera>();
            sb.AppendLine(string.Format("  相机（共 {0} 个）:", cams.Length));
            for (var i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (c == null) continue;
                sb.AppendLine(string.Format(
                    "     {0,-18} 场景={1,-10} enabled={2,-6} depth={3,-5} mask={4,-11} clear={5}",
                    c.name, c.gameObject.scene.name, c.enabled, c.depth, c.cullingMask, c.clearFlags));
            }
            sb.AppendLine(string.Format("  AudioListener 数    : {0}", FindObjectsOfType<AudioListener>().Length));

            // 按分支统计农场自己的渲染器
            sb.AppendLine("  农场渲染器按分支统计:");
            var total = 0;
            var children = new List<Transform>();
            for (var i = 0; i < transform.childCount; i++) children.Add(transform.GetChild(i));
            for (var i = 0; i < children.Count; i++)
            {
                var n = children[i].GetComponentsInChildren<Renderer>(true).Length;
                total += n;
                if (n > 0) sb.AppendLine(string.Format("     {0,-20} {1,7}", children[i].name, n));
            }
            sb.AppendLine(string.Format("     {0,-20} {1,7}", "（农场小计）", total));

            // ★ 按场景统计 —— 这一步用来定位"到底哪来的 1.8 万个渲染器"
            var sceneRenderers = 0;
            var scenes = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (var s = 0; s < scenes; s++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!sc.isLoaded) continue;
                var roots = sc.GetRootGameObjects();
                var n = 0;
                for (var r = 0; r < roots.Length; r++)
                    n += roots[r].GetComponentsInChildren<Renderer>(true).Length;
                sceneRenderers += n;
                sb.AppendLine(string.Format("     [场景 {0,-10}] 渲染器 {1,7}", sc.name, n));
            }
            sb.AppendLine(string.Format("     [全部合计] {0,18}", sceneRenderers));

            Debug.Log(sb.ToString(), this);
        }

        private static int CountDirectional(Light[] lights)
        {
            var n = 0;
            for (var i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional) n++;
            return n;
        }

        private static List<string> LoadedSceneNames()
        {
            var list = new List<string>();
            var n = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (var i = 0; i < n; i++)
            {
                var sc = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (sc.isLoaded) list.Add(sc.name);
            }
            return list;
        }

        private static Light FindDirectionalLight()
        {
            var lights = FindObjectsOfType<Light>();
            for (var i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].type == LightType.Directional) return lights[i];
            return null;
        }

        /// <summary>执行优化。可以重复调用。</summary>
        public void Run()
        {
            renderersBefore = CountRenderers();
            shadowCastersDisabled = 0;

            // ★ 先开 GPU Instancing —— 这是这批模型最有效的一招。
            //   200 株玉米共用同一份网格，每株只是换个矩阵再画一次。
            if (enableInstancing)
            {
                instancedMaterials = FarmGpuInstancing.Enable(gameObject);
                var s = FarmGpuInstancing.Measure(gameObject);
                Debug.Log(string.Format(
                    "[农场优化] GPU Instancing：新开 {0} 个材质；" +
                    "当前 唯一网格 {1} 个 / 唯一材质 {2} 个 / 渲染器 {3} 个。\n" +
                    "  （渲染器数不变是正常的：Instancing 省的是 draw call，不是对象数。\n" +
                    "   关键看 Statistics 的 Batches 与 SetPass calls。）",
                    instancedMaterials, s.UniqueMeshes, s.UniqueMaterials, s.Renderers), this);
            }

            if (combineMeshes) CombineAll();

            // 玉米/土壤的网格已经在 FarmFieldBuilder 里按预制体合并过了，
            // 这里只负责关掉它们的阴影投射。
            if (disableCornShadows) shadowCastersDisabled += DisableShadows("Plants");
            if (disableSoilShadows) shadowCastersDisabled += DisableShadows("Soil");
            if (disablePathShadows) shadowCastersDisabled += DisableShadows("Paths");
            if (disableAllShadows)
            {
                shadowCastersDisabled += DisableShadows("TerrainRing");
                shadowCastersDisabled += DisableShadows("RV");
            }

            // 统计还剩多少阴影投影物体 —— 这个数应该从十万降到几百
            var leftover = 0;
            var all = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r != null && r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) leftover++;
            }

            TightenCameraFarClip();

            renderersAfter = CountRenderers();

            Debug.Log(string.Format(
                "[农场优化] 渲染器 {0}；关闭 {1} 个物体的阴影投射，仍有 {2} 个投影物体；" +
                "阴影级联 {3} 档、距离 {4:0} m。\n" +
                "  按 {5} 可以一键开关全部阴影来对比性能；按 {6} 重新跑优化。",
                renderersAfter, shadowCastersDisabled, leftover,
                QualitySettings.shadowCascades, QualitySettings.shadowDistance,
                toggleShadowsKey, rerunKey), this);
        }

        private int CountRenderers()
        {
            return GetComponentsInChildren<Renderer>(true).Length;
        }

        // ==================================================================
        //  网格合并
        // ==================================================================

        /// <summary>
        /// 合并"只有一个实例"的部分。
        ///
        /// 【为什么这里不合并 Plants / Soil】
        ///   这两个是**大量重复实例**（玉米 600 个、土壤 36 个）。
        ///   对它们来说，正确的做法是在 <see cref="FarmFieldBuilder"/> 里
        ///   **按预制体合并一次，然后实例化 600 次** —— 合并成本只付一次。
        ///   如果在这里合并整棵树，就等于把 600 个实例的约 150 万顶点
        ///   全部重拼一遍，启动会卡十几秒。
        ///   （这个坑踩过：早期版本就是这么写的。）
        /// </summary>
        private void CombineAll()
        {
            // 单实例的部分：在这里合并最划算
            CombineChild("TerrainRing", "外围地形");
            CombineChild("RV", "房车");
            CombineChild("Paths", "田埂小路");
        }

        private void CombineChild(string childName, string label)
        {
            var t = transform.Find(childName);
            if (t == null) return;

            var before = t.GetComponentsInChildren<MeshFilter>(true).Length;
            if (before == 0) return;

            var r = MeshCombiner.Combine(t.gameObject);
            if (r.Combined)
            {
                Debug.Log(string.Format("[农场优化] {0}：{1} 个网格 → {2} 个合并块。",
                    label, r.Before, r.After), t);
            }
        }

        // ==================================================================
        //  阴影
        // ==================================================================

        private int DisableShadows(string childName)
        {
            var t = transform.Find(childName);
            if (t == null) return 0;
            return FarmLighting.DisableShadowCasting(t.gameObject);
        }

        private void TightenCameraFarClip()
        {
            if (cameraFarClip <= 0f) return;

            var cam = FindObjectOfType<FarmCameraRig>();
            if (cam == null) return;

            var c = cam.GetComponent<Camera>();
            if (c == null) return;

            if (c.farClipPlane > cameraFarClip) c.farClipPlane = cameraFarClip;
        }
    }
}
