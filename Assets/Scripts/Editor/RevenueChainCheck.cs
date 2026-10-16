// ---------------------------------------------------------------------------
//  RevenueChainCheck.cs —— 收益链路自检
// ---------------------------------------------------------------------------
//  作用：把"收益公式全工程只有一份、各屏拿到的数一致"变成一条能随时重跑的检查。
//
//  【为什么值得做】
//    收益链路是 Simulation.Revenue → RevenueReport.Create → YieldModel.Compute。
//    谁要是把公式抄到别处（UI、批量验证、临时脚本），就会出现
//    "界面显示的 Ffert"和"算 Y 用的 Ffert"不是同一个数 —— 这种错不会报错，
//    只会让数字悄悄偏掉，事后极难发现。
//
//  【检查项】
//    1. 完整四阶段：报告里的 Ffert / Fwater / Y 与独立重算一致，
//       且展开式 Y = Ymax,geo × Ffert × Fwater × Sstatic 成立。
//    2. 只结算 2 阶段：Ffert 只对已结算阶段求平均（退回"未结算按 0 摊"会立刻判失败）。
//    3. 水分权重：Fw,s 全 1 时 Fwater 必须恰好为 1（文档 [248]/[249]）；
//       α 加权按 Σα 归一化（S3 权重 0.4 ⇒ Fw,s3 减半时 Fwater = 0.8）。
//    4. 报告是不可变快照：继续推进模拟不会改动已生成的报告。
//
//  【跑法】
//    菜单：Engineer / 收益链路自检
//    命令行：Unity.exe -batchmode -quit -nographics -projectPath <工程>
//                     -executeMethod Engineer.Editor.RevenueChainCheck.Run -logFile -
//    全部通过时打印 "[收益链路自检] 全部通过"；否则打印 FAILED 并走 Debug.LogError。
// ---------------------------------------------------------------------------

using System.Text;
using UnityEditor;
using UnityEngine;
using Engineer.Core;

namespace Engineer.Editor
{
    /// <summary>收益链路自检。</summary>
    public static class RevenueChainCheck
    {
        /// <summary>浮点比较容差（相对）。</summary>
        private const float Eps = 1e-4f;

        /// <summary>自检用的土地面积（亩）。</summary>
        private const float ReportAreaMu = 10f;

        [MenuItem("Engineer/验证/收益链路自检")]
        private static void RunFromMenu()
        {
            Run();
        }

        /// <summary>
        /// 命令行入口（-executeMethod）。
        /// 【注意】失败时无法直接改进程退出码，请按日志里的 FAILED / [FAIL] 判读。
        /// </summary>
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 收益链路自检（公式唯一性 + 部分结算 + 快照） ===");

            var cfg = SimConfig.CreateDefault();
            int failed = 0;

            failed += CheckFullReport(cfg, RunSimulation(cfg, Stages.Count), sb);
            failed += CheckPartialReport(cfg, RunSimulation(cfg, 2), sb);
            failed += CheckWaterWeights(cfg, sb);
            failed += CheckSnapshot(cfg, sb);

            sb.AppendLine();
            if (failed == 0)
            {
                sb.AppendLine("[收益链路自检] 全部通过（0 项失败）");
                Debug.Log(sb.ToString());
            }
            else
            {
                sb.AppendLine($"[收益链路自检] FAILED：{failed} 项不通过");
                Debug.LogError(sb.ToString());
            }
        }

        // ------------------------------------------------------------------
        //  检查项
        // ------------------------------------------------------------------

