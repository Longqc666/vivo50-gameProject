using System;
using UnityEngine;
using UnityEngine.Events;
using D225.Quest;

namespace D225.Interact
{
    /// <summary>
    /// 可调查线索（桌面便笺、公告栏、值班记录）。调查后写入线索页并推进任务标记，
    /// 可以反复查看而不重复记入（T03）。
    /// </summary>
    [AddComponentMenu("D225/交互物/可调查线索 (InspectableClue)")]
    public sealed class InspectableClue : InteractableBase
    {
        [Header("线索")]
        [Tooltip("线索数据；文本写在 Description 里，直接作为字幕显示。")]
        [SerializeField]
        private ItemData clue;

        [Tooltip("调查表现：翻纸声、字幕淡入等，由成员5 接入。")]
        [SerializeField]
        private UnityEvent onInspected;

        /// <summary>调查事件（参数：线索数据）。</summary>
        public event Action<ItemData> Inspected;

        public ItemData Clue => clue;

        public bool HasBeenRead { get; private set; }

        public override string DisplayName
        {
            get
            {
                return clue != null ? clue.DisplayName : base.DisplayName;
            }
        }

        protected override InteractionState EvaluateStateCore(InteractionContext context)
        {
            return InteractionState.Inspectable;
        }

        protected override InteractionResult Execute(InteractionContext context)
        {
            if (clue == null)
            {
                Debug.LogError("[D225] InspectableClue 没有配置线索数据：" + name, this);
                return InteractionResult.Rejected("线索数据缺失");
            }

            bool newlyRecorded = false;
            if (context.Inventory != null)
            {
                newlyRecorded = context.Inventory.AddClue(clue, out _);
            }
            else
            {
                Debug.LogWarning("[D225] 调查线索时没有背包，线索未记入线索页：" + clue.DisplayName, this);
            }

            if (clue.InspectFlag != PuzzleFlag.None)
            {
                if (context.Quest != null)
                {
                    context.Quest.SetFlag(clue.InspectFlag);
                }
                else
                {
                    Debug.LogWarning("[D225] 调查线索时没有任务进度来源，标记未写入。", this);
                }
            }

            HasBeenRead = true;
            onInspected?.Invoke();
            Inspected?.Invoke(clue);

            string text = string.IsNullOrEmpty(clue.Description)
                ? "「" + clue.DisplayName + "」上只写着一些看不清的字。"
                : clue.Description;

            if (!newlyRecorded && context.Inventory != null)
            {
                text += "\n（这条线索已经记在任务日志里了）";
            }

            return InteractionResult.Ok(text);
        }

        /// <summary>编辑器工具与自测使用：绑定线索数据。</summary>
        public void Configure(ItemData clueData)
        {
            clue = clueData;
            HasBeenRead = false;
        }
    }
}
