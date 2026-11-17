using UnityEngine;
using D225.Interact;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Inventory))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("交互设置")]
    [SerializeField] private float interactRadius = 1.2f;

    [Header("运行时观察")]
    [SerializeField] private string currentPrompt;
    [SerializeField] private string lastFeedback;

    private PlayerController controller;
    private InteractionContext context;
    private MonoBehaviour[] candidates;

    public string CurrentPrompt => currentPrompt;
    public string LastFeedback => lastFeedback;
    public int FeedbackVersion { get; private set; }

    private void Start()
    {
        controller = GetComponent<PlayerController>();
        RefreshInteractionContext();
        RefreshTargets();
    }

    public void RefreshInteractionContext()
    {
        context = InteractionContext.For(gameObject);
    }

    // 新增交互物或加载区域后，可以再次调用
    public void RefreshTargets()
    {
        candidates = FindObjectsOfType<MonoBehaviour>(true);
    }

    private void Update()
    {
        PlayerHiding hiding = GetComponent<PlayerHiding>();

        bool isHidden = hiding != null && hiding.IsHidden;
        bool isCaught = hiding != null && hiding.IsCatchLocked;

        if (Time.timeScale == 0f ||
            isCaught ||
            (controller.IsControlLocked && !isHidden))
        {
            currentPrompt = string.Empty;
            return;
        }

        // 躲藏时只操作当前柜子，防止误拾取旁边的物品
        IInteractable target = isHidden
            ? hiding.CurrentSpot
            : FindNearest();
        currentPrompt = target != null
            ? target.GetPrompt(context)
            : string.Empty;

        if (target != null && Input.GetKeyDown(KeyCode.E))
        {
            InteractionResult result = target.Interact(context);

            if (result.HasMessage)
            {
                lastFeedback = result.Message;
                FeedbackVersion++;
                Debug.Log("玩家交互：" + result.Message, this);
            }
        }
    }

    private IInteractable FindNearest()
    {
        IInteractable nearest = null;
        float nearestDistance = interactRadius;

        foreach (MonoBehaviour candidate in candidates)
        {
            // 同时排除已销毁、关闭或禁用的组件
            if (candidate == null || !candidate.isActiveAndEnabled)
                continue;

            if (!(candidate is IInteractable interactable))
                continue;

            if (!interactable.IsActive || interactable.Anchor == null)
                continue;

            float distance = Vector2.Distance(
                transform.position,
                interactable.Anchor.position
            );

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = interactable;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}