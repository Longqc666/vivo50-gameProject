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
    // 失败重试时调用：释放柜子、恢复显示与物理、清除抓捕锁定
    public void ResetAfterFailure()
    {
        isCatchLocked = false;

        HidingSpot spot = CurrentSpot;

        if (spot != null && ReferenceEquals(spot.Occupant, this))
        {
            // 由柜子释放占用，并回调 ExitHiding
            spot.ForceRelease();
        }

        // 柜子已被移除时，也保证人物能恢复
        if (isHidden)
        {
            ExitHiding();
        }

        CurrentSpot = null;
        rb.velocity = Vector2.zero;

        // 重试复位完成后允许操作
        controller.SetControlLocked(false);
    }

    [ContextMenu("测试抓捕锁定")]
    private void TestCatchLock()
    {
        if (Application.isPlaying)
            SetCatchLocked(true);
    }

    [ContextMenu("测试失败复位")]
    private void TestFailureReset()
    {
        if (Application.isPlaying)
            ResetAfterFailure();
    }
}