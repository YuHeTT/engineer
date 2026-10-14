namespace Engineer.UI
{
    /// <summary>
    /// 每个界面屏实现这一个接口，由 <see cref="GameFlowController"/> 统一驱动。
    /// 约定：屏自己**不要**去 SetActive 别的屏，一切切换都走状态机。
    /// </summary>
    public interface IScreen
    {
        /// <summary>本屏对应的状态。</summary>
        GameState State { get; }

        /// <summary>进入本屏。args 由状态机原样透传，具体类型由各屏自行约定。</summary>
        void OnEnter(object args);

        /// <summary>离开本屏，做清理。</summary>
        void OnExit();
    }
}
