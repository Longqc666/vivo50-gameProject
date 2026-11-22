using System.Collections;
using D225.Core;
using D225.UI;
using UnityEngine;

namespace D225.Flow
{
    /// <summary>
    /// 成员6 的界面适配层：把成员5 的 <see cref="UiRouter"/> 接到流程状态机上，并补一个不依赖 UiRouter 的转场黑幕。
    ///
    /// 职责边界：
    /// · 不画任何 UI、不改 UiRouter 的脚本；只订阅它已经暴露的事件（PausedChanged / RetryRequested / ReturnToMenuRequested）。
    /// · <see cref="IGameFlowView"/> 的淡入淡出由本组件自己的 CanvasGroup 完成，所以 UiRouter.fadeGroup 留空也能转场。
    /// · 界面想要「暂停」，先问 GameManager 允不允许；界面显示什么页面，由 GameManager 的状态变化决定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/流程/界面适配 (UiRouterFlowAdapter)")]
    public sealed class UiRouterFlowAdapter : MonoBehaviour, IGameFlowView
    {
        [Tooltip("转场黑幕。留空时自动找子节点里的 Fade。")]
        [SerializeField] private CanvasGroup fadeGroup;

        [Tooltip("找不到 UiRouter 时打印一次提示（本模块不负责补 UI）。")]
        [SerializeField] private bool warnWhenMissingRouter = true;

        private UiRouter router;
        private bool subscribed;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            router = GetComponentInChildren<UiRouter>(true);

            if (fadeGroup == null)
            {
                Transform found = FindDeep(transform, "Fade");
                if (found != null)
                {
                    fadeGroup = found.GetComponent<CanvasGroup>();
                }
            }

            if (router == null)
            {
                if (warnWhenMissingRouter)
                {
                    Debug.LogWarning("[D225.流程] D225_FlowMenu 里没有 UiRouter：菜单/暂停/失败页不可用，" +
                                     "但流程状态与黑幕仍工作。", this);
                }

                return;
            }

            router.PausedChanged += HandlePausedChanged;
            router.RetryRequested += HandleRetryRequested;
            router.ReturnToMenuRequested += HandleReturnToMenuRequested;
            subscribed = true;

            StartCoroutine(SyncWithStateAfterFirstFrame());
        }

        private void OnDestroy()
        {
            if (!subscribed || router == null)
            {
                return;
            }

            router.PausedChanged -= HandlePausedChanged;
            router.RetryRequested -= HandleRetryRequested;
            router.ReturnToMenuRequested -= HandleReturnToMenuRequested;
            subscribed = false;
        }

        // ------------------------------------------------------------------ IGameFlowView

        public void ShowMainMenu()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (router == null)
            {
                return;
            }

            if (router.IsPaused)
            {
                router.Resume();
            }

            router.ShowMainMenu();
        }

        public void ShowGameplay()
        {
            if (router == null)
            {
                return;
            }

            if (router.IsPaused)
            {
                router.Resume();
            }

            router.StartGame();
        }

        public void ShowPause(bool paused)
        {
            if (router == null)
            {
                return;
            }

            if (paused)
            {
                router.Pause();
            }
            else
            {
                router.Resume();
            }
        }

        public void ShowFail()
        {
            router?.ShowFail();
        }

        public void ShowEnding()
        {
            if (router == null)
            {
                SetFadeInstant(0f);
                return;
            }

            router.ShowEnding();
        }

        public IEnumerator FadeOut(float seconds)
        {
            SetFadeInstant(1f);
            yield break;
        }

        public IEnumerator FadeIn(float seconds)
        {
            if (fadeGroup == null)
            {
                yield break;
            }

            fadeGroup.blocksRaycasts = true;
            float from = fadeGroup.alpha;
            float duration = Mathf.Max(0.01f, seconds);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 转场动画不受 timeScale（加载/重试时它是 0）影响
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, 0f, elapsed / duration);
                yield return null;
            }

            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
        }

        public void SetFadeInstant(float alpha)
        {
            if (fadeGroup == null)
            {
                return;
            }

            fadeGroup.alpha = Mathf.Clamp01(alpha);
            fadeGroup.blocksRaycasts = alpha > 0.01f;
        }

        // ------------------------------------------------------------------ 事件转发

        private void HandlePausedChanged(bool paused)
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return;
            }

            // 只有「玩家主动暂停」和「从暂停恢复」才往流程层递；失败/结算页引发的暂停由流程层自己驱动
            if (paused && manager.State == GameStateId.Explore)
            {
                manager.RequestPause(true);
            }
            else if (!paused && manager.State == GameStateId.Paused)
            {
                manager.RequestPause(false);
            }
        }

        private void HandleRetryRequested()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.RetryFromCheckpoint();
        }

        private void HandleReturnToMenuRequested()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.ReturnToMainMenu();
        }

        // ------------------------------------------------------------------ 内部

        private IEnumerator SyncWithStateAfterFirstFrame()
        {
            // 等一帧，确保 UiRouter.Start() 自己那次 ShowMainMenu 已经执行完
            yield return null;

            GameManager manager = GameManager.Instance;
            if (manager == null)
            {
                yield break;
            }

            switch (manager.State)
            {
                case GameStateId.Explore:
                    ShowGameplay();
                    SetFadeInstant(0f);
                    break;
                case GameStateId.Paused:
                    ShowGameplay();
                    ShowPause(true);
                    break;
                case GameStateId.Failed:
                    ShowGameplay();
                    ShowFail();
                    break;
                case GameStateId.Ending:
                    ShowGameplay();
                    ShowEnding();
                    break;
                default:
                    ShowMainMenu();
                    SetFadeInstant(0f);
                    break;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }
    }
}
