using System;
using System.Collections.Generic;

namespace D225.Core
{
    /// <summary>
    /// 流程状态机（纯 C#，可 EditMode 测试）。只做一件事：判断「能不能从当前状态走到目标状态」。
    /// 非法迁移一律拒绝并给出中文原因，便于控制台定位；重复迁移（比如连按两次 Esc）视为无操作。
    /// </summary>
    /// <remarks>
    /// 迁移表（策划案 5.6、9「状态与异常」）：
    /// Init     → MainMenu / Explore
    /// MainMenu → Explore
    /// Explore  → Paused / Failed / Ending
    /// Paused   → Explore / MainMenu          （暂停期间不允许失败，避免「暂停仍被抓」）
    /// Failed   → Explore / MainMenu
    /// Ending   → MainMenu
    /// </remarks>
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameStateId, GameStateId[]> Allowed =
            new Dictionary<GameStateId, GameStateId[]>
            {
                { GameStateId.Init, new[] { GameStateId.MainMenu, GameStateId.Explore } },
                { GameStateId.MainMenu, new[] { GameStateId.Explore } },
                { GameStateId.Explore, new[] { GameStateId.Paused, GameStateId.Failed, GameStateId.Ending } },
                { GameStateId.Paused, new[] { GameStateId.Explore, GameStateId.MainMenu } },
                { GameStateId.Failed, new[] { GameStateId.Explore, GameStateId.MainMenu } },
                { GameStateId.Ending, new[] { GameStateId.MainMenu } }
            };

        /// <summary>状态迁移成功后触发：(旧状态, 新状态)。</summary>
        public event Action<GameStateId, GameStateId> Changed;

        /// <summary>被拒绝的迁移：(目标状态, 原因)。用于日志与异常清单。</summary>
        public event Action<GameStateId, string> Rejected;

        public GameStateMachine(GameStateId initial = GameStateId.Init)
        {
            Current = initial;
        }

        public GameStateId Current { get; private set; }

        public bool IsGameplayActive => Current == GameStateId.Explore;

        /// <summary>目标状态是否合法（不产生副作用）。</summary>
        public bool CanEnter(GameStateId next)
        {
            if (next == Current)
            {
                return true; // 幂等
            }

            return Allowed.TryGetValue(Current, out GameStateId[] targets) &&
                   Array.IndexOf(targets, next) >= 0;
        }

        /// <summary>尝试迁移；非法时返回 false 并写出原因。</summary>
        public bool TryEnter(GameStateId next, out string reason)
        {
            if (next == Current)
            {
                reason = $"{Current} 已经是当前状态，忽略重复请求";
                return false;
            }

            if (!CanEnter(next))
            {
                reason = $"不允许从 {Current} 迁移到 {next}";
                Rejected?.Invoke(next, reason);
                return false;
            }

            GameStateId previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
            reason = string.Empty;
            return true;
        }

        /// <summary>测试与存档恢复用：不管迁移表，直接落位。正式流程不要调用。</summary>
        internal void ForceSet(GameStateId next)
        {
            Current = next;
        }
    }
}
