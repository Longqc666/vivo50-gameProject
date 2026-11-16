using System;
using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>交互物的四种状态（策划案 5.2）：决定 [E] 提示里的动词，或「暂不可用」的失败原因。</summary>
    public enum InteractionState
    {
        /// <summary>可拾取：手电、保险丝、钥匙等道具。</summary>
        Pickupable,

        /// <summary>可调查：便笺、公告栏、值班记录等线索。</summary>
        Inspectable,

        /// <summary>可使用：配电箱、门禁、出口联动装置、躲藏点等机关。</summary>
        Usable,

        /// <summary>暂不可用：条件未满足，提示里必须给出原因。</summary>
        Unavailable
    }

    /// <summary>一次交互的上下文：谁在按 E、背包是什么、任务进度从哪里读写。</summary>
    public readonly struct InteractionContext
    {
        public readonly GameObject Actor;
        public readonly Inventory Inventory;
        public readonly IQuestLog Quest;

        public InteractionContext(GameObject actor, Inventory inventory, IQuestLog quest)
        {
            Actor = actor;
            Inventory = inventory;
            Quest = quest;
        }

        public bool IsValid => Actor != null;

        /// <summary>
        /// 按角色自动装配上下文：背包取角色自身或父级，任务进度取场景内提供者。
        /// 供成员1 的 <c>PlayerInteractor</c> 直接调用。
        /// </summary>
        public static InteractionContext For(GameObject actor)
        {
            if (actor == null)
            {
                return default;
            }

            Inventory inventory = actor.GetComponentInParent<Inventory>();
            if (inventory == null)
            {
                inventory = actor.GetComponentInChildren<Inventory>(true);
            }

            return new InteractionContext(actor, inventory, QuestLogLocator.Resolve());
        }
    }

    /// <summary>交互执行结果：文本直接给 UI/字幕用，不抛异常、不返回 null。</summary>
    public readonly struct InteractionResult
    {
        public readonly bool Succeeded;
        public readonly string Message;

        private InteractionResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool HasMessage => !string.IsNullOrEmpty(Message);

        /// <summary>「本帧不该响应」：例如对象已被拿走，静默忽略。</summary>
        public static InteractionResult Ignored => new InteractionResult(false, string.Empty);

        public static InteractionResult Ok(string message = "")
        {
            return new InteractionResult(true, message);
        }

        public static InteractionResult Rejected(string reason)
        {
            return new InteractionResult(false, string.IsNullOrEmpty(reason) ? "暂时无法交互" : reason);
        }

        public override string ToString()
        {
            return (Succeeded ? "[OK] " : "[拒绝] ") + Message;
        }
    }

    /// <summary>
    /// 交互契约（成员4 定义，成员1/3/5 使用）。E 键、距离判定与提示显示都在调用方，
    /// 交互物只回答三件事：现在是什么状态、提示写什么、按下 E 之后发生什么。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>对象名称，提示文本与任务日志用。</summary>
        string DisplayName { get; }

        /// <summary>交互锚点，调用方用它算与玩家的距离。</summary>
        Transform Anchor { get; }

        /// <summary>是否还参与交互检测（被拾取后为 false，调用方应跳过）。</summary>
        bool IsActive { get; }

        /// <summary>当前四态之一；判定是只读的，不产生副作用。</summary>
        InteractionState EvaluateState(InteractionContext context);

        /// <summary>展示给玩家的提示：可用时形如「[E] 拾取 手电筒」，不可用时是原因文本。</summary>
        string GetPrompt(InteractionContext context);

        /// <summary>执行一次交互；被拒绝时 Message 是给玩家的原因。</summary>
        InteractionResult Interact(InteractionContext context);
    }
}
