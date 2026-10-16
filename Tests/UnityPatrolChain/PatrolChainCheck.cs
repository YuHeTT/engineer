using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Engineer.Core;
using Engineer.UI;

[InitializeOnLoad]
public static class PatrolChainCheck
{
    static int checks;
    static PatrolChainCheck() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("patrol-check",true);EditorApplication.isPlaying=true; }
    static void Set(object target,string name,object value)
        => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Check(bool ok,string name)
    { checks++;if(!ok)throw new Exception(name);Debug.Log("[PatrolChain] PASS "+name); }
    static void Tick()
    {
        if(!SessionState.GetBool("patrol-check",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool("patrol-check",false);
        try
        {
            var go=new GameObject("Fixture");go.SetActive(false);
            var flow=go.AddComponent<GameFlowController>();
            var session=new GameSession(SimConfig.CreateDefault());
            session.Begin(GameSession.BuildInput(6.5f,25,90,15,150),new System.Random(42));
            Set(flow,"session",session);flow.Context.BindSession(session);
            var grid=go.AddComponent<FarmFieldGrid>();
            var cells=new FarmFieldGrid.Cell[5];
            for(int i=0;i<5;i++)cells[i]=new FarmFieldGrid.Cell{Center=new Vector2(i*2,0),Area=1,SizeX=2,SizeZ=2};
            Set(grid,"cells",cells);Set(grid,"built",true);Set(grid,"totalArea",5f);
            Set(flow,"probeGrid",grid);
            var screens=new List<PlaceholderScreen>();
            foreach(var state in new[]{GameState.FarmStage,GameState.RobotCruise,GameState.Analyze})
            {
                var screenGo=new GameObject(state.ToString());
                var screen=screenGo.AddComponent<PlaceholderScreen>();screen.SetState(state);
                screens.Add(screen);
            }
            flow.SetScreens(screens);
            flow.GoTo(GameState.Analyze);
            Check(flow.Current==GameState.RobotCruise,"direct Analyze navigation redirects without confirmed patrol");
            Check(flow.CommitStageDosage()==null&&session.UiStage==1,"backend submit rejects missing patrol");
            grid.ForceCoverage(0.8f);flow.SyncProbe();
            Check(!flow.ConfirmProbe(),"80 percent cannot confirm");
            int scanned=grid.MarkScannedSegment(new Vector3(-1,0,0),new Vector3(9,0,0),0.2f);
            Check(scanned==1&&grid.Coverage==1f,"long movement segment covers skipped final cell");
            flow.SyncProbe();
            Check(!flow.CanAnalyze,"100 percent still requires confirmation");
            Check(flow.ConfirmProbe(),"confirmed completed patrol accepted");
            flow.GoTo(GameState.Analyze);
            Check(flow.Current==GameState.Analyze,"confirmed patrol enters analysis");
            var truth=session.Soil.Clone();
            var observation=flow.Probe.AnalysisSoil(session);
            Check(observation!=null&&session.Soil.N==truth.N,"observation snapshot leaves truth unchanged");
            var result=flow.CommitStageDosage();
            Check(result!=null&&session.UiStage==2,"confirmed stage submits once");
            Check(grid.Coverage==0f&&!flow.CanAnalyze,"stage submit resets grid and confirmation immediately");
            Check(flow.CommitStageDosage()==null&&session.UiStage==2,"second submission cannot reuse previous stage patrol");
            flow.GoTo(GameState.Analyze);
            Check(flow.Current==GameState.RobotCruise,"next stage Analyze is guarded");
            grid.ClearCoverage();
            Check(grid.MarkScannedSegment(new Vector3(-1,0,0),new Vector3(9,0,0),0.2f)==5,
                "low FPS traversal covers all five cells without endpoint gaps");
            var cruise=go.AddComponent<FarmCruiseController>();
            Set(cruise,"grid",grid);grid.ForceCoverage(0.4f);cruise.FinishNow();
            Check(Mathf.Approximately(grid.Coverage,0.4f),"early stop does not fabricate coverage");
            var rover=new GameObject("TestRover");Set(cruise,"rover",rover);
            var waypoints=(List<Vector3>)typeof(FarmCruiseController).GetField("waypoints",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cruise);
            waypoints.Add(Vector3.zero);Set(cruise,"waypointIndex",1);
            Set(cruise,"mode",FarmCruiseController.CruiseMode.Auto);
            var updateAuto=typeof(FarmCruiseController).GetMethod("UpdateAuto",BindingFlags.Instance|BindingFlags.NonPublic);
            updateAuto.Invoke(cruise,null);
            Check(cruise.Mode==FarmCruiseController.CruiseMode.Auto&&waypoints.Count==4&&Mathf.Approximately(grid.Coverage,0.4f),
                "route end appends three real unscanned cells without fabricating coverage");
            for(int i=1;i<waypoints.Count;i++)
            {
                rover.transform.position=waypoints[i];
                updateAuto.Invoke(cruise,null);
            }
            updateAuto.Invoke(cruise,null);
            Check(grid.Coverage==1f&&cruise.Mode==FarmCruiseController.CruiseMode.Finished,
                "automatic completion requires physically scanning remaining cell centers");
            // 巡航按钮应展示 RobotCruise HUD；进度条按实际覆盖面积裁切。
            var hudGo=new GameObject("CruiseHud");
            var hud=hudGo.AddComponent<RobotCruiseScreen>();hud.SetState(GameState.RobotCruise);
            var backButton=new GameObject("Back",typeof(RectTransform),typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
            hud.AddAction(backButton,ScreenActionKind.GoTo,GameState.FarmStage);hud.SetFlow(flow);
            var farmGo=new GameObject("FarmPage");var farmPage=farmGo.AddComponent<FarmStageScreen>();farmPage.SetState(GameState.FarmStage);
            var bar=new GameObject("Bar",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(UnityEngine.UI.Image));fill.transform.SetParent(bar.transform,false);
            hud.coverageFill=fill.GetComponent<UnityEngine.UI.Image>();
            hud.switchToFarmOnCruise=true; // 旧场景的开关也不能把玩家送离进度页。
            Set(hud,"grid",grid);Set(hud,"cruise",cruise);
            Set(grid,"showUncoveredMask",false);
            var field=go.AddComponent<FarmFieldBuilder>();Set(cruise,"field",field);
            farmPage.SetFlow(flow);
            var analyzePage=new GameObject("AnalysisPage").AddComponent<PlaceholderScreen>();analyzePage.SetState(GameState.Analyze);
            flow.SetScreens(new List<PlaceholderScreen>{farmPage,hud,analyzePage});flow.GoTo(GameState.FarmStage);
            hud.OnManualCruise();
            Check(flow.Current==GameState.RobotCruise&&cruise.Mode==FarmCruiseController.CruiseMode.Manual,
                "manual cruise enters progress HUD and retains manual mode");
            flow.GoTo(GameState.FarmStage);hud.OnAutoCruise();
            Check(flow.Current==GameState.RobotCruise&&cruise.Mode==FarmCruiseController.CruiseMode.Auto,
                "automatic cruise enters progress HUD and retains auto mode");
            var refreshHud=typeof(RobotCruiseScreen).GetMethod("RefreshCruiseState",BindingFlags.Instance|BindingFlags.NonPublic);
            grid.ForceCoverage(.4f);flow.SyncProbe();refreshHud.Invoke(hud,null);
            Check(Mathf.Approximately(hud.coverageFill.rectTransform.anchorMax.x,.4f),
                "progress bar width tracks actual 40 percent coverage without requiring sprite");
            grid.ForceCoverage(1f);flow.SyncProbe();refreshHud.Invoke(hud,null);
            Check(hud.coverageFill.rectTransform.anchorMax.x==1f&&hud.coverageFill.color.g>hud.coverageFill.color.b,
                "completed progress bar fills and turns green");
            Check(!flow.CanAnalyze,"full HUD progress still requires explicit data confirmation");
            hud.OnUpdateData();
            Check(flow.Current==GameState.RobotCruise&&flow.CanAnalyze,"update confirms snapshot and stays on patrol HUD");
            var firstObservation=flow.Probe.AnalysisSoil(session);
            hud.OnUpdateData();
            Check(flow.Probe.AnalysisSoil(session).N==firstObservation.N,"repeat update does not reroll confirmed observation");
            backButton.onClick.Invoke();
            Check(flow.Current==GameState.FarmStage&&flow.CanAnalyze,"return button opens farm and preserves confirmed data");
            typeof(FarmStageScreen).GetMethod("OnPrimaryClicked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(farmPage,null);
            Check(flow.Current==GameState.Analyze,"farm start analysis explicitly opens analysis page");
            flow.GoTo(GameState.RobotCruise);hud.OnManualCruise();
            Check(!flow.CanAnalyze&&grid.Coverage==0f,"new patrol invalidates old confirmation and clears coverage");
            grid.ForceCoverage(1f);flow.SyncProbe();hud.OnUpdateData();
            var secondObservation=flow.Probe.AnalysisSoil(session);
            Check(flow.Current==GameState.RobotCruise&&flow.CanAnalyze&&session.UiStage==2,
                "second patrol confirms without advancing game stage");
            Check(secondObservation.N!=firstObservation.N||secondObservation.P!=firstObservation.P||secondObservation.K!=firstObservation.K,
                "new patrol independently samples observation error");
            Debug.Log("[PatrolChain] ALL "+checks+" PASSED");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogError("[PatrolChain] FAILED "+error);EditorApplication.Exit(1);}
    }
}
