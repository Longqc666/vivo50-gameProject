using System;

namespace D225.Core
{
    /// <summary>
    /// 给「构建后冒烟」用的小钩子：让流程层可以在不引用界面程序集的前提下，
    /// 真实地按一次主菜单的「开始」按钮（而不是绕过界面直接调 BeginGame）。
    /// 由界面适配器在运行时注册；没有注册时 <see cref="TryInvoke"/> 返回 false。
    /// </summary>
    public static class FlowStartRequest
    {
        /// <summary>界面注册的「按下开始」回调；返回是否真的点到了按钮。</summary>
        public static Func<bool> Handler { get; set; }

        public static bool TryInvoke()
        {
            return Handler != null && Handler();
        }

        /// <summary>场景卸载/适配器销毁时撤销注册。</summary>
        public static void Clear(Func<bool> handler)
        {
            if (ReferenceEquals(Handler, handler))
            {
                Handler = null;
            }
        }
    }
}
