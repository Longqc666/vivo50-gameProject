using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class EnemyCatch : MonoBehaviour
{
    [SerializeField, Min(0f)] private float contactTolerance = 0.12f;

    private EnemyAI enemy;
    private EnemyPerception perception;
    private Collider2D bodyCollider;

    public bool HasCaught { get; private set; }
    public event Action<PlayerHiding> PlayerCaught;

    private void Awake()
    {
        enemy = GetComponent<EnemyAI>();
        perception = GetComponent<EnemyPerception>();
        bodyCollider = GetComponent<Collider2D>();
    }

    internal bool TryCatch(PlayerHiding player, Collider2D playerCollider)
    {
        if (HasCaught || !enemy.CanAct || enemy.State != EnemyAI.EnemyState.Chase
            || player == null || player.IsHidden || player.IsCatchLocked || playerCollider == null)
            return false;

        ColliderDistance2D contact = bodyCollider.Distance(playerCollider);
        if (!contact.isValid || contact.distance > contactTolerance || !perception.HasClearLineTo(player.transform))
            return false;

        // 先锁定玩家的交互，再通知失败流程。同一轮只发出一次事件。
        HasCaught = true;
        player.SetCatchLocked(true);
        PlayerCaught?.Invoke(player);
        return true;
    }

    internal void ResetLatch() => HasCaught = false;
}
