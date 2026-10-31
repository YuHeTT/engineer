using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 把一整屏的图形元素收进一张表，按"全息投影闪烁"的方式逐个改透明度：
    /// 每个元素自己随机明灭、按横向位置错峰，旧屏闪没、新屏闪出来。
    ///
    /// 【为什么改 CanvasGroup.alpha，而不是 Graphic.color】
    ///   改 color 会让每个元素重建网格（TextMeshPro 尤其贵），一屏几十个元素每帧重建会明显掉帧；
    ///   CanvasGroup.alpha 是在渲染期乘上去的，只更新批次透明度，不碰顶点。
    ///   所以第一次转场时会给每个元素补一个 CanvasGroup，之后一直复用。
    ///
    /// 【错峰】按元素的横向位置排先后（delay01），看起来像一条扫描线从左往右扫过去；
    ///   同一时刻各元素的开关系数是独立随机的，不会整屏一起闪。
    ///
    /// 这个类是纯数据 + 纯计算，不挂场景，由 <see cref="ScreenTransitionPlayer"/> 驱动。
    /// </summary>
    internal sealed class HologramFlicker
    {
        private sealed class Item
        {
            public CanvasGroup group;
            public float baseAlpha;
            public float delay01;
            public float seed;

            /// <summary>铺满整屏的底图：不参与闪烁，改成"盖住再换"，否则会露出后面的 3D 场景。</summary>
            public bool backdrop;
        }

        private readonly List<Item> items = new List<Item>();

        /// <summary>这一屏收进来多少个元素（排查"某个元素不闪"时看日志用）。</summary>
        public int Count
        {
            get { return items.Count; }
        }

        /// <summary>把一屏里所有可见 Graphic 收进来，并给它们补上 CanvasGroup。</summary>
        public void Capture(RectTransform root)
        {
            Restore();
            if (root == null) return;

            var graphics = root.GetComponentsInChildren<Graphic>(false);
            if (graphics.Length == 0) return;

            var half = Mathf.Max(1f, root.rect.width) * 0.5f;
            var centerX = root.position.x;
            var hasSize = root.rect.width > 1f && root.rect.height > 1f;
            var screenArea = Mathf.Max(1f, root.rect.width * root.rect.height);
            var seen = new HashSet<CanvasGroup>();

            for (var i = 0; i < graphics.Length; i++)
            {
                var graphic = graphics[i];
                if (graphic == null) continue;

                var group = graphic.GetComponent<CanvasGroup>();
                if (group == null) group = graphic.gameObject.AddComponent<CanvasGroup>();

                // 同一个对象上可能有多个 Graphic（极少见），别把它的透明度写两遍。
                if (!seen.Add(group)) continue;

                var rt = graphic.rectTransform;
                var x = rt.position.x;
                var delay = Mathf.Clamp01(0.5f + (x - centerX) / (half * 2f));
                var area = rt.rect.width * rt.rect.height;

                items.Add(new Item
                {
                    group = group,
                    baseAlpha = group.alpha,
                    delay01 = delay,
                    seed = Hash(x * 0.017f + rt.position.y * 0.031f),
                    backdrop = hasSize && area >= screenArea * 0.9f,
                });
            }
        }

        /// <summary>
        /// 按转场已经过去的时间刷新两屏的透明度。
        /// </summary>
        /// <param name="time">转场开始到现在的秒数。</param>
        /// <param name="incoming">true = 新屏（闪出来），false = 旧屏（闪没）。</param>
        /// <param name="startDelay">这一屏整体什么时候开始。</param>
        /// <param name="duration">单个元素的闪烁时长。</param>
        /// <param name="stagger">错峰总量：最右边的元素比最左边的晚这么久。</param>
        /// <param name="flickerRate">每秒明灭次数。</param>
        /// <param name="minAlpha">明灭时的最暗值。</param>
        /// <param name="total">整段转场的总时长，底图换页按它换算。</param>
        /// <param name="reverse">true = 从右往左扫（返回上一级时用，方向感和前进区分开）。</param>
        public void Apply(float time, bool incoming, float startDelay, float duration, float stagger,
                          float flickerRate, float minAlpha, float total, bool reverse)
        {
            var dur = Mathf.Max(0.0001f, duration);
            var all = Mathf.Max(0.0001f, total);

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.group == null) continue;

                float a;
                if (item.backdrop)
                {
                    // 底图不闪：新屏的底图先压上来，旧屏的底图再慢慢撤。
                    // 新屏有底图时旧的那层早被盖住了，撤掉看不见；新屏没底图时是一段平滑的过渡，
                    // 不会出现"啪一下露出 3D 场景"。
                    a = incoming
                        ? Mathf.Clamp01((time - all * 0.36f) / (all * 0.26f))
                        : 1f - Mathf.Clamp01((time - all * 0.55f) / (all * 0.15f));
                }
                else
                {
                    var delay = reverse ? 1f - item.delay01 : item.delay01;
                    var u = (time - (startDelay + delay * stagger)) / dur;
                    a = incoming
                        ? InAlpha(u, time, item.seed, flickerRate, minAlpha)
                        : OutAlpha(u, time, item.seed, flickerRate, minAlpha);
                }

                item.group.alpha = item.baseAlpha * a;
            }
        }

        /// <summary>还原到捕获时的透明度，并清空列表。</summary>
        public void Restore()
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].group != null) items[i].group.alpha = items[i].baseAlpha;
            }
            items.Clear();
        }

        // ------------------------------------------------------------------
        // 曲线
        // ------------------------------------------------------------------

        /// <summary>闪没：越往后整体越淡，同时随机明灭。</summary>
        private static float OutAlpha(float u, float time, float seed, float rate, float minAlpha)
        {
            if (u <= 0f) return 1f;
            if (u >= 1f) return 0f;

            var gate = Mathf.Lerp(minAlpha, 1f, Gate(time, seed, rate));
            return (1f - u) * gate;
        }

        /// <summary>闪出来：整体渐显，最后 12% 强制稳定，避免停在一个"灭"的瞬间。</summary>
        private static float InAlpha(float u, float time, float seed, float rate, float minAlpha)
        {
            if (u <= 0f) return 0f;
            if (u >= 1f) return 1f;

            var gate = u > 0.88f ? 1f : Mathf.Lerp(minAlpha, 1f, Gate(time, seed, rate));
            return u * gate;
        }

        /// <summary>0 或 1 的开关量：每个元素在同一时刻的相位不同，所以不会整屏同步闪。</summary>
        private static float Gate(float time, float seed, float rate)
        {
            var step = Mathf.Floor(time * rate + seed * 101f);
            return Hash(step) > 0.38f ? 1f : 0f;
        }

        /// <summary>便宜的确定性随机：同样的输入永远同样的输出。</summary>
        private static float Hash(float n)
        {
            var v = Mathf.Sin(n * 12.9898f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }
    }
}
