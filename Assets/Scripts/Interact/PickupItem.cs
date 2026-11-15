using System;
using UnityEngine;
using UnityEngine.Events;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 可拾取道具（手电、保险丝、钥匙）。拾取成功后把自己从交互检测里摘掉，
    /// 连续按 E 不会重复获得（T03）；任务标记只写一次。
    /// </summary>
    [AddComponentMenu("D225/交互物/可拾取道具 (PickupItem)")]
    public sealed class PickupItem : InteractableBase
    {
        [Header("物品")]
        [Tooltip("物品配置资产；ItemId 必须与谜题条件一致。")]
        [SerializeField]
        private ItemData item;

        [Tooltip("拾取后隐藏自身渲染与碰撞（原型期色块占位）。")]
        [SerializeField]
        private bool hideVisualsOnPickup = true;

        [Tooltip("拾取表现：音效、粒子、提示动画等，由成员5 接入。")]
        [SerializeField]
        private UnityEvent onPicked;

        /// <summary>拾取成功事件（参数：物品）。</summary>
        public event Action<ItemData> Picked;

        public ItemData Item => item;

        public bool HasBeenPicked { get; private set; }

        public override string DisplayName
        {
            get
            {
                string baseName = base.DisplayName;
                if (item != null)
                {
                    return item.DisplayName;
                }

                return baseName;
            }
        }

        protected override string InactiveMessage => "这里已经没有东西了";

        protected override InteractionState EvaluateStateCore(InteractionContext context)
        {
            return HasBeenPicked ? InteractionState.Unavailable : InteractionState.Pickupable;
        }

        protected override InteractionResult Execute(InteractionContext context)
        {
            if (item == null)
            {
                Debug.LogError("[D225] PickupItem 没有配置 ItemData：" + name, this);
                return InteractionResult.Rejected("物品数据缺失");
            }

            if (context.Inventory == null)
            {
                return InteractionResult.Rejected("背包未初始化");
            }

            if (!context.Inventory.TryAdd(item, out string reason))
            {
                return InteractionResult.Rejected(reason);
            }

            HasBeenPicked = true;
            available = false;

            if (item.PickupFlag != PuzzleFlag.None)
            {
                if (context.Quest != null)
                {
                    context.Quest.SetFlag(item.PickupFlag);
                }
                else
                {
                    Debug.LogWarning("[D225] 拾取「" + item.DisplayName + "」时没有任务进度来源，标记未写入。", this);
                }
            }

            if (hideVisualsOnPickup)
            {
                HideVisuals();
            }

            onPicked?.Invoke();
            Picked?.Invoke(item);

            string message = string.IsNullOrEmpty(item.Description)
                ? "获得「" + item.DisplayName + "」"
                : item.Description;
            return InteractionResult.Ok(message);
        }

        /// <summary>编辑器工具与自测使用：绑定物品数据。</summary>
        public void Configure(ItemData itemData, bool hideOnPickup = true)
        {
            item = itemData;
            hideVisualsOnPickup = hideOnPickup;
            HasBeenPicked = false;
            available = true;
        }

        private void HideVisuals()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
