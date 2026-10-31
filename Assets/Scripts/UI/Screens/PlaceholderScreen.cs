using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Engineer.UI
{
    /// <summary>白盒阶段按钮的行为类型。</summary>
    public enum ScreenActionKind
    {
        /// <summary>跳转到指定状态。</summary>
        GoTo = 0,

        /// <summary>返回上一步（走状态机历史栈）。</summary>
        Back = 1,

        /// <summary>开始新的模拟（重置数据 + 回到初始界面）。</summary>
        Restart = 2,

        /// <summary>还没有实现的动作，点击只打印日志，便于验收时看清每颗按钮的落点。</summary>
        LogOnly = 3,
    }

    /// <summary>一颗按钮 + 它的行为，由 UI 白盒构建器填好，运行时由本屏自动接线。</summary>
    [Serializable]
    public class ScreenAction
    {
        public Button button;
        public ScreenActionKind kind = ScreenActionKind.GoTo;
        public GameState target = GameState.Welcome;

        [Tooltip("LogOnly 时打印的说明文字。")]
        public string note;
    }

    /// <summary>
    /// 白盒界面屏的通用脚本：声明自己属于哪个状态、哪些按钮干什么、哪些文案在
    /// 「开始新的模拟」时要复位。所有 12 张界面图的屏都挂这个脚本（或用它的子类）。
    ///
    /// 白盒阶段的原则：**能点、能串起流程、能看到每块区域属于文档哪一条**，
    /// 具体美术表现一律留给后续贴图/动效阶段。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlaceholderScreen : MonoBehaviour, IScreen
    {
        [Header("状态")]
        [Tooltip("本屏对应的状态，构建器会自动写入。")]
        [SerializeField] private GameState state = GameState.Welcome;

        [Header("按钮行为（构建器自动填充）")]
        [SerializeField] private List<ScreenAction> actions = new List<ScreenAction>();

        [Header("白盒复位")]
        [Tooltip("勾选后，本屏所有 TMP 文本与输入框都会在「开始新的模拟」时复位到初始值。")]
        [SerializeField] private bool autoCacheAllTexts = true;

        [Header("诊断")]
        [Tooltip("打印每次按钮动作的分发与目标状态。排查「按钮跳错界面」这类问题时打开。")]
        [SerializeField] private bool logActionDispatch = true;

        private GameFlowController flow;
        private bool initialCached;
        private TMP_Text[] cachedTexts;
        private string[] cachedTextValues;
        private TMP_InputField[] cachedInputs;
        private string[] cachedInputValues;
        private bool wired;

        public GameState State
        {
            get { return state; }
        }

        public GameFlowController Flow
        {
            get { return flow; }
        }

        // ------------------------------------------------------------------
        // 状态机接口
        // ------------------------------------------------------------------

        public virtual void OnEnter(object args)
        {
            gameObject.SetActive(true);

            // 上次离场时按钮可能还停在"切片消散后隐藏"的状态，进来先还原。
            UiClickFeedback.RestoreAll(this);
        }

        public virtual void OnExit()
        {
            // 先还原按钮，再隐藏；这样下次进来不用等动效组件来收拾。
            UiClickFeedback.RestoreAll(this);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // 点击表现
        // ------------------------------------------------------------------

        /// <summary>
        /// 按钮点击的统一表现：音效 + 变色闪烁 + 横向切片消散。
        ///
        /// navigate = true（会换页）：碎裂后停在消失状态，动作在动画播完后执行——
        /// 如果直接 GoTo，新界面会立刻盖上来，消散动画根本看不见。
        /// navigate = false（不换页）：碎裂后快速重组回来，动作**立刻**执行，不挡后续点击。
        /// 没有挂 UiClickFeedback 时它会立刻执行 action，行为和以前一致。
        /// </summary>
        protected void ClickThen(Button button, bool navigate, Action action)
        {
            UiClickFeedback.Play(button, navigate, action);
        }

        // ------------------------------------------------------------------
        // 构建器接口
        // ------------------------------------------------------------------

        /// <summary>由构建器写入本屏状态。</summary>
        public void SetState(GameState value)
        {
            state = value;
        }

        /// <summary>追加一条按钮行为。</summary>
        public void AddAction(Button button, ScreenActionKind kind, GameState target)
        {
            AddAction(button, kind, target, null);
        }

        public void AddAction(Button button, ScreenActionKind kind, GameState target, string note)
        {
            if (button == null) return;
            actions.Add(new ScreenAction { button = button, kind = kind, target = target, note = note });
        }

        public List<ScreenAction> GetActions()
        {
            return actions;
        }

        public void ClearActions()
        {
            actions.Clear();
        }
        /// <summary>由状态机注入自己并接线按钮。</summary>
        public void SetFlow(GameFlowController value)
        {
            flow = value;
            Wire();
        }



        /// <summary>「开始新的模拟」时把本屏文案与输入框复位。子类可重写以复位自己的运行时状态。</summary>
        public virtual void ResetPlaceholderState()
        {
            CacheInitialState();

            if (cachedTexts != null)
            {
                for (var i = 0; i < cachedTexts.Length; i++)
                {
                    if (cachedTexts[i] != null) cachedTexts[i].text = cachedTextValues[i];
                }
            }

            if (cachedInputs != null)
            {
                for (var i = 0; i < cachedInputs.Length; i++)
                {
                    if (cachedInputs[i] != null) cachedInputs[i].SetTextWithoutNotify(cachedInputValues[i]);
                }
            }
        }

        /// <summary>白盒占位动作的统一日志。</summary>
        protected void LogPlaceholder(string what)
        {
            Debug.Log("[UI] " + name + " ：" + what + " 目前仍是白盒，功能待接入。", this);
        }

        // ------------------------------------------------------------------
        // 内部
        // ------------------------------------------------------------------

        private void Wire()
        {
            if (wired) return;
            wired = true;

            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null || action.button == null) continue;

                var captured = action;
                captured.button.onClick.AddListener(delegate { Handle(captured); });
            }
        }

        /// <summary>业务屏可接管按钮，防止旧白盒动作绕过输入校验。</summary>
        protected virtual bool OwnsButton(Button button) => false;

        private void Handle(ScreenAction action)
        {
            if (OwnsButton(action.button)) return;

            // 这张卡片如果挂了 OptionCardButton，由它自己负责写回数据 + 跳转；
            // 旧的 ScreenAction（GoTo / LogOnly）必须让路，否则两个监听会抢一次点击。
            if (action.button != null && action.button.GetComponent<OptionCardButton>() != null) return;

            if (logActionDispatch)
            {
                var buttonName = action.button != null ? action.button.name : "(null)";
                var targetName = action.kind == ScreenActionKind.GoTo
                    ? " -> " + GameStateInfo.DisplayName(action.target)
                    : string.Empty;
                Debug.Log(string.Format("[UI] {0} 的按钮 {1} 触发 {2}{3}",
                    name, buttonName, action.kind, targetName), this);
            }

            switch (action.kind)
            {
                case ScreenActionKind.GoTo:
                    ClickThen(action.button, true, delegate
                    {
                        if (flow != null) flow.GoTo(action.target);
                        else Debug.LogWarning("[UI] " + name + " 没有拿到 GameFlowController。", this);
                    });
                    break;

                case ScreenActionKind.Back:
                    ClickThen(action.button, true, delegate
                    {
                        if (flow != null) flow.Back();
                    });
                    break;

                case ScreenActionKind.Restart:
                    ClickThen(action.button, true, delegate
                    {
                        if (flow != null) flow.Restart();
                    });
                    break;

                case ScreenActionKind.LogOnly:
                default:
                    // 不切屏的按钮不做收起，只发声 + 闪一下，否则按钮会凭空消失。
                    ClickThen(action.button, false, delegate
                    {
                        LogPlaceholder(string.IsNullOrEmpty(action.note) ? "该操作" : action.note);
                    });
                    break;
            }
        }

        private void CacheInitialState()
        {
            if (initialCached) return;
            initialCached = true;

            cachedInputs = GetComponentsInChildren<TMP_InputField>(true);
            cachedInputValues = new string[cachedInputs.Length];
            for (var i = 0; i < cachedInputs.Length; i++)
                cachedInputValues[i] = cachedInputs[i] != null ? cachedInputs[i].text : string.Empty;

            if (!autoCacheAllTexts)
            {
                cachedTexts = new TMP_Text[0];
                cachedTextValues = new string[0];
                return;
            }

            cachedTexts = GetComponentsInChildren<TMP_Text>(true);
            cachedTextValues = new string[cachedTexts.Length];
            for (var i = 0; i < cachedTexts.Length; i++)
                cachedTextValues[i] = cachedTexts[i] != null ? cachedTexts[i].text : string.Empty;
        }
    }
}
