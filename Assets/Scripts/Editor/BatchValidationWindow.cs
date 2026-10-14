// ---------------------------------------------------------------------------
//  BatchValidationWindow.cs —— 数值批量验证窗口
// ---------------------------------------------------------------------------
//  作用：把"在 Unity 里手点着试数值"换成"一次跑几百组参数出对比表"。
//
//  【为什么这个工具值得优先做】
//    数值内核阶段最有价值的产出不是代码，而是"一张能交给策划的对比表"：
//      - 照抄建议时，四个阶段的适宜度与最终产量是多少
//      - 施肥 / 灌溉乘不同倍率，产量有没有区分度
//      - 初始土壤取区间的四个角，结果是否合理
//      - 有没有任何输入组合匹配不到推荐方案（判定空洞）
//      - 连续多轮模拟，指标会不会单调失控
//    这些问题在 Unity 里手点要几小时，这里几秒钟。
//
//  【已跑出来的结论（用同样逻辑在 Python 里验证过）】
//    1. 施肥从 1 倍到 3 倍，产量几乎不变 —— 因为适宜度是"平台型"，
//       落在最优区间内就恒等于 1.0，多施不再加分。
//    2. pH 会净下降 —— 有机肥项系数 0.1 弱于氮肥项 0.15。
//    详见 Docs/数值模拟验证记录.md
//
//  使用：菜单 Engineer / 数值批量验证
// ---------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Engineer.Core;

namespace Engineer.Editor
{
    /// <summary>数值批量验证窗口。</summary>
    public sealed class BatchValidationWindow : EditorWindow
    {
        private SimConfigAsset _asset;
        private Vector2 _scroll;
        private string _report = "点击下方按钮开始验证。";
        private bool _useDefault = true;

