// ---------------------------------------------------------------------------
//  FarmProbeState.cs —— 探测状态（原数据 / 探测数据）
// ---------------------------------------------------------------------------
//  作用：这是「农场 3D 巡航」和「UI 界面」之间的**共享数据**。
//
//  【为什么需要它】
//    策划的设定是"原数据可能不准，所以要探测"（见 Docs/漏洞与风险清单.md 的 I2）。
//    所以同一块土壤有两个层面的值：
//      · 原数据  = 界面进入时看到的、**玩家以为的**值
//      · 探测数据 = 机器人实探到的值，**带误差**，只在探测到之后才有
//    分析界面应该用探测值（否则"探测"这一步没有意义）。
//
//  【误差模型】（2026-10-15 与策划沟通后暂定）
//      误差幅度随覆盖率**从 10% 收敛到 5%**：
//        amplitude = Lerp(MaxError, MinError, coverage)
//        probe = truth * (1 + N(0, amplitude))      对大部分指标
//        probe = truth + N(0, amplitude * truth)    对 pH（乘性没意义）
//    覆盖率 100% 时仍有 5% 误差 —— 这样"探测"永远有意义，
//    但也不会因为误差太大导致分析结论乱跳。
//
//  【为什么不是随机每次刷新都变】
//    误差在**首次探测到该项时固定下来**（存进 probeValues），
//    之后刷新界面显示的是同一个值。否则同一个指标每次看都不一样，玩家会困惑。
// ---------------------------------------------------------------------------

using UnityEngine;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>一次模拟里的探测结果。由农场巡航写入，由 UI 读取。</summary>
    public sealed class FarmProbeState
    {
        /// <summary>误差幅度上限（覆盖率 0% 时）。</summary>
        public const float MaxError = 0.10f;

        /// <summary>误差幅度下限（覆盖率 100% 时）。</summary>
        public const float MinError = 0.05f;

        private readonly float[] truth = new float[SoilIndicators.Count];
        private readonly float[] probe = new float[SoilIndicators.Count];
        private readonly bool[] revealed = new bool[SoilIndicators.Count];

        private int seed;

        public bool IsActive { get; private set; }

        /// <summary>当前覆盖率 0~1。</summary>
        public float Coverage { get; private set; }

        /// <summary>已揭示的指标个数。</summary>
        public int RevealedCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < revealed.Length; i++) if (revealed[i]) n++;
                return n;
            }
        }

        // ==================================================================
        //  开始 / 重置
        // ==================================================================

        /// <summary>开始一次新的探测：抄一份真实土壤作为"原数据"。</summary>
        public void Begin(GameSession session)
        {
            IsActive = true;
            Coverage = 0f;
            seed = Random.Range(int.MinValue, int.MaxValue);

            for (var i = 0; i < truth.Length; i++)
            {
                truth[i] = session != null && session.IsStarted
                    ? session.GetIndicator(i, 0f)
                    : 0f;
                probe[i] = 0f;
                revealed[i] = false;
            }
        }

        /// <summary>清空。</summary>
        public void Reset()
        {
            IsActive = false;
            Coverage = 0f;
            for (var i = 0; i < revealed.Length; i++) revealed[i] = false;
        }

        // ==================================================================
        //  读
        // ==================================================================

        /// <summary>原数据（玩家以为的值）。</summary>
        public float GetSource(int index)
        {
            return InRange(index) ? truth[index] : 0f;
        }

        /// <summary>该项是否已经探测到。</summary>
        public bool IsRevealed(int index)
        {
            return InRange(index) && revealed[index];
        }

        /// <summary>探测到的值。未探测到时返回 <paramref name="fallback"/>。</summary>
        public float GetProbe(int index, float fallback = 0f)
        {
            return IsRevealed(index) ? probe[index] : fallback;
        }

        /// <summary>
        /// 给分析/推荐用的值：探测到就用探测值，没探测到就用真值兜底。
        /// 兜底是为了不让流程断掉（UI 上仍会把未探测的显示成「无」）。
        /// </summary>
        public float GetEffective(int index)
        {
            if (!InRange(index)) return 0f;
            return revealed[index] ? probe[index] : truth[index];
        }

        // ==================================================================
        //  写（由农场巡航调用）
        // ==================================================================

        /// <summary>
        /// 更新覆盖率。覆盖率上升会**按比例揭示**静态指标之外的前几项 ——
        /// 也就是"扫得越多，知道的越多"。
        /// </summary>
        public void SetCoverage(float coverage)
        {
            Coverage = Mathf.Clamp01(coverage);

            // 动态 5 项（pH / 腐殖丰度 / 速效氮 / 磷活度 / 钾储量）随覆盖率揭示
            var revealTarget = Mathf.FloorToInt(Coverage * SoilIndicators.DynamicCount + 1e-4f);
            for (var i = 0; i < SoilIndicators.DynamicCount && i < revealed.Length; i++)
            {
                if (i < revealTarget) Reveal(i);
            }
        }

        /// <summary>揭示某一项（如果还没揭示）。</summary>
        public void Reveal(int index)
        {
            if (!InRange(index) || revealed[index]) return;
            revealed[index] = true;
            probe[index] = ComputeProbe(index, Coverage);
        }

        /// <summary>揭示全部（巡航到 100% 时）。静态四项也一起给。</summary>
        public void RevealAll()
        {
            for (var i = 0; i < revealed.Length; i++) Reveal(i);
        }

        // ==================================================================
        //  误差
        // ==================================================================

        private float ComputeProbe(int index, float coverage)
        {
            var t = truth[index];
            if (Mathf.Approximately(t, 0f)) return t;

            var amplitude = Mathf.Lerp(MaxError, MinError, Mathf.Clamp01(coverage));

            // 用确定性的伪随机：同一个 seed + 同一个指标，结果固定，
            // 不会每次刷新都变（否则玩家会觉得数值在乱跳）。
            var r = Hash01(seed, index) * 2f - 1f;

            // pH 用加性误差（乘性对对数尺度没意义），其余用乘性
            if (index == (int)SoilMetric.pH)
                return t + r * amplitude * t;

            return t * (1f + r * amplitude);
        }

        private static bool InRange(int index)
        {
            return index >= 0 && index < SoilIndicators.Count;
        }

        private static float Hash01(int seed, int index)
        {
            unchecked
            {
                var x = (uint)(seed * 73856093 ^ index * 19349663);
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
