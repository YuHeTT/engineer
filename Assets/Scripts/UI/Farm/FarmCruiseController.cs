// ---------------------------------------------------------------------------
//  FarmCruiseController.cs —— 巡航小车（手动 + 自动）
// ---------------------------------------------------------------------------
//  作用：这是"数字孪生"玩法的核心 —— 小车在田里走，走过的地方被探测到，
//        未探测的地方盖着遮罩，覆盖率实时变化。
//
//  【模型】用用户导入的 reference_rover_lux3d_wheels.fbx（巡视车）。
//          没有指定预制体时会自动建一个胶囊体占位，保证逻辑能跑。
//
//  【两种模式】
//    手动：WASD / 方向键移动，鼠标右键或 QE 转朝向
//    自动：按"弓"字形（S 形来回）路点走遍全田，走到哪探测到哪
//
//  【探测是怎么生效的】
//    每帧检查小车是否移动超过 ProbeStepDistance；
//    一旦超过，就调 FarmFieldGrid.MarkScanned(位置, scanRadius)。
//    **不是靠物理触发器** —— 用距离判定更可控，而且不依赖层的配置。
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>驱动巡航小车，并把探测结果写进 <see cref="FarmFieldGrid"/>。</summary>
    [DisallowMultipleComponent]
    public class FarmCruiseController : MonoBehaviour
    {
        public enum CruiseMode
        {
            /// <summary>没在巡航（未开始）。</summary>
            Idle = 0,
            /// <summary>手动：玩家操作。</summary>
            Manual = 1,
            /// <summary>自动：按弓字形路点自己走。</summary>
            Auto = 2,
            /// <summary>自动跑完了。</summary>
            Finished = 3,
        }

        [Header("模型")]
        [Tooltip("小车模型或预制体。留空会自动建一个胶囊体占位。")]
        [SerializeField] private GameObject roverPrefab;

        [Tooltip("小车模型相对地面的高度偏移（米）。" +
                 "模型 pivot 不在底部时用它把车摆到地面上。")]
        [SerializeField] private float roverGroundOffset = 0f;

        [Tooltip("小车模型的朝向修正（度）。\n" +
                 "★ 不用改代码：Play 时按 **Z 键 + 90°**、**X 键 − 90°** 现场转，\n" +
                 "   看着转对了就把那个数字抄到这里。\n" +
                 "   FBX 头声明 FrontAxis = Z，所以理论上 0 就该对；\n" +
                 "   但如果模型内部有旋转，就需要额外偏移。")]
        [SerializeField] private float roverYawOffset = 0f;

        [Tooltip("调整朝向的按键（+90°）。")]
        [SerializeField] private KeyCode yawPlusKey = KeyCode.Z;

        [Tooltip("调整朝向的按键（−90°）。")]
        [SerializeField] private KeyCode yawMinusKey = KeyCode.X;

        [Tooltip("小车模型的整体缩放。\n" +
                 "★ 实测 reference_rover_lux3d_wheels.fbx 是 9.2×7.0×2.8 m —— " +
                 "在一小块田里是庞然大物，相机会贴进模型内部。\n" +
                 "0.25 大约把它缩到 2.3 m 长，和一株玉米（1.95 m）相称。")]
        [SerializeField] private float roverScale = 0.25f;

        [Tooltip("是否自动清理小车模型自带的影棚环境。\n" +
                 "★ 这个 fbx 里混进了 Blender 的影棚展示场景：\n" +
                 "   Studio floor（灰色大平面）/ Large studio softbox / Front fill /\n" +
                 "   Rear edge light / Three quarter reference view。\n" +
                 "   其中 Studio floor 是几米到几十米的大平面，" +
                 "   摆在田里就是一大块灰板 —— 这就是「田变灰」的直接原因。")]
        [SerializeField] private bool cleanStudioProps = true;

        [Header("探测")]
        [Tooltip("探测半径（米）。覆盖判定用这个半径画圆。")]
        [SerializeField] private float scanRadius = 3f;

        [Tooltip("走多远算「又探测了一次」（米）。太小会每帧刷覆盖率，太大会漏格。")]
        [SerializeField] private float probeStepDistance = 0.8f;

        [Header("移动")]
        [Tooltip("手动巡航速度（米/秒）。")]
        [SerializeField] private float moveSpeed = 8f;

        [Tooltip("转向速度（度/秒）。")]
        [SerializeField] private float turnSpeed = 150f;

        [Tooltip("自动巡航时，走到路点的容差（米）。太小会让小车在终点来回抖动。")]
        [SerializeField] private float waypointTolerance = 1.0f;

        [Tooltip("自动巡航速度（米/秒）。\n" +
                 "★ 实测：全田路程约 136 m，4 m/s 要 34 秒（太慢），15 m/s 约 9 秒。")]
        [SerializeField] private float autoSpeed = 15f;

        [Header("自动巡航加速")]
        [Tooltip("运行时加速倍率。按小键盘 + / - 或 [ / ] 调整。")]
        [Range(0.25f, 8f)][SerializeField] private float autoSpeedMultiplier = 1f;

        [Tooltip("按这个键把自动巡航速度调到最快（一键看完）。")]
        [SerializeField] private KeyCode fastForwardKey = KeyCode.F;

        [Tooltip("按这个键立刻结束自动巡航（把剩下的一次扫完）。")]
        [SerializeField] private KeyCode finishNowKey = KeyCode.G;

        [Header("依赖")]
        [Tooltip("留空会自动在场景里找。")]
        [SerializeField] private FarmFieldGrid grid;

        [Header("运行时状态（只读）")]
        [SerializeField] private CruiseMode mode = CruiseMode.Idle;
        [SerializeField] private int waypointIndex;

        private GameObject rover;
        private FarmFieldBuilder field;
        private readonly List<Vector3> waypoints = new List<Vector3>();
        private Vector3 lastProbePosition;

        /// <summary>
        /// 累积朝向角（不含 <see cref="roverYawOffset"/>）。
        /// 【为什么要单独存】朝向和移动方向必须由**同一个量**导出，
        /// 否则模型内部一转就会分叉（前四轮的问题都出在这里）。
        /// </summary>
        private float roverHeading;

        public CruiseMode Mode { get { return mode; } }
        public Transform Rover { get { return rover != null ? rover.transform : null; } }
        public float ScanRadius { get { return scanRadius; } }
        public int WaypointCount { get { return waypoints.Count; } }

        /// <summary>是否正在巡航（手动或自动）。相机据此决定"跟车还是看全田"。</summary>
        public bool IsCruising
        {
            get { return mode == CruiseMode.Manual || mode == CruiseMode.Auto; }
        }

        private void Awake()
        {
            Resolve();
            BuildRover();
        }

        private void Update()
        {
            if (mode == CruiseMode.Manual) UpdateManual();
            else if (mode == CruiseMode.Auto) UpdateAuto();
            else HandleShortcuts();
        }

        /// <summary>不在巡航时也能用的快捷键。</summary>
        private void HandleShortcuts()
        {
            HandleYawAdjustKeys();
        }

        /// <summary>
        /// 运行时用 Z / X 调整车身朝向（±90°）。
        ///
        /// 【为什么加这个】朝向取决于模型内部的旋转，从文件里读不准 ——
        /// 与其反复改代码重新编译，不如现场转、看着对就抄下来。
        /// </summary>
        private void HandleYawAdjustKeys()
        {
            if (Input.GetKeyDown(yawPlusKey))
            {
                roverYawOffset = Mathf.Repeat(roverYawOffset + 90f, 360f);
                Debug.Log(string.Format("[农场] 车身朝向修正 = {0:0}°（按 X 反方向转 90°）", roverYawOffset), this);
            }
            if (Input.GetKeyDown(yawMinusKey))
            {
                roverYawOffset = Mathf.Repeat(roverYawOffset - 90f, 360f);
                Debug.Log(string.Format("[农场] 车身朝向修正 = {0:0}°（按 Z 反方向转 90°）", roverYawOffset), this);
            }
        }

        /// <summary>打印车身朝向诊断：模型正面朝哪、行进方向朝哪。</summary>
        private void LogFacingDiagnostics()
        {
            if (rover == null)
            {
                Debug.LogWarning("[农场] 朝向诊断：rover 为空（模型没生成？）", this);
                return;
            }

            var fwd = rover.transform.forward;
            var biggest = "X";
            var f = new Vector3(Mathf.Abs(fwd.x), Mathf.Abs(fwd.y), Mathf.Abs(fwd.z));
            if (f.y > f.x && f.y > f.z) biggest = "Y";
            else if (f.z > f.x && f.z > f.y) biggest = "Z";

            // 可视节点（模型本体）的 forward —— 这才是"车头看起来朝哪"
            var visual = rover.transform.childCount > 0 ? rover.transform.GetChild(0) : null;
            var visualFwd = visual != null ? visual.forward : fwd;

            Debug.Log(string.Format(
                "[农场] 车身朝向诊断：\n" +
                "  roverYawOffset   = {0:0}°　（按 Z/X 各转 90°）\n" +
                "  rover.forward    = ({1:0.##}, {2:0.##}, {3:0.##})　主轴 {4}\n" +
                "  可视模型 forward  = ({5:0.##}, {6:0.##}, {7:0.##})\n" +
                "  ★ 移动方向 = rover.forward（现在两者是同一个量，不会错开）\n" +
                "  ★ 若「可视模型 forward」与 rover.forward 不一致，就调 roverYawOffset 直到重合。\n" +
                "  子物体数 = {8}{9}",
                roverYawOffset,
                fwd.x, fwd.y, fwd.z, biggest,
                visualFwd.x, visualFwd.y, visualFwd.z,
                rover.transform.childCount,
                visual == null ? "（★ 没有子物体 —— 偏移无处可施加！）" : "，可视节点 " + visual.name),
                this);
        }

        /// <summary>自动巡航里也能用的加速/结束快捷键。</summary>
        private void HandleAutoShortcuts()
        {
            HandleYawAdjustKeys();

            if (Input.GetKeyDown(KeyCode.RightBracket) || Input.GetKeyDown(KeyCode.Equals))
            {
                autoSpeedMultiplier = Mathf.Clamp(autoSpeedMultiplier * 1.5f, 0.25f, 8f);
                Debug.Log("[农场] 自动巡航倍率 ×" + autoSpeedMultiplier.ToString("0.##"), this);
            }
            if (Input.GetKeyDown(KeyCode.LeftBracket) || Input.GetKeyDown(KeyCode.Minus))
            {
                autoSpeedMultiplier = Mathf.Clamp(autoSpeedMultiplier / 1.5f, 0.25f, 8f);
                Debug.Log("[农场] 自动巡航倍率 ×" + autoSpeedMultiplier.ToString("0.##"), this);
            }
            if (Input.GetKeyDown(fastForwardKey))
            {
                autoSpeedMultiplier = 8f;
                Debug.Log("[农场] 自动巡航已调到最快 ×8。", this);
            }
            if (Input.GetKeyDown(finishNowKey))
            {
                FinishNow();
            }
        }

        /// <summary>立刻结束自动巡航：把剩下的区域一次扫完。</summary>
        public void FinishNow()
        {
            if (grid == null) return;
            grid.MarkAllScanned();
            grid.RefreshMask();
            waypointIndex = waypoints.Count;
            mode = CruiseMode.Finished;
            Debug.Log(string.Format("[农场] 自动巡航已手动结束，覆盖率补齐到 {0:0.0}%。",
                grid.Coverage * 100f), this);
        }

        private void Resolve()
        {
            if (grid == null) grid = FindObjectOfType<FarmFieldGrid>();
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
        }

        // ==================================================================
        //  对外接口
        // ==================================================================

        /// <summary>
        /// 把小车夹回田块范围内，**但不改变巡航模式、也不清覆盖率**。
        ///
        /// 【为什么需要单独一个方法】田块尺寸会随玩家选的亩数变化，
        /// 变小时小车可能站在田外。这时只需要"挪回田里"，
        /// 不能调 <see cref="ResetToStart"/> —— 那个会把 Mode 打回 Idle，
        /// 导致"点了巡航却看不到车在动"（踩过这个坑）。
        /// </summary>
        public void ClampInsideField()
        {
            if (rover == null) return;

            var margin = Mathf.Max(1f, scanRadius * 0.4f);
            var p = rover.transform.position;
            var clamped = ClampToField(p, margin);

            if ((clamped - p).sqrMagnitude < 0.0001f) return;
            rover.transform.position = GroundHeight(clamped);
            lastProbePosition = rover.transform.position;
        }

        /// <summary>摆放小车到田块一角，并清空探测状态。</summary>
        public void ResetToStart()
        {
            Resolve();
            if (grid == null) return;

            BuildRover();
            grid.ClearCoverage();
            grid.RefreshMask();

            var b = grid.FieldBounds;
            var start = new Vector3(b.min.x + 1.5f, 0f, b.min.z + 1.5f);
            if (rover != null)
            {
                rover.transform.position = GroundHeight(start);
                rover.transform.rotation = Quaternion.identity;
                ApplyVisualYawOffset();
            }

            lastProbePosition = start;
            mode = CruiseMode.Idle;
            waypointIndex = 0;
        }

        /// <summary>开始手动巡航。</summary>
        public void StartManual()
        {
            Resolve();
            if (grid == null) return;
            mode = CruiseMode.Manual;
            grid.BeginCruise();
            LogFacingDiagnostics();
            Debug.Log("[农场] 进入手动巡航：Q/E 转向，W/S 前进后退（也能用方向键）。\n" +
                      "  朝向不对就按 Z / X 每次转 90°，转对了把 Console 里的数值抄到 roverYawOffset。\n" +
                      "  相机自动切到跟车模式，键盘输入全给小车上。", this);
        }

        /// <summary>开始自动巡航（生成弓字形路点）。</summary>
        public void StartAuto()
        {
            Resolve();
            if (grid == null) return;

            BuildWaypoints();
            waypointIndex = 0;
            mode = waypoints.Count > 0 ? CruiseMode.Auto : CruiseMode.Finished;
            grid.BeginCruise();
            LogFacingDiagnostics();

            Debug.Log(string.Format("[农场] 进入自动巡航：{0} 个路点，预计 {1:0} 秒走完。\n" +
                                    "  按 ] / [ 调速，F 拉到最快，G 立刻结束。",
                waypoints.Count, EstimateRemainingSeconds()), this);
        }

        /// <summary>停止巡航。</summary>
        public void Stop()
        {
            if (mode == CruiseMode.Manual || mode == CruiseMode.Auto)
            {
                mode = CruiseMode.Finished;
                if (grid != null) grid.EndCruise();
            }
        }

        /// <summary>一键把探测标记全部补齐（"跳过巡航"调试用）。</summary>
        public void RevealEverything()
        {
            Resolve();
            if (grid == null) return;
            grid.MarkAllScanned();
            grid.RefreshMask();
            mode = CruiseMode.Finished;
        }

        // ==================================================================
        //  小车
        // ==================================================================

        private void BuildRover()
        {
            if (rover != null) return;

            if (roverPrefab != null)
            {
                // ★★ 两层结构：载体 + 视觉模型 ★★
                //
                // 【为什么必须分两层】你的两次观察证明了单层无解：
                //     offset = 0  → 车头朝 X，但 W 往 Z 走（车头左侧）
                //     offset = 90 → 车头朝 Z ✅，但 W 往 -X 走（车头右侧）
                //   因为单层时"移动方向 = transform.forward"，
                //   而 offset 同时改 rotation —— 两者永远差 90°，翻 offset 只会一起翻。
                //
                //   分成两层后：
                //     rover（载体）      : rotation 决定移动，forward 就是行进方向
                //       └── visual      : 纯视觉，额外转 roverYawOffset
                //   这样 offset 只影响"看起来朝哪"，**不可能再影响行进方向**。
                rover = new GameObject("Rover");
                rover.transform.SetParent(transform, false);

                var visual = Instantiate(roverPrefab, rover.transform);
                visual.name = "Visual";

                // ★ 先清理影棚环境，**再**缩放。
                //   清理时要量"本体尺寸"判断哪些是影棚大平面，
                //   先缩放虽然等比例，但阈值更容易误判。
                if (cleanStudioProps)
                {
                    var r = RoverModelCleaner.Clean(visual);
                    Debug.Log("[农场] 小车模型清理：" + r, this);
                }

                // 统一缩放：原始模型太大（9.2 m 长），相机会贴进去
                visual.transform.localScale = Vector3.one * Mathf.Max(0.01f, roverScale);
                visual.transform.localRotation = Quaternion.Euler(0f, roverYawOffset, 0f);

                Debug.Log(string.Format(
                    "[农场] 小车已装配：载体 Rover（负责移动，forward=行进方向）" +
                    "+ 视觉子物体 Visual（额外转 {0:0}°，只影响外观）。",
                    roverYawOffset), this);
            }
            else
            {
                // 没有模型就用胶囊体占位 —— 保证逻辑可跑。
                // 注意也要保持"载体 + 视觉"两层结构，否则朝向逻辑不一致。
                rover = new GameObject("Rover");
                rover.transform.SetParent(transform, false);

                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = "Visual";
                capsule.transform.SetParent(rover.transform, false);
                capsule.transform.localScale = new Vector3(0.8f, 0.45f, 1.2f);

                var col = capsule.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
                Debug.LogWarning("[农场] 没指定小车模型，用胶囊体占位。" +
                                 "把 Art/Models/Rover/reference_rover_lux3d_wheels.fbx 拖到 FarmCruiseController.roverPrefab。", this);
            }

            // rover 本体只负责"朝向与移动"，可视偏移交给 ApplyVisualYawOffset
            rover.transform.rotation = Quaternion.identity;
            ApplyVisualYawOffset();
        }

        /// <summary>把小车放到地面上（土壤垄面高度）。</summary>
        private Vector3 GroundHeight(Vector3 p)
        {
            // 注意：field 在 Resolve 里缓存，**不要**在这里调 FindObjectOfType
            // —— 这个方法每帧被调，那样会非常慢。
            var y = 0.5f;
            if (field != null && field.Config != null) y = field.Config.SoilRidgeTopY;
            return new Vector3(p.x, y + roverGroundOffset, p.z);
        }

        // ==================================================================
        //  手动
        // ==================================================================

        private void UpdateManual()
        {
            if (rover == null) return;

            // ---- 转向：Q/E 或 左/右方向键 ----
            var turn = 0f;
            if (Input.GetKey(KeyCode.Q)) turn -= 1f;
            if (Input.GetKey(KeyCode.E)) turn += 1f;
            if (Input.GetKey(KeyCode.LeftArrow)) turn -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) turn += 1f;

            // 累积朝向角（**包含** roverYawOffset，这样偏移一定生效）
            roverHeading += turn * turnSpeed * Time.deltaTime;

            // ★★ 关键：**先**设朝向，**再**用 transform.forward 当移动方向。
            //
            //   这样"移动方向"和"朝向"是同一帧算出来的同一个量，
            //   从结构上不可能错开 —— 前四轮都在错，是因为"朝向"和"移动方向"
            //   分别独立计算，一旦模型内部有 90° 旋转就会分叉。
            //
            //   而且 roverYawOffset 直接进朝向角，
            //   **不存在"模型没有子节点就静默失效"的问题**（上一版就是这么失效的）。
            rover.transform.rotation = Quaternion.Euler(0f, roverHeading, 0f);

            var forwardInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) forwardInput += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forwardInput -= 1f;

            if (Mathf.Abs(forwardInput) > 0.001f)
            {
                var dir = rover.transform.forward * Mathf.Sign(forwardInput);
                var speed = moveSpeed * Mathf.Abs(forwardInput);
                var next = rover.transform.position + dir * speed * Time.deltaTime;
                rover.transform.position = GroundHeight(ClampToField(next, 1f));
            }

            TickProbe();
        }

        /// <summary>
        /// 把 <see cref="roverYawOffset"/> 作为"可视修正"应用。
        ///
        /// 【为什么这样做】偏移量是"模型内部转了 90°"造成的，
        /// 而不是"车头方向算错了"。所以正确的处理是：
        ///   · rover 本体（决定 forward 与移动）保持语义正确
        ///   · 模型的可视部分额外转一个角度
        /// 这样移动永远朝着 forward，不会出现"车头朝 X、却往 Z 走"。
        /// </summary>
        private void ApplyVisualYawOffset()
        {
            if (rover == null) return;
            var visual = rover.transform.Find("Visual");
            if (visual == null) return;

            var want = Quaternion.Euler(0f, roverYawOffset, 0f);
            if (visual.localRotation != want) visual.localRotation = want;
        }



        private Vector3 ClampToField(Vector3 p, float margin)
        {
            if (grid == null) return p;
            var b = grid.FieldBounds;
            return new Vector3(
                Mathf.Clamp(p.x, b.min.x + margin, b.max.x - margin),
                p.y,
                Mathf.Clamp(p.z, b.min.z + margin, b.max.z - margin));
        }

        // ==================================================================
        //  自动：弓字形（S 形）路点
        // ==================================================================

        private void BuildWaypoints()
        {
            waypoints.Clear();
            if (grid == null) return;

            var b = grid.FieldBounds;
            var margin = Mathf.Max(1f, scanRadius * 0.6f);
            var usableX = b.size.x - margin * 2f;
            var usableZ = b.size.z - margin * 2f;

            // 每隔约 1.5 个扫描半径来回一趟，保证横向不留缝
            var laneStep = Mathf.Max(1.5f, scanRadius * 1.5f);
            var lanes = Mathf.Max(2, Mathf.CeilToInt(usableZ / laneStep) + 1);

            for (var i = 0; i < lanes; i++)
            {
                var t = lanes <= 1 ? 0.5f : (float)i / (lanes - 1);
                var z = b.min.z + margin + usableZ * t;
                var leftToRight = (i % 2) == 0;

                var xA = b.min.x + margin;
                var xB = b.min.x + margin + usableX;

                waypoints.Add(new Vector3(leftToRight ? xA : xB, 0f, z));
                waypoints.Add(new Vector3(leftToRight ? xB : xA, 0f, z));
            }
        }

        private void UpdateAuto()
        {
            HandleAutoShortcuts();

            if (rover == null || waypoints.Count == 0)
            {
                mode = CruiseMode.Finished;
                return;
            }

            if (waypointIndex >= waypoints.Count)
            {
                mode = CruiseMode.Finished;
                Debug.Log(string.Format("[农场] 自动巡航完成，覆盖率 {0:0.0}%。",
                    grid != null ? grid.Coverage * 100f : 0f), this);
                return;
            }

            var target = waypoints[waypointIndex];
            var pos = rover.transform.position;

            var flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            var dist = flat.magnitude;

            if (dist <= waypointTolerance)
            {
                waypointIndex++;
                // 每到一个路点报一次进度，方便知道还剩多久
                Debug.Log(string.Format("[农场] 自动巡航 {0}/{1} 路点，覆盖率 {2:0.0}%，预计还需 {3:0} 秒。",
                    waypointIndex, waypoints.Count,
                    grid != null ? grid.Coverage * 100f : 0f, EstimateRemainingSeconds()), this);
                return;
            }

            var dir = flat / dist;

            // ★ 和手动一致：先让 rover 本体**转向**目标路点，再沿自己的 forward 前进。
            //   这样 auto 与 manual 用的是同一套朝向语义，不会一个对一个错。
            var wantRot = Quaternion.LookRotation(dir, Vector3.up);
            rover.transform.rotation = Quaternion.RotateTowards(
                rover.transform.rotation, wantRot, turnSpeed * 1.5f * Time.deltaTime);

            var speed = autoSpeed * autoSpeedMultiplier;
            var next = Vector3.MoveTowards(rover.transform.position,
                                           new Vector3(target.x, rover.transform.position.y, target.z),
                                           speed * Time.deltaTime);
            rover.transform.position = GroundHeight(next);

            ApplyVisualYawOffset();
            TickProbe();
        }

        /// <summary>估算自动巡航还需要多少秒。</summary>
        public float EstimateRemainingSeconds()
        {
            if (rover == null || waypointIndex >= waypoints.Count) return 0f;

            var speed = Mathf.Max(0.1f, autoSpeed * autoSpeedMultiplier);
            var remaining = 0f;
            var from = rover.transform.position;

            for (var i = waypointIndex; i < waypoints.Count; i++)
            {
                remaining += Vector3.Distance(
                    new Vector3(from.x, 0f, from.z),
                    new Vector3(waypoints[i].x, 0f, waypoints[i].z));
                from = waypoints[i];
            }

            return remaining / speed;
        }

        /// <summary>自动巡航进度 0~1（按路点算）。</summary>
        public float AutoProgress
        {
            get
            {
                if (waypoints.Count == 0) return 0f;
                return Mathf.Clamp01((float)waypointIndex / waypoints.Count);
            }
        }

        // ==================================================================
        //  探测
        // ==================================================================

        /// <summary>每移动一定距离就标一次扫描范围。</summary>
        private void TickProbe()
        {
            if (grid == null || rover == null) return;

            var p = rover.transform.position;
            var moved = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(lastProbePosition.x, lastProbePosition.z));
            if (moved < probeStepDistance) return;

            lastProbePosition = p;
            var newly = grid.MarkScanned(p, scanRadius);
            if (newly > 0) grid.RefreshMask();
        }

        /// <summary>扫描范围的可视化（地面上的一个圆圈）。</summary>
        public void DrawScanGizmo()
        {
            if (rover == null) return;
            var c = rover.transform.position;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(new Vector3(c.x, 0.5f, c.z), scanRadius);
        }

        private void OnDrawGizmosSelected()
        {
            DrawScanGizmo();
        }
    }
}
