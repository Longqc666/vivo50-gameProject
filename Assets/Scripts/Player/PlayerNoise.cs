using System;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerNoise : MonoBehaviour
{
    [Header("噪声范围")]
    [SerializeField] private float walkRadius = 2f;
    [SerializeField] private float runRadius = 5f;

    [Header("上报间隔（原型参数）")]
    [SerializeField] private float walkInterval = 0.5f;
    [SerializeField] private float runInterval = 0.3f;

    [Header("调试")]
    [SerializeField] private bool logNoise;

    [Header("运行时观察")]
    [SerializeField] private float currentRadius;
    [SerializeField] private int emittedCount;

    // 参数：噪声发出者、声源位置、传播半径
    public static event Action<GameObject, Vector2, float> NoiseEmitted;

    private PlayerController controller;
    private PlayerHiding hiding;
    private float timer;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        hiding = GetComponent<PlayerHiding>();
    }

    private void Update()
    {
        bool silent =
            Time.timeScale == 0f ||
            controller.IsControlLocked ||
            !controller.IsMoving ||
            (hiding != null &&
             (hiding.IsHidden || hiding.IsCatchLocked));

        if (silent)
        {
            currentRadius = 0f;
            timer = 0f;
            return;
        }

        currentRadius = controller.IsRunning
            ? runRadius
            : walkRadius;

        timer -= Time.deltaTime;

        if (timer > 0f)
            return;

        timer = controller.IsRunning
            ? runInterval
            : walkInterval;

        emittedCount++;

        NoiseEmitted?.Invoke(
            gameObject,
            (Vector2)transform.position,
            currentRadius
        );

        if (logNoise)
        {
            Debug.Log(
                $"脚步噪声：半径 {currentRadius}，" +
                $"位置 {(Vector2)transform.position}",
                this
            );
        }
    }

    private void OnDisable()
    {
        currentRadius = 0f;
        timer = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || currentRadius <= 0f)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, currentRadius);
    }
}