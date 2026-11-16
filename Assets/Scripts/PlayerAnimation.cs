using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{
    private PlayerController player;
    private Animator animator;

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

        if (player.IsMoving)
        {
            state = player.IsRunning ? 2 : 1;
        }

        animator.SetInteger(MoveState, state);
    }
}