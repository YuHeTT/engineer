// ---------------------------------------------------------------------------
//  ScreenScriptFixer.cs —— 把 UI.unity 里"还挂着通用 PlaceholderScreen 的屏"换成专用脚本
// ---------------------------------------------------------------------------
//  修的是一个只在运行时才看得出来、而且不会报错的问题：
//
//    UI.unity 里这三屏如果挂的还是通用 PlaceholderScreen，
//      Screen_LandParams / Screen_Confirm / Screen_Revenue
//    那么它们各自的功能全部不生效：
//
//      * 土地参数页：六个输入框没有任何脚本去读。全工程只有 LandParamsScreen
//        会调 GameFlowController.BeginSimulation()，所以**数值内核根本不会启动**，
//        玩家填的数也进不了 GameContext。
//      * 确认页「本局配置一览」：12 行永远是建屏时写死的白盒占位值
//        （平川台 / 玉米 / 10 / 6.5 / 22 / 95 / 17 / 150 / 470 mV / 11 / 9 / 0.07 mg/kg），
//        ConfirmScreen.Refresh() 从来没跑过 —— 这就是"显示的数和我填的不一样"的原因。
//      * 收益页：显示的还是白盒公式占位文案，不是 RevenueReport 的真实报表。
//
//  【为什么不用 Inspector 手动换组件】
//    PlaceholderScreen 标了 [DisallowMultipleComponent]，不能直接在它旁边
//    再叠一个子类；必须先 Remove Component（actions 按钮接线一起丢），再 Add Component，
//    最后还要回 UI_Flow 的 GameFlowController.screens 列表把对应项重新指过去。
//    步骤多、会丢数据、容易漏。本工具把这套动作做成一键，并且**保留接线**。
//
//  【跑法】
//    菜单：Tools ▸ UI ▸ 修复屏脚本挂载（确认/土地参数/收益）
//    命令行：Unity.exe -batchmode -quit -projectPath "<工程根目录>"
//              -executeMethod Engineer.UI.EditorTools.ScreenScriptFixer.Run -logFile -
//
//  【安全】动手前先把 Assets/Scenes/UI.unity 复制到 Temp/UI.unity.bak_screens_<时间戳>，
//    改完打印逐屏报告。三屏都已经是对的时什么都不做、也不保存。
// ---------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Engineer.UI.EditorTools
{
    /// <summary>把通用 PlaceholderScreen 换成专用屏脚本（土地参数 / 确认 / 收益）。</summary>
    public static class ScreenScriptFixer
    {
        /// <summary>白盒界面所在场景。</summary>
        private const string ScenePath = UiWhiteboxBuilder.TargetScenePath;

        /// <summary>屏对象的父节点路径（相对于 UI_Root）。</summary>
        private const string ScreensPath = "UI_Root/Screen_Canvas/Screens";

        /// <summary>要处理的屏：场景对象名 → 应该挂的专用脚本。</summary>
        private sealed class Target
        {
            public readonly string ObjectName;
            public readonly Type Script;

            public Target(string objectName, Type script)
            {
                ObjectName = objectName;
                Script = script;
            }
        }

        /// <summary>顺序与 GameFlowController.screens 的下标无关，这里只按"哪个屏"列举。</summary>
        private static readonly Target[] Targets =
        {
            new Target("Screen_LandParams", typeof(LandParamsScreen)),
            new Target("Screen_Confirm", typeof(ConfirmScreen)),
            new Target("Screen_Revenue", typeof(RevenueScreen)),
        };

        // ==================================================================
        //  入口
        // ==================================================================

        [MenuItem("Tools/UI/修复屏脚本挂载（确认/土地参数/收益）", false, 20)]
        private static void FixFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("修复屏脚本挂载", "请先退出 Play 模式再修。", "好");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.path != ScenePath)
            {
                var switchScene = EditorUtility.DisplayDialog("修复屏脚本挂载",
                    "当前场景是：\n" + scene.path + "\n\n要修的屏在：\n" + ScenePath +
                    "\n\n是否切换过去？（当前场景未保存的改动请先自行保存）",
                    "切换并修复", "取消");
                if (!switchScene) return;
            }

            Run();
        }

        /// <summary>
        /// 命令行入口（-executeMethod）。会打开 UI.unity、修完保存。
        /// 【注意】失败时无法直接改进程退出码，请按日志里的 ✗ 判读。
        /// </summary>
        public static void Run()
        {
            var report = new StringBuilder();
            try
            {
                Fix(report);
            }
            catch (Exception e)
            {
                Debug.LogError("[修复屏脚本挂载] 失败：" + e);
                throw;
            }

            Debug.Log(report.ToString());
        }

        // ==================================================================
        //  主流程
        // ==================================================================

        /// <summary>逐屏处理；三屏都已经是对的时什么都不做，也不保存场景。</summary>
        private static void Fix(StringBuilder report)
        {
            report.AppendLine("=== 修复屏脚本挂载（土地参数 / 确认 / 收益） ===");

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
            {
                if (!File.Exists(ScenePath))
                {
                    report.AppendLine("✗ 找不到场景 " + ScenePath + "，什么都没做。");
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                report.AppendLine("已打开场景：" + scene.path);
            }
            else
            {
                report.AppendLine("场景：" + scene.path);
            }

            var backup = BackupScene();
            if (backup != null) report.AppendLine("备份：" + backup);

            var changed = 0;
            for (var i = 0; i < Targets.Length; i++) changed += FixOne(scene, Targets[i], report);

            if (changed == 0)
            {
                report.AppendLine("没有屏需要处理（三屏都已经是专用脚本）。");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            report.AppendLine(EditorSceneManager.SaveScene(scene)
                ? "场景已保存，共处理 " + changed + " 个屏。"
                : "⚠ 场景保存失败，请手动 Ctrl+S。");

            report.AppendLine();
            report.AppendLine("怎么验证：Play → 土地参数页改一下 pH（比如 6.2）→ 点「选择植物」→");
            report.AppendLine("确认页「本局配置一览」应当显示 6.2；四项静态指标（Eh / 土骨离子 /");
            report.AppendLine("毒砷潜势 / 汞积量）本来就是内核随机生成的，和填的五项不一样是正常的。");
        }

        /// <summary>处理一个屏：换组件 + 保住按钮接线 + 改 screens 列表。1=已处理，0=跳过。</summary>
        private static int FixOne(Scene scene, Target target, StringBuilder report)
        {
            var obj = FindInScene(scene, ScreensPath + "/" + target.ObjectName);
            if (obj == null)
            {
                report.AppendLine("✗ " + target.ObjectName + "：场景里找不到，跳过。");
                return 0;
            }

            if (obj.GetComponent(target.Script) != null)
            {
                report.AppendLine("= " + target.ObjectName + "：已经是 " + target.Script.Name + "，无需处理。");
                return 0;
            }

            var old = obj.GetComponent<PlaceholderScreen>();
            if (old == null)
            {
                report.AppendLine("! " + target.ObjectName + "：既没有 " + target.Script.Name +
                                  "，也没有 PlaceholderScreen，跳过（请人工检查）。");
                return 0;
            }

            if (old.GetType() != typeof(PlaceholderScreen))
            {
                report.AppendLine("! " + target.ObjectName + "：挂的是 " + old.GetType().Name +
                                  " 而不是通用 PlaceholderScreen，为免误伤，跳过。");
                return 0;
            }

            // 先把旧组件上的东西全读出来（含按钮接线与它在 screens 列表里的下标），
            // 再删组件 —— PlaceholderScreen 带 [DisallowMultipleComponent]，
            // 不能先把子类加上去再删基类。
            var state = old.State;
            var actions = new List<ScreenAction>(old.GetActions());
            var autoCacheAllTexts = ReadBool(old, "autoCacheAllTexts");
            var logActionDispatch = ReadBool(old, "logActionDispatch");
            var flowIndices = FlowListIndices(scene, old);

            UnityEngine.Object.DestroyImmediate(old);

            var created = obj.gameObject.AddComponent(target.Script) as PlaceholderScreen;
            if (created == null)
            {
                report.AppendLine("✗ " + target.ObjectName + "：添加 " + target.Script.Name + " 失败。");
                return 0;
            }

            created.SetState(state);
            created.ClearActions();
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null) continue;
                created.AddAction(action.button, action.kind, action.target, action.note);
            }

            WriteBool(created, "autoCacheAllTexts", autoCacheAllTexts);
            WriteBool(created, "logActionDispatch", logActionDispatch);
            EditorUtility.SetDirty(created);

            var repointed = RepointFlow(scene, flowIndices, created);

            report.AppendLine("✓ " + target.ObjectName + "：PlaceholderScreen → " + target.Script.Name +
                              "（保留按钮接线 " + actions.Count + " 条" +
                              (repointed > 0 ? "，screens 列表改了 " + repointed + " 处" : "") + "）");
            return 1;
        }
        // ==================================================================
        //  工具方法
        // ==================================================================

        /// <summary>在场景的所有根对象下按相对路径找节点。</summary>
        private static Transform FindInScene(Scene scene, string path)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var found = roots[i].transform.Find(path);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>找场景里的状态机（UI_Flow）。</summary>
        private static GameFlowController FindFlow(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var flow = roots[i].GetComponentInChildren<GameFlowController>(true);
                if (flow != null) return flow;
            }
            return null;
        }

        /// <summary>旧组件在 GameFlowController.screens 列表里的下标（可能不止一个）。</summary>
        private static List<int> FlowListIndices(Scene scene, PlaceholderScreen screen)
        {
            var indices = new List<int>();
            var flow = FindFlow(scene);
            if (flow == null) return indices;

            var list = flow.GetScreens();
            if (list == null) return indices;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == screen) indices.Add(i);
            }
            return indices;
        }

        /// <summary>把 screens 列表里指向旧组件的项改成指向新组件（不改的话状态机会找不到这个屏）。</summary>
        private static int RepointFlow(Scene scene, List<int> indices, PlaceholderScreen created)
        {
            if (indices.Count == 0) return 0;

            var flow = FindFlow(scene);
            if (flow == null) return 0;

            var so = new SerializedObject(flow);
            var screens = so.FindProperty("screens");
            if (screens == null || !screens.isArray) return 0;

            var changed = 0;
            for (var i = 0; i < indices.Count; i++)
            {
                var index = indices[i];
                if (index < 0 || index >= screens.arraySize) continue;
                screens.GetArrayElementAtIndex(index).objectReferenceValue = created;
                changed++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
            return changed;
        }

        /// <summary>读私有序列化字段（bool）。</summary>
        private static bool ReadBool(PlaceholderScreen screen, string field)
        {
            var so = new SerializedObject(screen);
            var property = so.FindProperty(field);
            return property != null && property.boolValue;
        }

        /// <summary>写私有序列化字段（bool）。</summary>
        private static void WriteBool(PlaceholderScreen screen, string field, bool value)
        {
            var so = new SerializedObject(screen);
            var property = so.FindProperty(field);
            if (property == null) return;

            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>动手前备份场景文件，命名与工程里以往的 Temp/UI.unity.bak_* 一致。</summary>
        private static string BackupScene()
        {
            if (!File.Exists(ScenePath)) return null;
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");

            var target = Path.Combine("Temp",
                "UI.unity.bak_screens_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            File.Copy(ScenePath, target, true);
            return target;
        }


    }
}