        /// <summary>1. 完整四阶段：报告与独立重算一致。</summary>
        private static int CheckFullReport(SimConfig cfg, Simulation sim, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 1. 完整四阶段 ---");

            var report = sim.Revenue(ReportAreaMu);
            int failed = 0;

            // 独立重算：特意不调用 YieldModel，避免"用同一段代码验证它自己"
            float manualFert = ManualAverage(report.StageFertility, report.CommittedCount);
            float manualY = cfg.Yield.YmaxGeo * manualFert * report.WaterFactor * report.StaticFactor;

            failed += Expect(Approx(report.OverallFertility, manualFert),
                $"Ffert 与重算一致：报告 {report.OverallFertility:F6} / 重算 {manualFert:F6}", sb);
            failed += Expect(Approx(report.Yield, manualY),
                $"Y 与重算一致：报告 {report.Yield:F6} / 重算 {manualY:F6}", sb);
            failed += Expect(Approx(report.WaterFactor, YieldModel.SimplifiedWaterFactor),
                $"Fwater = {report.WaterFactor:F4}（v1 应为 1）", sb);
            failed += Expect(!report.IsPartial && report.CommittedCount == Stages.Count,
                $"状态：CommittedCount={report.CommittedCount} IsPartial={report.IsPartial}（应为 {Stages.Count} / False）", sb);
            failed += Expect(Approx(report.AchievementRate, report.Yield / cfg.Yield.YmaxGeo),
                $"达成率 = Y / Ymax:geo = {report.AchievementRate:P2}", sb);
            failed += Expect(Approx(report.TotalYield, report.Yield * ReportAreaMu),
                $"总产量 = Y × 面积({ReportAreaMu:0.#} 亩) = {report.TotalYield:F2} kg", sb);
            failed += Expect(report.FinalSoil != null, "报告带最终土壤（界面要用它显示四个静态指标）", sb);

            sb.AppendLine($"  摘要：Ffert={report.OverallFertility:F4} Fwater={report.WaterFactor:F2} " +
                          $"Y={report.Yield:F2} kg/亩 达成率={report.AchievementRate:P1}");
            return failed;
        }

        /// <summary>2. 只结算 2 阶段：未结算阶段不能按 0 摊进平均。</summary>
        private static int CheckPartialReport(SimConfig cfg, Simulation sim, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 2. 只结算 2 个阶段（中间结果）---");

            var report = sim.Revenue();
            int failed = 0;

            float manual = ManualAverage(report.StageFertility, report.CommittedCount);
            float zeroPadded = ManualAverage(report.StageFertility, Stages.Count);   // 旧算法的结果

            failed += Expect(report.IsPartial && report.CommittedCount == 2,
                $"状态：CommittedCount={report.CommittedCount} IsPartial={report.IsPartial}（应为 2 / True）", sb);
            failed += Expect(report.OverallFertility >= zeroPadded - Eps,
                $"Ffert 只对已结算阶段求平均：{report.OverallFertility:F6}" +
                $"（旧算法会把未结算阶段按 0 摊成 {zeroPadded:F6}）", sb);
            failed += Expect(Approx(report.OverallFertility, manual),
                $"Ffert 与重算一致：报告 {report.OverallFertility:F6} / 重算 {manual:F6}", sb);
            failed += Expect(Approx(report.Yield,
                    cfg.Yield.YmaxGeo * report.OverallFertility * report.WaterFactor * report.StaticFactor),
                $"中间态的 Y 同样满足 Y = Ymax,geo × Ffert × Fwater × Sstatic = {report.Yield:F2} kg/亩", sb);

            sb.AppendLine($"  摘要：{report}");
            return failed;
        }

