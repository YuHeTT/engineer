// ---------------------------------------------------------------------------
//  FarmEquipment.cs —— 停在田边的智能农田房车
// ---------------------------------------------------------------------------
//  作用：把房车模型摆到田块外侧（环田路旁边），并播放它的"独立开合"动画。
//
//  【模型】房车_独立开合动作.glb / 智能农田房车_精细可开合_v2.glb
//          实测 957 个网格、19 个材质、1~3 个动画片段（开合动作是现成的）。
//          包围盒 9.195 × 7.011 × 2.790 m，底部在 y = −3.315，需要上抬。
//
//  【摆在哪】默认贴在田块的 −X 侧、环田路外面，车头朝向田块。
//            这是"房车停在自家田边"的常见构图。
// ---------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>田边的房车：摆放 + 开合动画。</summary>
    [DisallowMultipleComponent]
    public class FarmEquipment : MonoBehaviour
    {
        [Tooltip("房车模型或预制体。")]
        [SerializeField] private GameObject rvPrefab;

        [Tooltip("留空会自动查找 FarmFieldBuilder（用来算「田边」在哪）。")]
        [SerializeField] private FarmFieldBuilder field;

        [Header("摆放")]
        [Tooltip("房车与田块中心的距离系数。1 = 紧贴田块边，越大越远。")]
        [SerializeField] private float distanceScale = 1.25f;

        [Tooltip("房车相对田块的方位角（度）。0 = +Z 方向（田的远端），180 = −Z（近端）。")]
        [SerializeField] private float bearingDegrees = 200f;

        [Tooltip("房车朝向的额外修正（度）。模型正面不是 +Z 时用它转正。")]
        [SerializeField] private float yawOffset = 0f;

        [Tooltip("模型底部不在 y=0 时的高度修正（米）。" +
                 "房车模型实测底部在 y = −3.315，所以默认抬 3.315。")]
        [SerializeField] private float groundOffset = 3.315f;

        [Header("动画")]

        private GameObject rv;
        private readonly List<AnimationClip> clips = new List<AnimationClip>();
        private Animation legacyAnimation;

        public GameObject Rv { get { return rv; } }
        public int ClipCount { get { return clips.Count; } }

        private void Start()
        {
            // 只在运行时生成 —— 否则编辑期生成的房车会被序列化进 Farm.unity，
            // Play 时再生成一遍就成了两台。详见 FarmFieldBuilder.Awake 的说明。
            if (Application.isPlaying) Build();
        }

        /// <summary>生成并摆放房车。</summary>
        public void Build()
        {
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
            if (rv == null && transform.Find("RV") != null) rv = transform.Find("RV").gameObject;
            if (rv != null && field != null && field.UsesSavedSceneLayout)
            {
                CollectClips();
                if (Application.isPlaying) SetDeploymentPose(0f);
                return;
            }
            if (rvPrefab == null)
            {
                Debug.LogWarning("[农场] 没有指定房车模型，跳过。" +
                                 "把 Art/Models/RV/智能农田房车_精细可开合_v2.glb 拖到 FarmEquipment.rvPrefab。", this);
                return;
            }

            if (rv == null)
            {
                rv = Instantiate(rvPrefab, transform);
                rv.name = "RV";
            }

            Place();
            CollectClips();

            if (Application.isPlaying) SetDeploymentPose(0f);

            Debug.Log(string.Format(
                "[农场] 房车已就位：位置 {0}，朝向 {1:0}°，动画片段 {2} 个{3}。",
                rv.transform.position.ToString("0.0"),
                rv.transform.eulerAngles.y,
                clips.Count,
                clips.Count > 0 ? "（已播放「" + clips[0].name + "」）" : ""), this);
        }

        private Transform dockedRobot;
        private Transform dockParent;
        private Vector3 dockPosition;
        private Quaternion dockRotation;
        private Vector3 dockScale;
        [Header("巡航出舱镜头")]
        [SerializeField, Min(.1f)] private float cameraTravelSeconds = 1.2f;
        [SerializeField, Min(.1f)] private float doorOpenSeconds = 1.8f;
        [SerializeField, Range(0f, 1f)] private float openPoseNormalizedTime = .5f;

        public Transform DockedRobot
        {
            get
            {
                if (dockedRobot != null) return dockedRobot;
                if (rv == null) rv = transform.Find("RV") != null ? transform.Find("RV").gameObject : null;
                if (rv == null) return null;
                foreach (var child in rv.GetComponentsInChildren<Transform>(true))
                    if (child.name == "ROVER_ROOT")
                    {
                        dockedRobot = child; dockParent = child.parent;
                        dockPosition = child.localPosition; dockRotation = child.localRotation; dockScale = child.localScale;
                        return child;
                    }
                return null;
            }
        }

        public void DockRobot(Transform carrier)
        {
            StopDeparturePhysics();
            var robot = DockedRobot;
            if (robot == null || dockParent == null) return;
            robot.SetParent(dockParent, false);
            robot.localPosition = dockPosition; robot.localRotation = dockRotation; robot.localScale = dockScale;
            carrier.position = robot.position;
            var facing = Vector3.ProjectOnPlane(dockParent.forward, Vector3.up);
            carrier.rotation = facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            robot.SetParent(carrier, true);
            carrier.gameObject.SetActive(true);
            SetDeploymentPose(0f);
        }

        // 采样完整片段，支撑杆等部件与坡道同步；停在打开姿态而不自动关回。
        public void SetDeploymentPose(float amount)
        {
            if (rv == null) return;
            var animator = rv.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            if (legacyAnimation != null) legacyAnimation.Stop();
            if (clips.Count > 0)
                clips[0].SampleAnimation(legacyAnimation != null ? legacyAnimation.gameObject : animator != null ? animator.gameObject : rv,
                    Mathf.Lerp(.034f, clips[0].length * openPoseNormalizedTime, amount));
        }

        public IEnumerator DeployRobot(Transform carrier, Vector3 fieldStart, FarmCameraRig rig)
        {
            DockRobot(carrier);
            var start = carrier.position;
            var center = rv.transform.position;
            var bounds = new Bounds(center, Vector3.one);
            foreach (var renderer in rv.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
            center = bounds.center;
            // 机器人仓在车身后侧，不能用“离车中心的径向”当出舱方向（会撞后墙）。
            var outward = Vector3.ProjectOnPlane(dockParent.forward, Vector3.up).normalized;
            foreach (var child in rv.GetComponentsInChildren<Transform>(true))
                if (child.name == "CTRL_ROBOT_RAMP")
                {
                    var towardRamp = Vector3.ProjectOnPlane(child.position - start, Vector3.up);
                    if (towardRamp.sqrMagnitude > .01f) outward = towardRamp.normalized;
                    break;
                }
            if (outward.sqrMagnitude < .1f) outward = rv.transform.forward;
            var side = Vector3.Cross(Vector3.up, outward);
            var look = Vector3.Lerp(center, start, .35f) + Vector3.up * .5f;
            var size = Mathf.Max(4f, bounds.size.magnitude * .75f);
            var shot = look + (outward - side * .65f + Vector3.up * .65f).normalized * size;
            var returnPosition = rig != null ? rig.transform.position : Vector3.zero;
            var returnRotation = rig != null ? rig.transform.rotation : Quaternion.identity;
            if (rig != null)
            {
                rig.BeginDeploymentShot();
                yield return MoveCamera(rig, shot, Quaternion.LookRotation(look - shot), cameraTravelSeconds);
            }
            for (float t = 0; t < doorOpenSeconds; t += Time.deltaTime)
            { SetDeploymentPose(Mathf.Clamp01(t / doorOpenSeconds)); yield return null; }
            SetDeploymentPose(1f);
            // 舱门停稳后才建立物理接触面，避免动画舱门把刚体弹飞。
            PrepareDeparturePhysics(carrier, fieldStart);
            var exit = start + outward * 3f;
            DeploymentSucceeded = false;
            yield return DrivePhysicsTo(exit, 12f);
            if (!physicsArrived) { StopDeparturePhysics(); if (rig != null) rig.EndDeploymentShot(); yield break; }
            if (rig != null)
            {
                rig.GetPatrolShot(carrier.position, out returnPosition, out returnRotation);
                yield return MoveCamera(rig, returnPosition, returnRotation, cameraTravelSeconds);
                rig.EndDeploymentShot();
            }
            // 已驶离坡道即可交给巡航，不再先强制驶到田角而长时间锁住操作。
            // 自动模式仍会从当前位置驶向它的真实路点。
            DeploymentSucceeded = true;
            StopDeparturePhysics();
            if (rig != null) rig.EndDeploymentShot();
        }

        private Rigidbody departureBody;
        private BoxCollider departureShape;
        private PhysicMaterial departureFriction;
        private readonly List<Collider> departureSurfaces = new List<Collider>();
        private GameObject supportGround;
        private GameObject approachSurface;
        private Mesh approachMesh;
        private bool physicsArrived;
        public bool DeploymentSucceeded { get; private set; }
        [SerializeField, Min(.1f)] private float departureSpeed = 1.2f;

        private void PrepareDeparturePhysics(Transform carrier, Vector3 destination)
        {
            StopDeparturePhysics();
            var localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            foreach (var filter in carrier.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var point = carrier.InverseTransformPoint(filter.transform.TransformPoint(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                    if (!hasBounds) { localBounds = new Bounds(point, Vector3.zero); hasBounds = true; } else localBounds.Encapsulate(point);
                }
            }
            departureShape = carrier.gameObject.AddComponent<BoxCollider>();
            departureFriction = new PhysicMaterial("DepartureTracks")
            { dynamicFriction = .12f, staticFriction = .15f, frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0f };
            departureShape.sharedMaterial = departureFriction;
            departureShape.center = localBounds.center;
            departureShape.size = hasBounds ? Vector3.Max(Vector3.Scale(localBounds.size, new Vector3(.9f, 1f, .9f)), Vector3.one * .01f) : new Vector3(.6f, .4f, .8f);
            departureBody = carrier.GetComponent<Rigidbody>();
            if (departureBody == null) departureBody = carrier.gameObject.AddComponent<Rigidbody>();
            departureBody.isKinematic = false; departureBody.useGravity = true;
            departureBody.mass = 35f; departureBody.drag = .5f; departureBody.angularDrag = 5f;
            departureBody.interpolation = RigidbodyInterpolation.Interpolate;
            departureBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            departureBody.constraints = RigidbodyConstraints.None;
            departureBody.solverIterations = 12;
            departureBody.solverVelocityIterations = 6;
            // 只为出舱接触面建立碰撞，避免给整辆房车上千个零件都做物理。
            foreach (var filter in rv.GetComponentsInChildren<MeshFilter>())
            {
                var name = filter.name.ToLowerInvariant();
                if (filter.sharedMesh == null || !(name == "robot_ramp_inner_deck" || name == "robot_ramp_outer_door" || name.Contains("garage floor") || name.Contains("garage_floor") || name.Contains("threshold bridge") || name.Contains("robot_floor"))) continue;
                if (filter.GetComponent<Collider>() != null) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh;
                departureSurfaces.Add(collider);
            }
            // 非种植区原模型没有Collider：增加仅用于出舱的地面支撑，不参与渲染。
            supportGround = new GameObject("DepartureGround");
            var ground = supportGround.AddComponent<BoxCollider>();
            var center = (carrier.position + destination) * .5f;
            supportGround.transform.position = new Vector3(center.x, -.15f, center.z);
            ground.size = new Vector3(Mathf.Abs(carrier.position.x - destination.x) + 16f, .3f, Mathf.Abs(carrier.position.z - destination.z) + 16f);
            // 出舱地面仍是原高度；用连续承托坡面接到巡航使用的垄顶高度。
            // 不再让车在低于可见土壤的GroundPad上行驶，然后在交接时突然抬高。
            if (field != null)
            {
                var offset = carrier.position.y - (carrier.TransformPoint(localBounds.center - Vector3.up * localBounds.extents.y)).y;
                var top = destination.y - offset;
                var from = carrier.position + Vector3.ProjectOnPlane(destination - carrier.position, Vector3.up).normalized * 3f;
                from.y = 0f;
                var to = destination; to.y = top;
                var direction = Vector3.ProjectOnPlane(to - from, Vector3.up).normalized;
                var side = Vector3.Cross(Vector3.up, direction) * 3f;
                approachMesh = new Mesh { name = "DepartureApproach" };
                var deckEnd = to + direction * 3f;
                approachMesh.vertices = new[] { from-side, from+side, to-side, to+side, deckEnd-side, deckEnd+side };
                approachMesh.triangles = new[] { 0,2,1, 1,2,3, 2,4,3, 3,4,5 };
                approachMesh.RecalculateBounds(); approachMesh.RecalculateNormals();
                approachSurface = new GameObject("DepartureSoilApproach");
                approachSurface.AddComponent<MeshCollider>().sharedMesh = approachMesh;
            }
            Physics.SyncTransforms();
        }

        private IEnumerator DrivePhysicsTo(Vector3 destination, float timeout)
        {
            physicsArrived = false;
            float stable = 0f;
            var origin = departureBody.position;
            var route = Vector3.ProjectOnPlane(destination - origin, Vector3.up);
            var wait = new WaitForFixedUpdate();
            for (float time = 0; time < timeout; time += Time.fixedDeltaTime)
            {
                if (departureBody == null) yield break;
                var delta = Vector3.ProjectOnPlane(destination - departureBody.position, Vector3.up);
                var passedTarget = route.sqrMagnitude > .01f && Vector3.Dot(Vector3.ProjectOnPlane(departureBody.position-origin, Vector3.up), route.normalized) >= route.magnitude;
                if (delta.magnitude < .3f || passedTarget)
                {
                    departureBody.velocity = new Vector3(0, departureBody.velocity.y, 0);
                    stable += Time.fixedDeltaTime;
                    if (stable >= .2f && Mathf.Abs(departureBody.velocity.y) < .3f) { physicsArrived = true; break; }
                }
                else
                {
                    stable = 0f;
                    // 只施加世界Y轴转向，不把物理决定的坡面俯仰强行拉平。
                    var planarForward = Vector3.ProjectOnPlane(departureBody.rotation * Vector3.forward, Vector3.up);
                    var turn = Vector3.SignedAngle(planarForward, delta.normalized, Vector3.up);
                    departureBody.MoveRotation(Quaternion.AngleAxis(Mathf.Clamp(turn, -100f * Time.fixedDeltaTime,
                        100f * Time.fixedDeltaTime), Vector3.up) * departureBody.rotation);
                    // 只有脚下有接触面才给水平驱动力，空中由重力自然下落。
                    RaycastHit contact;
                    var grounded = Physics.Raycast(departureBody.position + Vector3.up * .1f, Vector3.down, out contact, 1.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    var forward = Vector3.ProjectOnPlane(departureBody.transform.forward, Vector3.up).normalized;
                    if (grounded && Vector3.Dot(forward, delta.normalized) > .85f)
                    {
                        var targetVelocity = forward * Mathf.Min(departureSpeed, Mathf.Max(.5f, delta.magnitude * 2f));
                        var horizontal = Vector3.ProjectOnPlane(departureBody.velocity, Vector3.up);
                        var acceleration = Vector3.ClampMagnitude((targetVelocity - horizontal) * 8f, 6f);
                        acceleration -= Vector3.ProjectOnPlane(Physics.gravity, contact.normal);
                        departureBody.AddForce(acceleration, ForceMode.Acceleration);
                    }
                }
                yield return wait;
            }
            if (!physicsArrived) Debug.LogWarning("[农场] 小车出舱路线受阻，请检查坡道/地面碰撞；未启动巡航，不会传送小车或补齐覆盖率。", this);
        }

        public void StopDeparturePhysics()
        {
            if (departureBody != null)
            {
                departureBody.velocity = Vector3.zero; departureBody.angularVelocity = Vector3.zero;
                departureBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                departureBody.isKinematic = true;
                // 巡航使用Transform，交接后关闭刚体插值，避免渲染帧被旧物理位姿拉回。
                departureBody.interpolation = RigidbodyInterpolation.None;
                departureBody.useGravity = false;
            }
            if (departureShape != null) { departureShape.enabled = false; Destroy(departureShape); }
            foreach (var collider in departureSurfaces) if (collider != null) { collider.enabled = false; Destroy(collider); }
            departureSurfaces.Clear();
            if (departureFriction != null) { Destroy(departureFriction); departureFriction = null; }
            if (supportGround != null) { supportGround.SetActive(false); Destroy(supportGround); }
            if (approachSurface != null) { approachSurface.SetActive(false); Destroy(approachSurface); }
            if (approachMesh != null) { Destroy(approachMesh); approachMesh = null; }
        }

        private static IEnumerator MoveCamera(FarmCameraRig rig, Vector3 end, Quaternion rotation, float duration)
        {
            var start = rig.transform.position; var fromRotation = rig.transform.rotation;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                var blend = Mathf.SmoothStep(0f, 1f, t / duration);
                rig.transform.SetPositionAndRotation(Vector3.Lerp(start, end, blend), Quaternion.Slerp(fromRotation, rotation, blend));
                yield return null;
            }
            rig.transform.SetPositionAndRotation(end, rotation);
        }

        private void Place()
        {
            float halfX = 15f, halfZ = 11f;
            if (field != null && field.Config != null)
            {
                var size = field.Config.FieldSize;
                halfX = size.x * 0.5f;
                halfZ = size.y * 0.5f;
            }

            var rad = bearingDegrees * Mathf.Deg2Rad;
            var rx = Mathf.Cos(rad) * halfX * distanceScale;
            var rz = Mathf.Sin(rad) * halfZ * distanceScale;

            rv.transform.localPosition = new Vector3(rx, groundOffset, rz);

            // 车头朝向田块中心
            var toCenter = new Vector3(-rx, 0f, -rz);
            if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector3.forward;
            var yaw = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg + yawOffset;
            rv.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private void CollectClips()
        {
            clips.Clear();
            if (rv == null) return;

            // glTFast 导入的动画通常挂在 Animator 上，也可能只有 Animation 组件
            var animator = rv.GetComponentInChildren<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                var c = animator.runtimeAnimatorController;
                for (var i = 0; i < c.animationClips.Length; i++)
                    clips.Add(c.animationClips[i]);
            }

            legacyAnimation = rv.GetComponentInChildren<Animation>();
            if (legacyAnimation != null)
            {
                foreach (AnimationState st in legacyAnimation)
                {
                    if (st != null && st.clip != null && !clips.Contains(st.clip))
                        clips.Add(st.clip);
                }
            }
        }

        /// <summary>播放指定片段。房车的"开合"动作就是靠这个触发的。</summary>
        public void PlayClip(int index)
        {
            if (index < 0 || index >= clips.Count) return;

            if (legacyAnimation != null)
            {
                legacyAnimation.Play(clips[index].name);
                return;
            }

            var animator = rv != null ? rv.GetComponentInChildren<Animator>() : null;
            if (animator != null) animator.Play(clips[index].name);
        }

        /// <summary>触发开合动画（默认就是第 0 个片段）。</summary>
        public void ToggleOpen()
        {
            if (rv == null) Build();
            if (clips.Count == 0)
            {
                Debug.LogWarning("[农场] 房车没有可播放的开合片段，请检查动画导入设置。", this);
                return;
            }
            PlayClip(0);
        }
    }
}
