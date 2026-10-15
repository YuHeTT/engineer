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
        [Tooltip("是否自动播放第一个动画片段（开合动作）。")]
        [SerializeField] private bool autoPlayAnimation = true;

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

            if (autoPlayAnimation && clips.Count > 0) PlayClip(0);

            Debug.Log(string.Format(
                "[农场] 房车已就位：位置 {0}，朝向 {1:0}°，动画片段 {2} 个{3}。",
                rv.transform.position.ToString("0.0"),
                rv.transform.eulerAngles.y,
                clips.Count,
                clips.Count > 0 ? "（已播放「" + clips[0].name + "」）" : ""), this);
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
            if (clips.Count == 0) return;
            PlayClip(0);
        }
    }
}
