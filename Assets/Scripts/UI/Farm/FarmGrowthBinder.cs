// ---------------------------------------------------------------------------
//  FarmGrowthBinder.cs —— 把数值内核的状态绑到农场的视觉上
// ---------------------------------------------------------------------------
//  作用：**这是"数据 → 表现"的唯一桥梁**。
//        它读 GameSession 的阶段与长势，然后让 FarmFieldBuilder 换玉米模型。
//
//  【为什么需要这一层】
//    可视化层不该去问"现在是第几阶段"再自己判断该显示什么 ——
//    那是数值逻辑。它只该说"请显示 S3、长势正常"，剩下的由 FarmFieldBuilder 处理。
//    这样将来数值改了（比如阶段含义变了），只需改这一层的映射。
//
//  【轮询而不是事件】
//    GameSession 目前没有提供状态变化事件。用轮询（每帧比一下）最简单可靠，
//    而且成本极低（只比较两个枚举值）。等 GameSession 加了事件再改成订阅。
// ---------------------------------------------------------------------------

using UnityEngine;
using Engineer.Core;

namespace Engineer.UI
{
    /// <summary>把 GameSession 的阶段/长势同步到农场可视化。</summary>
    [DisallowMultipleComponent]
    public class FarmGrowthBinder : MonoBehaviour
    {
        [Header("数据源")]
        [Tooltip("留空会在场景里自动查找 GameFlowController。")]
        [SerializeField] private GameFlowController flow;

        [Header("表现目标")]
        [Tooltip("留空会在场景里自动查找 FarmFieldBuilder。")]
        [SerializeField] private FarmFieldBuilder field;

        [Header("可选：调试显示")]
        [Tooltip("把当前阶段与长势写到一个文本上（没有 UI 也可以留空）。")]
        [SerializeField] private TMPro.TMP_Text debugLabel;

        [Header("选项")]
        [Tooltip("没有开始模拟时（还没填六项基础数据）显示哪个阶段。")]
        [SerializeField] private FarmFieldBuilder.CornStage idleStage = FarmFieldBuilder.CornStage.Mature;

        [Tooltip("是否在模拟开始时按玩家选的亩数重排田块。\n" +
                 "★ 玩家选的亩数 = 田块数（1 块 = 1 亩），会拆成接近 16:10 的行列布局。")]
        [SerializeField] private bool applyLandAreaAsBlockCount = true;

        [Tooltip("是否在 Console 打印每次同步。\n" +
                 "★ 排查阶段开，稳定后建议关掉 —— 之前日志太多导致没法排查。")]
        [SerializeField] private bool logSync = false;

        /// <summary>上一次已经应用到田块布局的亩数（-1 = 还没应用过）。</summary>
        private float appliedArea = -1f;

        /// <summary>上一次同步时的 UI 阶段，用来检测"进入新阶段"。</summary>
        private int lastStageForReset = -1;

        /// <summary>
        /// 上一次同步到的状态，用来判断"变了才动"。
        /// 【注意】这里必须用一个**永远不可能等于真实状态**的初值：
        ///   真实 uiStage >= 1、growth >= 0、started 只可能 false/true。
        ///   如果用 started=false / uiStage=-1 当缓存初值，那么"还没开始模拟"
        ///   这个状态第一次进来时会被判成"没变化"而**跳过 Apply** ——
        ///   结果就是 idleStage 一次都没生效过，玉米永远不显示。（踩过这个坑）
        /// </summary>
        private int lastUiStage = int.MinValue;
        private GrowthLevel lastGrowth = (GrowthLevel)int.MinValue;
        private bool lastStarted;

        private void Awake()
        {
            Resolve();
        }

        private void Start()
        {
            // Awake 里 GameFlowController 可能还没建好 GameSession，Start 里强制同步一次
            ForceSync();
        }

        private void Update()
        {
            Sync();
        }

        private void Resolve()
        {
            if (flow == null) flow = FindObjectOfType<GameFlowController>();
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
        }

        /// <summary>每帧比一下，变了才动。</summary>
        public void Sync()
        {
            if (field == null) field = FindObjectOfType<FarmFieldBuilder>();
            if (field == null) return;      // 场景里根本没有田，无从同步

            if (flow == null) flow = FindObjectOfType<GameFlowController>();
            if (flow == null)
            {
                // 独立跑 Farm 场景时没有 GameFlowController，属于正常情况。
                // 这时只应用一次 idleStage 就够了，不做任何"未开始"的日志刷屏。
                if (lastUiStage == int.MinValue) Apply(null, GrowthLevel.Normal);
                return;
            }

            var session = flow.Session;
            var started = session != null && session.IsStarted;

            // ★ 用 null 表示"没有阶段"，而不是拿 -1 当哨兵 ——
            //   StageFromUiStage 的 default 分支会返回 Mature，-1 传进去语义就错了。
            int? uiStage = started ? session.UiStage : (int?)null;
            var growth = started ? session.Growth : GrowthLevel.Normal;

            // 只用一个 int 编码"未开始"，比较时不必再判哨兵
            const int Idle = int.MinValue;
            var stageCode = uiStage.HasValue ? uiStage.Value : Idle;

            if (started == lastStarted && growth == lastGrowth && stageCode == lastUiStage) return;

            lastStarted = started;
            lastUiStage = stageCode;
            lastGrowth = growth;

            Apply(uiStage, growth);
        }

        /// <summary>无视缓存，强制同步一次。</summary>
        public void ForceSync()
        {
            Resolve();
            lastStarted = false;
            lastUiStage = int.MinValue;
            lastGrowth = (GrowthLevel)int.MinValue;
            Sync();
        }

