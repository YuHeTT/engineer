using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Engineer.UI;
public static class PhysicsDepartureCheck
{
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Debug.Log("[DeparturePhysics] PASS "+label);}
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);
    public static void Run()
    {
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Physics.autoSimulation=false;
            var root=new GameObject("Fixture");var eq=root.AddComponent<FarmEquipment>();
            var rv=new GameObject("RV");rv.transform.SetParent(root.transform,false);Set(eq,"rv",rv);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="ROBOT_GARAGE_FLOOR";floor.transform.SetParent(rv.transform,false);
            floor.transform.position=new Vector3(0,-.05f,0);floor.transform.localScale=new Vector3(8,.1f,12);
            var carrier=new GameObject("Carrier");carrier.transform.position=new Vector3(0,2,0);
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.transform.SetParent(carrier.transform,false);visual.transform.localScale=new Vector3(.6f,.4f,.8f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            Call(eq,"PrepareDeparturePhysics",carrier.transform,new Vector3(0,0,3));
            var body=carrier.GetComponent<Rigidbody>();
            Check(body!=null&&!body.isKinematic&&body.useGravity,"departure uses dynamic rigidbody and gravity");
            Check(carrier.GetComponent<BoxCollider>()!=null,"robot has physical collision shape");
            float start=body.position.y;
            Physics.SyncTransforms();for(int i=0;i<120;i++)Physics.Simulate(.02f);
            Check(body.position.y<start-1f&&body.position.y>.1f,"robot falls under gravity and stops on contact surface");
            var drive=(IEnumerator)Call(eq,"DrivePhysicsTo",new Vector3(0,0,3),12f);
            for(int i=0;i<700&&drive.MoveNext();i++)Physics.Simulate(.02f);
            bool arrived=(bool)eq.GetType().GetField("physicsArrived",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(eq);
            Debug.Log("[DeparturePhysics] position="+body.position+" velocity="+body.velocity);
            Check(arrived&&Mathf.Abs(body.position.z-3)<.4f,"force driven departure reaches ground target");
            Check(Mathf.Abs(body.position.y)<.4f,"horizontal drive stays grounded rather than interpolating altitude");
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.transform.position=new Vector3(2,.7f,0);
            ramp.transform.localScale=new Vector3(2,.15f,4);ramp.transform.rotation=Quaternion.Euler(20,0,0);
            body.position=new Vector3(2,1.7f,-1.4f);body.rotation=Quaternion.identity;body.velocity=Vector3.zero;
            Physics.SyncTransforms();for(int i=0;i<100;i++)Physics.Simulate(.02f);
            float rampHeight=body.position.y;
            var descend=(IEnumerator)Call(eq,"DrivePhysicsTo",new Vector3(2,0,3),18f);
            for(int i=0;i<1000&&descend.MoveNext();i++)Physics.Simulate(.02f);
            bool down=(bool)eq.GetType().GetField("physicsArrived",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(eq);
            Check(down&&body.position.y<rampHeight-.5f&&body.position.y>.05f,"robot physically descends slope and lands on ground");
            var field=root.AddComponent<FarmFieldBuilder>();Set(eq,"field",field);
            body.interpolation=RigidbodyInterpolation.None;
            carrier.transform.SetPositionAndRotation(new Vector3(0,.21f,0),Quaternion.identity);
            body.position=carrier.transform.position;body.rotation=Quaternion.identity;body.velocity=Vector3.zero;
            Physics.SyncTransforms();
            var raisedTarget=new Vector3(0,field.Config.SoilRidgeTopY+.2f,7);
            Call(eq,"PrepareDeparturePhysics",carrier.transform,raisedTarget);
            var enterField=(IEnumerator)Call(eq,"DrivePhysicsTo",raisedTarget,20f);
            for(int i=0;i<1100&&enterField.MoveNext();i++)Physics.Simulate(.02f);
            bool fieldArrived=(bool)eq.GetType().GetField("physicsArrived",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(eq);
            Debug.Log("[DeparturePhysics] raised position="+body.position+" target="+raisedTarget);
            Check(fieldArrived&&Mathf.Abs(body.position.y-raisedTarget.y)<.08f,
                "physical arrival matches patrol soil height without wheels sinking below ridge");
            eq.StopDeparturePhysics();
            Check(body.isKinematic,"cleanup stops physical driving before patrol handoff");
            Check(body.interpolation==RigidbodyInterpolation.None&&!body.useGravity,"handoff disables rigidbody interpolation and gravity to avoid transform jitter");
            var shape=carrier.GetComponent<BoxCollider>();
            Check(Mathf.Abs(shape.size.y-.4f)<.001f,"physics shape preserves wheel contact bottom instead of shrinking height");
            Check(!carrier.GetComponent<BoxCollider>().enabled,"cleanup disables temporary robot collider");
            Physics.autoSimulation=true;Debug.Log("[DeparturePhysics] ALL "+checks+" PASSED");EditorApplication.Exit(0);
        }catch(Exception e){Physics.autoSimulation=true;Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
