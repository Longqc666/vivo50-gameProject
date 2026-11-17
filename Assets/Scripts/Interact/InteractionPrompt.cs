namespace D225.Interact
{
    /// <summary>
    /// 提示文本的统一格式。成员5 的 HUD 直接用 <see cref="For"/> 与 <see cref="Action"/> 的输出，
    /// 保证「[E] 动作 名称」在全组只有一种写法（改键位时只改 <see cref="KeyLabel"/>）。
    /// </summary>
    public static class InteractionPrompt
    {
        /// <summary>交互键显示名，默认 E（策划案 4 操作表）。</summary>
        public static string KeyLabel { get; set; } = "E";

        /// <summary>四态对应的动词；暂不可用没有动词，直接显示原因。</summary>
        public static string VerbOf(InteractionState state)
        {
            switch (state)
            {
                case InteractionState.Pickupable:
                    return "拾取";
                case InteractionState.Inspectable:
                    return "查看";
                case InteractionState.Usable:
                    return "使用";
                default:
                    return string.Empty;
            }
        }

        /// <summary>可用状态的标准提示，例如「[E] 拾取 手电筒」。</summary>
        public static string For(InteractionState state, string displayName)
        {
            if (state == InteractionState.Unavailable)
            {
                return displayName ?? string.Empty;
            }

            return Action(VerbOf(state), displayName);
        }

        /// <summary>自定义动词的提示，例如躲藏点离开时用「[E] 离开 储物柜」。</summary>
        public static string Action(string verb, string displayName)
        {
            string name = string.IsNullOrEmpty(displayName) ? "目标" : displayName;
            return "[" + KeyLabel + "] " + verb + " " + name;
        }
    }
}