        [MenuItem("Engineer/数值批量验证")]
        public static void Open()
        {
            var w = GetWindow<BatchValidationWindow>("数值批量验证");
            w.minSize = new Vector2(760, 520);
            w.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("数据源", EditorStyles.boldLabel);

            _useDefault = EditorGUILayout.ToggleLeft(
                "使用文档默认值（SimConfig.CreateDefault，不含资产覆盖）", _useDefault);

            using (new EditorGUI.DisabledScope(_useDefault))
            {
                _asset = (SimConfigAsset)EditorGUILayout.ObjectField(
                    "数值配置资产", _asset, typeof(SimConfigAsset), false);
            }

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("1. 适宜度曲线自检", GUILayout.Height(24))) RunCurveCheck();
                if (GUILayout.Button("2. 单阶段方案分布", GUILayout.Height(24))) RunPlanDistribution();
                if (GUILayout.Button("3. 施肥/灌溉倍率扫描", GUILayout.Height(24))) RunMultiplierScan();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("4. 初始区间四角", GUILayout.Height(24))) RunCornerScan();
                if (GUILayout.Button("5. 规则覆盖性（10万组）", GUILayout.Height(24))) RunCoverage();
                if (GUILayout.Button("6. 连续多轮漂移", GUILayout.Height(24))) RunDrift();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部跑一遍", GUILayout.Height(26))) RunAll();
                if (GUILayout.Button("复制到剪贴板", GUILayout.Height(26)))
                    EditorGUIUtility.systemCopyBuffer = _report;
            }

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private SimConfig BuildConfig()
        {
            if (_useDefault || _asset == null) return SimConfig.CreateDefault();
            return _asset.BuildSimConfig();
        }

        // ===================================================================
        //  1. 适宜度曲线自检
        // ===================================================================
        private void RunCurveCheck()
        {
            var cfg = BuildConfig();
            var sb = new StringBuilder();
            sb.AppendLine("=== 1. 适宜度曲线自检 ===");
            sb.AppendLine("检查每条曲线：最优区间内应为 1.0，两端保底应为 0.2，单调性正确。");
            sb.AppendLine();

            foreach (var m in SoilMetrics.Dynamic)
            {
                var c = cfg.CurveOf(m);
                if (c == null) { sb.AppendLine($"{m}: 缺少曲线"); continue; }

                c.GetOptimalRange(out float lo, out float hi);
                float atLo = c.Evaluate(lo);
                float atHi = c.Evaluate(hi);
                float below = c.Evaluate(c.B0 - 1f);
                float above = c.Evaluate(c.B3 + 1f);
                float mid = c.Evaluate((c.B1 + c.B2) * 0.5f);

                bool ok = Mathf.Approximately(atLo, c.Peak) && Mathf.Approximately(atHi, c.Peak)
                       && Mathf.Approximately(mid, c.Peak)
                       && Mathf.Approximately(below, c.Floor) && Mathf.Approximately(above, c.Floor);

                sb.AppendLine($"{SoilMetrics.DisplayName(m)}");
                sb.AppendLine($"  边界 [{c.B0}, {c.B1}, {c.B2}, {c.B3}]  保底 {c.Floor}  平台 {c.Peak}");
                sb.AppendLine($"  最优区间 [{lo}, {hi}] -> f({lo})={atLo:F3} f({hi})={atHi:F3} f(中值)={mid:F3}");
                sb.AppendLine($"  越界：f({c.B0 - 1f})={below:F3}  f({c.B3 + 1f})={above:F3}");
                sb.AppendLine($"  结果：{(ok ? "通过" : "★ 异常")}");
                sb.AppendLine();
            }
            sb.AppendLine("参考：适宜度越高不代表指标越大 —— 过量同样会掉到保底值。");
            _report = sb.ToString();
        }

        // ===================================================================
        //  2. 单阶段方案分布
        // ===================================================================
        private void RunPlanDistribution()
        {
            var cfg = BuildConfig();
            var table = new PlanTable(cfg);
            var sb = new StringBuilder();
            sb.AppendLine("=== 2. 推荐方案表 & 单阶段匹配分布 ===");
            sb.AppendLine("用一个虚拟土壤（适宜度 = 0.2 ~ 1.0）走一遍，看每阶段会命中哪条规则。");
            sb.AppendLine();

            const int steps = 9;
            foreach (var stage in Stages.All)
            {
                sb.AppendLine($"--- {Stages.DisplayName(stage)} ---");
                sb.AppendLine($"规则数：{table.RulesOf(stage).Count}");

                // 逐档测试
                for (int i = 0; i < steps; i++)
                {
                    float v = 0.2f + 0.8f * i / (steps - 1);
                    var fit = new SuitabilityResult(v, v, v, v, v);
                    var plan = table.Match(stage, fit);
                    string flag = table.LastMatchUsedFallback ? "  <<< 走了兜底（文档未定义）" : "";
                    sb.AppendLine($"  适宜度全={v:F2} -> {plan?.Label}  [水{plan?.BaseIrrigation}]{flag}");
                }
                sb.AppendLine();
            }
            _report = sb.ToString();
        }

        // ===================================================================
        //  3. 施肥/灌溉倍率扫描
        // ===================================================================
        private void RunMultiplierScan()
        {
            var cfg = BuildConfig();
            var sb = new StringBuilder();
            sb.AppendLine("=== 3. 施肥 / 灌溉倍率扫描 ===");
            sb.AppendLine("起点取输入区间中值。倍率 = 实际投入 ÷ 建议量。");
            sb.AppendLine("【关注】产量是否随倍率变化。若不变，说明这是达标制 —— 模型没有连续响应。");
            sb.AppendLine();

            sb.AppendLine("--- 施肥倍率（灌溉固定 = 建议）---");
            sb.AppendLine("倍率     Ffert     Y/Ymax");
            foreach (float mult in new[] { 0f, 0.5f, 1f, 1.5f, 2f, 3f })
            {
                var r = RunOne(cfg, 1f, mult);
                sb.AppendLine($"{mult,4:F1}x   {r.fert:F4}   {r.yield / r.ymax:P2}");
            }

            sb.AppendLine();
            sb.AppendLine("--- 灌溉倍率（施肥固定 = 建议）---");
            sb.AppendLine("倍率     Ffert     Y/Ymax");
            foreach (float mult in new[] { 0f, 0.5f, 1f, 1.5f, 2f, 3f })
            {
                var r = RunOne(cfg, mult, 1f);
                sb.AppendLine($"{mult,4:F1}x   {r.fert:F4}   {r.yield / r.ymax:P2}");
            }

            sb.AppendLine();
            sb.AppendLine("【判读】如果 1.0x 到 3.0x 之间数值几乎不动，");
            sb.AppendLine("        说明玩家多施肥没有反馈 —— 这是当前设计已知的问题。");
            _report = sb.ToString();
        }

        private (float fert, float yield, float ymax) RunOne(SimConfig cfg, float waterMult, float fertMult)
        {
            var sim = new Simulation(cfg);
            sim.Initialize(SoilGenerator.MakeMidpointInput(cfg));

            while (!sim.IsFinished)
            {
                var rec = sim.Recommend();
                if (rec == null) break;
                var app = rec.ToApplication(cfg);
                app.Irrigation *= waterMult;
                app.Organic    *= fertMult;
                app.N          *= fertMult;
                app.P          *= fertMult;
                app.K          *= fertMult;
                sim.Commit(app);
            }
            var rep = sim.Revenue();
            return (rep.OverallFertility, rep.Yield, rep.YmaxGeo);
        }

        // ===================================================================
        //  4. 初始区间四角
        // ===================================================================
        private void RunCornerScan()
        {
            var cfg = BuildConfig();
            var sb = new StringBuilder();
            sb.AppendLine("=== 4. 初始土壤区间取不同位置 ===");
            sb.AppendLine("照抄建议方案，看起点土壤对最终产量的影响。");
            sb.AppendLine("【关注】指标填得越高，是否反而得分越低 —— 若是，说明适宜度下降段与输入区间不匹配。");
            sb.AppendLine();

            sb.AppendLine("起点位置            各阶段Ffert                       平均      Y/Ymax");
            foreach (float t in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                var sim = new Simulation(cfg);
                sim.Initialize(SoilGenerator.MakeAtRatio(cfg, t));

                var stageF = new List<string>();
                while (!sim.IsFinished)
                {
                    var rec = sim.Recommend();
                    if (rec == null) break;
                    var res = sim.Commit(rec.ToApplication(cfg));
                    stageF.Add($"{res.StageFertility:F3}");
                }
                var rep = sim.Revenue();
                string label = $"{(t == 0f ? "下界" : t == 1f ? "上界" : $"{(int)(t * 100)}%")}";
                sb.AppendLine($"{label,-18} {string.Join(", ", stageF),-34} {rep.OverallFertility:F4}   {rep.AchievementRate:P2}");
            }
            _report = sb.ToString();
        }

        // ===================================================================
        //  5. 规则覆盖性
        // ===================================================================
        private void RunCoverage()
        {
            var cfg = BuildConfig();
            var table = new PlanTable(cfg);
            var sb = new StringBuilder();
            sb.AppendLine("=== 5. 规则覆盖性检查 ===");
            sb.AppendLine("在阈值相关采样点上枚举全部组合（12^5 ≈ 25 万组 × 4 阶段），");
            sb.AppendLine("检查每个阶段是否都有精确匹配的规则。无匹配即为判定空洞。");
            sb.AppendLine();

            bool ok = table.CheckCoverage(out var report);
            if (ok) sb.AppendLine("★ 通过：文档规则表完备，无判定空洞。");
            else
            {
                sb.AppendLine("★ 规则表不完备（空洞已由兜底规则接管，不会崩，但建议补规则）：");
                foreach (var line in report) sb.AppendLine("  " + line);
            }
            _report = sb.ToString();
        }

        // ===================================================================
        //  6. 连续多轮漂移
        // ===================================================================
        private void RunDrift()
        {
            var cfg = BuildConfig();
            var sb = new StringBuilder();
            sb.AppendLine("=== 6. 连续多轮模拟的指标漂移 ===");
            sb.AppendLine("每轮都照抄建议方案，把上一轮的最终土壤作为下一轮的起点。");
            sb.AppendLine("【关注】OM / N / P / K 是否单调失控（例如 S4 只消耗钾却不施钾）。");
            sb.AppendLine();

            var soil = SoilGenerator.MakeMidpointInput(cfg);
            const int rounds = 6;

            sb.AppendLine("轮次  起始 pH   起始OM   起始N    起始P    起始K    Y/Ymax");
            for (int i = 0; i < rounds; i++)
            {
                var start = soil.Clone();
                var sim = new Simulation(cfg);
                // 静态四项不参与本检查，直接沿用
                sim.Initialize(soil);

                while (!sim.IsFinished)
                {
                    var rec = sim.Recommend();
                    if (rec == null) break;
                    sim.Commit(rec.ToApplication(cfg));
                }
                var rep = sim.Revenue();
                soil = rep.FinalSoil ?? soil;

                sb.AppendLine($"{i + 1,3}   {start.pH,7:F2}  {start.OM,7:F2}  {start.N,7:F2}  " +
                              $"{start.P,7:F2}  {start.K,7:F2}   {rep.AchievementRate:P2}");
            }
            sb.AppendLine();
            sb.AppendLine("【判读】钾若持续下降，说明 S4 的钾消耗（22.5）没有对应的补充建议。");
            _report = sb.ToString();
        }

        // ===================================================================
        //  全部
        // ===================================================================
        private void RunAll()
        {
            var parts = new List<string>();
            RunCurveCheck();             parts.Add(_report);
            RunPlanDistribution();       parts.Add(_report);
            RunMultiplierScan();         parts.Add(_report);
            RunCornerScan();             parts.Add(_report);
            RunCoverage();               parts.Add(_report);
            RunDrift();                  parts.Add(_report);
            _report = string.Join("\n", parts);
            Debug.Log("[批量验证] 完成，可点「复制到剪贴板」取走结果。");
        }
    }
}