        /// <summary>3. 水分因子的取值规则（挂起项 D2 接入前就必须正确）。</summary>
        private static int CheckWaterWeights(SimConfig cfg, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 3. 水分因子与 α 权重 ---");

            int failed = 0;

            // 全 1 ⇒ Fwater = 1（文档 [248] 的取值 + [249] 的结论）
            float ones = YieldModel.WaterFactor(YieldModel.SimplifiedWaterFactors(), cfg.Yield);
            failed += Expect(Approx(ones, 1f), $"Fw,s 全为 1 ⇒ Fwater = 1（实得 {ones:F6}）", sb);

            // α = 0.2/0.2/0.4/0.2，只有 S3 减半 ⇒ Fwater = 1 − 0.4 × 0.5 = 0.8
            var factors = new[] { 1f, 1f, 0.5f, 1f };
            float weighted = YieldModel.WaterFactor(factors, cfg.Yield);
            failed += Expect(Approx(weighted, 0.8f),
                $"α 加权（S3 权重 0.4）× Fw,s={{1,1,0.5,1}} ⇒ Fwater = 0.8（实得 {weighted:F6}；" +
                "按文档字面 ÷4 会得 0.2，与 [249] 矛盾）", sb);

            // 报告要能自己说明"水分是简化处理"，界面不许写死这句话
            var detailed = RevenueReport.Create(new[] { 0.5f, 0.5f, 0.5f, 0.5f }, factors, cfg.Yield, 0f, null, Stages.Count);
            failed += Expect(YieldModel.IsSimplifiedWater(YieldModel.SimplifiedWaterFactors()),
                "全 1 被识别为 v1 简化（IsSimplifiedWater = true）", sb);
            failed += Expect(!detailed.IsWaterSimplified && detailed.WaterFactor < 1f,
                $"接了 Fw,s 后报告自动改口：IsWaterSimplified={detailed.IsWaterSimplified}、" +
                $"Fwater={detailed.WaterFactor:F4}，文案「{detailed.WaterNote}」", sb);

            return failed;
        }

        /// <summary>4. 报告是不可变快照：后续推进不会改动它。</summary>
        private static int CheckSnapshot(SimConfig cfg, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("--- 4. 报告快照 ---");

            var sim = RunSimulation(cfg, 1);
            var before = sim.Revenue();
            float fertBefore = before.OverallFertility;
            float yieldBefore = before.Yield;

            var rec = sim.Recommend();
            sim.Commit(rec.ToApplication(cfg));
            var after = sim.Revenue();

            int failed = 0;
            failed += Expect(Approx(before.OverallFertility, fertBefore) && Approx(before.Yield, yieldBefore),
                $"旧报告没有被后续推进改写：Ffert={before.OverallFertility:F6}（提交前 {fertBefore:F6}）", sb);
            failed += Expect(!ReferenceEquals(before.StageWaterFactors, sim.StageWaterFactors()),
                "旧报告持有的是水分因子的副本，不是 Simulation 的内部数组", sb);
            failed += Expect(!ReferenceEquals(before.FinalSoil, sim.CurrentSoil),
                "旧报告持有的是土壤的副本，不是 Simulation 的内部状态", sb);
            failed += Expect(after.CommittedCount == before.CommittedCount + 1,
                $"新报告跟着推进走：CommittedCount {before.CommittedCount} → {after.CommittedCount}", sb);

            return failed;
        }

        // ------------------------------------------------------------------
        //  工具
        // ------------------------------------------------------------------

        /// <summary>照抄推荐方案跑若干阶段。</summary>
        private static Simulation RunSimulation(SimConfig cfg, int stages)
        {
            var sim = new Simulation(cfg);
            sim.Initialize(SoilGenerator.MakeMidpointInput(cfg));

            for (int i = 0; i < stages && !sim.IsFinished; i++)
            {
                var rec = sim.Recommend();
                if (rec == null) break;
                sim.Commit(rec.ToApplication(cfg));
            }
            return sim;
        }

        /// <summary>
        /// 自检自己实现一遍"取前 count 项的平均" ——
        /// 特意不调用 YieldModel.OverallFertility，避免"用同一段代码验证它自己"。
        /// </summary>
        private static float ManualAverage(float[] values, int count)
        {
            if (values == null || count <= 0) return 0f;

            int n = Mathf.Min(count, values.Length);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += values[i];
            return sum / n;
        }

        private static bool Approx(float a, float b)
            => Mathf.Abs(a - b) <= Eps * Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));

        private static int Expect(bool ok, string message, StringBuilder sb)
        {
            sb.AppendLine((ok ? "  [OK]   " : "  [FAIL] ") + message);
            return ok ? 0 : 1;
        }
    }
}