        /// <summary>
        /// 按玩家选的亩数重排田块。
        ///
        /// 【规则】玩家选的亩数 = 田块数（1 块 = 1 亩），
        /// 但**不能直接用任意数字** —— 必须吸附到能拆成规整行列的值
        /// （见 <see cref="FarmVisualConfig.SelectableBlockCounts"/>），
        /// 否则 7 亩会变成 7×1 的长条，很难看。
        ///
        /// 重排后会连带重建田块、田埂、地形，并让相机重新取景。
        /// </summary>
        private void ApplyLandArea()
        {
            var session = flow != null ? flow.Session : null;
            if (session == null || !session.IsStarted) return;

            // 亩数存在 GameContext 上（它是 UI 的输入数据），不是在内核里
            var context = flow.Context;
            var area = context != null ? context.LandAreaMu : 0f;
            if (area <= 0f) return;
            if (Mathf.Approximately(area, appliedArea)) return;

            appliedArea = area;

            var blocks = SnapToSelectable(Mathf.RoundToInt(area));

            // 田块布局
            var cfg = field.Config;
            cfg.ApplyBlockCount(blocks);
            field.Build();

            // 几何重建了，依赖田块尺寸的组件也要跟着重来
            var paths = FindObjectOfType<FarmFieldPaths>();
            if (paths != null) paths.Build();

            var terrain = FindObjectOfType<FarmTerrainRing>();
            if (terrain != null) terrain.Build();

            var grid = FindObjectOfType<FarmFieldGrid>();
            if (grid != null) grid.Build();

            // 【注意】**不要**在这里调 cruise.ResetToStart()。
            //
            //   踩过的坑：ResetToStart() 会把 CruiseController.Mode 打回 Idle。
            //   而调用链是：
            //     点「手动巡航」→ StartManual()（mode = Manual）
            //       → 切到农场场景 → FarmGrowthBinder 同步 → ApplyLandArea()
            //       → ResetToStart() → **mode 被打回 Idle，巡航当场结束**
            //   现象就是"点了巡航却看不到车在动"。
            //
            //   田块尺寸变了的时候小车可能站在田外，所以只需要把它夹回田内，
            //   而不是整个重置（模式与覆盖率都要保留）。
            var cruise = FindObjectOfType<FarmCruiseController>();
            if (cruise != null) cruise.ClampInsideField();

            // 相机重新取景
            var rig = FindObjectOfType<FarmCameraRig>();
            if (rig != null) rig.FrameField(field.FieldBounds, cfg.BlockGroupSize.x);

            Debug.Log(string.Format(
                "[农场] 按玩家选的 {0:0.#} 亩重排田块 → {1} 块（{2}×{3} 布局）。\n" +
                "  整片田 {4:0.#}×{5:0.#} m，玉米 {6} 株。",
                area, blocks, cfg.BlockColumns, cfg.BlockRows,
                cfg.FieldSize.x, cfg.FieldSize.y, field.VisiblePlantCount), this);
        }

        /// <summary>把任意亩数吸附到能拆成规整行列的值。</summary>
        private static int SnapToSelectable(int area)
        {
            var options = FarmVisualConfig.SelectableBlockCounts;
            if (options == null || options.Length == 0) return Mathf.Max(1, area);

            var best = options[0];
            var bestDiff = int.MaxValue;
            for (var i = 0; i < options.Length; i++)
            {
                var d = Mathf.Abs(options[i] - area);
                if (d < bestDiff) { bestDiff = d; best = options[i]; }
            }
            return best;
        }

        /// <param name="uiStage">UI 阶段；<c>null</c> 表示"模拟还没开始"，用 idleStage 占位。</param>
        private void Apply(int? uiStage, GrowthLevel growth)
        {
            var isIdle = !uiStage.HasValue;

            // ★ 亩数变了就重排田块（1 块 = 1 亩）
            if (!isIdle && applyLandAreaAsBlockCount) ApplyLandArea();

            // ★ 进入新阶段就清空巡航覆盖率 —— 按策划确认的"每阶段重新巡航"。
            //   每个生育期的土壤状况会变，所以每一阶段都要重新下地探测。
            if (!isIdle && uiStage.Value != lastStageForReset)
            {
                lastStageForReset = uiStage.Value;
                var grid = FindObjectOfType<FarmFieldGrid>();
                if (grid != null)
                {
                    grid.ClearCoverage();
                    grid.EndCruise();      // 撤掉遮罩，等玩家重新开始巡航
                }

                // 探测状态也一起重置：上一阶段的探测值不该带到这一阶段
                var probe = flow != null ? flow.Probe : null;
                if (probe != null) probe.Reset();

                if (logSync)
                {
                    Debug.Log(string.Format("[农场] 进入 S{0}，巡航覆盖率与探测数据已清零（每阶段需重新探测）。",
                        uiStage.Value), this);
                }
            }

            var stage = isIdle
                ? idleStage
                : FarmFieldBuilder.StageFromUiStage(uiStage.Value);

            field.ApplyStage(stage, growth);

            var what = isIdle
                ? "模拟未开始（占位阶段 " + stage + "）"
                : "S" + uiStage.Value + " " + StageMap.DisplayName(uiStage.Value);

            if (debugLabel != null)
            {
                debugLabel.text = isIdle
                    ? "尚未开始模拟"
                    : string.Format("阶段 S{0}（{1}）\n长势 {2}", uiStage.Value, stage,
                        GrowthLevels.DisplayName(growth));
            }

            if (logSync)
            {
                Debug.Log(string.Format(
                    "[农场] 同步视觉：{0}，长势 {1}（玉米：{2}，实际显示 {3} 株）",
                    what, GrowthLevels.DisplayName(growth),
                    stage == FarmFieldBuilder.CornStage.None ? "不显示植株（S1 只见土地）" : stage.ToString(),
                    field.VisiblePlantCount), this);
            }
        }
    }
}
