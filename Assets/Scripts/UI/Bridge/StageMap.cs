// ---------------------------------------------------------------------------
//  StageMap.cs —— 阶段编号换算（UI 与数值内核之间的唯一转换点）
// ---------------------------------------------------------------------------
//  作用：UI 用 1~5，数值内核用 S1~S4 + Done，本类是两者之间【唯一】的换算处。
//
//  【为什么必须集中在一处】
//    UI 的 GameContext.StageIndex 是 1~5（1..4 生长阶段，5 收获期）；
//    内核的 Stage 枚举是 S1=0..S4=3 加一个 Done=4（收获期不属于 Stage，
//    因为收益公式是 (S1+S2+S3+S4)/4，把收获期算进去就错了）。
//
//    如果让各处自己写 +1 / -1，迟早会出现"某个屏偏移了一位"这种
//    极难排查的错误 —— 而且它不会编译报错，只是结果不对。
//
//  【对照表】
//      UI StageIndex | Core Stage  | 含义
//      --------------|-------------|------------------
//           1        | S1          | 播种苗期
//           2        | S2          | 营养旺长期
//           3        | S3          | 生殖关键期
//           4        | S4          | 灌浆成熟期
//           5        | Done        | 收获期（不参与演算与产量）
// ---------------------------------------------------------------------------

using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>UI 的 1~5 阶段编号与内核 <see cref="Stage"/> 的换算。</summary>
    public static class StageMap
    {
        /// <summary>UI 的收获期编号。</summary>
        public const int UiHarvestStage = 5;

        /// <summary>UI 的生长阶段数量（不含收获期）。</summary>
        public const int UiGrowthStageCount = Stages.Count;

        /// <summary>
        /// UI 阶段编号 → 内核阶段。
        /// UI 的 5（收获期）没有对应的内核阶段，返回 false。
        /// </summary>
        public static bool TryToCore(int uiStage, out Stage stage)
        {
            stage = Stage.S1;
            if (uiStage < 1 || uiStage > Stages.Count) return false;
            stage = Stages.FromIndex(uiStage - 1);
            return true;
        }

        /// <summary>
        /// 内核阶段 → UI 阶段编号。<see cref="Stage.Done"/> 映射为 5。
        /// </summary>
        public static int ToUi(Stage stage)
        {
            if (stage == Stage.Done) return UiHarvestStage;
            return Stages.ToIndex(stage) + 1;
        }

        /// <summary>
        /// UI 阶段编号 → 内核阶段；收获期返回 <see cref="Stage.Done"/>。
        /// 需要"一定能拿到一个阶段值"时用它（比如查表、写日志）。
        /// </summary>
        public static Stage ToCoreOrDefault(int uiStage)
        {
            Stage stage;
            return TryToCore(uiStage, out stage) ? stage : Stage.Done;
        }

        /// <summary>是否是收获期。</summary>
        public static bool IsHarvest(int uiStage)
        {
            return uiStage >= UiHarvestStage;
        }

        /// <summary>
        /// UI 阶段名。**刻意与 GameContext.StageNames 的纯阶段名格式一致**
        /// （"播种苗期" 而不是 "第一阶段 · 播种苗期"），
        /// 因为现有界面上的写法是 "S1 播种苗期"，拼的是 "S"+编号+名称。
        /// </summary>
        public static string DisplayName(int uiStage)
        {
            switch (uiStage)
            {
                case 1: return "播种苗期";
                case 2: return "营养旺长期";
                case 3: return "生殖关键期";
                case 4: return "灌浆成熟期";
                case 5: return "收获期";
                default: return "未知阶段";
            }
        }

        /// <summary>
        /// UI 指标下标（<see cref="SoilIndicators"/> 的顺序）转内核指标枚举。
        /// 两者顺序【完全一致】（动态 5 项在前、静态 4 项在后），所以是直接强转；
        /// 但仍然走这个方法而不是到处 (SoilMetric)i，方便将来顺序变化时只改一处。
        /// </summary>
        public static SoilMetric ToMetric(int indicatorIndex)
        {
            if (indicatorIndex < 0 || indicatorIndex >= SoilMetrics.Count) return SoilMetric.pH;
            return (SoilMetric)indicatorIndex;
        }
    }
}
