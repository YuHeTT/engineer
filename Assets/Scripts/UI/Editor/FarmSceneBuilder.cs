// ---------------------------------------------------------------------------
//  FarmSceneBuilder.cs —— 一键搭建农场可视化场景
// ---------------------------------------------------------------------------
//  作用：把所有可视化元件（光照 / 天空盒 / 相机 / 田块根节点）自动建好，
//        并在可能的情况下自动找到土壤块与玉米的 glb 并挂上。
//
//  【为什么用编辑器脚本搭而不是手工拖】
//    · 田块要摆几十块土壤 + 上百株玉米，手工不现实（那是运行时的活）
//    · 但"场景骨架"（相机 / 光照 / 天空盒 / 根节点）手工建很容易漏配置、
//      而且没法进版本管理。脚本化之后可以一键重建、一键对比。
//    · 与现有的 UiWhiteboxBuilder 是同一套思路。
//
//  菜单：Engineer / 农场 / 一键搭建农场场景
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Engineer.UI.EditorTools
{
    /// <summary>农场可视化场景的构建器。</summary>
    public static class FarmSceneBuilder
    {
        // ==================================================================
        //  素材路径
        // ==================================================================
        //  2026-10 素材整理后的结构：
        //    Assets/Art/Models/{Terrain, TerrainPlus, RV, Corn, Soil}/
        //    Assets/Art/Skybox/
        //    Assets/Art/UI_Tech/
        //  旧的 Assets/可能用到的建模 仍然保留（未删），但新代码一律走 Art/Models。
        // ==================================================================

        private const string ArtRoot = "Assets/Art/Models";

        private const string SoilBlockPath = ArtRoot + "/Soil/玉米田土壤_四垄.glb";

        private const string SkyboxMaterialPath = "Assets/Art/Skybox/sky-7.mat";

        /// <summary>外围地形用的模型。平川台是 6×6×6 的实体台地，实测非可平铺贴片。</summary>
        private const string TerrainPath = ArtRoot + "/Terrain/平川台.glb";

        /// <summary>巡航小车模型（User 导入的巡视车 FBX，含车轮）。</summary>
        private const string RoverPath = ArtRoot + "/Rover/reference_rover_lux3d_wheels.fbx";

        /// <summary>房车模型（含开合动画）。</summary>
        private const string RvPath = ArtRoot + "/RV/智能农田房车_精细可开合_v2.glb";

        /// <summary>
        /// 玉米预制体，顺序**固定**为：幼苗(好,正常,差)、成长(好,正常,差)、成熟(好,正常,差)。
        /// FarmFieldBuilder.ResolveCornPrefab 就是按"阶段×长势 → 下标 = (阶段-1)*3 + 长势"取值的，
        /// 改这个数组的顺序会直接导致模型错配。
        /// </summary>
        private static readonly string[] CornPaths =
        {
            ArtRoot + "/Corn/玉米_幼苗_好.glb",
            ArtRoot + "/Corn/玉米_幼苗_正常.glb",
            ArtRoot + "/Corn/玉米_幼苗_差.glb",
            ArtRoot + "/Corn/玉米_成长_好.glb",
            ArtRoot + "/Corn/玉米_成长_正常.glb",
            ArtRoot + "/Corn/玉米_成长_差.glb",
            ArtRoot + "/Corn/玉米_成熟_好.glb",
            ArtRoot + "/Corn/玉米_成熟_正常.glb",
            ArtRoot + "/Corn/玉米_成熟_差.glb",
        };

        private const string FarmScenePath = "Assets/Scenes/Farm.unity";

        // ==================================================================
        //  一键搭建
        // ==================================================================

        [MenuItem("Engineer/农场/一键搭建农场场景", false, 10)]
        public static void BuildFarmScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = new FarmVisualConfig();

            var sunRoot = BuildLightingAndSky();
            var fieldRoot = BuildFieldRoot(config, sunRoot);

            // ★ 这里**故意不调用**任何 Build()。
            //
            // 【为什么这是本项目最重要的一条约定】
            //   所有几何（土壤、玉米、田埂、地形、房车）都是"运行时生成"的。
            //   如果在编辑期调 Build()，Unity 会把它们**序列化进 Farm.unity**：
            //     实测 20,333 个 GameObject、46 MB 场景文件、20,059 个 MeshRenderer。
            //   而 Play 时 Awake 又生成一遍 → 场景里**同时存在两套几何** →
            //     渲染器累积涨到 40,251、draw call 爆炸、
            //     阴影投影物体 10 万、画面还叠加渲染变亮。
            //   这一条坑了我们好几轮排查。
            //
            //   所以：场景只存"根节点 + 组件 + 配置"，几何一律留到运行时。
            //   代价是编辑期看不到预览 —— 按 Play 看即可。
            var cam = BuildCamera(fieldRoot);

            // 保存场景
            var dir = Path.GetDirectoryName(FarmScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            EditorSceneManager.SaveScene(scene, FarmScenePath);

            Debug.Log(string.Format(
                "[农场构建器] 场景已生成并保存到 {0}\n{1}\n" +
                "接下来检查三件事：\n" +
                "  1. FarmFieldBuilder 上的 soilPiecePrefab 与 cornPrefabs 是否已自动挂上\n" +
                "  2. 相机取景是否合适（不合适就拖相机 Transform，或用菜单「自动取景」）\n" +
                "  3. 玉米有没有陷进土里或浮空 —— 默认开了【自动贴垄面】，一般不需要调；\n" +
                "     真有问题就先关掉 AutoAlignCornToSoil，再手动调 CornBaseOffsetY",
                FarmScenePath, config), fieldRoot);

            Selection.activeGameObject = fieldRoot.gameObject;
        }

        [MenuItem("Engineer/农场/只重建田块与玉米（保留相机光照）", false, 11)]
        public static void RebuildFieldOnly()
        {
            var builder = Object.FindObjectOfType<FarmFieldBuilder>();
            if (builder == null)
            {
                Debug.LogWarning("[农场构建器] 当前场景里没有 FarmFieldBuilder。");
                return;
            }

            // ★ 编辑期不再生成几何 —— 否则会被序列化进场景文件，
            //   Play 时又生成一遍就成了两套（见 BuildFarmScene 里的说明）。
            //   现在改参数在 Play 中会自动重建。
            Debug.LogWarning(
                "[农场构建器] 几何改为**只在运行时生成**，编辑期不再烘焙进场景。\n" +
                "  要改田块/玉米：Play 之后在 FarmFieldBuilder.Config 上直接改，" +
                "运行时会自动重建（Console 会打印「检测到 Config 改动」）。", builder);
            EditorUtility.SetDirty(builder);
        }

        [MenuItem("Engineer/农场/自动取景（相机对齐田块）", false, 12)]
        public static void FrameCamera()
        {
            var rig = Object.FindObjectOfType<FarmCameraRig>();
            var builder = Object.FindObjectOfType<FarmFieldBuilder>();
            if (rig == null || builder == null)
            {
                Debug.LogWarning("[农场构建器] 需要同时存在 FarmCameraRig 与 FarmFieldBuilder。");
                return;
            }
            rig.FrameField(builder.FieldBounds, builder.Config.BlockGroupSize.x);
        }

        // ==================================================================
        //  巡航遮罩开关（编辑器里预览用）
        // ==================================================================

        [MenuItem("Engineer/农场/切换未覆盖遮罩（编辑器预览）", false, 13)]
        public static void ToggleMaskPreview()
        {
            var grid = Object.FindObjectOfType<FarmFieldGrid>();
            if (grid == null)
            {
                Debug.LogWarning("[农场构建器] 当前场景里没有 FarmFieldGrid。");
                return;
            }

            var root = grid.transform.Find("UncoveredMask");
            if (root == null)
            {
                // 还没生成过：手动建一次（这时绕过 play mode 限制）
                var refresh = typeof(FarmFieldGrid).GetMethod("RefreshMask",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                refresh?.Invoke(grid, null);
                root = grid.transform.Find("UncoveredMask");
            }

            if (root == null)
            {
                Debug.LogWarning("[农场构建器] 遮罩没生成。检查 FarmFieldGrid.showUncoveredMask。");
                return;
            }

            var on = !root.gameObject.activeSelf;
            root.gameObject.SetActive(on);
            Debug.Log("[农场构建器] 未覆盖遮罩预览：" + (on ? "开" : "关") +
                      "。注意运行时遮罩由 FarmFieldGrid 自己管理，这个开关只影响编辑器里看不看得到。", grid);
            EditorUtility.SetDirty(grid);
        }

        // ==================================================================
        //  Bootstrap 场景：把 UI 与 Farm 组合起来
        // ==================================================================

        [MenuItem("Engineer/农场/生成 Bootstrap 场景（UI + Farm 一起跑）", false, 20)]
        public static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            var loader = go.AddComponent<SceneLoader>();

            // 场景名要和 Build Settings 里的名字一致，这里用资源文件名
            SetPrivate(loader, "uiSceneName", System.IO.Path.GetFileNameWithoutExtension(UiScenePath));
            SetPrivate(loader, "farmSceneName", System.IO.Path.GetFileNameWithoutExtension(FarmScenePath));

            EnsureInBuildSettings(UiScenePath);
            EnsureInBuildSettings(FarmScenePath);
            EnsureInBuildSettings(BootstrapScenePath);

            var dir = Path.GetDirectoryName(BootstrapScenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            EditorSceneManager.SaveScene(scene, BootstrapScenePath);

            Debug.Log(string.Format(
                "[农场构建器] Bootstrap 场景已生成：{0}\n" +
                "它会在运行时加性加载「{1}」与「{2}」两个场景。\n" +
                "★ 请把 Bootstrap.unity 设为 Build Settings 里的第 0 个场景，然后 Play 它。\n" +
                "★ 三个场景都已自动登记进 Build Settings。",
                BootstrapScenePath,
                System.IO.Path.GetFileNameWithoutExtension(UiScenePath),
                System.IO.Path.GetFileNameWithoutExtension(FarmScenePath)), go);

            Selection.activeGameObject = go;
        }

        private const string UiScenePath = "Assets/Scenes/UI.unity";
        private const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";

        private static void SetPrivate(object target, string fieldName, object value)
        {
            if (target == null) return;
            var f = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        /// <summary>把一个场景登记进 Build Settings（已登记则跳过）。</summary>
        private static void EnsureInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            for (var i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == scenePath) return;
            }

            var list = new List<EditorBuildSettingsScene>(scenes);
            list.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ==================================================================
        //  光照与天空盒
        // ==================================================================

        /// <summary>
        /// 建太阳 + 天空盒，然后把光照的**控制权交给 FarmLighting 组件**。
        ///
        /// 数值不写在这里，是因为它要能实时调 —— 农场和 UI 是叠加显示的，
        /// 农场一亮，半透明的 UI 面板就压不住背景、文字看不清。
        /// 放在组件上就能 Play 时直接拖滑杆。
        /// </summary>
        private static GameObject BuildLightingAndSky()
        {
            var sunGo = new GameObject("Sun");
            sunGo.AddComponent<Light>().type = LightType.Directional;

            var lighting = sunGo.AddComponent<FarmLighting>();

            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (sky != null)
            {
                SetPrivate(lighting, "skybox", sky);
                Debug.Log("[农场构建器] 天空盒用 sky-7（8K 白天晴空）。");
            }
            else
            {
                Debug.LogWarning("[农场构建器] 没找到天空盒材质 " + SkyboxMaterialPath +
                                 "，先用默认天空。");
            }

            lighting.Apply();
            return sunGo;
        }

        // ==================================================================
        //  田块根节点
        // ==================================================================

        private static Transform BuildFieldRoot(FarmVisualConfig config, GameObject sunRoot)
        {
            var root = new GameObject("FarmRoot");
            root.transform.position = Vector3.zero;

            var builder = root.AddComponent<FarmFieldBuilder>();
            builder.Configure(config);

            var soil = AssetDatabase.LoadAssetAtPath<GameObject>(SoilBlockPath);
            var corns = new GameObject[CornPaths.Length];
            var missing = new List<string>();

            for (var i = 0; i < CornPaths.Length; i++)
            {
                corns[i] = AssetDatabase.LoadAssetAtPath<GameObject>(CornPaths[i]);
                if (corns[i] == null) missing.Add(CornPaths[i]);
            }

            builder.SetPrefabs(soil, corns);

            // ---- 找不到时的报错：重点提示"是不是没装 glTF 导入器" ----
            // 这是真实踩过的坑：Unity 2022.3 内置【不支持】glb/gltf。
            // 没有导入器时，.glb 的 .meta 会是 DefaultImporter（当成普通文件），
            // AssetDatabase.LoadAssetAtPath<GameObject> 必然返回 null，
            // 但文件明明就在那里 —— 很容易误判成"路径写错了"。
            if (soil == null)
            {
                Debug.LogError(
                    "[农场构建器] 找不到土壤块模型：" + SoilBlockPath + "\n" +
                    "如果文件确实存在，几乎可以肯定是【Unity 没有把 .glb 当模型导入】。\n" +
                    "  自检：选中那个 .glb，看 Inspector 顶部是 glTF 导入设置（正常）\n" +
                    "        还是只显示几行 Name/Path（说明被当成普通文件了）。\n" +
                    "  修法：装 Unity 官方 glTF 导入包 —— Window ▸ Package Manager ▸\n" +
                    "        + ▸ Add package by name ▸ com.unity.cloud.gltfast\n" +
                    "        （Unity 2022.3 内置不支持 glb，必须装这个包）\n" +
                    "  装完等 Unity 重新导入，再跑一次本菜单。");
            }

            if (missing.Count > 0)
            {
                Debug.LogError("[农场构建器] 有 " + missing.Count + " / " + CornPaths.Length +
                               " 个玉米模型没找到：\n  " + string.Join("\n  ", missing.ToArray()) +
                               "\n（原因同上：没装 glTF 导入器，或路径变了）");
            }

            // 同步绑定器
            var binder = root.AddComponent<FarmGrowthBinder>();

            // 田埂与小路（田块之间 + 外围一圈）
            var paths = root.AddComponent<FarmFieldPaths>();
            // paths.Build();   ← 编辑期不生成几何，留到运行时（见 BuildFarmScene 的说明）

            // 外围地形：平川台铺一条在 +Z 侧，让画面有远方
            var terrain = root.AddComponent<FarmTerrainRing>();
            var terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPath);
            if (terrainPrefab != null)
            {
                var tf = typeof(FarmTerrainRing).GetField("terrainPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tf != null) tf.SetValue(terrain, terrainPrefab);
                // terrain.Build();   ← 编辑期不生成几何，留到运行时（见 BuildFarmScene 的说明）
            }
            else
            {
                Debug.LogWarning("[农场构建器] 没找到地形模型 " + TerrainPath +
                                 "，外围地形留空（只铺田埂与小路）。");
            }

            // ---- 巡航：网格 + 小车 ----
            var fieldGrid = root.AddComponent<FarmFieldGrid>();
            // fieldGrid.Build();   ← 编辑期不生成几何，留到运行时（见 BuildFarmScene 的说明）
            // 注意：这里**不调** RefreshMask()。
            // 遮罩铺满整片田，编辑器里生成会把场景盖成一片黑，
            // 在 Scene 视图里就完全看不出田和玉米长什么样了。
            // FarmFieldGrid.RefreshMask 内部也有 maskOnlyInPlayMode 兜底。

            var cruise = root.AddComponent<FarmCruiseController>();
            var rover = AssetDatabase.LoadAssetAtPath<GameObject>(RoverPath);
            if (rover != null)
            {
                var rf = typeof(FarmCruiseController).GetField("roverPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (rf != null) rf.SetValue(cruise, rover);
            }
            else
            {
                Debug.LogWarning("[农场构建器] 没找到小车模型 " + RoverPath +
                                 "，巡航会用胶囊体占位。");
            }

            // ---- 田边房车 ----
            var equipment = root.AddComponent<FarmEquipment>();
            var rv = AssetDatabase.LoadAssetAtPath<GameObject>(RvPath);
            if (rv != null)
            {
                var ef = typeof(FarmEquipment).GetField("rvPrefab",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (ef != null) ef.SetValue(equipment, rv);
                // equipment.Build();   ← 编辑期不生成几何，留到运行时（见 BuildFarmScene 的说明）
            }
            else
            {
                Debug.LogWarning("[农场构建器] 没找到房车模型 " + RvPath + "，跳过。");
            }

            // ---- 性能优化（必须在所有生成器之后）----
            // Awake 顺序与 AddComponent 顺序一致，所以这一行要放在最后。
            root.AddComponent<FarmOptimizer>();

            // ---- 自检：把"组件到底加进场景没有"这件事查清楚 ----
            // 这个检查是被坑出来的：FarmLighting / FarmOptimizer 是后加的，
            // 而旧场景里没有它们，导致调亮度、按 F8、关阴影全部无效，
            // 排查了好几轮才发现是"场景里压根没这个组件"。
            VerifyComponents(root, sunRoot);

            return root.transform;
        }

        /// <summary>校验农场场景必需的关键组件都在。</summary>
        private static void VerifyComponents(GameObject root, GameObject sunRoot)
        {
            var missing = new List<string>();

            if (root.GetComponent<FarmFieldBuilder>() == null) missing.Add("FarmFieldBuilder");
            if (root.GetComponent<FarmFieldGrid>() == null) missing.Add("FarmFieldGrid");
            if (root.GetComponent<FarmCruiseController>() == null) missing.Add("FarmCruiseController");
            if (root.GetComponent<FarmFieldPaths>() == null) missing.Add("FarmFieldPaths");
            if (root.GetComponent<FarmTerrainRing>() == null) missing.Add("FarmTerrainRing");
            if (root.GetComponent<FarmEquipment>() == null) missing.Add("FarmEquipment");
            if (root.GetComponent<FarmOptimizer>() == null) missing.Add("FarmOptimizer");
            if (sunRoot != null && sunRoot.GetComponent<FarmLighting>() == null) missing.Add("FarmLighting(在 Sun 上)");

            if (missing.Count > 0)
            {
                Debug.LogError("[农场构建器] 场景自检失败，以下组件没加上：" +
                               string.Join("、", missing.ToArray()) +
                               "\n这会导致对应功能完全无效（例如缺 FarmLighting 就调不了亮度）。", root);
            }
            else
            {
                Debug.Log("[农场构建器] 场景自检通过：8 个农场组件全部就位。", root);
            }
        }

        // ==================================================================
        //  相机
        // ==================================================================

        private static Camera BuildCamera(Transform fieldRoot)
        {
            var camGo = new GameObject("FarmCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 800f;
            cam.clearFlags = CameraClearFlags.Skybox;

            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<FarmCameraRig>();

            // 让相机跟着田块中心
            var centerGo = new GameObject("FieldCenter");
            centerGo.transform.SetParent(fieldRoot.parent, false);
            var centerField = typeof(FarmCameraRig).GetField("fieldCenter",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (centerField != null) centerField.SetValue(rig, centerGo.transform);

            var builder = fieldRoot.GetComponent<FarmFieldBuilder>();

            // 【注意】编辑期不再生成几何，所以 builder.FieldBounds 是空的。
            // 相机的初始取景改成按 Config 推算的尺寸来算 ——
            // 运行时 FarmFieldBuilder 生成完后会自己调 FrameField 修正。
            var cfgSize = builder != null && builder.Config != null
                ? builder.Config.FieldSize
                : new Vector2(27f, 21f);
            var blockW = builder != null && builder.Config != null
                ? builder.Config.BlockGroupSize.x
                : 12f;

            var fake = new Bounds(Vector3.zero,
                new Vector3(cfgSize.x, 1f, cfgSize.y));
            centerGo.transform.position = Vector3.zero;
            rig.FrameField(fake, blockW);

            // 先按 FrameField 的结果把相机摆到位，避免第一帧从原点飞过来
            var size = Mathf.Max(cfgSize.x, cfgSize.y);
            var dist = Mathf.Max(18f, size * 1.15f);
            camGo.transform.position = new Vector3(0f, dist * 0.85f, -dist * 0.62f);
            camGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0.6f, 0f) - camGo.transform.position, Vector3.up);

            Debug.Log(string.Format(
                "[农场构建器] 相机已按田块取景：田块 {0:0.#}×{1:0.#} m，" +
                "相机摆到 (0, {2:0.#}, {3:0.#})。觉得太远/太近就用滚轮缩放，或按 Tab 切第三人称。",
                builder.FieldBounds.size.x, builder.FieldBounds.size.z,
                camGo.transform.position.y, camGo.transform.position.z), camGo);

            return cam;
        }
    }
}
