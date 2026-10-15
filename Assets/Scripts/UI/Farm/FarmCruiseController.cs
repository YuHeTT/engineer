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

        [Tooltip("小车模型的朝向修正（度）。模型正面不是 +Z 时用它转正。")]
        [SerializeField] private float roverYawOffset = 0f;

        [Header("探测")]
        [Tooltip("探测半径（米）。覆盖判定用这个半径画圆。")]
        [SerializeField] private float scanRadius = 3f;

        [Tooltip("走多远算「又探测了一次」（米）。太小会每帧刷覆盖率，太大会漏格。")]
        [SerializeField] private float probeStepDistance = 0.8f;

        [Header("移动")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 120f;

        [Tooltip("自动巡航时，走到路点的容差（米）。")]
        [SerializeField] private float waypointTolerance = 0.8f;

        [Tooltip("自动巡航速度（米/秒）。比手动的慢一点，方便看。" )]
        [SerializeField] private float autoSpeed = 4f;

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
        private float roverYaw;

        public CruiseMode Mode { get { return mode; } }
        public Transform Rover { get { return rover != null ? rover.transform : null; } }
        public float ScanRadius { get { return scanRadius; } }
        public int WaypointCount { get { return waypoints.Count; } }

        private void Awake()
        {
            Resolve();
            BuildRover();
        }

        private void Update()
        {
            if (mode == CruiseMode.Manual) UpdateManual();
            else if (mode == CruiseMode.Auto) UpdateAuto();
        }

        private void Resolve()
        {
            if (grid == null) grid = FindObjectOfType<FarmFieldGrid>();
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
        }

        // ==================================================================
        //  对外接口
        // ==================================================================

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
                roverYaw = 0f;
                rover.transform.rotation = Quaternion.Euler(0f, roverYaw, 0f);
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
            Debug.Log("[农场] 进入手动巡航：WASD / 方向键移动，Q/E 转向。", this);
        }

        /// <summary>开始自动巡航（生成弓字形路点）。</summary>
        public void StartAuto()
        {
            Resolve();
            if (grid == null) return;

            BuildWaypoints();
            waypointIndex = 0;
            mode = waypoints.Count > 0 ? CruiseMode.Auto : CruiseMode.Finished;

            Debug.Log(string.Format("[农场] 进入自动巡航：{0} 个路点，弓字形走遍全田。", waypoints.Count), this);
        }

        /// <summary>停止巡航。</summary>
        public void Stop()
        {
            if (mode == CruiseMode.Manual || mode == CruiseMode.Auto)
                mode = CruiseMode.Finished;
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
                rover = Instantiate(roverPrefab, transform);
                rover.name = "Rover";
            }
            else
            {
                // 没有模型就用胶囊体占位 —— 保证逻辑可跑
                rover = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                rover.name = "Rover(占位胶囊)";
                rover.transform.SetParent(transform, false);
                rover.transform.localScale = new Vector3(0.8f, 0.45f, 1.2f);
                var col = rover.GetComponent<Collider>();
                if (col != null)
                {
                    if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
                }
                Debug.LogWarning("[农场] 没指定小车模型，用胶囊体占位。" +
                                 "把 Art/Models/Rover/reference_rover_lux3d_wheels.fbx 拖到 FarmCruiseController.roverPrefab。", this);
            }

            rover.transform.rotation = Quaternion.Euler(0f, roverYawOffset, 0f);
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

            // Q/E 转向
            var turn = 0f;
            if (Input.GetKey(KeyCode.Q)) turn -= 1f;
            if (Input.GetKey(KeyCode.E)) turn += 1f;
            roverYaw += turn * turnSpeed * Time.deltaTime;

            // WASD / 方向键：把输入转到"相对当前朝向"，走起来才自然
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");
            var dir = new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            if (dir.sqrMagnitude > 0.0001f)
            {
                dir = Quaternion.Euler(0f, roverYaw, 0f) * dir;
                var next = rover.transform.position + dir * moveSpeed * Time.deltaTime;
                rover.transform.position = GroundHeight(ClampToField(next, 1f));

                // 车头转向行进方向
                roverYaw = Mathf.LerpAngle(roverYaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0.25f);
            }

            rover.transform.rotation = Quaternion.Euler(0f, roverYaw + roverYawOffset, 0f);
            TickProbe();
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
                return;
            }

            var dir = flat / dist;
            var next = pos + dir * autoSpeed * Time.deltaTime;
            rover.transform.position = GroundHeight(next);
            roverYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            rover.transform.rotation = Quaternion.Euler(0f, roverYaw + roverYawOffset, 0f);

            TickProbe();
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
