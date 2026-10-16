using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Engineer.UI;
using Engineer.UI.EditorTools;
[InitializeOnLoad]
public static class FarmEditPreviewCheck
{
    static int count;
    static FarmEditPreviewCheck() { EditorApplication.update += Tick; }
    static void Check(bool ok,string message) { if(!ok)throw new Exception(message); count++; Debug.Log("[FarmLayout] PASS " + message); }
    public static void Run()
    {
        try {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var model=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var prefab=PrefabUtility.SaveAsPrefabAsset(model,"Assets/LayoutModel.prefab");
            UnityEngine.Object.DestroyImmediate(model);
            var root=new GameObject("FarmSource");var field=root.AddComponent<FarmFieldBuilder>();
            field.Config.BlockCount=1;field.Config.BlockColumns=1;field.Config.BlockRows=1;
            field.SetPrefabs(prefab,new[]{prefab,prefab,prefab,prefab,prefab,prefab,prefab,prefab,prefab});
            root.AddComponent<FarmFieldPaths>();root.AddComponent<FarmEquipment>();root.AddComponent<FarmFieldGrid>();root.AddComponent<FarmCruiseController>();
            FarmSceneAuthoring.PrepareEditableFarm(field);
            Check(field.UsesSavedSceneLayout && field.IsBuilt,"farm becomes editable authored layout");
            var soil=root.transform.Find("Soil").GetChild(0);soil.localPosition=new Vector3(123,4,5);
            var corn=root.transform.Find("Plants/Stage_3").GetChild(0);corn.localPosition=new Vector3(6,7,8);
            var total=root.GetComponentsInChildren<Transform>(true).Length;
            FarmSceneAuthoring.EnsureOpenScenes();
            Check(total==root.GetComponentsInChildren<Transform>(true).Length && soil.localPosition.x==123,"automatic scene check preserves manual edits");
            EditorSceneManager.SaveScene(scene,"Assets/SavedLayoutCheck.unity");
            EditorSceneManager.OpenScene("Assets/SavedLayoutCheck.unity");
            field=UnityEngine.Object.FindObjectOfType<FarmFieldBuilder>();
            Check(field.transform.Find("Soil").GetChild(0).localPosition.x==123,"Ctrl S and reopen retain dragged soil");
            Check(field.transform.Find("Soil").GetChild(0).GetComponentInChildren<MeshFilter>().sharedMesh!=null,"saved combined mesh survives reopen");
            SessionState.SetBool("layout-play-check",true);EditorApplication.isPlaying=true;
        } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool("layout-play-check",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        SessionState.SetBool("layout-play-check",false);
        try {
            var field=UnityEngine.Object.FindObjectOfType<FarmFieldBuilder>();
            Check(field.transform.Find("Soil").GetChild(0).localPosition.x==123,"Play reuses manually positioned soil");
            Check(field.transform.Find("Rover")!=null,"Play reuses saved rover");
            int rovers=0;foreach(Transform child in field.transform)if(child.name=="Rover")rovers++;
            Check(rovers==1,"no duplicate rover after Awake");
            field.ApplyStage(FarmFieldBuilder.CornStage.None,field.CurrentGrowth);
            field.ApplyStage(FarmFieldBuilder.CornStage.Seedling,field.CurrentGrowth);
            Check(field.transform.Find("Plants/Stage_1").GetChild(0).localPosition==new Vector3(6,7,8),"stage switch retains dragged corn layout");
            field.Build();
            Check(field.transform.Find("Soil").GetChild(0).localPosition.x==123,"runtime rebuild request preserves authored terrain");
            EditorApplication.Exit(0);
        } catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
