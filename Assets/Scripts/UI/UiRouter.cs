using System;
using System.Collections;
using UnityEngine;

namespace D225.UI
{
    /// <summary>
    /// 界面总开关：主菜单、局内 HUD、暂停、失败、结局、返回确认，以及转场黑幕。
    /// 规则只有一条——「同一时刻只有一个页面亮着，Esc 只归这里管」。
    /// 它不判断游戏逻辑（能不能重试、能不能回菜单由流程模块决定），只负责显示和冻结时间。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("D225/界面/界面路由 (UiRouter)")]
    public sealed class UiRouter : MonoBehaviour
    {
        [Header("页面（拖场景里的对象，全部 RectTransform 根节点）")]
        [SerializeField] private GameObject mainMenu;
        [SerializeField] private GameObject hud;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private GameObject failOverlay;
        [SerializeField] private GameObject endingOverlay;

        [Header("返回确认（二次确认小窗，可留空）")]
        [SerializeField] private GameObject backConfirm;

        [Header("转场黑幕（全屏黑色 Image + CanvasGroup）")]
        [SerializeField] private CanvasGroup fadeGroup;

        [Header("行为")]
        [Tooltip("勾上后本脚本负责把 Time.timeScale 设为 0/1。等成员6 的 GameManager 接管后取消勾选")]
        [SerializeField] private bool manageTimeScale = true;

        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        [Tooltip("启动时是否先显示主菜单")]
        [SerializeField] private bool startInMainMenu = true;

        /// <summary>暂停状态变化（音效、输入门禁可以订阅）。</summary>
        public event Action<bool> PausedChanged;

        /// <summary>玩家点了「从检查点重试」——具体恢复交给成员6。</summary>
        public event Action RetryRequested;

        /// <summary>玩家确认返回主菜单——具体加载场景交给成员6。</summary>
        public event Action ReturnToMenuRequested;

        public bool InGame { get; private set; }
        public bool IsPaused { get; private set; }

        // 失败/结局界面亮着时，Esc 不应该把玩家「救」回游戏
        private bool escLocked;

        private void Start()
        {
            if (startInMainMenu)
            {
                ShowMainMenu();
            }
            else
            {
                StartGame();
            }
        }

        private void Update()
        {
            if (!InGame || escLocked)
            {
                return;
            }

            if (backConfirm != null && backConfirm.activeSelf)
            {
                return; // 确认框开着时，Esc 交给确认框自己处理
            }

            if (Input.GetKeyDown(pauseKey))
            {
                if (IsPaused)
                {
                    Resume();
                }
                else
                {
                    Pause();
                }
            }
        }

        // ---------- 主流程 ----------

        public void ShowMainMenu()
        {
            InGame = false;
            escLocked = false;
            SetActive(mainMenu, true);
            SetActive(hud, false);
            SetActive(pauseOverlay, false);
            SetActive(failOverlay, false);
            SetActive(endingOverlay, false);
            SetActive(backConfirm, false);
            ApplyPause(false);
        }

        public void StartGame()
        {
            InGame = true;
            escLocked = false;
            SetActive(mainMenu, false);
            SetActive(hud, true);
            SetActive(pauseOverlay, false);
            SetActive(failOverlay, false);
            SetActive(endingOverlay, false);
            SetActive(backConfirm, false);
            ApplyPause(false);
        }

        public void Pause()
        {
            if (!InGame || IsPaused)
            {
                return;
            }

            SetActive(pauseOverlay, true);
            ApplyPause(true);
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            SetActive(pauseOverlay, false);
            SetActive(backConfirm, false);
            ApplyPause(false);
        }

        public void ShowFail()
        {
            SetActive(pauseOverlay, false);
            SetActive(failOverlay, true);
            escLocked = true;
            ApplyPause(true);
        }

        public void ShowEnding()
        {
            SetActive(pauseOverlay, false);
            SetActive(endingOverlay, true);
            escLocked = true;
            ApplyPause(true);
        }

        /// <summary>失败界面的「重试检查点」按钮绑这个。</summary>
        public void RequestRetry()
        {
            SetActive(failOverlay, false);
            SetActive(endingOverlay, false);
            escLocked = false;
            ApplyPause(false);
            RetryRequested?.Invoke();
        }

        /// <summary>暂停界面的「返回主菜单」按钮绑这个（会先弹二次确认）。</summary>
        public void AskBackToMenu()
        {
            if (backConfirm != null)
            {
                SetActive(backConfirm, true);
                return;
            }

            ConfirmBackToMenu();
        }

        public void CancelBack()
        {
            SetActive(backConfirm, false);
        }

        public void ConfirmBackToMenu()
        {
            SetActive(backConfirm, false);
            ShowMainMenu();
            ReturnToMenuRequested?.Invoke();
        }

        // ---------- 转场黑幕 ----------

        public IEnumerator FadeOut(float seconds = 0.6f)
        {
            yield return FadeTo(1f, seconds);
        }

        public IEnumerator FadeIn(float seconds = 0.6f)
        {
            yield return FadeTo(0f, seconds);
        }

        /// <summary>切场景前先把黑幕拉满，避免加载时露出空白画面。</summary>
        public void SetFadeInstant(float alpha)
        {
            if (fadeGroup == null)
            {
                return;
            }

            fadeGroup.alpha = Mathf.Clamp01(alpha);
            fadeGroup.blocksRaycasts = alpha > 0.01f;
        }

        private IEnumerator FadeTo(float target, float seconds)
        {
            if (fadeGroup == null)
            {
                yield break;
            }

            fadeGroup.blocksRaycasts = true;
            float from = fadeGroup.alpha;
            float duration = Mathf.Max(0.01f, seconds);
            float t = 0f;

            while (t < duration)
            {
                // 界面动画一律用 unscaledDeltaTime，暂停时也能正常淡入淡出
                t += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(from, target, t / duration);
                yield return null;
            }

            fadeGroup.alpha = target;
            fadeGroup.blocksRaycasts = target > 0.01f;
        }

        // ---------- 内部 ----------

        private void ApplyPause(bool paused)
        {
            IsPaused = paused;

            if (manageTimeScale)
            {
                Time.timeScale = paused ? 0f : 1f;
            }

            PausedChanged?.Invoke(paused);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
