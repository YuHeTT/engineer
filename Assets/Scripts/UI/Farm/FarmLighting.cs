// ---------------------------------------------------------------------------
//  FarmLighting.cs —— 农场的太阳与天空（集中管理，方便调）
// ---------------------------------------------------------------------------
//  作用：把太阳光、天空盒、环境光、雾集中到一个组件上，参数可在 Inspector 实时调。
//
//  【为什么要单独做一个组件】
//    原来这些是 FarmSceneBuilder 里的硬编码值（太阳 intensity 1.15 等），
//    想调亮度就得改代码重跑菜单。放在组件上之后，Play 时直接拖滑杆即可。
//
//  【关键：亮度要压住，别盖过 UI】
//    农场和 UI 是叠加显示的（UI 浮在农场之上）。如果农场太亮，
//    半透明的 UI 面板就压不住背景，文字会看不清。
//    所以这里默认把太阳压到 0.85、环境光 0.85，并让地面偏暗。
//
//  【和 SceneLoader 的配合】
//    两个场景叠加时**只能有一个 AudioListener**，否则 Unity 每次都会警告
//    "There are 2 audio listeners in the scene"。
//    SceneLoader 会关掉 UI 相机；本组件不负责那个，只负责光照。
// ---------------------------------------------------------------------------

using UnityEngine;

namespace Engineer.UI
{
    /// <summary>农场的太阳 / 天空 / 环境光 / 雾。</summary>
    [DisallowMultipleComponent]
    public class FarmLighting : MonoBehaviour
    {
        [Header("太阳")]
        [Tooltip("太阳强度。太亮会盖过叠加在上面的 UI。\n" +
                 "★ 这是「农场别把 UI 压住」的主要旋钮：UI 是叠加在农场之上的，" +
                 "农场越亮，半透明白盒面板上的文字越看不清。\n" +
                 "0.4 左右画面已经偏暗但还看得清田；想更暗就继续往下调。")]
        [Range(0f, 3f)][SerializeField] private float sunIntensity = 0.4f;

        [SerializeField] private Color sunColor = new Color(1f, 0.96f, 0.88f);

        [Tooltip("太阳角度（俯仰，度）。")]
        [Range(5f, 89f)][SerializeField] private float sunPitch = 48f;

        [Tooltip("太阳方位（度）。")]
        [SerializeField] private float sunYaw = -35f;

        [Header("阴影")]
        [Tooltip("是否投射阴影。\n" +
                 "★ 关掉能省掉一大半 draw call：每个投射阴影的物体" +
                 "都要按阴影级联重画一遍。实测关掉前 283,776 batches。")]
        [SerializeField] private bool castShadows = true;

        [Tooltip("阴影级联数。级联越多，同一物体被重画的次数越多。\n" +
                 "演示用 1 档足够，而且比 4 档快非常多。")]
        [Range(1, 4)][SerializeField] private int shadowCascades = 1;

        [Tooltip("阴影距离（米）。离相机超过这个距离的物体不投影。")]
        [SerializeField] private float shadowDistance = 45f;

        [Tooltip("阴影分辨率。演示用 Medium 足够。")]
        [SerializeField] private UnityEngine.Rendering.LightShadowResolution shadowResolution
            = UnityEngine.Rendering.LightShadowResolution.Medium;

        [Tooltip("太阳阴影强度。")]
        [Range(0f, 1f)][SerializeField] private float shadowStrength = 0.7f;

        [Header("天空")]
        [Tooltip("天空盒材质。留空则不动当前设置。")]
        [SerializeField] private Material skybox;

        [Header("环境光")]
        [Tooltip("环境光强度。这是「整体偏亮」最直接的旋钮。\n" +
                 "降到 0.3 左右农场会明显退到 UI 后面。")]
        [Range(0f, 3f)][SerializeField] private float ambientIntensity = 0.3f;

        [Header("雾")]
        [SerializeField] private bool fogEnabled = true;
        [SerializeField] private Color fogColor = new Color(0.74f, 0.80f, 0.84f);

        [Tooltip("雾开始距离（米）。太近会把天空和远景糊成一片灰白。")]
        [SerializeField] private float fogStart = 160f;
        [SerializeField] private float fogEnd = 520f;

        [Header("地面")]
        [Tooltip("底板颜色。偏暗一点，UI 才压得住。")]
        [SerializeField] private Color groundPadColor = new Color(0.13f, 0.09f, 0.06f);

        [Tooltip("地形垫层颜色（外围地形之间的草/土色）。")]
        [SerializeField] private Color terrainPadColor = new Color(0.26f, 0.27f, 0.18f);

        private Light sun;

        /// <summary>上一次应用的参数指纹，用来检测 Inspector 改动。</summary>
        private float lastFingerprint = float.NaN;

        private void Awake()
        {
            Apply();
            lastFingerprint = Fingerprint();
        }

