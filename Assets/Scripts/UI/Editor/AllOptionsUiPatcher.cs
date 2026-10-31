using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI.EditorTools
{
    /// <summary>
    /// 一次性 UI 补丁：把土地/植物选择页的 2/3/4 号卡也开放。
    ///
    /// 方案与 LandPreviewUiPatcher 一致：不重跑白盒构建，只按名字改现有场景。
    /// 运行时 <see cref="AllOptionsUi.Apply"/> 已经能保证 Play 时四张卡可用；
    /// 这个菜单额外把场景里的按钮文案、interactable 和旧 ScreenAction 清干净，
    /// 让编辑模式也显示「选择」。
    /// </summary>
    public static class AllOptionsUiPatcher
    {
        private const string ScenePath = "Assets/Scenes/UI.unity";
        private const string RootName = "UI_Root";

        [MenuItem("Tools/UI/开放全部土地与植物", false, 21)]
        public static void ApplyPatch()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("开放全部选项", "请先退出 Play 模式再执行。", "好");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                Debug.LogError("[UI] 找不到 " + RootName + "，补丁中止。");
                return;
            }

            AllOptionsUi.Apply(root.transform);

            var removed = 0;
            var screens = root.GetComponentsInChildren<PlaceholderScreen>(true);
            for (var i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                if (screen == null) continue;

                string prefix;
                if (screen.State == GameState.LandSelect) prefix = "Card_Land";
                else if (screen.State == GameState.PlantSelect) prefix = "Card_Plant";
                else continue;

                for (var cardIndex = 1; cardIndex <= 4; cardIndex++)
                {
                    var card = FindDeep(screen.transform, prefix + cardIndex);
                    if (card == null) continue;

                    var buttonTransform = card.Find("Button_Select");
                    if (buttonTransform == null) buttonTransform = card.Find("Button_Locked");
                    if (buttonTransform == null) continue;

                    var button = buttonTransform.GetComponent<Button>();
                    if (button != null) removed += RemoveActionFor(screen, button);
                }

                EditorUtility.SetDirty(screen);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[UI] 已开放全部土地/植物卡片；移除旧 ScreenAction " + removed + " 条。");
        }

        private static int RemoveActionFor(PlaceholderScreen screen, Button button)
        {
            if (screen == null || button == null) return 0;

            var so = new SerializedObject(screen);
            var actions = so.FindProperty("actions");
            if (actions == null) return 0;

            var removed = 0;
            for (var i = actions.arraySize - 1; i >= 0; i--)
            {
                var element = actions.GetArrayElementAtIndex(i);
                var buttonProperty = element.FindPropertyRelative("button");
                if (buttonProperty == null || buttonProperty.objectReferenceValue != button) continue;

                actions.DeleteArrayElementAtIndex(i);
                removed++;
            }

            if (removed > 0) so.ApplyModifiedPropertiesWithoutUndo();
            return removed;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name) return all[i];
            }

            return null;
        }
    }
}
