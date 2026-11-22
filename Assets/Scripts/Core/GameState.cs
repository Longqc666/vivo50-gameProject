using System;

namespace D225.Core
{
    /// <summary>
    /// 游戏流程状态（分工表契约「GameState」）。成员6 定义，全员只读使用。
    /// </summary>
    public enum GameStateId
    {
        /// <summary>尚未接管流程（在编辑器里单独打开某个关卡场景时就是它）。此状态不冻结任何东西，方便分区调试。</summary>
        Init = 0,

        /// <summary>主菜单：可以开始游戏，不能操作角色。</summary>
        MainMenu = 1,

        /// <summary>探索中：唯一允许移动、交互、被抓捕结算的状态。</summary>
        Explore = 2,

        /// <summary>暂停：冻结角色、鬼怪与动画，不结算抓捕。</summary>
        Paused = 3,

        /// <summary>失败：已判定被抓，等待「从检查点重试」或「返回主菜单」。</summary>
        Failed = 4,

        /// <summary>结算：撤离成功，等待返回主菜单。</summary>
        Ending = 5
    }

    /// <summary>
    /// 全局状态门面。用法与文档保持一致：<c>if (GameState.Current != GameState.Explore) return;</c>
    /// 没有 <see cref="GameManager"/> 的场景里 <see cref="Current"/> 恒为 <see cref="Init"/>，
    /// 于是单场景调试与成员4 的联调场景行为不变。
    /// </summary>
    public static class GameState
    {
        /// <summary>与 <see cref="GameStateId"/> 同名的常量，便于写 <c>GameState.Current != GameState.Explore</c>。</summary>
        public const GameStateId Init = GameStateId.Init;
        public const GameStateId MainMenu = GameStateId.MainMenu;
        public const GameStateId Explore = GameStateId.Explore;
        public const GameStateId Paused = GameStateId.Paused;
        public const GameStateId Failed = GameStateId.Failed;
        public const GameStateId Ending = GameStateId.Ending;

        private static GameStateId current = GameStateId.Init;

        /// <summary>当前状态。</summary>
        public static GameStateId Current => current;

        /// <summary>状态变化：(旧状态, 新状态)。由 <see cref="GameManager"/> 发布。</summary>
        public static event Action<GameStateId, GameStateId> Changed;

        /// <summary>探索中（可以移动/交互/被抓）。</summary>
        public static bool IsGameplayActive => current == GameStateId.Explore;

        /// <summary>
        /// 是否应当冻结玩法输入。注意 <see cref="GameStateId.Init"/> 视为「流程未接管」，
        /// 不冻结，否则直接打开关卡场景调试时会连路都走不了。
        /// </summary>
        public static bool BlocksGameplayInput =>
            current == GameStateId.MainMenu ||
            current == GameStateId.Paused ||
            current == GameStateId.Failed ||
            current == GameStateId.Ending;

        /// <summary>由 GameManager 调用；外部不要直接改。</summary>
        internal static void Publish(GameStateId next)
        {
            if (current == next)
            {
                return;
            }

            GameStateId previous = current;
            current = next;
            Changed?.Invoke(previous, next);
        }

        /// <summary>回到「流程未接管」，只在域重载/测试之间使用。</summary>
        internal static void ResetForNewSession()
        {
            current = GameStateId.Init;
            Changed = null;
        }
    }
}