        private void Update()
        {
            // 在 Inspector 里拖滑杆时自动重新应用。
            // （之前的问题：Apply 只在 Awake 调一次，Play 中改参数没反应，
            //   用户会以为"亮度改了没用"。）
            var f = Fingerprint();
            if (!Mathf.Approximately(f, lastFingerprint))
            {
                lastFingerprint = f;
                Apply();
            }
        }

        private float Fingerprint()
        {
            return sunIntensity * 1.0f
                 + ambientIntensity * 7.0f
                 + sunPitch * 13.0f
                 + sunYaw * 17.0f
                 + shadowDistance * 19.0f
                 + shadowStrength * 23.0f
                 + shadowCascades * 29.0f
                 + (castShadows ? 31.0f : 37.0f)
                 + fogStart * 41.0f
                 + fogEnd * 43.0f
                 + (fogEnabled ? 47.0f : 53.0f);
        }

        /// <summary>把当前参数应用到场景。</summary>
        public void Apply()
        {
            ApplySun();
            ApplySky();
            ApplyAmbientAndFog();
            ApplyGroundColors();
            DynamicGI.UpdateEnvironment();
        }

        private void ApplySun()
        {
            if (sun == null)
            {
                // ★ 只找**本场景**的平行光。
                //   之前用 GetComponentsInChildren + 找不到就新建，逻辑没错，
                //   但如果 Sun 不在 FarmLighting 所在的物体树下就会走到"新建"分支，
                //   结果场景里出现两个平行光。这里改成显式限定场景 + 名字优先。
                sun = FindOwnSun();
            }

            if (sun == null)
            {
                var go = new GameObject("Sun");
                go.transform.SetParent(transform, false);
                sun = go.AddComponent<Light>();
                sun.type = LightType.Directional;
                Debug.LogWarning("[农场] 没找到本场景的平行光，已新建一个 Sun。", this);
            }

            sun.intensity = sunIntensity;
            sun.color = sunColor;
            sun.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            sun.shadowStrength = shadowStrength;
            sun.shadowResolution = shadowResolution;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);

            // 阴影级联与距离属于「质量设置」，不是光源自己的属性
            QualitySettings.shadowCascades = shadowCascades;
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadows = castShadows ? ShadowQuality.HardOnly : ShadowQuality.Disable;
        }

        /// <summary>
        /// 把一批物体设成「不投影阴影、但能接受阴影」。
        /// 用来处理玉米这种"数量巨大、影子又不重要"的物体。
        /// </summary>
        public static int DisableShadowCasting(GameObject root)
        {
            if (root == null) return 0;
            var n = 0;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = true;
                n++;
            }
            return n;
        }

        private void ApplySky()
        {
            if (skybox != null) RenderSettings.skybox = skybox;
        }

        private void ApplyAmbientAndFog()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = ambientIntensity;

            RenderSettings.fog = fogEnabled;
            if (!fogEnabled) return;

            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        /// <summary>把地面颜色压暗。这几块是运行时生成的，所以按名字找。</summary>
        private void ApplyGroundColors()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;

                var n = r.gameObject.name;
                Color? target = null;
                if (n.StartsWith("TerrainPad")) target = terrainPadColor;
                else if (n == "GroundPad") target = groundPadColor;
                if (!target.HasValue) continue;

                var m = r.sharedMaterial;
                if (m == null) continue;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", target.Value);
                if (m.HasProperty("_Color")) m.SetColor("_Color", target.Value);
            }
        }

        /// <summary>
        /// 找本场景（FarmLighting 所在的场景）的平行光。
        /// 优先名字叫 "Sun" 的，其次本场景任意平行光；**不会跨场景找** ——
        /// 否则会拿到 UI 场景自带那个 Directional Light，导致双重照明。
        /// </summary>
        private Light FindOwnSun()
        {
            var myScene = gameObject.scene;
            var all = FindObjectsOfType<Light>();

            Light fallback = null;
            for (var i = 0; i < all.Length; i++)
            {
                var l = all[i];
                if (l == null || l.type != LightType.Directional) continue;
                if (myScene.IsValid() && l.gameObject.scene != myScene) continue;   // 不跨场景

                if (l.name == "Sun") return l;
                if (fallback == null) fallback = l;
            }
            return fallback;
        }
        public void SetBrightness(float mul)
        {
            mul = Mathf.Max(0f, mul);
            sunIntensity = Mathf.Clamp(0.85f * mul, 0f, 3f);
            ambientIntensity = Mathf.Clamp(0.85f * mul, 0f, 3f);
            Apply();
            Debug.Log(string.Format("[农场] 亮度 ×{0:0.##}：太阳 {1:0.##}，环境光 {2:0.##}",
                mul, sunIntensity, ambientIntensity), this);
        }
    }
}
