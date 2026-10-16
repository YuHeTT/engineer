// ---------------------------------------------------------------------------
//  MathX.cs —— 纯 C# 数学小工具
// ---------------------------------------------------------------------------
//  作用：本程序集（Engineer.Core）通过 asmdef 关闭了 UnityEngine 引用，
//        所以不能用 Mathf。这里提供需要的那几个函数。
//
//  【为什么值得单独一个文件】
//    这些函数是数值内核的基础设施，后面所有随机、裁剪、插值都走这里，
//    保证"不依赖 UnityEngine"这条约束在实现层面是自洽的，而不是靠自觉。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>纯 C# 数学工具。等价于 Mathf 里我们真正用到的那部分。</summary>
    public static class MathX
    {
        /// <summary>拒绝 NaN/无穷值，避免污染后续阶段。</summary>
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>统一输入解析：支持全角数字/小数点，不接受分组逗号或特殊浮点值。</summary>
        public static bool TryParseInput(string text, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Normalize(System.Text.NormalizationForm.FormKC);
            return float.TryParse(normalized, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out value) && IsFinite(value);
        }

        /// <summary>把 v 限制到 [min, max]。</summary>
        public static float Clamp(float v, float min, float max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        /// <summary>把 v 限制到 [0, 1]。</summary>
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        /// <summary>取较大值。</summary>
        public static float Max(float a, float b) => a > b ? a : b;

        /// <summary>取较小值。</summary>
        public static float Min(float a, float b) => a < b ? a : b;

        /// <summary>取较小值（整数重载）。</summary>
        public static int Min(int a, int b) => a < b ? a : b;

        /// <summary>取较大值（整数重载）。</summary>
        public static int Max(int a, int b) => a > b ? a : b;

        /// <summary>线性插值（t=0 返回 a，t=1 返回 b）。</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>反向线性插值：v 在 [a, b] 中的归一化位置。</summary>
        public static float InverseLerp(float a, float b, float v)
        {
            if (a == b) return 0f;
            return (v - a) / (b - a);
        }

        /// <summary>a 到 b 的线性映射到 c 到 d。</summary>
        public static float Remap(float v, float a, float b, float c, float d)
            => Lerp(c, d, InverseLerp(a, b, v));

        /// <summary>是否在 [min, max] 区间内（含端点）。</summary>
        public static bool InRange(float v, float min, float max) => v >= min && v <= max;

        /// <summary>是否在 [min, max) 半开区间内。文档里大量"≥lo 且 &lt;hi"的判定用它。</summary>
        public static bool InRangeHalfOpen(float v, float lo, float hi) => v >= lo && v < hi;

        /// <summary>浮点近似相等。判定边界（0.6 / 0.8）时用，避免浮点误差误判。</summary>
        public static bool ApproxEqual(float a, float b, float eps = 1e-5f)
            => (a > b ? a - b : b - a) <= eps;
    }
}
