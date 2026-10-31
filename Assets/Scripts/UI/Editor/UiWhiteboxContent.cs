namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 白盒界面的全部文案。**文案只从这里取**，策划改字不用动布局代码。
    /// 所有字符串逐字来自《Unity界面.docx》（个别明显笔误已就地标注）。
    /// </summary>
    public static class UiContent
    {
        // ---------------- 1 初始界面 / 2 项目简介 ----------------

        public const string AppTitle = "工创赛 · 数字化农田管理决策演示系统";
        public const string AppSubtitle = "白盒占位：正式版这里是项目主视觉 / 3D 农田场景";
        public const string AppFooter = "Unity 2022.3.62f2c1 · uGUI + TextMeshPro · 白盒 v1（仅用于布局评审）";

        public const string IntroTitle = "项目简介";
        public const string IntroBody =
            "本项目面向工创赛数字化工程场景，以玉米田为对象，构建融合数字孪生与智能装备的农田管理决策演示系统。" +
            "无人机和地面巡检机器人采集环境与土壤数据，系统在虚拟农田中呈现酸碱度、腐殖丰度及氮磷钾等关键指标，" +
            "并依据玉米四阶段生长模型生成灌溉、施肥建议。用户可自主调整管理方案，直观看到作物长势、产量和收益的变化，" +
            "形成\u201c感知—分析—决策—反馈\u201d闭环。项目以可交互、可解释的方式，展示数字技术在精准农业和农田水肥管理中的应用价值。";

        // ---------------- 3 土地类型选择 ----------------

        public static readonly string[] LandNames = { "平川台", "泽洼域", "崇岭阶", "高原坪" };
        public static readonly string[] LandIds = { "flat_plain", "low_wetland", "steep_ridge", "plateau" };

        public static readonly string[] LandIntros =
        {
            "地势平缓开阔，土层较厚，排水条件适中，地下水位适宜，便于农业利用。",
            "地势低洼，地下水位较高，雨季容易积水，土壤湿度偏大，需做好排水防涝。",
            // 文档原文：「坡度超过高，土层较薄……」，"超过"两字疑似笔误，待策划确认后统一。
            "坡度超过高，土层较薄，地表径流较强，易发生水土流失，需加强植被保护。",
            "海拔高，气压较低，昼夜温差较大，地势相对平缓，气候较凉爽。",
        };

        public const string LandSelectNote = "备注：四块地均可选择。";

        // ---------------- 5 植物类型选择 ----------------

        public static readonly string[] PlantNames = { "玉米", "甜菜", "红薯", "土豆" };
        public static readonly string[] PlantIds = { "corn", "beet", "sweet_potato", "potato" };

        public static readonly string[] PlantIntros =
        {
            "玉米适宜在地势平缓、土层较厚的地块生长，喜温喜光，生长期需要充足水分。平川台排水适中，有利于根系发育和籽粒成熟。",
            "甜菜属于根用作物，喜凉爽气候和疏松土壤，块根可用于制糖。泽洼域需改善排水，避免雨季积水影响根系生长和糖分积累。",
            "红薯喜温暖、光照充足的环境，块根可作粮食和加工原料。崇岭阶坡度较大，种植应选择梯田，覆盖并控制径流，减轻水土流失。",
            "土豆喜凉爽气候和疏松、排水良好的土壤，地下块茎可食用。高原坪昼夜温差较大，种植时应注意防霜和补水，保障块茎发育。",
        };

        public const string PlantSelectNote = "备注：四种作物均可选择。";

        // ---------------- 4 土地基础数据设置 ----------------

        /// <summary>六项基础数值：标签（含文档给的取值范围）。</summary>
        public static readonly string[] LandParamLabels =
        {
            "土地面积（亩数）",
            "酸衡值（5.8~8）",
            "腐殖丰度（15~28）",
            "速效氮元（70~130）",
            "磷活度（8~25）",
            "钾储量（90~200）",
        };

        /// <summary>六项基础数值的默认值（文档「平川台地形初始数值」未给具体值，先取区间中值占位）。</summary>
        public static readonly string[] LandParamDefaults = { "10", "6.5", "22", "95", "17", "150" };

        public const string LandParamNote = "说明：前五项会随水肥方案逐阶段变化，土地面积仅用于换算单亩用量。";

        // ---------------- 6 确认选择 ----------------

        public const string ConfirmTitle = "确认选择";
        public const string ConfirmNote = "确认后进入第三人称视角农场，视角中心是玉米田，旁边停着智能房车。";

        // ---------------- 7 农场主界面 ----------------

        public const string FarmHint = "请通过机器人探测，设置水肥来收集所有的数据，数据收集完成之后点击开始分析进行分析检测。";

        public const string DataPanelTitle = "所有数据";
        public const string DataUpdatedTag = "已更新";
        public const string DataNotUpdatedTag = "未更新";

        public const string FarmBackendNote = "状态说明与长势由「适宜度公式 + 推荐水肥方案总表」驱动，接入数值内核后自动显示。";

        // ---------------- 8 机器人操控 ----------------

        public const string CruiseHint =
            "机器人探测：请选择手动或者自动巡航进行数据收集。\n" +
            "• 手动巡航：通过按键\u201cw、a、s、d\u201d操控机器人通过未覆盖的玉米田，同时观察探测数据；" +
            "当探测数据全部显示时，点击更新数据，将探测数据覆盖原数据。\n" +
            "• 自动巡航：机器人沿 S 型走位自动探测，此时自动巡航按钮变为「正在检测」；" +
            "探测完成后按钮变回「自动巡航」，同时探测数据全部显示，点击更新数据覆盖原数据。";

        public const string CruiseSourceTitle = "原数据";
        public const string CruiseProbeTitle = "探测数据";
        public const string CruiseManual = "手动巡航";
        public const string CruiseAuto = "自动巡航";
        public const string CruiseAutoBusy = "正在检测…";
        public const string CruiseUpdate = "更新数据";

        // ---------------- 10 开始分析 ----------------

        public const string AnalyzeHint = "根据灌溉建议设置灌溉量，施肥量。";
        public const string AnalyzeSuggestionTitle = "本阶段灌溉量施肥量建议";
        public const string AnalyzeDosageTitle = "本阶段水肥量";
        public const string AnalyzePending = "待接入";

        /// <summary>五项适宜度显示名（顺序与 SoilIndicators 前五项一致）。</summary>
        public static readonly string[] SuitabilityLabels =
        {
            "f<sub>PH</sub>　pH 适宜度",
            "f<sub>OM</sub>　腐殖丰度适宜度",
            "f<sub>N</sub>　速效氮适宜度",
            "f<sub>P</sub>　磷活度适宜度",
            "f<sub>K</sub>　钾储量适宜度",
        };

        /// <summary>五项适宜度的值域，来自文档「值域 [0.2,1]」。</summary>
        public const string SuitabilityRange = "值域 [0.2, 1]（结果得 1 即 100%）";

        public static readonly string[] SuggestionLabels = { "推荐肥料类型", "单亩施肥量（kg/亩）", "单亩基准灌溉（m³/亩）", "作物生理说明" };

        /// <summary>本阶段水肥量输入行，顺序与 GameContext.StageDosage 一致。</summary>
        public static readonly string[] DosageLabels =
        {
            "灌溉量（m³/亩）",
            "有机肥（kg/亩）",
            "钾肥（kg/亩）",
            "氮肥（kg/亩）",
            "磷肥（kg/亩）",
        };

        // ---------------- 11/12 收益计算 ----------------

        public const string RevenueTitle = "收益计算";
        public const string RevenueStageTableTitle = "各阶段参数（docx「推荐水肥方案总表」参数）";
        public const string RevenueStaticTitle = "四个不变参数";
        public const string RevenueFormulaTitle = "计算公式";
        public const string RevenueResultTitle = "最终收益 Y";
        public const string RevenueResultPlaceholder = "白盒占位：接入产量模型后显示 Y";
        public const string RevenueRestart = "开始新的模拟";

        /// <summary>四个不变参数（第一阶段随机生成后全程不变）。</summary>
        public static readonly string[] StaticParamNames = { "氧还势 (Eh)", "土骨离子", "毒砷潜势", "汞积量" };

        /// <summary>四个不变参数的默认值（白盒占位，接入 GameContext 后刷新）。</summary>
        public static readonly string[] StaticParamDefaults = { "470 mV", "11", "9", "0.07 mg/kg" };

        /// <summary>收益计算页「各阶段参数」表头。</summary>
        public static readonly string[] StageTableHeaders = { "阶段", "本阶段水肥量", "适宜度结果" };

        /// <summary>收益计算界面列出的公式（下标用 TMP 富文本）。</summary>
        public static readonly string[] FormulaLines =
        {
            "F<sub>fert</sub> = (F<sub>fert,s1</sub> + F<sub>fert,s2</sub> + F<sub>fert,s3</sub> + F<sub>fert,s4</sub>) / 4",
            "F<sub>water</sub> = (α<sub>1</sub>F<sub>w,s1</sub> + α<sub>2</sub>F<sub>w,s2</sub> + α<sub>3</sub>F<sub>w,s3</sub> + α<sub>4</sub>F<sub>w,s4</sub>) / 4",
            "S<sub>static</sub> = 1",
            "Y = Y<sub>max,geo</sub> × F<sub>fert</sub> × F<sub>water</sub> × S<sub>static</sub>",
        };

        /// <summary>收益计算界面顶部说明（文档原话）。</summary>
        public const string RevenueNote =
            "列出第二至第五阶段下方表格中的参数，以及四个不变参数；按左侧公式计算并输出最终收益 Y。";
    }
}
