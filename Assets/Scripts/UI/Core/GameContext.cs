using System;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>
    /// 跨屏共享的数据。第一版只保存「用户在界面上直接选择 / 输入」的内容，
    /// 真正的数值内核（Docs/数值模拟验证记录.md 里的公式）接入后，只需要在这里补代理字段。
    ///
    /// 注意：本类不引用 Core 数值内核，避免白盒界面阶段产生编译依赖。
    /// </summary>
    [Serializable]
    public class GameContext
    {
        /// <summary>阶段总数：S1 播种苗期 → S5 收获期。</summary>
        public const int MaxStage = 5;

        /// <summary>五个阶段的名称，下标 0 对应 S1。</summary>
        public static readonly string[] StageNames =
        {
            "播种苗期",
            "营养旺长期",
            "生殖关键期",
            "灌浆成熟期",
            "收获期",
        };

        // ------------------------------------------------------------------
        // 选择结果
        // ------------------------------------------------------------------

        /// <summary>土地类型 Id（Demo 只开放「平川台」）。</summary>
        public string LandTypeId = "pingchuantai";

        /// <summary>土地类型显示名。</summary>
        public string LandTypeName = "平川台";

        /// <summary>植物类型 Id。</summary>
        public string PlantTypeId = "corn";

        /// <summary>植物类型显示名。</summary>
        public string PlantTypeName = "玉米";

        // ------------------------------------------------------------------
        // 土地基础数据设置（六项）
        // ------------------------------------------------------------------

        /// <summary>土地面积（亩）。</summary>
        public float LandAreaMu = 10f;

        /// <summary>1 酸衡值 (pH)。</summary>
        public float Ph = 6.5f;

        /// <summary>2 腐殖丰度。</summary>
        public float OrganicMatter = 22f;

        /// <summary>3 速效氮元。</summary>
        public float AvailableNitrogen = 95f;

        /// <summary>4 磷活度。</summary>
        public float LabilePhosphorus = 17f;

        /// <summary>5 钾储量。</summary>
        public float Potassium = 150f;

        /// <summary>6 静态指标（氧还势 / 土骨离子 / 毒砷潜势 / 汞积量），第一阶段随机生成后不变。</summary>
        public float[] StaticIndicators = { 470f, 11f, 9f, 0.07f };

        // ------------------------------------------------------------------
        // 流程
        // ------------------------------------------------------------------

        /// <summary>当前阶段，1~5。</summary>
        public int StageIndex = 1;

        /// <summary>本阶段水肥施用量（顺序：灌溉量、有机肥、钾肥、氮肥、磷肥）。</summary>
        public readonly float[] StageDosage = new float[5];

        /// <summary>当前阶段名。</summary>
        public string CurrentStageName
        {
            get { return StageName(StageIndex); }
        }

        /// <summary>是否是最后一个阶段（此时农场界面的按钮变成「收益计算」）。</summary>
        public bool IsLastStage
        {
            get { return StageIndex >= MaxStage; }
        }

        public static string StageName(int stageIndex)
        {
            var index = Mathf.Clamp(stageIndex, 1, MaxStage) - 1;
            return StageNames[index];
        }

        /// <summary>按 <see cref="SoilIndicators"/> 的下标读取指标值，静态项走 StaticIndicators。</summary>
        public float GetIndicator(int indicatorIndex)
        {
            switch (indicatorIndex)
            {
                case 0: return Ph;
                case 1: return OrganicMatter;
                case 2: return AvailableNitrogen;
                case 3: return LabilePhosphorus;
                case 4: return Potassium;
                default:
                    var staticIndex = indicatorIndex - SoilIndicators.DynamicCount;
                    if (staticIndex >= 0 && staticIndex < StaticIndicators.Length) return StaticIndicators[staticIndex];
                    return 0f;
            }
        }

        /// <summary>按 <see cref="SoilIndicators"/> 的下标写入指标值。</summary>
        public void SetIndicator(int indicatorIndex, float value)
        {
            switch (indicatorIndex)
            {
                case 0: Ph = value; break;
                case 1: OrganicMatter = value; break;
                case 2: AvailableNitrogen = value; break;
                case 3: LabilePhosphorus = value; break;
                case 4: Potassium = value; break;
                default:
                    var staticIndex = indicatorIndex - SoilIndicators.DynamicCount;
                    if (staticIndex >= 0 && staticIndex < StaticIndicators.Length) StaticIndicators[staticIndex] = value;
                    break;
            }
        }

        /// <summary>恢复到「开始新的模拟」时的初始值。</summary>
        public void Reset()
        {
            LandTypeId = "pingchuantai";
            LandTypeName = "平川台";
            PlantTypeId = "corn";
            PlantTypeName = "玉米";

            LandAreaMu = 10f;
            Ph = 6.5f;
            OrganicMatter = 22f;
            AvailableNitrogen = 95f;
            LabilePhosphorus = 17f;
            Potassium = 150f;
            StaticIndicators = new float[] { 470f, 11f, 9f, 0.07f };

            StageIndex = 1;
            for (var i = 0; i < StageDosage.Length; i++) StageDosage[i] = 0f;
        }
    }
}
