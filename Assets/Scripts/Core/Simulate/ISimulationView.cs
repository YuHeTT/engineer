// ---------------------------------------------------------------------------
//  ISimulationView.cs —— 模拟的只读视图接口
// ---------------------------------------------------------------------------
//  作用：把"UI 能看到的"和"UI 能改的"分开。
//
//  【为什么需要这个接口】
//    UI 层和 Gameplay 层只需要"读当前状态"，不需要"推进流程"。
//    如果它们直接依赖 Simulation 具体类，就什么都能调 ——
//    包括 Initialize / Reset / Commit，很容易误用（比如 UI 刷新时
//    误调 Commit 把阶段推进了）。
//
//    让 UI 依赖这个接口之后：
//      · 编译期就挡住了误调写操作
//      · 写 UI 单元测试时可以塞一个假的 ISimulationView，不需要真跑数值
//
//  【典型用法】
//      // 场景启动时
//      var sim = new Simulation(cfg);
//      screen.Bind(sim.View);          // 只把只读视图交给 UI
//
//      // UI 内部
//      void Refresh() {
//          var fit  = _view.Evaluate();
//          var rec  = _view.Recommend();
//          var grow = _view.CurrentGrowth();
//      }
//
//      // 只有 GameFlowController 持有真正的 Simulation，由它调 Commit
//
//  【注意】CurrentSoil 仍然返回内部引用（只读语义）。
//    这是刻意的：拷贝一份给 UI 展示没有必要，而且会让"当前土壤"
//    和"刚提交的土壤"出现两个不同的对象，反而容易搞混。
//    约定：拿到 SoilState 后不要修改；要改先 Clone()。
// ---------------------------------------------------------------------------

namespace Engineer.Core
{
    /// <summary>模拟的只读视图。UI 与表现层应依赖这个接口。</summary>
    public interface ISimulationView
    {
        /// <summary>当前阶段。Done 表示四阶段已全部结算。</summary>
        Stage CurrentStage { get; }

        /// <summary>四阶段是否已全部结束。</summary>
        bool IsFinished { get; }

        /// <summary>已完成结算的阶段数（0~4）。</summary>
        int CommittedCount { get; }

        /// <summary>当前土壤状态。只读语义，不要直接修改。</summary>
        SoilState CurrentSoil { get; }

        /// <summary>当前阶段的五个适宜度（内部有缓存，重复调用不重算）。</summary>
        SuitabilityResult Evaluate();

        /// <summary>当前阶段的推荐方案。永不返回 null（除非流程已结束）。</summary>
        RecommendedPlan Recommend();

        /// <summary>当前阶段的长势等级，用于切换玉米贴图。</summary>
        GrowthLevel CurrentGrowth();

        /// <summary>取某阶段的历史结果。未结算返回 null。</summary>
        StageResult GetHistory(Stage s);
    }
}
