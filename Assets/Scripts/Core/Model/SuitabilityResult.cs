// ---------------------------------------------------------------------------
//  SuitabilityResult.cs —— 五个适宜度的计算结果
// ---------------------------------------------------------------------------
//  作用：把 SoilState 的 5 个动态指标，过一遍 SuitabilityCurve，得到 5 个适宜度。
//
//  这是"土壤数值"到"评分"的转换层。后续三处都依赖它：
//      1. 推荐方案表  —— 按适宜度决定给什么建议
//      2. 长势判定    —— 按适宜度决定玉米贴图
//      3. 产量计算    —— Ffert,stage = fPH × fOM × fN × fP × fK
//
//  【为什么不直接用 SoilState 算】
//    适宜度只在"阶段开始"时算一次，然后被三处复用。
//    缓存下来避免重复计算，也让上下游解耦（改适宜度函数不影响调用方）。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>某一阶段五个动态指标的适宜度，均在 [Floor, Peak] 内（通常 [0.2, 1.0]）。</summary>
    public sealed class SuitabilityResult
    {
        /// <summary>pH 适宜度 fPH</summary>
        public float fpH;

        /// <summary>腐殖丰度适宜度 fOM</summary>
        public float fOM;

        /// <summary>速效氮适宜度 fN</summary>
        public float fN;

        /// <summary>磷活度适宜度 fP</summary>
        public float fP;

        /// <summary>钾储量适宜度 fK</summary>
        public float fK;

        public SuitabilityResult() { }

        public SuitabilityResult(float ph, float om, float n, float p, float k)
        {
            fpH = ph; fOM = om; fN = n; fP = p; fK = k;
        }

        /// <summary>按下标读写。静态指标没有适宜度，统一返回 1（不参与连乘判定）。</summary>
        public float this[SoilMetric m]
        {
            get
            {
                switch (m)
                {
                    case SoilMetric.pH: return fpH;
                    case SoilMetric.OM: return fOM;
                    case SoilMetric.N:  return fN;
                    case SoilMetric.P:  return fP;
                    case SoilMetric.K:  return fK;
                    default:            return 1f;
                }
            }
            set
            {
                switch (m)
                {
                    case SoilMetric.pH: fpH = value; break;
                    case SoilMetric.OM: fOM = value; break;
                    case SoilMetric.N:  fN = value; break;
                    case SoilMetric.P:  fP = value; break;
                    case SoilMetric.K:  fK = value; break;
                }
            }
        }

        /// <summary>
        /// 阶段肥力系数 Ffert,stage = fPH × fOM × fN × fP × fK（文档 TABLE 14 行 6）。
        /// 五个因子连乘 —— 任一因子崩掉都会拖垮整体（木桶效应）。
        /// </summary>
        public float StageFertility() => fpH * fOM * fN * fP * fK;

        /// <summary>取最小值。推荐方案表里"任意项 &lt; 0.6"这类判定用得到。</summary>
        public float Min()
        {
            float m = fpH;
            if (fOM < m) m = fOM;
            if (fN  < m) m = fN;
            if (fP  < m) m = fP;
            if (fK  < m) m = fK;
            return m;
        }

        /// <summary>是否所有适宜度都满足条件。长势判定用。</summary>
        public bool All(System.Func<float, bool> predicate)
        {
            return predicate(fpH) && predicate(fOM) && predicate(fN)
                && predicate(fP)  && predicate(fK);
        }

        /// <summary>是否至少有一项满足条件。推荐方案表的"任意项"用。</summary>
        public bool Any(System.Func<float, bool> predicate)
        {
            return predicate(fpH) || predicate(fOM) || predicate(fN)
                || predicate(fP)  || predicate(fK);
        }

        /// <summary>满足条件的项数。</summary>
        public int Count(System.Func<float, bool> predicate)
        {
            int c = 0;
            if (predicate(fpH)) c++;
            if (predicate(fOM)) c++;
            if (predicate(fN))  c++;
            if (predicate(fP))  c++;
            if (predicate(fK))  c++;
            return c;
        }

        /// <summary>
        /// 从土壤状态计算全部适宜度。
        /// 这是唯一的入口 —— 别在别处手写分段函数。
        /// </summary>
        public static SuitabilityResult From(SoilState soil, SimConfig config)
        {
            return From(soil, config, null);
        }

        /// <summary>
        /// 指定作物曲线集合的入口。curves 为 null 或某下标为空时回退到 config 的全局曲线。
        /// </summary>
        public static SuitabilityResult From(SoilState soil, SimConfig config, SuitabilityCurve[] curves)
        {
            if (soil == null || config == null) return new SuitabilityResult();

            var r = new SuitabilityResult();
            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                var metric = SoilMetrics.Dynamic[i];
                var curve = (curves != null && i < curves.Length && curves[i] != null)
                    ? curves[i]
                    : config.CurveOf(metric);
                r[metric] = curve != null ? curve.Evaluate(soil[metric]) : 1f;
            }
            return r;
        }

        public override string ToString()
            => $"fPH={fpH:F3} fOM={fOM:F3} fN={fN:F3} fP={fP:F3} fK={fK:F3} " +
               $"(Ffert={StageFertility():F4})";
    }
}
