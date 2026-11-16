using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerVisual : MonoBehaviour
{
    [SerializeField] private Transform eye;

    private PlayerController controller;
    private SpriteRenderer body;
    private Vector3 eyeStartPosition;
    private int facingDirection = 1;

    private void Awake()
    {
        controller = GetComponent<PlayerController>();
        body = GetComponent<SpriteRenderer>();

        if (eye != null)
        {
            eyeStartPosition = eye.localPosition;
        }
    }

    private void Update()
    {
        float input = controller.IsControlLocked
    ? 0f
    : Input.GetAxisRaw("Horizontal");

        if (input != 0f)
        {
            facingDirection = input > 0f ? 1 : -1;
        }

        if (eye != null)
        {
            eye.localPosition = new Vector3(
                Mathf.Abs(eyeStartPosition.x) * facingDirection,
                eyeStartPosition.y,
                eyeStartPosition.z
            );
        }

        // 用颜色暂时显示三种状态
        if (body != null)
        {
            if (!controller.IsMoving)
            {
                body.color = Color.green;
            }
            else if (controller.IsRunning)
            {
                body.color = new Color(1f, 0.6f, 0.2f);
            }
            else
            {
                body.color = Color.cyan;
            }
        }
    }
}