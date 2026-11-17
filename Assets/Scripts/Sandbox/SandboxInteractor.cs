using System.Collections.Generic;
using UnityEngine;
using D225.Interact;

namespace D225.Sandbox
{
    /// <summary>
    /// 联调演示用的交互入口：距离 1.2、E 键、提示转发与反馈文本，按接口文档第 5 节的示例实现。
    /// 正式实现属于成员1 的 PlayerInteractor；本脚本只用于本场景手工验收与录制。
    /// </summary>
    [AddComponentMenu("D225/联调/演示交互入口 (SandboxInteractor)")]
    public sealed class SandboxInteractor : MonoBehaviour
    {
        [Tooltip("与策划案一致的交互半径（单位）。")]
        [SerializeField]
        private float interactRadius = 1.2f;

        [Tooltip("距离从角色胸口量起，避免高处的公告栏/配电箱够不到。")]
        [SerializeField]
        private float chestOffset = 0.5f;

        [SerializeField]
        private float feedbackSeconds = 2.6f;

        private readonly List<IInteractable> interactables = new List<IInteractable>();
        private InteractionContext context;
        private IInteractable current;
        private string feedback = string.Empty;
        private float feedbackTimer;

        public string Prompt { get; private set; } = string.Empty;

        public string Feedback => feedback;

        public bool FeedbackSucceeded { get; private set; }

        public IInteractable Current => current;

        private void Awake()
        {
            context = InteractionContext.For(gameObject);
            CacheInteractables();
        }

        private void Update()
        {
            current = FindNearest();

            if (current != null && current.IsActive)
            {
                string prompt = current.GetPrompt(context);
                Prompt = prompt ?? string.Empty;
            }
            else
            {
                Prompt = string.Empty;
            }

            if (feedbackTimer > 0f)
            {
                feedbackTimer -= Time.deltaTime;
                if (feedbackTimer <= 0f)
                {
                    feedback = string.Empty;
                }
            }

            if (current != null && Input.GetKeyDown(KeyCode.E))
            {
                InteractionResult result = current.Interact(context);
                if (result.HasMessage)
                {
                    feedback = result.Message;
                    FeedbackSucceeded = result.Succeeded;
                    feedbackTimer = feedbackSeconds;
                }
            }
        }

        /// <summary>按当前场景里实现的接口收集一次；演示场景的道具都是静态摆放。</summary>
        private void CacheInteractables()
        {
            interactables.Clear();
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IInteractable interactable)
                {
                    interactables.Add(interactable);
                }
            }
        }

        private IInteractable FindNearest()
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * chestOffset;
            float bestDistance = interactRadius;
            IInteractable best = null;

            for (int i = 0; i < interactables.Count; i++)
            {
                IInteractable candidate = interactables[i];
                if (candidate == null || !candidate.IsActive)
                {
                    continue;
                }

                float distance = Vector2.Distance(origin, candidate.Anchor.position);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
