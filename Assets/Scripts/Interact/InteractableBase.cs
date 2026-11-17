using UnityEngine;

namespace D225.Interact
{
    /// <summary>
    /// 交互物公共基类：统一「四态判定 → 提示文本 → 执行」的流程，子类只写条件与结果。
    /// 调用方（成员1 的交互入口）负责距离、按键与提示显示；本类不做物理检测。
    /// </summary>
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [Header("交互物")]
        [Tooltip("提示与任务日志里显示的名字。")]
        [SerializeField]
        protected string displayName = "可交互物";

        [Tooltip("是否还参与交互检测；拾取类对象在拿走后由子类置为 false。")]
        [SerializeField]
        protected bool available = true;

        public virtual string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        public virtual Transform Anchor => transform;

        public virtual bool IsActive => available && enabled && gameObject.activeInHierarchy;

        /// <summary>对象已经不在原处（例如被拿走）时的提示。</summary>
        protected virtual string InactiveMessage => DisplayName + "已经不在原处";

        /// <summary>只读判定：不修改任何状态。</summary>
        public InteractionState EvaluateState(InteractionContext context)
        {
            if (!IsActive)
            {
                return InteractionState.Unavailable;
            }

            return EvaluateStateCore(context);
        }

        /// <summary>四态判定的子类实现（此时对象一定是激活状态）。</summary>
        protected abstract InteractionState EvaluateStateCore(InteractionContext context);

        public virtual string GetPrompt(InteractionContext context)
        {
            InteractionState state = EvaluateState(context);
            if (state == InteractionState.Unavailable)
            {
                return BlockedReason(context);
            }

            return InteractionPrompt.For(state, DisplayName);
        }

        /// <summary>暂不可用的原因；子类按条件给出具体文案。</summary>
        public virtual string BlockedReason(InteractionContext context)
        {
            return InactiveMessage;
        }

        public InteractionResult Interact(InteractionContext context)
        {
            if (!IsActive)
            {
                return InteractionResult.Rejected(InactiveMessage);
            }

            if (!context.IsValid)
            {
                return InteractionResult.Rejected("没有可操作的角色");
            }

            InteractionState state = EvaluateStateCore(context);
            if (state == InteractionState.Unavailable)
            {
                return InteractionResult.Rejected(BlockedReason(context));
            }

            return Execute(context);
        }

        /// <summary>真正执行交互；此时状态一定是可用。</summary>
        protected abstract InteractionResult Execute(InteractionContext context);

        /// <summary>编辑器工具与自测使用：改名而不改 Prefab。</summary>
        public void SetDisplayName(string value)
        {
            displayName = value;
        }
    }
}
