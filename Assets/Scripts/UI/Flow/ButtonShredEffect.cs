using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>
    /// 按钮的「横向切片消散」：把按钮复制成 N 条横线，每条随机延迟、随机横向偏移、淡出。
    ///
    /// 【为什么要复制】按钮其实是"底图 Image + 文字 TMP"两个 Graphic 叠出来的，
    /// 单个 Graphic 没法按 y 切条；所以复制整棵按钮，再让每条横线用一个 RectMask2D 裁一刀，
    /// 视觉上就是"整个按钮被切成横线"。两个 Graphic 用的是同一个按钮坐标系，切出来是对齐的。
    ///
    /// 【代价】一次会 Instantiate N 个按钮副本（约 3~6 ms），所以条数有上限、播完立刻销毁。
    /// 只在点击那一瞬间用，不常驻。
    /// </summary>
    internal sealed class ButtonShredEffect
    {
        private sealed class Strip
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Vector2 basePos;
            public float offsetX;
            public float delay01;
            public float life01;
        }

        private readonly List<Strip> strips = new List<Strip>();
        private RectTransform layer;

        public int Count
        {
            get { return strips.Count; }
        }

        /// <summary>按按钮的屏幕矩形切条并复制。调用前按钮必须还在原位、可见。</summary>
        public void Begin(RectTransform button, float stripHeight, float spread, int maxStrips)
        {
            End();
            if (button == null) return;

            var canvas = button.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var canvasRt = (RectTransform)canvas.transform;

            // 全屏的承载层，放在画布最上层，播完整棵销毁。
            layer = new GameObject("ButtonShred", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(canvasRt, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;
            layer.SetAsLastSibling();

            var corners = new Vector3[4];
            button.GetWorldCorners(corners);

            // 转成承载层的局部坐标（原点 = 画布中心），后面直接当 anchoredPosition 用。
            var bl = layer.InverseTransformPoint(corners[0]);
            var tr = layer.InverseTransformPoint(corners[2]);
            var center = (corners[0] + corners[2]) * 0.5f;

            var width = Mathf.Abs(tr.x - bl.x);
            var height = Mathf.Abs(tr.y - bl.y);
            if (width < 1f || height < 1f)
            {
                End();
                return;
            }

            var cap = Mathf.Max(4, maxStrips);
            var count = Mathf.Clamp(Mathf.CeilToInt(height / Mathf.Max(1f, stripHeight)), 4, cap);
            var stripH = height / count;
            var centerX = (bl.x + tr.x) * 0.5f;
            var bottom = Mathf.Min(bl.y, tr.y);

            for (var i = 0; i < count; i++)
            {
                var stripGo = new GameObject("Shred_" + i,
                    typeof(RectTransform), typeof(RectMask2D), typeof(CanvasGroup));

                var rt = (RectTransform)stripGo.transform;
                rt.SetParent(layer, false);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, stripH);
                rt.anchoredPosition = new Vector2(centerX, bottom + (i + 0.5f) * stripH);

                // 复制整棵按钮，对齐到原位；RectMask2D 只留下这一条。
                var clone = Object.Instantiate(button.gameObject, rt, false);
                var cloneRt = (RectTransform)clone.transform;
                cloneRt.anchorMin = new Vector2(0.5f, 0.5f);
                cloneRt.anchorMax = new Vector2(0.5f, 0.5f);
                cloneRt.pivot = new Vector2(0.5f, 0.5f);
                cloneRt.sizeDelta = new Vector2(button.rect.width, button.rect.height);
                cloneRt.position = center;
                cloneRt.localScale = Vector3.one;

                foreach (var graphic in clone.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }

                // 【不要再 Destroy 克隆体上的 Button / OptionCardButton】
                // OptionCardButton 用 [RequireComponent(typeof(Button))] 锁住 Button，
                // 这里逐个 Destroy 会触发 Unity 的依赖保护，每条切片刷一次
                // "Can't remove Button..."，严重时把主线程拖死。
                // 切片只是视觉副本：关掉射线检测 + 关掉 CanvasGroup 交互就够，
                // 克隆体会在动画结束后随 layer 一起销毁。

                strips.Add(new Strip
                {
                    rect = rt,
                    group = stripGo.GetComponent<CanvasGroup>(),
                    basePos = rt.anchoredPosition,
                    offsetX = Random.Range(-spread, spread),
                    delay01 = Random.Range(0f, 0.45f),
                    life01 = Random.Range(0.45f, 0.75f),
                });

                var stripGroup = stripGo.GetComponent<CanvasGroup>();
                if (stripGroup != null)
                {
                    stripGroup.interactable = false;
                    stripGroup.blocksRaycasts = false;
                }
            }
        }

        /// <summary>按已经过去的时间刷新每条横线：横向漂移 + 淡出。</summary>
        public void Tick(float time, float total)
        {
            var all = Mathf.Max(0.01f, total);

            for (var i = 0; i < strips.Count; i++)
            {
                var strip = strips[i];
                if (strip.rect == null) continue;

                var start = strip.delay01 * all * 0.55f;
                var life = Mathf.Max(0.04f, strip.life01 * all);
                var k = Mathf.Clamp01((time - start) / life);

                // 先快后慢地甩出去，同时淡掉。
                var ease = 1f - (1f - k) * (1f - k);

                strip.rect.anchoredPosition = strip.basePos + new Vector2(strip.offsetX * ease, 0f);
                if (strip.group != null) strip.group.alpha = 1f - k;
            }
        }

        /// <summary>销毁所有横线。可重复调用。</summary>
        public void End()
        {
            strips.Clear();

            if (layer != null)
            {
                Object.Destroy(layer.gameObject);
                layer = null;
            }
        }
    }
}
