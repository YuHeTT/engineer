using System;
using Engineer.Core;
using Engineer.UI;

// 独立于 Unity 的回归入口；预期值用固定数值或阶段历史独立重算。
static class Program
{
    static int count;
    static void Check(bool ok, string name)
    {
        count++;
        if (!ok) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS " + name);
    }
    static bool Near(float a, float b) => Math.Abs(a-b) < 0.001f;
    static void Throws(Action action, string name)
    {
        bool thrown = false;
        try { action(); } catch (ArgumentException) { thrown = true; }
        Check(thrown, name);
    }
    static void Main()
    {
        float v;
        foreach (var text in new[] { "", "abc", "NaN", "Infinity", "-Infinity", "1e999", "1,200", "１２，３" })
            Check(!MathX.TryParseInput(text, out v), "拒绝非法输入 " + text);
        Check(MathX.TryParseInput(" １２．５ ", out v) && Near(v,12.5f), "全角小数");
        Check(MathX.TryParseInput("1e2", out v) && Near(v,100), "科学计数法");
        var cfg = SimConfig.CreateDefault();
        var one = new float[] {1,1,1,1};
        var report = RevenueReport.Create(one, one, cfg.Yield, 4, pricePerKg:2, totalCost:100);
        Check(Near(report.Yield,733), "全优单亩产量733");
        Check(Near(report.TotalYield,2932), "4亩总产量2932kg");
        Check(Near(report.GrossIncome,5864) && Near(report.NetIncome,5764), "显式经济输入毛收入及净收益");
        var missing = RevenueReport.Create(one,one,cfg.Yield,pricePerKg:2);
        Check(!missing.HasArea && Near(missing.TotalYield,0) && !missing.HasEconomics, "无面积不冒充总产量和金额");
        Check(!RevenueReport.Create(one,one,cfg.Yield,4).HasEconomics, "默认不启用经济层");
        var partial = RevenueReport.Create(new float[]{1,0.5f,0,0},new float[]{1,0.5f,0,0},cfg.Yield,4,committedCount:2);
        Check(partial.IsPartial && Near(partial.OverallFertility,0.75f), "未结算阶段不摊入肥力平均");
        Check(Near(partial.WaterFactor,0.75f), "未结算水分因子不摊入平均");
        Check(Near(partial.Yield,412.3125f), "中间产量固定算例");
        one[0]=0;
        Check(Near(report.StageFertility[0],1) && Near(report.StageWaterFactors[0],1), "报告数组快照");
        Throws(()=>RevenueReport.Create(one,one,cfg.Yield,float.NaN),"拒绝NaN面积");
        Throws(()=>RevenueReport.Create(one,one,cfg.Yield,-1),"拒绝负面积");
        Throws(()=>RevenueReport.Create(one,one,cfg.Yield,4,pricePerKg:-1),"拒绝负单价");
        Throws(()=>RevenueReport.Create(one,one,cfg.Yield,4,totalCost:float.PositiveInfinity),"拒绝无穷成本");
        Throws(()=>YieldModel.Compute(new float[]{float.NaN},null,cfg.Yield),"拒绝NaN肥力");
        Throws(()=>YieldModel.Compute(new float[]{1},new float[]{float.NaN},cfg.Yield),"NaN水分不能伪装简化值");
        var session = new GameSession(cfg);
        Check(session.CommitFromDosage(new float[5])==null,"未开始不提交");
        session.Begin(GameSession.BuildInput(6.5f,25,90,15,150),new Random(42));
        var before = session.Soil.Clone();
        var staticValues = new[]{before.Eh,before.BaseIon,before.As,before.Hg};
        foreach(var invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            Check(session.CommitFromDosage(new[]{invalid,1f,2f,3f,4f})==null && session.UiStage==1,
                "非法水肥不推进 " + invalid);
            Check(Near(session.Soil.N,before.N),"非法提交不改土壤");
        }
        Check(session.CommitFromDosage(null)==null && session.CommitFromDosage(new float[4])==null,"缺失水肥数组不提交");
        float sum=0;
        for(int stage=1;stage<=4;stage++)
        {
            var oldReport=session.Revenue(4);
            var result=session.CommitFromDosage(new[]{10f,1f,2f,3f,4f});
            Check(result!=null && session.UiStage==stage+1,"推进阶段"+stage);
            Check(Near(result.Applied.K,2) && Near(result.Applied.N,3) && Near(result.Applied.P,4),"UI钾氮磷顺序"+stage);
            sum+=result.StageFertility;
            Check(oldReport.CommittedCount==stage-1,"旧报告结算次数不变"+stage);
            Check(Near(session.Soil.Eh,staticValues[0]) && Near(session.Soil.BaseIon,staticValues[1])
                && Near(session.Soil.As,staticValues[2]) && Near(session.Soil.Hg,staticValues[3]),"静态四项不参与演变"+stage);
        }
        var final=session.Revenue(4);
        Check(session.IsFinished && !final.IsPartial && final.CommittedCount==4,"四阶段最终报告");
        Check(Near(final.Yield,733*sum/4) && Near(final.TotalYield,final.Yield*4),"最终产量与历史一致");
        Check(session.CommitFromDosage(new float[5])==null && session.UiStage==5,"结束后重复提交不推进");
        Check(final.HasInputSummary && Near(final.CumulativeInputPerMu.Irrigation,40)
            && Near(final.TotalInput.Irrigation,160) && Near(final.TotalInput.Organic,16)
            && Near(final.TotalInput.K,32) && Near(final.TotalInput.N,48) && Near(final.TotalInput.P,64),
            "累计实际投入按亩数换算全田投入");
        Check(Near(session.Revenue(4).TotalInput.Irrigation,160),"重复查询报告不重复累计");
        session.Restart();
        Check(!session.IsStarted && session.Revenue(4).CommittedCount==0 && Near(session.Revenue(4).Yield,0),"重开清空旧收益");
        Check(Near(session.Revenue(4).TotalInput.Irrigation,0),"重开清空累计投入");
        var limited = new GameSession(cfg);
        limited.Begin(GameSession.BuildInput(6.5f,25,90,15,150));
        var capped = limited.CommitFromDosage(new[]{100000f,100000f,100000f,100000f,100000f});
        Check(capped != null && capped.Applied.Irrigation <= cfg.Inputs.AbsoluteMaxIrrigation,
            "超量输入保留既有上限钳制");
        Check(Near(limited.Revenue(4).TotalInput.Irrigation,capped.Applied.Irrigation*4)
            && Near(limited.Revenue(4).TotalInput.N,capped.Applied.N*4),"累计采用钳制后实际投入");
        var sim = new Simulation(cfg);
        sim.Initialize(GameSession.BuildInput(6.5f,25,90,15,150));
        Throws(()=>sim.Initialize(GameSession.BuildInput(float.NaN,25,90,15,150)),"拒绝非法初始土壤");
        Check(Near(sim.CurrentSoil.pH,6.5f) && sim.CommittedCount==0,"非法初始化不破坏旧局");
        var observedSession = new GameSession(cfg);
        observedSession.Begin(GameSession.BuildInput(6.5f,25,90,15,150), new Random(12));
        var probe = new FarmProbeState();
        probe.Begin(observedSession);
        Check(!probe.CanAnalyze(observedSession) && probe.AnalysisSoil(observedSession)==null,"零覆盖不能分析也不泄漏真值");
        probe.SetCoverage(0.8f);
        Check(!probe.Confirm(observedSession) && !probe.CanAnalyze(observedSession),"80%仍不能确认");
        float firstProbe = probe.GetProbe(0);
        probe.Begin(observedSession);
        Check(Near(probe.Coverage,0.8f) && Near(probe.GetProbe(0),firstProbe),"重进巡航页保留覆盖和探测值");
        probe.SetCoverage(1f);
        Check(probe.RevealedCount==9 && !probe.CanAnalyze(observedSession),"覆盖完成但未确认仍不可分析");
        Check(probe.Confirm(observedSession) && probe.CanAnalyze(observedSession),"100%确认后允许分析");
        var observation=probe.AnalysisSoil(observedSession);
        Check(observation!=null && Near(observation.N,probe.GetProbe(2)),"分析快照采用探测值");
        float saved=observation.N; observation.N=999;
        Check(Near(probe.AnalysisSoil(observedSession).N,saved),"外部修改不会污染确认快照");
        bool fallback;
        var observationForPlan = probe.AnalysisSoil(observedSession);
        var planFromProbe = observedSession.RecommendFromObservation(observationForPlan,out fallback);
        var independentPlan = new PlanTable(cfg).Match(Stage.S1,SuitabilityResult.From(observationForPlan,cfg));
        Check(planFromProbe!=null && planFromProbe.Label==independentPlan.Label,"建议来自观测快照");
        Check(Near(observedSession.Soil.N,90),"探测与推荐不改写真实土壤");
        var frozen=probe.GetProbe(0);probe.SetCoverage(1f);
        Check(Near(probe.GetProbe(0),frozen),"确认后的值稳定不跳动");
        probe.SetCoverage(0f);
        Check(!probe.CanAnalyze(observedSession) && probe.RevealedCount==0,"重新扫描使旧确认失效");
        probe.SetCoverage(1f);probe.Confirm(observedSession);
        observedSession.CommitFromDosage(new float[5]);
        Check(!probe.CanAnalyze(observedSession),"下一阶段不能复用上一阶段确认");
        probe.Begin(observedSession);
        Check(Near(probe.Coverage,0) && !probe.IsConfirmed,"新阶段清零采集状态");
        probe.SetCoverage(1f);probe.Confirm(observedSession);
        observedSession.Begin(GameSession.BuildInput(6.5f,25,90,15,150));
        Check(!probe.CanAnalyze(observedSession),"同一Session重开新局旧快照失效");
        probe.Reset();
        Check(!probe.IsActive && !probe.IsConfirmed && probe.RevealedCount==0,"重置清空采集数据");
        Console.WriteLine("ALL PASSED: " + count);
    }
}
