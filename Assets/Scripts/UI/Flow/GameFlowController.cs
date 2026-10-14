using System.Collections.Generic;
using UnityEngine;

namespace Engineer.UI
{
    /// <summary>
    /// 界面状态机。挂在场景根的 UI_Flow 对象上，持有全部屏并负责互斥显示。
    ///
    /// 约定（与 Docs/项目执行流程.md 的「GameFlowController」一致）：
    ///   * 所有屏都在场景里常驻，只切换 active，不做销毁重建；
    ///   * 「返回上一步」按历史栈回退；
    ///   * 「开始新的模拟」清空历史并重置 <see cref="GameContext"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameFlowController : MonoBehaviour
    {
        [Header("状态机")]
        [Tooltip("场景启动时进入的状态。")]
        [SerializeField] private GameState startState = GameState.Welcome;

        [Tooltip("是否在控制台打印状态切换日志。")]
        [SerializeField] private bool logTransitions = true;

        [Header("全部界面屏（由 UI 白盒构建器自动收集）")]
        [SerializeField] private List<PlaceholderScreen> screens = new List<PlaceholderScreen>();

        private readonly Dictionary<GameState, PlaceholderScreen> screenMap = new Dictionary<GameState, PlaceholderScreen>();
        private readonly Stack<GameState> history = new Stack<GameState>();
        private GameContext context = new GameContext();
        private PlaceholderScreen current;

        /// <summary>跨屏共享数据。</summary>
        public GameContext Context
        {
            get { return context; }
        }

        /// <summary>当前状态。</summary>
        public GameState Current
        {
            get { return current != null ? current.State : startState; }
        }

        private void Awake()
        {
            RebuildScreenMap();

            // 场景里同时存在多个屏时，除起始屏外全部隐藏，避免打开时所有屏叠在一起。
            foreach (var pair in screenMap)
            {
                if (pair.Value == null) continue;
                pair.Value.SetFlow(this);
                pair.Value.gameObject.SetActive(pair.Key == startState);
            }
            current = Find(startState);
        }

        private void Start()
        {
            // Awake 里已经把起始屏打开了，这里只补一次 OnEnter，保证屏的进入逻辑一定被调用。
            if (current != null) current.OnEnter(null);
        }

        /// <summary>收集场景里的屏，重复状态以最后一个为准并给出警告。</summary>
        public void RebuildScreenMap()
        {
            screenMap.Clear();
            for (var i = 0; i < screens.Count; i++)
            {
                var screen = screens[i];
                if (screen == null) continue;
                if (screenMap.ContainsKey(screen.State))
                {
                    Debug.LogWarning("[UI] 状态 " + screen.State + " 有多个屏，已忽略 " + screen.name, screen);
                    continue;
                }
                screenMap.Add(screen.State, screen);
            }
        }

        /// <summary>切换状态（记录历史）。</summary>
        public void GoTo(GameState state)
        {
            var next = Find(state);
            if (next == null)
            {
                Debug.LogWarning("[UI] 没有为状态 " + GameStateInfo.DisplayName(state) + " 注册屏，忽略本次切换。");
                return;
            }

            if (current != null)
            {
                if (current.State == state) return;
                history.Push(current.State);
                current.OnExit();
            }

            current = next;
            current.OnEnter(null);

            if (logTransitions) Debug.Log("[UI] → " + GameStateInfo.DisplayName(state));
        }

        /// <summary>返回上一步。</summary>
        public void Back()
        {
            if (history.Count == 0)
            {
                Debug.Log("[UI] 已在起始界面，没有可返回的上一步。");
                return;
            }

            var previous = history.Pop();
            var target = Find(previous);
            if (target == null)
            {
                Debug.LogWarning("[UI] 上一步 " + GameStateInfo.DisplayName(previous) + " 的屏已丢失，回到起始界面。");
                Restart();
                return;
            }

            if (current != null) current.OnExit();
            current = target;
            current.OnEnter(null);

            if (logTransitions) Debug.Log("[UI] ← " + GameStateInfo.DisplayName(previous));
        }

        /// <summary>开始新的模拟：清空历史与数据，回到初始界面。</summary>
        public void Restart()
        {
            history.Clear();
            context.Reset();
            ResetScreens();

            var welcome = Find(GameState.Welcome);
            if (welcome == null)
            {
                Debug.LogWarning("[UI] 场景里没有初始界面。");
                return;
            }

            if (current != null && current != welcome) current.OnExit();
            current = welcome;
            current.OnEnter(null);

            if (logTransitions) Debug.Log("[UI] 重新开始模拟。");
        }

        /// <summary>进入下一个阶段；已经是最后阶段时不做任何事。</summary>
        public bool AdvanceStage()
        {
            if (context.IsLastStage) return false;
            context.StageIndex++;
            return true;
        }

        /// <summary>提交本阶段水肥量后清空输入槽。</summary>
        public void ClearStageDosage()
        {
            for (var i = 0; i < context.StageDosage.Length; i++) context.StageDosage[i] = 0f;
        }

        public PlaceholderScreen Find(GameState state)
        {
            PlaceholderScreen screen;
            return screenMap.TryGetValue(state, out screen) ? screen : null;
        }

        /// <summary>供构建器/编辑器保存时补全屏列表。</summary>
        public void SetScreens(List<PlaceholderScreen> value)
        {
            screens = value ?? new List<PlaceholderScreen>();
            RebuildScreenMap();
        }

        public List<PlaceholderScreen> GetScreens()
        {
            return screens;
        }

        private void ResetScreens()
        {
            foreach (var pair in screenMap)
            {
                if (pair.Value != null) pair.Value.ResetPlaceholderState();
            }
        }
    }
}
