// ---------------------------------------------------------------------------
//  SoilState.cs —— 9 项土壤指标的数值集合
// ---------------------------------------------------------------------------
//  作用：承载"某一时刻土壤长什么样"。这是全工程流动的核心数据。
//
//  数据流：
//      玩家输入(前5项) + 随机(后4项)
//              ↓
//          SoilState  ──→ SuitabilityResult（适宜度）──→ 推荐方案
//              ↓                    ↓
//          演算下一阶段         长势判定 / 产量
//
//  【为什么用 9 个具名字段而不是 float[9] 或 Dictionary】
//    1. 具名字段在代码里可读：state.pH 比 state[0] 清楚得多
//    2. 能被 JsonUtility / CSV 导入器直接序列化（字段名就是列名）
//    3. 取适宜度时用 indexer 走数组下标，兼顾性能
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>一组土壤指标数值。9 项，前 5 动态后 4 静态。</summary>
    public sealed class SoilState
    {
        // ---- 动态 5 项：每阶段会被水肥演变 ----
        /// <summary>酸衡值 pH</summary>
        public float pH;

        /// <summary>腐殖丰度 OM</summary>
        public float OM;

        /// <summary>速效氮元 N</summary>
        public float N;

        /// <summary>磷活度 P</summary>
        public float P;

        /// <summary>钾储量 K</summary>
        public float K;

        // ---- 静态 4 项：第一阶段随机一次，之后不变 ----
        /// <summary>氧还势 Eh</summary>
        public float Eh;

        /// <summary>土骨离子（土壤盐基离子）</summary>
        public float BaseIon;

        /// <summary>毒砷潜势</summary>
        public float As;

        /// <summary>汞积量</summary>
        public float Hg;

        public SoilState() { }

        /// <summary>拷贝构造。演算时用，避免改到"当前阶段"的原值。</summary>
        public SoilState(SoilState other)
        {
            if (other == null) return;
            pH = other.pH; OM = other.OM; N = other.N; P = other.P; K = other.K;
            Eh = other.Eh; BaseIon = other.BaseIon; As = other.As; Hg = other.Hg;
        }

        /// <summary>按下标读写。方便按 SoilMetric 遍历，避免写 9 个 switch。</summary>
        public float this[SoilMetric m]
        {
            get
            {
                switch (m)
                {
                    case SoilMetric.pH:      return pH;
                    case SoilMetric.OM:      return OM;
                    case SoilMetric.N:       return N;
                    case SoilMetric.P:       return P;
                    case SoilMetric.K:       return K;
                    case SoilMetric.Eh:      return Eh;
                    case SoilMetric.BaseIon: return BaseIon;
                    case SoilMetric.As:      return As;
                    case SoilMetric.Hg:      return Hg;
                    default:                 return 0f;
                }
            }
            set
            {
                switch (m)
                {
                    case SoilMetric.pH:      pH = value; break;
                    case SoilMetric.OM:      OM = value; break;
                    case SoilMetric.N:       N = value; break;
                    case SoilMetric.P:       P = value; break;
                    case SoilMetric.K:       K = value; break;
                    case SoilMetric.Eh:      Eh = value; break;
                    case SoilMetric.BaseIon: BaseIon = value; break;
                    case SoilMetric.As:      As = value; break;
                    case SoilMetric.Hg:      Hg = value; break;
                }
            }
        }

        /// <summary>深拷贝。跨阶段传递时一律用它，避免引用共享。</summary>
        public SoilState Clone() => new SoilState(this);

        /// <summary>调试/日志用的一行摘要。</summary>
        public override string ToString()
            => $"pH={pH:F2} OM={OM:F2} N={N:F2} P={P:F2} K={K:F2} | " +
               $"Eh={Eh:F1} BaseIon={BaseIon:F1} As={As:F2} Hg={Hg:F3}";
    }
}
