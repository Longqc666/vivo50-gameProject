using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("移动速度")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 4.7f;

    [Header("耐力设置")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float drainRate = 15f;
    [SerializeField] private float recoveryRate = 18f;
    [SerializeField] private float recoveryDelay = 1.5f;

    [Header("运行时状态（观察用）")]
    [SerializeField] private float currentStamina;
    [SerializeField] private bool isRunning;
    [SerializeField] private bool isExhausted;

    private Rigidbody2D rb;
    private float moveInput;
    private float recoveryTimer;

    // 后续供耐力条、动画和鬼怪模块读取
    public float StaminaRatio => currentStamina / maxStamina;
    public bool IsRunning => isRunning;
    public bool IsMoving => Mathf.Abs(rb.velocity.x) > 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentStamina = maxStamina;
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        bool wantsToRun =
            Input.GetKey(KeyCode.LeftShift) &&
            moveInput != 0f;

        isRunning =
            wantsToRun &&
            !isExhausted &&
            currentStamina > 0f;

        if (isRunning)
        {
            currentStamina = Mathf.Max(
                0f,
                currentStamina - drainRate * Time.deltaTime
            );

            recoveryTimer = recoveryDelay;

            if (currentStamina <= 0f)
            {
                isExhausted = true;
                isRunning = false;
            }
        }
        else
        {
            recoveryTimer = Mathf.Max(
                0f,
                recoveryTimer - Time.deltaTime
            );

            if (recoveryTimer <= 0f)
            {
                currentStamina = Mathf.Min(
                    maxStamina,
                    currentStamina + recoveryRate * Time.deltaTime
                );
            }

            // 耗尽后恢复到20%，才允许再次奔跑
            if (isExhausted &&
                currentStamina >= maxStamina * 0.2f)
            {
                isExhausted = false;
            }
        }
    }

    private void FixedUpdate()
    {
        float speed = isRunning ? runSpeed : walkSpeed;

        rb.velocity = new Vector2(
            moveInput * speed,
            rb.velocity.y
        );
    }
}