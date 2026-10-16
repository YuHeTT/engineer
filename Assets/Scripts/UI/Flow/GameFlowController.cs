using System.Collections.Generic;
using UnityEngine;
using Engineer.Config;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>
    /// 界面状态机。挂在场景根的 UI_Flow 对象上，持有全部屏并负责互斥显示。
    ///
    /// 约定（与 Docs/项目执行流程.md 的「GameFlowController」一致）：
    ///   * 所有屏都在场景里常驻，只切换 active，不做销毁重建；
    ///   * 「返回上一步」按历史栈回退；
    ///   * 「开始新的模拟」清空历史并重置 <see cref="GameContext"/>。
    ///
    /// 【与数值内核的关系】
    ///   本类是**唯一**创建 <see cref="GameSession"/> 的地方，并在 Awake 时把它注入
    ///   <see cref="GameContext"/>。各个屏只通过 GameContext.Session 读数据、提交水肥，
    ///   不自己去 new Simulation —— 避免出现多个互不相同的内核实例。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameFlowController : MonoBehaviour
    {
        [Header("状态机")]
        [Tooltip("场景启动时进入的状态。")]
        [SerializeField] private GameState startState = GameState.Welcome;

        [Tooltip("是否在控制台打印状态切换日志。")]
        [SerializeField] private bool logTransitions = true;

        [Header("数值内核")]
        [Tooltip("数值配置资产。留空则使用 SimConfig.CreateDefault()（文档默认数值）。")]
        [SerializeField] private SimConfigAsset configAsset;

        [Tooltip("是否在启动时打印一次配置自检结果（有错误会打成 Error）。")]
        [SerializeField] private bool validateConfigOnAwake = true;

        [Header("全部界面屏（由 UI 白盒构建器自动收集）")]
        [SerializeField] private List<PlaceholderScreen> screens = new List<PlaceholderScreen>();

        private readonly Dictionary<GameState, PlaceholderScreen> screenMap = new Dictionary<GameState, PlaceholderScreen>();
        private readonly Stack<GameState> history = new Stack<GameState>();
        private GameContext context = new GameContext();
        private GameSession session;
        private readonly FarmProbeState probe = new FarmProbeState();
        private PlaceholderScreen current;

        /// <summary>跨屏共享数据。</summary>
        public GameContext Context
        {
            get { return context; }
        }

        /// <summary>数值内核桥接层。</summary>
        public GameSession Session
        {
            get { return session; }
        }

        /// <summary>
        /// 探测状态（巡航得到的数据）。
        ///
        /// 【为什么放在这里】它是**农场场景**（巡航小车）和 **UI 场景**（巡航/分析界面）
        /// 都要用的共享数据。放在 GameFlowController 上，
        /// 两边都通过 FindObjectOfType 拿到同一个实例。
        ///
        /// 注意它**不写回数值内核** —— 内核里的土壤是权威数据，
        /// 探测值只影响界面显示和推荐用的输入。
        /// </summary>
        public FarmProbeState Probe
        {
            get { return probe; }
        }

        /// <summary>当前状态。</summary>
        public GameState Current
        {
            get { return current != null ? current.State : startState; }
        }

        private void Awake()
        {
            CreateSession();
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

        /// <summary>创建数值内核会话并注入共享数据。配置资产留空时用文档默认值兜底。</summary>
        private void CreateSession()
        {
            SimConfig config = null;

            if (configAsset != null)
            {
                config = configAsset.BuildSimConfig();
                if (validateConfigOnAwake && !configAsset.Validate(config))
                    Debug.LogError("[UI] 数值配置自检未通过，见 SimConfigAsset 的 lastValidationMessage。");
            }

            if (config == null)
            {
                config = SimConfig.CreateDefault();
                if (logTransitions)
                    Debug.Log("[UI] 未指定数值配置资产，使用 SimConfig.CreateDefault()（文档默认数值）。");
            }

            session = new GameSession(config);
            context.BindSession(session);
        }

        private void Start()
        {
            // Awake 里已经把起始屏打开了，这里只补一次 OnEnter，保证屏的进入逻辑一定被调用。
            if (current != null) current.OnEnter(null);

            // ★ 启动时也要应用一次输入策略 ——
            //   Awake 走的是"直接打开起始屏"，没经过 GoTo，
            //   不补这一下的话起始界面时相机仍然在抢键盘。
            ApplyInputPolicy(Current);
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

        private FarmFieldGrid probeGrid;
        private FarmCruiseController probeCruise;
        private float nextProbeResolve;
        private bool wasAutoCruising;
        public bool CanAnalyze => probe.CanAnalyze(session);

        /// <summary>由常驻状态机同步，页面隐藏后采集仍继续。</summary>
        public void SyncProbe()
        {
            if (session == null || !session.IsStarted || session.IsFinished) return;
            if (probeGrid == null && Time.unscaledTime >= nextProbeResolve)
            {
                nextProbeResolve = Time.unscaledTime + 1f;
                probeGrid = FindObjectOfType<FarmFieldGrid>();
                probeCruise = FindObjectOfType<FarmCruiseController>();
            }
            probe.Begin(session);
            if (probeGrid != null) probe.SetCoverage(probeGrid.Coverage);
        }
        private void LateUpdate()
        {
            SyncProbe();
            bool auto = probeCruise != null && probeCruise.Mode == FarmCruiseController.CruiseMode.Auto;
            if (wasAutoCruising && !auto && probeCruise != null
                && probeCruise.Mode == FarmCruiseController.CruiseMode.Finished
                && Current == GameState.FarmStage && !CanAnalyze)
            {
                var screen = Find(GameState.RobotCruise) as RobotCruiseScreen;
                if (screen == null || screen.returnAfterAutoCruise) GoTo(GameState.RobotCruise);
            }
            wasAutoCruising = auto;
        }
        private void ResetStageProbe()
        {
            probe.Reset();
            if (probeCruise != null) probeCruise.Stop();
            if (probeGrid != null) { probeGrid.ClearCoverage(); probeGrid.EndCruise(); }
            wasAutoCruising = false;
        }
        public bool ConfirmProbe()
        {
            SyncProbe();
            return probe.Confirm(session);
        }

        /// <summary>切换状态（记录历史）。</summary>
        public void GoTo(GameState state)
        {
            if (state == GameState.Analyze)
            {
                SyncProbe();
                if (!CanAnalyze) state = GameState.RobotCruise;
            }
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
            ApplyInputPolicy(state);

            if (logTransitions) Debug.Log("[UI] → " + GameStateInfo.DisplayName(state));
        }

        /// <summary>
        /// 按当前界面决定"键盘归谁"。
        ///
        /// 【为什么需要】之前相机在任何界面都抢 WASD / QE / 滚轮，
        /// 导致"进入农场操作界面后其他按键无法使用"。
        /// 现在集中在这里判定，只有确实在看 3D 农场时才把键盘给相机。
        ///
        /// 农场主界面 / 机器人巡航界面 = 有 3D 农场 → 相机可用
        /// 其它界面（欢迎、选地、分析、收益…）→ 相机完全不读输入，键盘交还 UI
        /// </summary>
        private void ApplyInputPolicy(GameState state)
        {
            var cameraHasInput = state == GameState.FarmStage || state == GameState.RobotCruise;

            var rig = FindObjectOfType<FarmCameraRig>();
            if (rig != null) rig.SetInputEnabled(cameraHasInput);

            if (logTransitions && cameraHasInput)
            {
                Debug.Log(string.Format(
                    "[UI] 键盘交给农场相机（{0}）。Tab 切视角、C 切档位、V 切天空、QE 旋转、滚轮缩放。",
                    GameStateInfo.DisplayName(state)));
            }
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
            if (previous == GameState.Analyze && !CanAnalyze) previous = GameState.FarmStage;
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
            ApplyInputPolicy(previous);

            if (logTransitions) Debug.Log("[UI] ← " + GameStateInfo.DisplayName(previous));
        }

        /// <summary>开始新的模拟：清空历史与数据，回到初始界面。</summary>
        public void Restart()
        {
            history.Clear();
            context.Reset();
            ResetStageProbe();
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

        /// <summary>
        /// 推进阶段计数（纯 UI 侧，不碰数值内核）。
        ///
        /// 【重要】接了内核之后，正常情况下**不要**调这个方法 ——
        /// 阶段推进应当由 <see cref="CommitStageDosage"/> 提交水肥时驱动，
        /// 否则界面会显示到下一阶段，但内核土壤还停在原地。
        /// 保留它是为了没有接内核时（纯白盒走查）仍能串起流程。
        /// </summary>
        public bool AdvanceStage()
        {
            if (context.IsLastStage) return false;
            context.StageIndex++;
            return true;
        }

        /// <summary>
        /// 提交本阶段水肥并推进。
        /// 这是接了内核之后**唯一**正确的推进方式。
        /// </summary>
        /// <returns>本阶段结果；未开始或已结束时返回 null</returns>
        public Engineer.Core.StageResult CommitStageDosage()
        {
            if (session == null || !session.IsStarted || !CanAnalyze) return null;

            var result = session.CommitFromDosage(context.StageDosage);
            if (result == null) return null;
            ResetStageProbe();
            context.SyncStageFromSession();
            ClearStageDosage();
            return result;
        }

        /// <summary>用六项基础数据启动数值内核。由「土地基础数据设置」界面调用。</summary>
        public bool BeginSimulation()
        {
            ResetStageProbe();
            var ok = context.BeginSimulation();
            if (ok) context.SyncStageFromSession();
            return ok;
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
