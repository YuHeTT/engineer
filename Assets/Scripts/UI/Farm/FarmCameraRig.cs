// ---------------------------------------------------------------------------
//  FarmCameraRig.cs —— 农场相机（俯视 + 第三人称 两档）
// ---------------------------------------------------------------------------
//  作用：用一个组件管两套相机行为，按键切换。
//
//  【为什么做两档】
//    策划文档写"进入第三人称视角农场"，但概念图（image7/8/9/10）画的是
//    **从高处俯视整块田**。两者不是一回事：
//      · 俯视：一眼看到全田、垄沟、机器人轨迹 —— 适合"看数据/看全局"
//      · 第三人称：跟在机器人后面 —— 适合"操控巡航"
//    所以两档都做，默认俯视，用按键切换。这样文档两种理解都满足，成本也低。
//
//  【默认按键】
//    Tab      —— 切换 俯视 / 第三人称
//    WASD     —— 第三人称时移动跟随目标（俯视时平移视角）
//    Q / E    —— 俯视时旋转，第三人称时绕目标旋转
//    滚轮      —— 拉近拉远
//    注意：本项目还没定用旧 Input 还是新 Input System，这里先用旧的 Input.GetKey，
//          将来换成新系统的话只需替换本文件里的输入读取部分。
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Engineer.UI
{
    /// <summary>农场相机机架：俯视 / 第三人称两档。</summary>
    [DisallowMultipleComponent]
    public class FarmCameraRig : MonoBehaviour
    {
        public enum Mode
        {
            /// <summary>俯视整片田（默认）。</summary>
            Overhead = 0,
            /// <summary>第三人称跟随目标。</summary>
            ThirdPerson = 1,
        }

        /// <summary>
        /// 取景档位。亩数一变，田块尺寸就变，靠一档取景是看不好的：
        /// 9 亩时拉到能看全，单株玉米就只剩几个像素；贴近了又看不到全局。
        /// </summary>
        public enum ViewLevel
        {
            /// <summary>全景：看整片田的户型、田埂、小路。展示"我选了多大一片地"。</summary>
            Panorama = 0,
            /// <summary>中景：看一个田块 + 相邻边界。日常操作与看长势（推荐默认）。</summary>
            Medium = 1,
            /// <summary>近景：贴到单株玉米看细节，用来对比长势好坏。</summary>
            CloseUp = 2,
        }

        [Header("模式")]
        [SerializeField] private Mode mode = Mode.Overhead;
        [Tooltip("是否允许按键切换模式。")]
        [SerializeField] private bool allowModeSwitch = true;
        [Tooltip("切换模式的按键。")]
        [SerializeField] private KeyCode switchKey = KeyCode.Tab;

        [Header("取景档位")]
        [SerializeField] private ViewLevel viewLevel = ViewLevel.Medium;

        [Tooltip("允许按键循环切换取景档位（全景 → 中景 → 近景）。")]
        [SerializeField] private bool allowViewLevelSwitch = true;

        [Tooltip("切换取景档位的按键。")]
        [SerializeField] private KeyCode viewLevelKey = KeyCode.C;

        [Tooltip("全景档的额外拉远系数（相对中景）。")]
        [SerializeField] private float panoramaScale = 1.9f;

        [Tooltip("近景档的额外拉近系数（相对中景）。")]
        [SerializeField] private float closeUpScale = 0.32f;

        [Tooltip("中景档大致覆盖田块宽度的比例。1.0 = 正好一个田块的宽度。")]
        [SerializeField] private float mediumCoverage = 1.15f;

        [Header("目标")]
        [Tooltip("第三人称跟随的对象。留空会找 Tag 为 Player 的对象，再找不到就跟随本对象的父级。")]
        [SerializeField] private Transform target;

        [Tooltip("俯视时的观察中心。留空会用 FarmFieldBuilder 的田块中心。")]
        [SerializeField] private Transform fieldCenter;

        [Header("俯视参数")]
        [Tooltip("俯视高度（米）。")]
        [SerializeField] private float overheadHeight = 22f;

        [Tooltip("俯视俯角（度）。\n" +
                 "★ 这个值决定能不能看到天空：\n" +
                 "   俯角 < 垂直视场角/2 时，视锥上边缘才会高过地平线。\n" +
                 "   视场角 55° → 俯角要小于 27.5°，否则画面里【完全没有天空】。\n" +
                 "   20° 大约留 15~20% 的画面上方给天空和远景。")]
        [Range(5f, 90f)][SerializeField] private float overheadPitch = 20f;

        [Tooltip("俯视时的额外拉远距离。")]
        [SerializeField] private float overheadDistance = 14f;

        [Tooltip("按 V 键在「带天空的远景视角」和「纯俯视看全局」之间切换。")]
        [SerializeField] private bool allowVistaToggle = true;

        [Tooltip("纯俯视时的俯角（度）。这个角度看不到天空，但能一眼看全整片田。")]
        [Range(30f, 90f)][SerializeField] private float topDownPitch = 68f;

        /// <summary>当前俯视用的是哪个俯角：远景（带天空）还是纯俯视。</summary>
        private bool topDownMode;

        [Header("第三人称参数")]
        [Tooltip("相机与目标的距离。")]
        [SerializeField] private float thirdPersonDistance = 8f;
        [Tooltip("相机注视点相对目标的高度偏移。")]
        [SerializeField] private float thirdPersonLookHeight = 1.2f;
        [Tooltip("相机高度。")]
        [SerializeField] private float thirdPersonHeight = 3.5f;

        [Header("控制")]
        [SerializeField] private float moveSpeed = 12f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float zoomSpeed = 8f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 60f;

        [Header("平滑")]
        [SerializeField] private float smoothTime = 0.12f;

        private float yaw;
        private float distanceScale = 1f;
        private Vector3 velocity;
        private float currentYaw;

        public Mode CurrentMode { get { return mode; } }

        private void Awake()
        {
            Resolve();
            currentYaw = yaw;
        }

        private void Start()
        {
            // 开局先摆到位，避免第一帧从原点飞过来
            var pos = ComputePosition();
            transform.position = pos;
            transform.rotation = ComputeRotation(pos);
        }

        private void LateUpdate()
        {
            HandleInput();

            var desired = ComputePosition();
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                ComputeRotation(transform.position), 1f - Mathf.Exp(-12f * Time.deltaTime));
        }

        private void Resolve()
        {
            if (target == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (fieldCenter == null)
            {
                var builder = FindObjectOfType<FarmFieldBuilder>();
                if (builder != null) fieldCenter = builder.transform;
            }
        }

        // ------------------------------------------------------------------
        //  输入
        // ------------------------------------------------------------------

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) return;

            if (allowModeSwitch && Input.GetKeyDown(switchKey))
            {
                mode = (mode == Mode.Overhead) ? Mode.ThirdPerson : Mode.Overhead;
                Debug.Log("[农场] 相机切换到 " + (mode == Mode.Overhead ? "俯视" : "第三人称"), this);
            }

            // V：在「带天空的远景」和「看不到天空但能看全整片田的纯俯视」之间切换
            if (allowVistaToggle && Input.GetKeyDown(KeyCode.V))
            {
                topDownMode = !topDownMode;
                Debug.Log("[农场] 俯视角度：" + (topDownMode
                    ? "纯俯视（看全局，无天空）"
                    : "远景（带天空，俯角 " + overheadPitch.ToString("0") + "°）"), this);
            }

            // C：循环切换取景档位（全景 → 中景 → 近景）
            if (allowViewLevelSwitch && Input.GetKeyDown(viewLevelKey))
            {
                var next = ((int)viewLevel + 1) % 3;
                SetViewLevel((ViewLevel)next);
            }

            // 旋转
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.Q)) yaw -= rotateSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E)) yaw += rotateSpeed * Time.deltaTime;

            // 平移（俯视时移动观察中心，第三人称时不该动相机中心，这里简化成平移机架）
            if (mode == Mode.Overhead && (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f))
            {
                var forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                var move = (forward * v + right * h) * moveSpeed * Time.deltaTime;

                if (fieldCenter != null) fieldCenter.position += move;
                else transform.position += move;
            }

            // 缩放
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distanceScale = Mathf.Clamp(distanceScale - scroll * (zoomSpeed * 0.1f), 0.3f, 4f);
            }

            if (Input.GetKey(KeyCode.R)) distanceScale = 1f;

            // 把缩放后的实际距离限制在 [minDistance, maxDistance] 内
            var baseDist = (mode == Mode.Overhead) ? overheadHeight + overheadDistance : thirdPersonDistance;
            if (baseDist * distanceScale < minDistance) distanceScale = minDistance / Mathf.Max(0.01f, baseDist);
            if (baseDist * distanceScale > maxDistance) distanceScale = maxDistance / Mathf.Max(0.01f, baseDist);
            distanceScale = Mathf.Clamp(distanceScale, 0.05f, 10f);
        }

        // ------------------------------------------------------------------
        //  位姿计算
        // ------------------------------------------------------------------

        private Vector3 CenterPoint()
        {
            if (mode == Mode.ThirdPerson && target != null)
                return target.position + Vector3.up * thirdPersonLookHeight;

            if (fieldCenter != null) return fieldCenter.position;
            if (target != null) return target.position;
            return Vector3.zero;
        }

        /// <summary>当前该用的俯视角度。</summary>
        private float CurrentPitch
        {
            get { return topDownMode ? topDownPitch : overheadPitch; }
        }

        /// <summary>当前取景档位。</summary>
        public ViewLevel CurrentViewLevel { get { return viewLevel; } }

        /// <summary>切到指定取景档位，并按档位重算相机高度。</summary>
        public void SetViewLevel(ViewLevel level)
        {
            viewLevel = level;
            ApplyViewLevelDistance();
            Debug.Log("[农场] 取景档位：" + viewLevel, this);
        }

        /// <summary>按当前档位算出相机高度与拉远距离。</summary>
        private void ApplyViewLevelDistance()
        {
            // 基准：田块物理宽度（12 m）。没拿到就用 12。
            var blockWidth = fieldBlockWidth > 0f ? fieldBlockWidth : 12f;

            // 中景要能看全一个田块，所以：
            //   可见宽度 ≈ 2 × 距离 × tan(水平FOV/2)
            //   取 fieldOfView(垂直) → 水平 FOV 按 16:9 折算 ≈ 1.55 × 垂直FOV
            const float aspect = 16f / 9f;
            var vFov = 55f * Mathf.Deg2Rad;
            var hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * aspect);
            var wanted = blockWidth * mediumCoverage;

            var baseDistance = wanted / (2f * Mathf.Tan(hFov * 0.5f));

            float scale;
            switch (viewLevel)
            {
                case ViewLevel.Panorama: scale = panoramaScale; break;
                case ViewLevel.CloseUp: scale = closeUpScale; break;
                default: scale = 1f; break;
            }

            var dist = baseDistance * scale;
            // overheadHeight 与 overheadDistance 共同决定最终距离，
            // 这里把总距离摊到两者上（比例保持 22:14）
            var total = overheadHeight + overheadDistance;
            if (total <= 0.01f) total = 36f;
            overheadHeight = dist * (22f / 36f);
            overheadDistance = dist * (14f / 36f);
        }

        /// <summary>田块物理宽度（米），由 FrameField 传入。</summary>
        private float fieldBlockWidth;

        private Vector3 ComputePosition()
        {
            var center = CenterPoint();

            if (mode == Mode.Overhead)
            {
                var dist = (overheadHeight + overheadDistance * distanceScale) * 0.9f;
                var rot = Quaternion.Euler(CurrentPitch, yaw, 0f);
                return center + rot * Vector3.back * dist + Vector3.up * 2f;
            }

            // 第三人称：在目标后方、上方
            var tpDist = thirdPersonDistance * distanceScale;
            var offset = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0f, -tpDist);
            return center + offset + Vector3.up * thirdPersonHeight;
        }

        private Quaternion ComputeRotation(Vector3 fromPosition)
        {
            var center = CenterPoint();
            // 俯视时稍微向前看一点，让田块不在画面最底边
            var look = (mode == Mode.Overhead) ? center + Vector3.forward * 1.5f : center;
            var dir = look - fromPosition;
            if (dir.sqrMagnitude < 0.0001f) return transform.rotation;
            return Quaternion.LookRotation(dir, Vector3.up);
        }

        /// <summary>
        /// 按田块尺寸自动取景。
        /// <paramref name="blockWidth"/> 是**一个田块**的物理宽度（不是整片田），
        /// 因为取景档位是按"看得清一个田块"来定的。
        /// </summary>
        public void FrameField(Bounds bounds, float blockWidth = 0f)
        {
            fieldBlockWidth = blockWidth > 0f ? blockWidth : 12f;

            var size = Mathf.Max(bounds.size.x, bounds.size.z);
            overheadHeight = Mathf.Clamp(size * 0.9f, 6f, 120f);
            overheadDistance = Mathf.Clamp(size * 0.5f, 2f, 60f);

            if (fieldCenter == null)
            {
                var go = new GameObject("FieldCenter");
                go.transform.SetParent(transform.parent, false);
                fieldCenter = go.transform;
            }
            fieldCenter.position = new Vector3(bounds.center.x, 0f, bounds.center.z);

            // 按当前档位覆盖上面的估算值 —— 档位才是权威
            ApplyViewLevelDistance();
        }
    }
}
