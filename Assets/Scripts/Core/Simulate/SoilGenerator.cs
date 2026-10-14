// ---------------------------------------------------------------------------
//  SoilGenerator.cs —— 初始土壤生成
// ---------------------------------------------------------------------------
//  作用：把"玩家填的 5 个动态指标 + 随机生成的 4 个静态指标"合成一份完整土壤状态。
//
//  【文档要求】
//    第一阶段：酸衡值、腐殖丰度、速效氮元、磷活度、钾储量 是前面用户输入的值；
//              氧还势、土骨离子、毒砷潜势、汞积量 根据 TABLE 2 随机生成。
//    第二三四五阶段：动态五项会被水肥演变；静态四项保持不变。
//
//  【可复现性】
//    使用 System.Random 并支持传 seed，这样：
//      - 策划复现问题时可以固定种子
//      - 批量验证可以跑几千组不同随机
//      - 将来要做"同一个存档重放"也不会出问题
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>初始土壤生成器。</summary>
    public static class SoilGenerator
    {
        /// <summary>
        /// 生成初始土壤：动态五项取自用户输入，静态四项按配置随机。
        /// </summary>
        /// <param name="userInput">玩家输入的土壤（静态四项会被覆盖）</param>
        /// <param name="config">提供静态指标随机范围</param>
        /// <param name="rng">随机源；传 null 则用固定值（取范围下界）</param>
        public static SoilState Generate(SoilState userInput, SimConfig config, System.Random rng)
        {
            var soil = userInput != null ? new SoilState(userInput) : new SoilState();
            if (config == null) return soil;

            for (int i = 0; i < SoilMetrics.StaticCount; i++)
            {
                var range = (config.StaticRanges != null && i < config.StaticRanges.Length)
                    ? config.StaticRanges[i] : null;
                if (range == null) continue;

                soil[range.Metric] = rng != null ? range.Sample(rng) : range.Min;
            }
            return soil;
        }

        /// <summary>
        /// 只随机静态四项，动态五项保持不动。
        /// 用于"第一阶段已经算好、后面阶段不要重新随机"的场景。
        /// </summary>
        public static void RandomizeStatic(SoilState soil, SimConfig config, System.Random rng)
        {
            if (soil == null || config == null) return;
            for (int i = 0; i < SoilMetrics.StaticCount; i++)
            {
                var range = (config.StaticRanges != null && i < config.StaticRanges.Length)
                    ? config.StaticRanges[i] : null;
                if (range == null) continue;
                soil[range.Metric] = rng != null ? range.Sample(rng) : range.Min;
            }
        }

        /// <summary>
        /// 建一份"区间中值"的土壤，用于测试与默认值。
        /// </summary>
        public static SoilState MakeMidpointInput(SimConfig config)
        {
            var soil = new SoilState();
            if (config == null) return soil;

            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                var range = (config.DynamicInputRanges != null && i < config.DynamicInputRanges.Length)
                    ? config.DynamicInputRanges[i] : null;
                if (range == null) continue;
                soil[range.Metric] = (range.Min + range.Max) * 0.5f;
            }
            return soil;
        }

        /// <summary>
        /// 建一份指定取值的土壤，用于批量验证（如取区间的四个角）。
        /// </summary>
        /// <param name="t">0 = 全部取下界，1 = 全部取上界，0.5 = 中值</param>
        public static SoilState MakeAtRatio(SimConfig config, float t)
        {
            var soil = new SoilState();
            if (config == null) return soil;

            t = MathX.Clamp01(t);
            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                var range = (config.DynamicInputRanges != null && i < config.DynamicInputRanges.Length)
                    ? config.DynamicInputRanges[i] : null;
                if (range == null) continue;
                soil[range.Metric] = MathX.Lerp(range.Min, range.Max, t);
            }
            return soil;
        }

        /// <summary>把动态五项裁剪进配置允许的输入范围内，挡住非法输入。</summary>
        public static void ClampToInputRange(SoilState soil, SimConfig config)
        {
            if (soil == null || config == null) return;
            for (int i = 0; i < SoilMetrics.DynamicCount; i++)
            {
                var range = (config.DynamicInputRanges != null && i < config.DynamicInputRanges.Length)
                    ? config.DynamicInputRanges[i] : null;
                if (range == null) continue;
                soil[range.Metric] = MathX.Clamp(soil[range.Metric], range.Min, range.Max);
            }
        }
    }
}
