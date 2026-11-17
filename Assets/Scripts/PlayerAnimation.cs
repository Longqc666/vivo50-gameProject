using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private float scaredDuration = 0.5f;

    private PlayerController player;
    private Animator animator;
    private float scaredTimer;

    private static readonly int MoveState =
        Animator.StringToHash("MoveState");

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        int state = 0;

        if (scaredTimer > 0f)
        {
            state = 3;
            scaredTimer = Mathf.Max(
                0f,
                scaredTimer - Time.deltaTime
            );
        }
        else if (player.IsMoving)
        {
            state = player.IsRunning ? 2 : 1;
        }

        animator.SetInteger(MoveState, state);
    }

    // 之后由鬼怪首次发现玩家时调用
    public void PlayScared()
    {
        if (Time.timeScale == 0f || player.IsControlLocked)
            return;

        // 播放期间忽略重复触发
        if (scaredTimer > 0f)
            return;

        scaredTimer = scaredDuration;
    }

    [ContextMenu("测试受惊动画")]
    private void TestScared()
    {
        if (Application.isPlaying)
            PlayScared();
    }
}