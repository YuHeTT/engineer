using UnityEngine;

namespace Engineer.UI
{
    /// <summary>
    /// 地形观察页 / 构建加载层要用的全部文案与 id 映射。
    ///
    /// 【为什么运行时也要一份】<c>UiContent</c> 在 Editor 程序集里，打包后不参与编译；
    /// 观察页要在运行时按玩家选的地形刷新文字，所以这里放一份**只属于 UI 流程**的副本。
    /// 以后策划改文案时，两处（这里和 UiContent）一起改。
    /// </summary>
    public static class LandPreviewContent
    {
        /// <summary>四种地形的规范 id，顺序与选地页的四张卡一致。</summary>
        public static readonly string[] Ids = { "flat_plain", "low_wetland", "steep_ridge", "plateau" };

        /// <summary>同一地形的历史 id（旧存档 / 旧场景序列化里出现过）。</summary>
        private static readonly string[][] Aliases =
        {
            new[] { "flat_plain", "pingchuantai" },
            new[] { "low_wetland", "zewayu" },
            new[] { "steep_ridge", "chonglingjie" },
            new[] { "plateau", "gaoyuanping" },
        };

        public static readonly string[] Names = { "平川台", "泽洼域", "崇岭阶", "高原坪" };

        public static readonly string[] Intros =
        {
            "地势平缓开阔，土层较厚，排水条件适中，地下水位适宜，便于农业利用。",
            "地势低洼，地下水位较高，雨季容易积水，土壤湿度偏大，需做好排水防涝。",
            "坡度超过高，土层较薄，地表径流较强，易发生水土流失，需加强植被保护。",
            "海拔高，气压较低，昼夜温差较大，地势相对平缓，气候较凉爽。",
        };

        /// <summary>一行关键指标（建议值，待策划确认）。</summary>
        public static readonly string[] Facts =
        {
            "坡度 0~12°　·　土层 2.0 m　·　排水 适中",
            "坡度 0~3°　·　地下水位 高　·　雨季易积水",
            "坡度 15~30°　·　土层 较薄　·　台地 3 m",
            "海拔 较高　·　昼夜温差 大　·　台顶 平缓",
        };

        // ---------------- 观察页固定文案 ----------------

        public const string Breadcrumb = "土地选择 › 地形观察";
        public const string ControlHint = "WASD 飞行　·　右键转视角　·　Q/E 升降　·　Shift 加速";
        public const string BuildHint = "将在平坦处安置农场与房车";
        public const string BuildLabel = "构建农场";
        public const string BuildAgainLabel = "继续";
        public const string GeneratingHint = "正在生成地形…";
        public const string BuildButtonNote = "构建农场";

        // ---------------- 加载层文案 ----------------

        public const string LoadingTitle = "正在构建农场";
        public const string LoadingBadgeSuffix = "地形观察";

        public static readonly string[] LoadingStatus =
        {
            "正在整理地形…",
            "正在平整农场用地…",
            "正在安置农场与房车…",
            "正在恢复视角…",
            "准备完成",
        };

        /// <summary>Tip 里的 {0} 会被地形名替换。</summary>
        public static readonly string[] LoadingTips =
        {
            "正在把{0}整理成一片可耕种的平地",
            "正在找一块足够平的地方",
            "正在把 Farm 场景放进这片地",
            "马上就好",
            string.Empty,
        };

        /// <summary>
        /// 本局是否已经点过「选择地形」。
        /// 用来决定选地页要不要亮「● 已选」——GameContext 里本来就有默认值，
        /// 光看 LandTypeId 分不出"选过"还是"没选过"。
        /// </summary>
        public static bool LandChosen;

        /// <summary>把任意写法（规范 id / 旧 id / 中文名）映射成 0~3；认不出返回 -1。</summary>
        public static int IndexFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return -1;

            var key = id.Trim();
            for (var i = 0; i < Aliases.Length; i++)
            {
                for (var j = 0; j < Aliases[i].Length; j++)
                {
                    if (string.Equals(key, Aliases[i][j], System.StringComparison.OrdinalIgnoreCase)) return i;
                }
            }

            for (var i = 0; i < Names.Length; i++)
            {
                if (string.Equals(key, Names[i], System.StringComparison.Ordinal)) return i;
            }

            return -1;
        }

        public static string SafeName(int index)
        {
            return Names[Mathf.Clamp(index, 0, Names.Length - 1)];
        }

        public static string SafeIntro(int index)
        {
            return Intros[Mathf.Clamp(index, 0, Intros.Length - 1)];
        }

        public static string SafeFact(int index)
        {
            return Facts[Mathf.Clamp(index, 0, Facts.Length - 1)];
        }

        public static string Badge(int index)
        {
            return "地形观察 · " + SafeName(index);
        }

        /// <summary>根据 0~1 进度取阶段序号（0~4）。</summary>
        public static int StageFor(float progress)
        {
            if (progress < 0.15f) return 0;
            if (progress < 0.50f) return 1;
            if (progress < 0.75f) return 2;
            if (progress < 0.90f) return 3;
            return 4;
        }

        public static string TipFor(int stage, string landName)
        {
            var tip = LoadingTips[Mathf.Clamp(stage, 0, LoadingTips.Length - 1)];
            return string.Format(tip, string.IsNullOrEmpty(landName) ? "这块地" : landName);
        }
    }
}
