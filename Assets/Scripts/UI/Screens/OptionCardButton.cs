using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>选项卡片写回的数据种类。</summary>
    public enum OptionCategory
    {
        /// <summary>土地类型（写回 GameContext.LandTypeId / LandTypeName）。</summary>
        Land = 0,

        /// <summary>植物类型（写回 GameContext.PlantTypeId / PlantTypeName）。</summary>
        Plant = 1,
    }

    /// <summary>
    /// 选项卡片按钮：点一下就把"选了哪一项"写回 <see cref="GameContext"/>，然后跳转。
    ///
    /// 【为什么需要它】
    ///   土地选择屏（Screen_LandSelect）与植物选择屏（Screen_PlantSelect）的卡片按钮
    ///   之前只有 `PlaceholderScreen` 的 GoTo 行为 —— **只跳转，不记录选了哪张卡**。
    ///   于是 GameContext.LandTypeId / PlantTypeId 永远是默认的"平川台 / 玉米"。
    ///   v1 只做这两种所以看不出来，但一旦要开放泽洼域/崇岭阶/高原坪就会立刻出问题。
    ///
    /// 【怎么用（在 Unity 里）】
    ///   1. 给每张卡片的按钮对象加这个组件（挂在**同一对象**上，和 Button 一起）
    ///   2. 填 Id / DisplayName；Category 选 Land 或 Plant；TargetState 选下一屏
    ///   3. 不要再去 PlaceholderScreen 的 actions 列表里给这颗按钮配 GoTo —— 本组件会自己跳转
    ///
    /// 【与被锁定卡片的关系】
    ///   未开放的卡片（如泽洼域）保持 Button.interactable = false，不挂本组件即可。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class OptionCardButton : MonoBehaviour
    {
        [Header("选项数据")]
        [Tooltip("与 SimConfig.Terrains/Crops 里的 Id 对应，例如 flat_plain / corn。")]
        public string optionId = "";

        [Tooltip("界面上显示的选项名，例如「平川台」「玉米」。")]
        public string displayName = "";

        [Tooltip("写回 GameContext 的哪一组字段。")]
        public OptionCategory category = OptionCategory.Land;

        [Header("跳转")]
        [Tooltip("选中后跳到哪个界面。")]
        public GameState targetState = GameState.LandParams;

        [Header("附加说明（可选）")]
        [Tooltip("卡片上的介绍文本，选中时不做处理，仅作记录方便排查。")]
        public string description = "";

        private Button button;
        private bool wired;

        private void Awake()
        {
            Wire();
        }

        private void OnEnable()
        {
            // 屏被反复 SetActive 时保证只接线一次
            Wire();
        }

        private void Wire()
        {
            if (wired) return;
            wired = true;

            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogWarning("[OptionCard] " + name + " 上没有 Button 组件。", this);
                return;
            }

            button.onClick.AddListener(OnClicked);
        }

        /// <summary>点击：写回选择 + 跳转。也允许在 Inspector 里手动挂到 onClick 上。</summary>
        public void OnClicked()
        {
            var flow = FindFlow();
            if (flow == null)
            {
                Debug.LogWarning("[OptionCard] " + name + " 找不到 GameFlowController，无法记录选择。", this);
                return;
            }

            var context = flow.Context;
            if (context != null) Apply(context);

            flow.GoTo(targetState);
        }

        /// <summary>把本卡片的选择写回共享数据。</summary>
        public void Apply(GameContext context)
        {
            if (context == null) return;

            // 没填 displayName 时退回用节点名，避免界面上出现空字符串
            var name = string.IsNullOrEmpty(displayName) ? this.name : displayName;
            var id = string.IsNullOrEmpty(optionId) ? name : optionId;

            switch (category)
            {
                case OptionCategory.Land:
                    context.LandTypeId = id;
                    context.LandTypeName = name;

                    // 换地形后，之前的初始土壤数值未必还合适 —— 目前四套地形共用同一组区间，
                    // 所以只记选择、不重置数值。将来地形有独立区间时，在这里补默认值。
                    break;

                case OptionCategory.Plant:
                    context.PlantTypeId = id;
                    context.PlantTypeName = name;
                    break;
            }

            Debug.Log("[OptionCard] 已选择 " + category + "：" + name + "（id=" + id + "）", this);
        }

        /// <summary>从父级往上找状态机（屏本身不持有它，只有 PlaceholderScreen 有）。</summary>
        private GameFlowController FindFlow()
        {
            var screens = GetComponentsInParent<PlaceholderScreen>(true);
            for (var i = 0; i < screens.Length; i++)
            {
                if (screens[i] != null && screens[i].Flow != null) return screens[i].Flow;
            }
            return Object.FindObjectOfType<GameFlowController>();
        }
    }
}
