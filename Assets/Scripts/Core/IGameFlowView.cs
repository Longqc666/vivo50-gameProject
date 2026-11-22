using System.Collections;
using UnityEngine;

namespace D225.Core
{
    /// <summary>
    /// 流程模块对「表现层」的唯一要求：显示哪个页面、什么时候淡入淡出。
    /// 由 UI 侧的适配器实现（本仓库是 <c>D225.Flow.UiRouterFlowAdapter</c>，包装成员5 的 <c>UiRouter</c>），
    /// 这样 <c>D225.Core</c> 不必引用界面代码，界面也可以随时替换而不动流程。
    /// </summary>
    public interface IGameFlowView
    {
        /// <summary>显示主菜单页。</summary>
        void ShowMainMenu();

        /// <summary>进入局内：隐藏菜单，显示 HUD。</summary>
        void ShowGameplay();

        /// <summary>暂停页开/关。</summary>
        void ShowPause(bool paused);

        /// <summary>失败页（重试检查点 / 返回主菜单）。</summary>
        void ShowFail();

        /// <summary>结算页（撤离成功）。</summary>
        void ShowEnding();

        /// <summary>淡出到黑（切场景、重试前）。</summary>
        IEnumerator FadeOut(float seconds);

        /// <summary>从黑淡入（场景加载完成后）。</summary>
        IEnumerator FadeIn(float seconds);

        /// <summary>立刻设置黑幕透明度（加载中避免露出空白画面）。</summary>
        void SetFadeInstant(float alpha);
    }
}
