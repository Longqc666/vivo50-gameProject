using System;
using UnityEngine;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 躲藏点（储物柜、清洁间）。占用状态由本组件维护，移动锁定交给角色的 <see cref="IHidableActor"/>；
    /// 被鬼怪贴身抓住时（<see cref="IHidableActor.IsCatchLocked"/>）拒绝进入，避免瞬间免疫（T06）。
    /// </summary>
    [AddComponentMenu("D225/交互物/躲藏点 (HidingSpot)")]
    public sealed class HidingSpot : InteractableBase
    {
        [Header("躲藏点")]
        [Tooltip("玩家躲进去后停留的位置；留空则用自身位置。")]
        [SerializeField]
        private Transform hidePoint;

        [Tooltip("第一次进入躲藏点时写入的任务标记（教学用）。")]
        [SerializeField]
        private PuzzleFlag firstHideFlag = PuzzleFlag.FirstHiding;

        [Tooltip("剧情锁：勾选后暂时进不去。")]
        [SerializeField]
        private bool locked;

        private IHidableActor occupant;

        /// <summary>是否已有人躲在里面。</summary>
        public bool IsOccupied => Occupant != null;

        /// <summary>当前占用者（可能为空）。</summary>
        public IHidableActor Occupant
        {
            get
            {
                if (IsDestroyed(occupant))
                {
                    occupant = null;
                }

                return occupant;
            }
        }

        public Transform HidePoint => hidePoint != null ? hidePoint : transform;

        public bool IsLocked => locked;

        /// <summary>离开躲藏点事件（参数：是否有人仍占用）。</summary>
        public event Action<HidingSpot> OccupancyChanged;

        public override string DisplayName => string.IsNullOrWhiteSpace(base.DisplayName) ? "躲藏点" : base.DisplayName;

        public override string GetPrompt(InteractionContext context)
        {
            IHidableActor actor = ResolveActor(context);
            if (actor != null && ReferenceEquals(actor, Occupant))
            {
                return InteractionPrompt.Action("离开", DisplayName);
            }

            if (EvaluateState(context) == InteractionState.Usable)
            {
                return InteractionPrompt.Action("躲进", DisplayName);
            }

            return base.GetPrompt(context);
        }

        protected override InteractionState EvaluateStateCore(InteractionContext context)
        {
            IHidableActor actor = ResolveActor(context);
            if (actor == null)
            {
                return InteractionState.Unavailable;
            }

            if (ReferenceEquals(actor, Occupant))
            {
                return InteractionState.Usable;
            }

            if (Occupant != null || locked || actor.IsCatchLocked || actor.IsHidden)
            {
                return InteractionState.Unavailable;
            }

            return InteractionState.Usable;
        }

        public override string BlockedReason(InteractionContext context)
        {
            if (!IsActive)
            {
                return base.BlockedReason(context);
            }

            IHidableActor actor = ResolveActor(context);
            if (actor == null)
            {
                return "这里不能躲藏";
            }

            if (ReferenceEquals(actor, Occupant))
            {
                return "你已经在这里了";
            }

            if (Occupant != null)
            {
                return DisplayName + "里已经有人了";
            }

            if (locked)
            {
                return DisplayName + "卡住了，打不开";
            }

            if (actor.IsCatchLocked)
            {
                return "被鬼怪抓住的瞬间无法躲藏";
            }

            if (actor.IsHidden)
            {
                return "先离开现在的位置";
            }

            return "现在不能躲进去";
        }

        protected override InteractionResult Execute(InteractionContext context)
        {
            IHidableActor actor = ResolveActor(context);
            if (actor == null)
            {
                return InteractionResult.Rejected("这里不能躲藏");
            }

            if (ReferenceEquals(actor, Occupant))
            {
                ReleaseOccupant();
                return InteractionResult.Ok("你从" + DisplayName + "里钻了出来，走廊里暂时没有声音。");
            }

            if (Occupant != null)
            {
                return InteractionResult.Rejected(DisplayName + "里已经有人了");
            }

            if (locked)
            {
                return InteractionResult.Rejected(DisplayName + "卡住了，打不开");
            }

            if (actor.IsCatchLocked)
            {
                return InteractionResult.Rejected("被鬼怪抓住的瞬间无法躲藏");
            }

            if (actor.IsHidden)
            {
                return InteractionResult.Rejected("先离开现在的位置");
            }

            occupant = actor;
            actor.EnterHiding(this);
            OccupancyChanged?.Invoke(this);

            if (firstHideFlag != PuzzleFlag.None && context.Quest != null)
            {
                context.Quest.SetFlag(firstHideFlag);
            }

            return InteractionResult.Ok("你躲进了" + DisplayName + "，屏住呼吸。");
        }

        /// <summary>流程侧强制清空（检查点重试/场景卸载时调用）。</summary>
        public void ForceRelease()
        {
            if (Occupant == null)
            {
                return;
            }

            IHidableActor previous = Occupant;
            occupant = null;
            previous.ExitHiding();
            OccupancyChanged?.Invoke(this);
        }

        /// <summary>编辑器工具与自测使用：设置隐藏点与锁。</summary>
        public void Configure(Transform point, bool isLocked = false, PuzzleFlag flagOnFirstHide = PuzzleFlag.FirstHiding)
        {
            hidePoint = point;
            locked = isLocked;
            firstHideFlag = flagOnFirstHide;
            occupant = null;
        }

        private void ReleaseOccupant()
        {
            IHidableActor previous = Occupant;
            occupant = null;
            if (previous != null)
            {
                previous.ExitHiding();
            }

            OccupancyChanged?.Invoke(this);
        }

        private IHidableActor ResolveActor(InteractionContext context)
        {
            if (context.Actor == null)
            {
                return null;
            }

            MonoBehaviour[] candidates = context.Actor.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IHidableActor actor)
                {
                    return actor;
                }
            }

            return null;
        }

        private static bool IsDestroyed(IHidableActor actor)
        {
            return actor is UnityEngine.Object unityObject && unityObject == null;
        }
    }
}
