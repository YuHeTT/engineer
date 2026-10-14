// ---------------------------------------------------------------------------
//  Stage.cs —— 生长阶段枚举
// ---------------------------------------------------------------------------
//  作用：定义五个生长阶段，以及阶段相关的判断工具。
//
//  【导航】文档用的是两套编号，本文件统一成一套：
//    文档正文  "阶段一/二/三/四/五"
//    文档表格  "S1/S2/S3/S4"
//    本工程    Stage.S1 .. Stage.S4，外加 Stage.Done 表示"全部结束"
//
//  对应关系（以文档 TABLE 13 为准）：
//    S1 = 播种苗期
//    S2 = 营养旺长期
//    S3 = 生殖关键期
//    S4 = 灌浆成熟期
//    收获期 = 第 5 阶段，不需要玩家操作，不参与演算与产量计算
//
//  【重要】只有 S1~S4 参与演算和产量。收获期在 UI 上是一个界面，
//    但在数值上不属于 Stage 的一部分 —— 这样收益计算 (S1+S2+S3+S4)/4 才成立。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>生长阶段。S1~S4 参与数值演算，Done 表示流程结束。</summary>
    public enum Stage
    {
        /// <summary>播种苗期。文档 TABLE 13 行 1~2。</summary>
        S1 = 0,
        /// <summary>营养旺长期。文档 TABLE 13 行 3~5。</summary>
        S2 = 1,
        /// <summary>生殖关键期。文档 TABLE 13 行 6~8。水分权重 0.4，胁迫最敏感。</summary>
        S3 = 2,
        /// <summary>灌浆成熟期。文档 TABLE 13 行 9~11。</summary>
        S4 = 3,

        /// <summary>四个阶段全部结束，可以出收益报告了。</summary>
        Done = 4,
    }

    /// <summary>阶段相关工具。</summary>
    public static class Stages
    {
        /// <summary>参与数值计算的阶段数（4）。</summary>
        public const int Count = 4;

        /// <summary>S1..S4，用于遍历。</summary>
        public static readonly Stage[] All = { Stage.S1, Stage.S2, Stage.S3, Stage.S4 };

        /// <summary>是否是有效的可计算阶段。</summary>
        public static bool IsPlayable(Stage s) => s >= Stage.S1 && s <= Stage.S4;

        /// <summary>转成数组下标（S1 -&gt; 0）。调用前应确认 IsPlayable。</summary>
        public static int ToIndex(Stage s) => (int)s;

        /// <summary>下标转阶段。</summary>
        public static Stage FromIndex(int i) => (Stage)i;

        /// <summary>中文显示名，对应文档"当前阶段，状态"栏。</summary>
        public static string DisplayName(Stage s)
        {
            switch (s)
            {
                case Stage.S1: return "第一阶段 · 播种苗期";
                case Stage.S2: return "第二阶段 · 营养旺长期";
                case Stage.S3: return "第三阶段 · 生殖关键期";
                case Stage.S4: return "第四阶段 · 灌浆成熟期";
                case Stage.Done: return "收获期";
                default: return s.ToString();
            }
        }

        /// <summary>短名，用于配置表与日志。与文档 TABLE 13 的写法一致。</summary>
        public static string ShortName(Stage s) => s.ToString();
    }
}
