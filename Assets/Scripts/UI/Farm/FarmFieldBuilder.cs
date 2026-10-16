// ---------------------------------------------------------------------------
//  FarmFieldBuilder.cs —— 生成农田地块与玉米
// ---------------------------------------------------------------------------
//  作用：按 <see cref="FarmVisualConfig"/> 的配置，把"土壤块 + 玉米"实例化出来。
//
//  【为什么运行时生成而不是在编辑器里摆好】
//    一块田要摆几十块土壤 + 上百株玉米，手工摆不现实；
//    而且阶段一变（S2→S3→S4）要整片换模型。运行时生成 + 按需重建最省事。
//
//  【阶段 → 玉米模型 的对应（★ 待策划确认）】
//    UI 有 5 个阶段，玉米只有 3 个阶段模型，文档说"S1 只见土地、S5 与 S4 共用"：
//      S1 播种苗期   → 不显示植株
//      S2 营养旺长期 → 幼苗（0.28~0.42 m）
//      S3 生殖关键期 → 成长（1.00~1.51 m，无果穗）
//      S4 灌浆成熟期 → 成熟（1.50~2.26 m，带果穗）
//      S5 收获期     → 与 S4 共用
//    每个阶段还有"好/正常/差"三档，由长势决定。
//
//  【按需重建策略】
//    9 个模型 = 3 阶段 × 3 长势。为避免"切换时卡一下"：
//      · 切换阶段：如果该阶段已经按当前长势建好 → 只切 SetActive；否则建一次
//      · 切换长势：只把**当前显示的那个阶段**标为待重建，其余留到要用时再建
//    每个阶段首次进入会重建一次（百级 Instantiate，几十毫秒），之后就只是开关显示。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>生成并管理农田的土壤与玉米。</summary>
    [DisallowMultipleComponent]
    public class FarmFieldBuilder : MonoBehaviour
    {
        /// <summary>玉米的生长阶段（用于选模型），与 UI 的 1~5 阶段对应。</summary>
        public enum CornStage
        {
            /// <summary>S1 播种苗期：只见土地，不显示植株。</summary>
            None = 0,
            /// <summary>S2 营养旺长期。</summary>
            Seedling = 1,
            /// <summary>S3 生殖关键期。</summary>
            Growing = 2,
            /// <summary>S4/S5 灌浆成熟与收获期。</summary>
            Mature = 3,
        }

        [Header("配置")]
        [SerializeField] private FarmVisualConfig config = new FarmVisualConfig();

        [Header("预制体")]
        [Tooltip("土壤块预制体（玉米田土壤_四垄）。把 Assets 里的 glb 拖进来即可。")]
        [SerializeField] private GameObject soilPiecePrefab;

        [Tooltip("玉米预制体，顺序固定为：幼苗好/正常/差、成长好/正常/差、成熟好/正常/差（共 9 个）。" +
                 "留空的格子会用第一个非空的顶替。")]
        [SerializeField] private GameObject[] cornPrefabs = new GameObject[9];

        [Header("运行时状态（只读）")]
        [SerializeField] private int soilPieceCount;
        [SerializeField] private int plantCount;
        [SerializeField] private int visiblePlantCount;
        [SerializeField] private CornStage currentStage = CornStage.Mature;
        [SerializeField] private GrowthLevel currentGrowth = GrowthLevel.Normal;

        /// <summary>每个阶段已生成的玉米实例，下标 1~3 有效。</summary>
        private readonly List<Transform>[] plantsByStage = new List<Transform>[4];

        /// <summary>每个阶段的容器物体，下标 1~3 有效。</summary>
        private readonly Transform[] stageRoots = new Transform[4];

        /// <summary>每个阶段当前是否"已按 builtGrowth 建好"。false 表示需要重建。</summary>
        private readonly bool[] stageReady = new bool[4];

        private Transform soilRoot;
        private Transform plantRoot;
        private bool built;
        [SerializeField, HideInInspector] private bool savedSceneLayout;
        public bool UsesSavedSceneLayout { get { return savedSceneLayout; } }

        // 保存的层级是布局来源；运行时只恢复索引，不再重铺土壤。
        public void UseSavedSceneLayout()
        {
            savedSceneLayout = true;
            soilRoot = transform.Find("Soil");
            plantRoot = transform.Find("Plants");
            plantCount = 0;
            for (var s = 1; s <= 3; s++)
            {
                plantsByStage[s] = new List<Transform>();
                stageRoots[s] = plantRoot != null ? plantRoot.Find("Stage_" + s) : null;
                if (stageRoots[s] != null)
                    foreach (Transform child in stageRoots[s]) plantsByStage[s].Add(child);
                stageReady[s] = stageRoots[s] != null && stageRoots[s].childCount > 0;
                plantCount += plantsByStage[s].Count;
            }
            builtGrowth = currentGrowth;
            built = true;
            var size = config.FieldSize;
            FieldBounds = new Bounds(transform.position + Vector3.up * (config.SoilRidgeTopY * .5f),
                new Vector3(size.x, config.SoilRidgeTopY + 3f, size.y));
        }


        /// <summary>当前已生成的玉米是按哪个长势建的。</summary>
        private GrowthLevel builtGrowth = GrowthLevel.Normal;

        public FarmVisualConfig Config { get { return config; } }
        public int SoilPieceCount { get { return soilPieceCount; } }

        /// <summary>已经实例化出来的玉米总数（含未显示的那些阶段）。</summary>
        public int PlantCount { get { return plantCount; } }

        /// <summary>
        /// 当前**实际显示**的玉米株数。
        /// S1（播种苗期）按设计不显示植株，所以这时是 0 —— 那是正常的，不是 bug。
        /// </summary>
        public int VisiblePlantCount { get { return visiblePlantCount; } }

        public CornStage CurrentStage { get { return currentStage; } }
        public GrowthLevel CurrentGrowth { get { return currentGrowth; } }
        public bool IsBuilt { get { return built; } }

        /// <summary>整片田的世界包围盒（供相机自动取景用）。</summary>
        public Bounds FieldBounds { get; private set; }

        private void Awake()
        {
            // 已保存的场景布局直接恢复索引；旧场景仍兼容运行时生成。
            if (Application.isPlaying)
            {
                if (savedSceneLayout) UseSavedSceneLayout(); else Build();
            }
            lastConfigSignature = ConfigSignature();
        }

        [Header("日志")]
        [Tooltip("是否打印生成/合并日志。建议排查时开、平时关。")]
        [SerializeField] private bool verboseLogs = true;

        private void Update()
        {
            // 在 Inspector 里改 Config 时自动重建。
            //
            // 【注意两个坑】
            //   ① 必须记录"上次已经重建过的签名"，而不是每帧跟 Build() 时的值比 ——
            //      否则 Build() 之后签名对不上就会**每帧重建**，直接卡死。
            //   ② 重建是重活（几百个 Instantiate），所以只在签名真的变了才做。
            if (savedSceneLayout) return;
            var sig = ConfigSignature();
            if (sig == lastConfigSignature) return;

            lastConfigSignature = sig;
            if (verboseLogs)
                Debug.Log("[农场] 检测到 Config 改动，重建田块与玉米…", this);
            Build();
        }

        /// <summary>
        /// 配置指纹：把 config 里"影响生成结果"的字段折成一个数。
        /// 变了就说明需要重建。
        /// </summary>
        private float ConfigSignature()
        {
            if (config == null) return 0f;
            unchecked
            {
                return config.BlockCount * 1.0f
                     + config.BlockColumns * 3.0f
                     + config.BlockRows * 5.0f
                     + config.SoilPiecesPerSide * 7.0f
                     + config.BlockGap * 11.0f
                     + config.FieldMargin * 13.0f
                     + config.PathWidth * 17.0f
                     + config.CornPatchSize.x * 19.0f
                     + config.CornPatchSize.y * 23.0f
                     + config.CornSpacing * 29.0f
                     + config.PlantDensityScale * 31.0f
                     + config.RidgePitch * 37.0f
                     + config.SoilRidgeTopY * 41.0f
                     + config.CornBaseOffsetY * 43.0f
                     + (config.AlignRowsToRidges ? 47.0f : 53.0f)
                     + (config.EdgeToEdgePlanting ? 59.0f : 61.0f)
                     + (config.AutoAlignCornToSoil ? 67.0f : 71.0f)
                     + (config.RandomizePlants ? 73.0f : 79.0f)
                     + (optimizeMeshes ? 83.0f : 89.0f)
                     + (optimizeInstancing ? 97.0f : 101.0f);
            }
        }

        private float lastConfigSignature = float.NaN;

        /// <summary>设置配置（构建器或代码调用）。</summary>
        public void Configure(FarmVisualConfig value)
        {
            if (value != null) config = value;
        }

        /// <summary>设置预制体引用（构建器或代码调用）。</summary>
        public void SetPrefabs(GameObject soil, GameObject[] corns)
        {
            soilPiecePrefab = soil;
            if (corns != null && corns.Length == 9) cornPrefabs = corns;
        }

        // ==================================================================
        //  生成 / 清理
        // ==================================================================

        /// <summary>重建整片田。可以重复调用（会先清掉旧的）。</summary>
        public void Build()
        {
            Build(true);
        }

        /// <summary>编辑预览可禁止重置已手工调整的相机。</summary>
        public void Build(bool frameCamera)
        {
            if (savedSceneLayout) { UseSavedSceneLayout(); return; }
            Clear();
            config.Sanitize();

            soilRoot = CreateChild("Soil");
            plantRoot = CreateChild("Plants");

            // 【关键】每个阶段一个容器。
            // 因为一次合并只能覆盖一个物体树，而"分阶段显隐"是按 SetActive 做的，
            // 如果把三个阶段合并到一起，就无法单独显示了。
            // 所以：每个阶段一个容器，各合并一次 → 每阶段 6 个合并块（按 6 个材质）。
            for (var s = 1; s <= 3; s++)
                stageRoots[s] = CreateChild(plantRoot, "Stage_" + s);

            BuildSoil();
            ApplyGeneratedShadows(soilRoot, false);
            EnsureGroundPad(config.FieldSize);

            // 土壤块是 36 个重复实例。合并是在 GetRenderPrefab 里按**单个预制体**做的
            // （111 网格 → 6 个），这里只需要再开 Instancing 让 36 个实例能合批。
            if (optimizeInstancing && soilRoot != null)
                FarmGpuInstancing.Enable(soilRoot.gameObject);

            built = true;
            builtGrowth = currentGrowth;
            for (var s = 1; s <= 3; s++) stageReady[s] = false;

            // 让当前阶段立刻可见
            ApplyStage(currentStage, currentGrowth);

            var size = config.FieldSize;
            FieldBounds = new Bounds(
                transform.position + Vector3.up * (config.SoilRidgeTopY * 0.5f),
                new Vector3(size.x, config.SoilRidgeTopY + 3f, size.y));

            // 生成完后让相机按真实包围盒重新取景。
            // （场景里不再烘焙几何，所以编辑期算不出 FieldBounds，
            //   相机的初始位置是按 Config 推算的近似值，这里修正成精确值。）
            var rig = FindObjectOfType<FarmCameraRig>();
            if (frameCamera && rig != null && rig.CurrentViewLevel != FarmCameraRig.ViewLevel.CloseUp)
                rig.FrameField(FieldBounds, config.BlockGroupSize.x);

            Debug.Log(string.Format(
                "[农场] 已生成：土壤 {0} 块；玉米已建 {1} 株（三个生长阶段各一组，只显示当前那一组），当前显示 {2} 株。{3}",
                soilPieceCount, plantCount, visiblePlantCount, config), this);
        }

        /// <summary>清掉已生成的内容。</summary>
        public void Clear()
        {
            DestroyChildren(soilRoot);
            DestroyChildren(plantRoot);
            ClearCombinedCache();

            for (var s = 0; s < plantsByStage.Length; s++)
            {
                if (plantsByStage[s] != null) plantsByStage[s].Clear();
                else plantsByStage[s] = new List<Transform>();
                stageReady[s] = false;
            }

            soilPieceCount = 0;
            plantCount = 0;
            built = false;
        }

        private Transform CreateChild(string childName)
        {
            var existing = transform.Find(childName);
            if (existing != null) return existing;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            var existing = parent.Find(childName);
            if (existing != null) return existing;
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void DestroyChildren(Transform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                // 同帧重建不能继续复用待删除的 Stage 容器。
                child.SetActive(false);
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
        }

        // ==================================================================
        //  土壤
        // ==================================================================

        private void BuildSoil()
        {
            if (soilPiecePrefab == null)
            {
                Debug.LogWarning("[农场] 没有指定土壤块预制体，跳过地皮生成。" +
                                 "请把「玉米田土壤_四垄」拖到 FarmFieldBuilder.soilPiecePrefab。", this);
                return;
            }

            var n = Mathf.Max(1, config.SoilPiecesPerSide);
            var piece = FarmVisualConfig.SoilPieceSize;
            var group = config.BlockGroupSize;

            for (var by = 0; by < config.BlockRows; by++)
            {
                for (var bx = 0; bx < config.BlockColumns; bx++)
                {
                    var center = BlockCenterLocal(bx, by);

                    for (var tz = 0; tz < n; tz++)
                    {
                        for (var tx = 0; tx < n; tx++)
                        {
                            var local = new Vector3(
                                center.x - group.x * 0.5f + piece.x * (tx + 0.5f),
                                0f,
                                center.z - group.y * 0.5f + piece.y * (tz + 0.5f));

                            var go = Instantiate(GetRenderPrefab(soilPiecePrefab), soilRoot);
                            go.name = string.Format("Soil_B{0}_{1}_{2}_{3}", bx, by, tx, tz);
                            go.transform.localPosition = local;
                            go.transform.localRotation = Quaternion.identity;
                            go.SetActive(true); // 缓存模板保持 inactive，实际土壤需要显式启用。
                            soilPieces.Add(go.transform);
                            soilPieceCount++;
                        }
                    }
                }
            }
        }

        private readonly List<Transform> soilPieces = new List<Transform>();

        /// <summary>第 (bx,by) 个田块组的中心（本地坐标）。</summary>
        private Vector3 BlockCenterLocal(int bx, int by)
        {
            var g = config.BlockGroupSize;
            var total = config.FieldSize;
            var stepX = g.x + config.BlockGap;
            var stepZ = g.y + config.BlockGap;

            var x0 = -total.x * 0.5f + g.x * 0.5f;
            var z0 = -total.y * 0.5f + g.y * 0.5f;

            return new Vector3(x0 + stepX * bx, 0f, z0 + stepZ * by);
        }

        /// <summary>整片田下面垫一层深色平板，遮住田块之间的缝隙。</summary>
        private void EnsureGroundPad(Vector2 totalSize)
        {
            var pad = transform.Find("GroundPad");
            if (pad == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "GroundPad";
                go.transform.SetParent(transform, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
                pad = go.transform;
            }

            const float margin = 2f;
            pad.localPosition = new Vector3(0f, -0.06f, 0f);
            pad.localScale = new Vector3(totalSize.x + margin, 0.1f, totalSize.y + margin);

            var r = pad.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = GetOrCreatePadMaterial();
        }

        private static Material padMaterial;

        private static Material GetOrCreatePadMaterial()
        {
            if (padMaterial != null) return padMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            padMaterial = new Material(shader);
            padMaterial.name = "FarmGroundPad";
            // 深褐土色，比土壤块略暗，看起来像翻过的地
            if (padMaterial.HasProperty("_BaseColor")) padMaterial.SetColor("_BaseColor", new Color(0.16f, 0.10f, 0.06f));
            if (padMaterial.HasProperty("_Color")) padMaterial.SetColor("_Color", new Color(0.16f, 0.10f, 0.06f));
            return padMaterial;
        }

        // ==================================================================
        //  玉米：按需生成 + 只切显示
        // ==================================================================

        /// <summary>
        /// 切换显示。这是外部唯一需要调的方法。
        /// </summary>
        /// <param name="stage">玉米生长阶段</param>
        /// <param name="growth">长势（决定用"好/正常/差"哪个模型）</param>
        public void ApplyStage(CornStage stage, GrowthLevel growth)
        {
            var previousStage = currentStage;
            var poses = new List<Transform>();
            if (savedSceneLayout)
            {
                var previousRoot = plantRoot != null ? plantRoot.Find("Stage_" + (int)(previousStage == CornStage.None ? CornStage.Mature : previousStage)) : null;
                if (previousRoot != null) foreach (Transform child in previousRoot) poses.Add(child);
            }
            var positions = new List<Vector3>();
            var rotations = new List<Quaternion>();
            var scales = new List<Vector3>();
            foreach (var t in poses) { positions.Add(t.localPosition); rotations.Add(t.localRotation); scales.Add(t.localScale); }
            currentStage = stage;

            if (!built) return;

            // 长势变了：所有已建好的阶段都作废，标记成需要重建
            if (growth != builtGrowth)
            {
                builtGrowth = growth;
                for (var s = 1; s <= 3; s++) stageReady[s] = false;
            }

            currentGrowth = growth;

            // 需要显示某个植株阶段时，确保它已建好
            if (stage != CornStage.None)
            {
                var s = Mathf.Clamp((int)stage, 1, 3);
                if (!stageReady[s]) RebuildStage(stage, growth);
            }

            if (savedSceneLayout && stage != CornStage.None && positions.Count > 0)
            {
                var target = plantsByStage[(int)stage];
                for (var i = 0; i < target.Count && i < positions.Count; i++)
                { target[i].localPosition = positions[i]; target[i].localRotation = rotations[i]; target[i].localScale = scales[i]; }
            }
            ActivateOnly(stage);
        }

        /// <summary>只让某个阶段的玉米可见；CornStage.None 表示全部隐藏。</summary>
        private void ActivateOnly(CornStage stage)
        {
            var wanted = (stage == CornStage.None) ? -1 : Mathf.Clamp((int)stage, 1, 3);
            visiblePlantCount = 0;

            for (var s = 1; s <= 3; s++)
            {
                var list = plantsByStage[s];
                if (list == null) continue;

                var active = (s == wanted) && stageReady[s];
                for (var i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t != null) t.gameObject.SetActive(active);
                }
                if (active) visiblePlantCount += list.Count;
            }
        }

        /// <summary>
        /// 创建一株玉米，并把它**自动对齐到垄面**。
        ///
        /// 【为什么要自动对齐】
        ///   这批模型是从 Blender 导出的，根部未必正好落在 pivot 上
        ///   （实测玉米的包围盒最低点 ≈ y 0.42，而土壤垄面是 0.475）。
        ///   靠人手调 CornBaseOffsetY 既慢又容易错，所以这里直接量：
        ///   把整株的最低点移到 soilY，保证玉米"站"在垄面上。
        ///
        /// 【怎么量的】
        ///   先把实例摆到原点、scale 1，算出所有 Renderer 在**本地空间**的合并包围盒，
        ///   再把包围盒的 minY 消掉。这样和 pivot 在哪无关。
        ///   `CornBaseOffsetY` 作为额外的手动微调（正值上移）。
        /// </summary>
        private GameObject CreateAlignedCorn(GameObject prefab, Vector3 localPosition, Quaternion rotation, float scale, int stage)
        {
            var parent = (stage >= 1 && stage <= 3 && stageRoots[stage] != null) ? stageRoots[stage] : plantRoot;
            var go = Instantiate(GetRenderPrefab(prefab), parent);
            go.name = prefab.name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var lift = 0f;
            if (config.AutoAlignCornToSoil)
            {
                var bounds = ComputeLocalBounds(go);
                if (bounds.HasValue) lift = -bounds.Value.min.y;
            }

            go.transform.localPosition = localPosition + Vector3.up * (lift + config.CornBaseOffsetY);
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one * scale;
            go.SetActive(false);
            return go;
        }

        /// <summary>算出实例内所有 Renderer 在**实例本地空间**的合并包围盒。</summary>
        private static Bounds? ComputeLocalBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return null;

            var toRoot = root.transform.worldToLocalMatrix;
            var has = false;
            var result = new Bounds();

            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                // 取包围盒的 8 个角，变换到实例本地空间后再合并 ——
                // 直接变换 center/size 在旋转下会算错。
                var b = r.bounds;
                var c = b.center;
                var e = b.extents;
                for (var k = 0; k < 8; k++)
                {
                    var corner = new Vector3(
                        c.x + ((k & 1) == 0 ? -e.x : e.x),
                        c.y + ((k & 2) == 0 ? -e.y : e.y),
                        c.z + ((k & 4) == 0 ? -e.z : e.z));
                    // 注意：实例此刻就摆在原点，所以 worldToLocalMatrix 等价于父级逆变换
                    var local = toRoot.MultiplyPoint3x4(corner);
                    if (!has) { result = new Bounds(local, Vector3.zero); has = true; }
                    else result.Encapsulate(local);
                }
            }
            return has ? result : (Bounds?)null;
        }

        /// <summary>按指定长势重建某一个阶段的玉米。</summary>
        private void RebuildStage(CornStage stage, GrowthLevel growth)
        {
            if (stage == CornStage.None) return;
            var s = Mathf.Clamp((int)stage, 1, 3);

            DestroyStageInstances(s);

            var prefabProbe = ResolveCornPrefab(s, growth);
            if (prefabProbe == null)
            {
                Debug.LogWarning("[农场] 找不到阶段 " + s + " 的玉米预制体，跳过。", this);
                return;
            }

            var positions = config.ComputePlantPositions();
            if (positions.Count == 0)
            {
                Debug.LogWarning("[农场] 种植位置算出来是 0 个，检查 CornPatchSize / CornSpacing。", this);
                return;
            }

            for (var by = 0; by < config.BlockRows; by++)
            {
                for (var bx = 0; bx < config.BlockColumns; bx++)
                {
                    var center = BlockCenterLocal(bx, by);

                    for (var i = 0; i < positions.Count; i++)
                    {
                        var local = center + positions[i];

                        var go = CreateAlignedCorn(prefabProbe, local,
                            BuildRotation(bx, by, i, 0), BuildScale(bx, by, i, 0), s);
                        go.name = string.Format("Corn_S{0}_B{1}_{2}_{3}", s, bx, by, i);
                        plantsByStage[s].Add(go.transform);
                        plantCount++;
                    }
                }
            }

            // 这一阶段的玉米就位后开 Instancing。
            // 网格合并已经在 GetRenderPrefab 里按**单个预制体**做过了
            // （74 网格 → 6 个），所以这里不能对整片再合并一次 ——
            // 那会触发 65,535 顶点上限、拆块并复制几何。
            if (stageRoots[s] != null && optimizeInstancing)
                FarmGpuInstancing.Enable(stageRoots[s].gameObject);

            ApplyGeneratedShadows(stageRoots[s], true);
            stageReady[s] = true;
        }

        private void ApplyGeneratedShadows(Transform generated, bool corn)
        {
            var optimizer = GetComponent<FarmOptimizer>();
            if (optimizer != null && generated != null)
                optimizer.ApplyGeneratedShadows(generated.gameObject, corn);
        }

        private void DestroyStageInstances(int s)
        {
            var list = plantsByStage[s];
            if (list == null) return;

            for (var i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null) continue;
                if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject);
            }
            plantCount -= list.Count;
            list.Clear();
            stageReady[s] = false;
        }

        [Header("性能")]
        [Tooltip("是否把每个预制体的零碎网格合并成一个（按材质）。\n" +
                 "★ 默认【开启】，这是性能的关键：\n" +
                 "  一株玉米 74 个网格 → 合并成 6 个（6 个材质）→ 实例化 200 次 = 1,200 个渲染器。\n" +
                 "  不合并的话是 200 × 74 = 14,800 个渲染器，Unity 每帧要逐个处理，必卡。\n" +
                 "  合并粒度是【单个物体】而不是整片 —— 这样不会触发 65,535 顶点上限，" +
                 "  也就不会拆块、不会复制几何。")]
        [SerializeField] private bool optimizeMeshes = true;

        [Tooltip("是否给玉米/土壤的材质打开 GPU Instancing。\n" +
                 "★ 这才是这批模型的正确优化：200 株玉米共用同一份网格，" +
                 "每株只是换个矩阵再画一次，由 GPU 批处理，而且不复制几何。")]
        [SerializeField] private bool optimizeInstancing = true;

        /// <summary>
        /// 已合并过的预制体缓存。
        /// 【为什么要缓存】一株玉米有 74 个网格，如果每株都合并一次，
        /// 200 株就是 200 次合并 —— 启动会卡很久。合并一次、复用 200 次才对。
        /// </summary>
        private readonly Dictionary<int, GameObject> combinedCache = new Dictionary<int, GameObject>();

        /// <summary>取合并版预制体（没有就合并一次并缓存）。</summary>
        private GameObject GetRenderPrefab(GameObject prefab)
        {
            if (prefab == null) return null;
            if (!optimizeMeshes) return prefab;

            var id = prefab.GetInstanceID();
            GameObject cached;
            if (combinedCache.TryGetValue(id, out cached) && cached != null) return cached;

            var combined = MeshCombiner.CombinePrefab(prefab);
            combinedCache[id] = combined;
            return combined;
        }

        /// <summary>清掉合并缓存（重建时应该一起清，否则会泄漏）。</summary>
        private void ClearCombinedCache()
        {
            foreach (var kv in combinedCache)
            {
                if (kv.Value == null) continue;
                // 合并网格由代码 new 出，销毁 GameObject 不会释放这些原生 Mesh。
                foreach (var filter in kv.Value.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.name.StartsWith("Combined_")) continue;
#if UNITY_EDITOR
                    if (UnityEditor.EditorUtility.IsPersistent(mesh)) continue;
#endif
                    if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
                }
                if (Application.isPlaying) Destroy(kv.Value); else DestroyImmediate(kv.Value);
            }
            combinedCache.Clear();
        }

        /// <summary>用确定性伪随机，保证每次生成结果一致（方便截图对比、复现问题）。</summary>
        private Quaternion BuildRotation(int bx, int by, int ix, int iz)
        {
            if (!config.RandomizePlants) return Quaternion.identity;
            var h = Hash01((uint)(bx * 73856093 ^ by * 19349663 ^ ix * 83492791 ^ iz * 2654435));
            var yaw = (h * 2f - 1f) * config.PlantYawJitter;
            return Quaternion.Euler(0f, yaw, 0f);
        }

        private float BuildScale(int bx, int by, int ix, int iz)
        {
            if (!config.RandomizePlants) return 1f;
            var h = Hash01((uint)(bx * 15485863 ^ by * 32452843 ^ ix * 49979687 ^ iz * 8602812));
            return 1f + (h * 2f - 1f) * config.PlantScaleJitter;
        }

        private static float Hash01(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352du;
            x ^= x >> 15; x *= 0x846ca68bu;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// <summary>
        /// 按"生长阶段 + 长势"取玉米预制体。
        /// cornPrefabs 顺序固定：幼苗(好,正常,差)、成长(好,正常,差)、成熟(好,正常,差)。
        /// </summary>
        private GameObject ResolveCornPrefab(int stage, GrowthLevel growth)
        {
            if (cornPrefabs == null || cornPrefabs.Length == 0) return null;

            var stageIndex = Mathf.Clamp(stage - 1, 0, 2);
            var qualityIndex = QualityIndex(growth);
            var index = stageIndex * 3 + qualityIndex;

            if (index >= 0 && index < cornPrefabs.Length && cornPrefabs[index] != null)
                return cornPrefabs[index];

            for (var i = 0; i < cornPrefabs.Length; i++)
                if (cornPrefabs[i] != null) return cornPrefabs[i];
            return null;
        }

        /// <summary>长势 → 0=好 / 1=正常 / 2=差。</summary>
        public static int QualityIndex(GrowthLevel growth)
        {
            switch (growth)
            {
                case GrowthLevel.Good: return 0;
                case GrowthLevel.Bad: return 2;
                default: return 1;
            }
        }

        /// <summary>把 UI 的 1~5 阶段编号转成玉米阶段。</summary>
        public static CornStage StageFromUiStage(int uiStage)
        {
            switch (uiStage)
            {
                case 1: return CornStage.None;
                case 2: return CornStage.Seedling;
                case 3: return CornStage.Growing;
                default: return CornStage.Mature;   // S4 与 S5 共用
            }
        }
    }
}
