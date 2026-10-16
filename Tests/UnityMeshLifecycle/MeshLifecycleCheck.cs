using System;
using UnityEditor;
using UnityEngine;
using Engineer.UI;

// 在独立临时 Unity 工程的真实 Play 模式运行，验证 Destroy 的帧末语义。
[InitializeOnLoad]
public static class MeshLifecycleCheck
{
    static MeshLifecycleCheck() { EditorApplication.update += Tick; }
    public static void Run()
    {
        SessionState.SetBool("mesh-check-pending", true);
        EditorApplication.isPlaying = true;
    }
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("[MeshLifecycle] PASS " + message);
    }
    static void Tick()
    {
        if (!SessionState.GetBool("mesh-check-pending", false) || !EditorApplication.isPlaying
            || EditorApplication.isCompiling || EditorApplication.isPaused) return;
        SessionState.SetBool("mesh-check-pending", false);
        try
        {
            var root = new GameObject("SyntheticSource");
            var material = new Material(Shader.Find("Standard"));
            for (int i=0;i<2;i++)
            {
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.transform.SetParent(root.transform,false);
                part.transform.localPosition=new Vector3(i*2,0,0);
                part.GetComponent<Renderer>().sharedMaterial=material;
            }
            var template=MeshCombiner.CombinePrefab(root);
            Assert(template.GetComponentsInChildren<MeshRenderer>(true).Length==1,
                "source renderers removed from template immediately, before frame end");
            Assert(!template.activeSelf,"cached template is inactive");
            var clone=UnityEngine.Object.Instantiate(template);
            clone.SetActive(true);
            Assert(clone.GetComponentsInChildren<MeshRenderer>(true).Length==1,
                "same-frame clone contains only one merged renderer");
            Assert(clone.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length==72,
                "two cubes retain exactly 24 triangles without duplicate geometry");
            var large=new GameObject("LargeSource");
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Part");go.transform.SetParent(large.transform,false);
                var mesh=new Mesh();var vertices=new Vector3[36000];
                vertices[1]=Vector3.right;vertices[2]=Vector3.up;
                mesh.vertices=vertices;mesh.triangles=new[]{0,1,2};mesh.RecalculateNormals();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
            var merged=MeshCombiner.CombinePrefab(large);
            Assert(merged.GetComponentsInChildren<MeshRenderer>(true).Length==1,
                "single-object merge does not split at 65535 vertices");
            var output=merged.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            Assert(output.vertexCount==72000 && output.indexFormat==UnityEngine.Rendering.IndexFormat.UInt32,
                "large merged mesh preserves vertices with 32-bit indices");
            Assert(output.triangles.Length==6,"large merge preserves exactly two triangles");
            Debug.Log("[MeshLifecycle] ALL 7 PASSED");
            EditorApplication.Exit(0);
        }
        catch(Exception error)
        {
            Debug.LogError("[MeshLifecycle] FAILED " + error);
            EditorApplication.Exit(1);
        }
    }
}
