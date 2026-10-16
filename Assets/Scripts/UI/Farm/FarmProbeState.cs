using System;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>阶段探测快照：采集、确认与分析共用；不直接改写真实土壤。</summary>
    public sealed class FarmProbeState
    {
        public const float MaxError = 0.10f;
        public const float MinError = 0.05f;
        public const float RequiredCoverage = 1f;
        private readonly float[] truth = new float[SoilMetrics.Count];
        private readonly float[] probe = new float[SoilMetrics.Count];
        private readonly bool[] revealed = new bool[SoilMetrics.Count];
        private SoilState capturedSoil;
        private GameSession owner;
        private int seed;
        public int StageIndex { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsConfirmed { get; private set; }
        public float Coverage { get; private set; }
        public int RevealedCount
        {
            get { int n = 0; foreach (bool value in revealed) if (value) n++; return n; }
        }
        public bool IsCurrent(GameSession session) => IsActive && session != null && session.IsStarted
            && !session.IsFinished && ReferenceEquals(owner, session)
            && StageIndex == session.UiStage && ReferenceEquals(capturedSoil, session.Soil);
        public bool CanAnalyze(GameSession session) => IsCurrent(session) && IsConfirmed
            && Coverage >= RequiredCoverage && RevealedCount == SoilMetrics.Count;

        /// <summary>同阶段重复进页面保留进度和随机种子；新局/新阶段才建立新快照。</summary>
        public void Begin(GameSession session)
        {
            if (IsCurrent(session)) return;
            Reset();
            if (session == null || !session.IsStarted || session.IsFinished) return;
            owner = session;
            capturedSoil = session.Soil;
            StageIndex = session.UiStage;
            seed = Guid.NewGuid().GetHashCode(); // 每次主动重扫独立取样，不受连续点击的时钟粒度影响。
            IsActive = true;
            for (int i = 0; i < truth.Length; i++) truth[i] = session.GetIndicator(i, 0f);
        }
        public void Reset()
        {
            IsActive = false; IsConfirmed = false; Coverage = 0f;
            owner = null; capturedSoil = null; StageIndex = 0;
            Array.Clear(truth, 0, truth.Length);
            Array.Clear(probe, 0, probe.Length);
            Array.Clear(revealed, 0, revealed.Length);
        }
        public float GetSource(int index) => InRange(index) ? truth[index] : 0f;
        public bool IsRevealed(int index) => InRange(index) && revealed[index];
        public float GetProbe(int index, float fallback = 0f) => IsRevealed(index) ? probe[index] : fallback;
        // 保留旧接口，但未采集的值不再用真值兜底。
        public float GetEffective(int index) => GetProbe(index);
        public void SetCoverage(float value)
        {
            if (!IsActive || !MathX.IsFinite(value)) return;
            value = MathX.Clamp01(value);
            if (value < Coverage) // 主动重扫或网格重置：旧确认立即失效。
            {
                IsConfirmed = false;
                Array.Clear(revealed, 0, revealed.Length);
                Array.Clear(probe, 0, probe.Length);
            }
            if (IsConfirmed) return; // 已确认快照冻结。
            Coverage = value;
            int target = (int)Math.Floor(Coverage * SoilMetrics.DynamicCount + 1e-4f);
            for (int i = 0; i < target && i < SoilMetrics.DynamicCount; i++) Reveal(i);
            // 固定误差方向，幅度随覆盖率收敛；同覆盖率刷新不会跳值。
            for (int i = 0; i < revealed.Length; i++) if (revealed[i]) probe[i] = ComputeProbe(i);
            if (Coverage >= RequiredCoverage) RevealAll();
        }
        public void Reveal(int index)
        {
            if (!IsActive || !InRange(index) || revealed[index]) return;
            revealed[index] = true; probe[index] = ComputeProbe(index);
        }
        public void RevealAll()
        {
            if (!IsActive || Coverage < RequiredCoverage) return;
            for (int i = 0; i < revealed.Length; i++) Reveal(i);
        }
        public bool Confirm(GameSession session)
        {
            if (!IsCurrent(session) || Coverage < RequiredCoverage) return false;
            RevealAll();
            IsConfirmed = RevealedCount == SoilMetrics.Count;
            return IsConfirmed;
        }
        /// <summary>只允许取得本阶段已确认的观测值；调用方拿到的是副本。</summary>
        public SoilState AnalysisSoil(GameSession session)
        {
            if (!CanAnalyze(session)) return null;
            var soil = new SoilState();
            for (int i = 0; i < probe.Length; i++) soil[(SoilMetric)i] = probe[i];
            return soil;
        }
        private float ComputeProbe(int index)
        {
            float amplitude = MaxError + (MinError - MaxError) * Coverage;
            float r = Hash01(seed, index) * 2f - 1f;
            return truth[index] * (1f + r * amplitude);
        }
        private static bool InRange(int index) => index >= 0 && index < SoilMetrics.Count;
        private static float Hash01(int seed, int index)
        {
            unchecked
            {
                uint x = (uint)(seed * 73856093 ^ index * 19349663);
                x ^= x >> 16; x *= 0x7feb352du;
                x ^= x >> 15; x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
