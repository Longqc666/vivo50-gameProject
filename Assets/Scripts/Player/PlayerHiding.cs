using UnityEngine;
using D225.Interact;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerHiding : MonoBehaviour, IHidableActor
{
    [Header("运行时观察")]
    [SerializeField] private bool isHidden;
    [SerializeField] private bool isCatchLocked;

    private PlayerController controller;
    private Rigidbody2D rb;
    private SpriteRenderer[] renderers;
    private bool[] previousVisibility;

    private Vector2 entryPosition;
    private bool previousSimulated;
    private bool previousControlLocked;

    public bool IsHidden => isHidden;
    public bool IsCatchLocked => isCatchLocked;
    public HidingSpot CurrentSpot { get; private set; }

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    public void EnterHiding(HidingSpot spot)
    {
        if (isHidden || isCatchLocked || spot == null)
            return;

        entryPosition = rb.position;
        previousSimulated = rb.simulated;
        previousControlLocked = controller.IsControlLocked;

        CurrentSpot = spot;
        isHidden = true;

        controller.SetControlLocked(true);
        rb.velocity = Vector2.zero;

        // 躲藏期间停止物理模拟，避免重力导致人物下落
        rb.simulated = false;
        rb.position = (Vector2)spot.HidePoint.position;

        previousVisibility = new bool[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            previousVisibility[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
    }

    public void ExitHiding()
    {
        if (!isHidden)
            return;

        // 返回进入柜子前的位置
        rb.position = entryPosition;
        rb.velocity = Vector2.zero;
        rb.simulated = previousSimulated;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = previousVisibility[i];
        }

        isHidden = false;
        CurrentSpot = null;

        controller.SetControlLocked(
            previousControlLocked || isCatchLocked
        );
    }

    // 供鬼怪抓捕模块调用
    public void SetCatchLocked(bool locked)
    {
        isCatchLocked = locked;

        if (locked)
            controller.SetControlLocked(true);
    }
}