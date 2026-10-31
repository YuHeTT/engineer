namespace Engineer.UI
{
    /// <summary>
    /// 界面状态。命名与 Docs/项目执行流程.md 的「GameState」一节严格一致。
    /// 与《Unity界面.docx》的 12 张界面图的对应关系：
    ///   image1 Welcome / image2 Intro / image3 LandSelect / image4 LandParams /
    ///   image5 PlantSelect / image6 Confirm / image7 FarmStage /
    ///   image8+image9 RobotCruise（手动 / 自动）/ image10 Analyze /
    ///   image11 FarmStage(第五阶段) / image12 Revenue
    /// 其中 image7 与 image11 是同一个屏（第五阶段把「开始分析」按钮换成「收益计算」）。
    /// </summary>
    public enum GameState
    {
        /// <summary>1 初始界面：项目简介 / 开始体验。</summary>
        Welcome = 0,

        /// <summary>2 项目简介弹窗。</summary>
        Intro = 1,

        /// <summary>3 土地类型选择（平川台 / 泽洼域 / 崇岭阶 / 高原坪）。</summary>
        LandSelect = 2,

        /// <summary>4 土地基础数据设置（六项）。</summary>
        LandParams = 3,

        /// <summary>5 植物类型选择（玉米 / 甜菜 / 红薯 / 土豆）。</summary>
        PlantSelect = 4,

        /// <summary>6 确认选择。</summary>
        Confirm = 5,

        /// <summary>7 农场主界面（S1–S5）。</summary>
        FarmStage = 6,

        /// <summary>8/9 机器人操控（手动巡航 / 自动巡航）。</summary>
        RobotCruise = 7,

        /// <summary>10 开始分析（左侧建议 + 右侧本阶段水肥量输入）。</summary>
        Analyze = 8,

        /// <summary>11/12 收益计算。</summary>
        Revenue = 9,

        /// <summary>
        /// 地形观察（LandSelect 之后、LandParams 之前）。
        ///
        /// 【只能追加在最后】场景里 PlaceholderScreen.state / OptionCardButton.targetState
        /// 都是按 int 序列化的，插在中间会把已有屏的编号全部错位。
        /// </summary>
        LandPreview = 10,
    }

    /// <summary>状态的显示名与调试信息，供状态机写日志、供后续做面包屑导航使用。</summary>
    public static class GameStateInfo
    {
        private static readonly string[] DisplayNames =
        {
            "初始界面",
            "项目简介",
            "土地类型选择",
            "土地基础数据设置",
            "植物类型选择",
            "确认选择",
            "农场主界面",
            "机器人操控",
            "开始分析",
            "收益计算",
            "地形观察",
        };

        public static string DisplayName(GameState state)
        {
            var index = (int)state;
            if (index < 0 || index >= DisplayNames.Length) return state.ToString();
            return DisplayNames[index];
        }

        /// <summary>状态对应的场景对象名，构建器与状态机共用同一套命名。</summary>
        public static string ScreenObjectName(GameState state)
        {
            return "Screen_" + state;
        }
    }
}
