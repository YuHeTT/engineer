namespace Engineer.UI
{
    /// <summary>
    /// 九项土壤指标的定义。顺序即界面显示顺序，与《Unity界面.docx》「所有数据」一节完全一致：
    /// 动态五项：酸衡值 (pH)、腐殖丰度、速效氮元、磷活度、钾储量
    /// 静态四项：氧还势 (Eh)、土骨离子、毒砷潜势、汞积量（第一阶段随机生成后全程不变）
    ///
    /// 说明：这里的名称**沿用策划文档的写法**（酸衡值 / 速效氮元 / 土骨离子 / 氧还势 / 毒砷潜势 / 汞积量），
    /// 以便与策划逐字对照。农业规范名（土壤 pH / 速效氮 / 土壤盐基离子 / 氧化还原电位 Eh /
    /// 砷有效性 / 汞累积量）见 Docs/Unity界面-方案分析.md 第五节，等策划确认后再统一替换。
    /// </summary>
    public static class SoilIndicators
    {
        /// <summary>指标总数。</summary>
        public const int Count = 9;

        /// <summary>动态指标数量（用户输入，且被水肥影响）。</summary>
        public const int DynamicCount = 5;

        /// <summary>显示名（与策划文档逐字一致）。</summary>
        public static readonly string[] Names =
        {
            "酸衡值 (pH)",
            "腐殖丰度",
            "速效氮元",
            "磷活度",
            "钾储量",
            "氧还势 (Eh)",
            "土骨离子",
            "毒砷潜势",
            "汞积量",
        };

        /// <summary>是否为静态指标（第一阶段的随机值，之后全程不变）。</summary>
        public static readonly bool[] IsStatic =
        {
            false, false, false, false, false,
            true, true, true, true,
        };

        /// <summary>
        /// 数值提示区间。前五项是「土地基础数据设置」里用户可填的范围；
        /// 后四项是文档规定由系统随机生成的范围，界面上只展示不可编辑。
        /// </summary>
        public static readonly string[] ValueRanges =
        {
            "5.8~8.0", "15~28", "70~130", "8~25", "90~200",
            "400~550", "7~16", "5~15", "0.03~0.12",
        };

        /// <summary>单位。文档未定义动态五项的单位（见方案分析 A10），先留空占位、不做臆测。</summary>
        public static readonly string[] Units =
        {
            "", "", "", "", "",
            "mV", "", "", "mg/kg",
        };

        /// <summary>没有数据时统一显示的字样。</summary>
        public const string NoDataText = "无";

        /// <summary>按指标下标取值格式化，用于「所有数据」面板。</summary>
        public static string Format(int index, float value)
        {
            return value.ToString("0.##") + Units[index];
        }
    }
}
