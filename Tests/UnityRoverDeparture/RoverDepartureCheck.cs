using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Engineer.UI;
[InitializeOnLoad]
public static class RoverDepartureCheck
{
    static int phase, checks; static double since; static FarmCruiseController cruise;
    static FarmFieldGrid grid; static FarmEquipment equipment; static FarmCameraRig rig; static Transform robot;
    static RoverDepartureCheck(){EditorApplication.update+=Tick;}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Debug.Log("[Departure] PASS "+label);}
    public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool("departure-check",true);EditorApplication.isPlaying=true;}
    static void Tick()
    {
        if(!SessionState.GetBool("departure-check",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try {
            if(phase==0){
                var root=new GameObject("Farm");root.SetActive(false);
                var field=root.AddComponent<FarmFieldBuilder>();field.UseSavedSceneLayout();
                grid=root.AddComponent<FarmFieldGrid>();Set(grid,"showUncoveredMask",false);
                equipment=root.AddComponent<FarmEquipment>();
                foreach(var key in new[]{"cameraTravelSeconds","doorOpenSeconds"})Set(equipment,key,.06f);
                var rv=new GameObject("RV");rv.transform.SetParent(root.transform,false);rv.transform.position=new Vector3(-10,0,-10);
                var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(rv.transform,false);body.transform.localScale=new Vector3(8,3,3);
                var bot=new GameObject("ROVER_ROOT");bot.transform.SetParent(rv.transform,false);bot.transform.localPosition=new Vector3(-3,.5f,2);robot=bot.transform;
                var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.transform.SetParent(robot,false);
                var old=new GameObject("Rover");old.transform.SetParent(root.transform,false);
                cruise=root.AddComponent<FarmCruiseController>();root.SetActive(true);
                var cameraGo=new GameObject("Camera",typeof(Camera));rig=cameraGo.AddComponent<FarmCameraRig>();
                Check(cruise.Rover!=null&&robot.IsChildOf(cruise.Rover),"actual RV robot adopted by patrol carrier");
                Check(!old.activeSelf,"old standalone rover disabled");
                Vector3 shot;Quaternion shotRotation;rig.GetPatrolShot(robot.position,out shot,out shotRotation);
                Check(Vector3.Distance(shot,robot.position)>=12f && shot.y-robot.position.y>=7f,
                    "half follow shot stays far enough behind and above robot");
                var direction=shotRotation*Vector3.forward;
                Check(Vector3.Dot(direction,(robot.position-shot).normalized)>.999f,"half follow shot aims at robot");
                var ground=(Vector3)typeof(FarmCruiseController).GetMethod("GroundHeight",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cruise,new object[]{Vector3.zero});
                var bottom=cruise.Rover.GetComponentInChildren<Renderer>().bounds.min.y;
                Check(Mathf.Abs(ground.y-(cruise.Rover.position.y-bottom)-field.Config.SoilRidgeTopY)<.001f,
                    "ground height accounts for actual embedded robot pivot offset");
                cruise.ResetToStart();cruise.StartManual();
                Check(cruise.IsDeploying&&cruise.Mode==FarmCruiseController.CruiseMode.Idle,"manual waits for deployment instead of accepting input");
                Check(grid.Coverage==0f,"deployment does not fabricate coverage");
                since=EditorApplication.timeSinceStartup;phase=1;
            } else if(phase==1){
                if(cruise.IsDeploying){if(EditorApplication.timeSinceStartup-since>120)throw new Exception("deployment timeout");return;}
                Check(cruise.Mode==FarmCruiseController.CruiseMode.Manual,"departure finishes into manual mode");
                Check(!(bool)typeof(FarmCameraRig).GetField("deploymentShot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig),"camera control released after departure");
                Check(robot.IsChildOf(cruise.Rover),"same RV robot remains the patrol vehicle");
                cruise.ResetToStart();cruise.StartAuto();
                Check(cruise.IsDeploying,"automatic patrol also plays departure");
                since=EditorApplication.timeSinceStartup;phase=2;
            } else if(phase==2){
                if(cruise.IsDeploying){if(EditorApplication.timeSinceStartup-since>120)throw new Exception("auto deployment timeout");return;}
                Check(cruise.Mode==FarmCruiseController.CruiseMode.Auto,"departure finishes into automatic mode");
                cruise.ResetToStart();cruise.StartManual();cruise.Stop();
                Check(!cruise.IsDeploying&&cruise.Mode==FarmCruiseController.CruiseMode.Idle,"interrupted departure cancels safely");
                Check(!(bool)typeof(FarmCameraRig).GetField("deploymentShot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(rig),"interrupted departure releases camera");
                Debug.Log("[Departure] ALL "+checks+" PASSED");SessionState.SetBool("departure-check",false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
