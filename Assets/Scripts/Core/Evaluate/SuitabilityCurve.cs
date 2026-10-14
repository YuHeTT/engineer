// ---------------------------------------------------------------------------
//  SuitabilityCurve.cs —— 适宜度曲线（全工程唯一实现）
// ---------------------------------------------------------------------------
//  作用：把"某个土壤指标的实际数值"换成"对玉米的适宜程度"，取值 [Floor, Peak]。
//
//  【这是整个数值内核最核心的一个类】
//    文档里给了 5 张适宜度函数表（fPH / fOM / fN / fP / fK），
//    但它们的分段结构完全相同，都是五段：
//
//        适宜度
//          1.0 |          ______________
//              |         /              \
//              |        /                \
//          0.2 |_______/                  \_______
//              +-------|-------|-------|--------→ 指标数值
//                     B0      B1      B2      B3
//              <B0:Floor  [B0,B1)升  [B1,B2]平  (B2,B3]降  >B3:Floor
//
//    所以只需要一个 Evaluate()，五个指标的差别只是 B0..B3 和 Floor 的取值。
//    这样"加一个新指标"或"改一个区间"都只改配置，不动代码。
//
//  【边界语义 —— 实现时必须和文档表格一致】
//    x <  B0        -> Floor                  （严重不足，保底）
//    B0 <= x < B1   -> 线性上升到 Peak        （不足，越高越好）
//    B1 <= x <= B2  -> Peak                   （最优区间，满分）
//    B2 <  x <= B3  -> 线性下降到 Floor        （过量，越高越差）
//    x >  B3        -> Floor                  （严重过量，保底）
//
//    注意左侧是半开区间 [B0, B1)，右侧是半开区间 (B2, B3]，
//    平台是闭区间 [B1, B2]。这与文档表格的写法一致。
//
//  【文档原文示例（pH）】
//    pH < 4.5           -> 0.2                 严重过酸，最低保底值
//    4.5 <= pH < 5.5    -> 0.2+0.8*(pH-4.5)/1   偏酸，pH 越高适宜度越高
//    5.5 <= pH <= 7.0   -> 1.0                 酸碱最优区间，满分
//    7.0 < pH <= 8.5    -> 1.0-0.8*(pH-7.0)/1.5 偏碱，pH 越高适宜度越低
//    pH > 8.5           -> 0.2                 严重过碱，最低保底值
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>
    /// 五段式适宜度曲线。数据驱动，五个土壤指标共用这一个实现。
    /// </summary>
    public sealed class SuitabilityCurve
    {
        /// <summary>左端保底边界（低于它取 Floor）。</summary>
        public float B0;

        /// <summary>上升段结束 / 平台开始。</summary>
        public float B1;

        /// <summary>平台结束 / 下降段开始。</summary>
        public float B2;

        /// <summary>右端保底边界（高于它取 Floor）。</summary>
        public float B3;

        /// <summary>两端保底值。文档中五个指标均为 0.2。</summary>
        public float Floor = 0.2f;

        /// <summary>平台值（最优区间的适宜度）。文档中为 1.0。</summary>
        public float Peak = 1.0f;

        public SuitabilityCurve() { }

        public SuitabilityCurve(float b0, float b1, float b2, float b3,
                                float floor = 0.2f, float peak = 1.0f)
        {
            B0 = b0; B1 = b1; B2 = b2; B3 = b3;
            Floor = floor; Peak = peak;
        }

        /// <summary>
        /// 按文档的分段函数求适宜度。
        /// </summary>
        /// <param name="value">土壤指标的实际数值（如 pH=6.5）</param>
        /// <returns>适宜度，落在 [Floor, Peak] 内</returns>
        public float Evaluate(float value)
        {
            // 低于左端边界：严重不足，保底
            if (value < B0) return Floor;

            // 上升段 [B0, B1)
            if (value < B1)
            {
                float t = MathX.InverseLerp(B0, B1, value);   // 0..1
                return MathX.Lerp(Floor, Peak, t);
            }

            // 平台 [B1, B2]
            if (value <= B2) return Peak;

            // 下降段 (B2, B3]
            if (value <= B3)
            {
                float t = MathX.InverseLerp(B2, B3, value);   // 0..1
                return MathX.Lerp(Peak, Floor, t);
            }

            // 高于右端边界：严重过量，保底
            return Floor;
        }

        /// <summary>
        /// 反推：给定目标适宜度，落在上升段的哪个指标值。
        /// 目前只用于调试与"要做到多少分需要多少指标"的验证，正式流程不用。
        /// </summary>
        public float InverseOnRising(float suitability)
        {
            float t = MathX.InverseLerp(Floor, Peak, suitability);
            return MathX.Lerp(B0, B1, t);
        }

        /// <summary>该曲线的平台（最优）区间，UI 上用来提示玩家"目标区间"。</summary>
        public void GetOptimalRange(out float lo, out float hi)
        {
            lo = B1;
            hi = B2;
        }

        /// <summary>配置的合法性检查。导表时调用，能挡住"边界写反"这类低级错误。</summary>
        public bool Validate(out string error)
        {
            if (!(B0 < B1 && B1 < B2 && B2 < B3))
            {
                error = $"适宜度曲线边界必须严格递增，当前为 {B0}/{B1}/{B2}/{B3}";
                return false;
            }
            if (Peak < Floor)
            {
                error = $"Peak({Peak}) 不能小于 Floor({Floor})";
                return false;
            }
            error = null;
            return true;
        }
    }
}
