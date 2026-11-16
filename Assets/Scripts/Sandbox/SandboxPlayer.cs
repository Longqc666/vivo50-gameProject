using UnityEngine;
using D225.Interact;

namespace D225.Sandbox
{
    /// <summary>
    /// 联调演示用的最小角色控制器（不是成员1 的正式 PlayerController）。
    /// 只做水平移动、跑动与躲藏接口，供本场景验证交互与谜题；成员1 接入正式角色后可删除本脚本。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [AddComponentMenu("D225/联调/演示角色 (SandboxPlayer)")]
    public sealed class SandboxPlayer : MonoBehaviour, IHidableActor
    {
        [SerializeField]
        private float walkSpeed = 3.0f;

        [SerializeField]
        private float runSpeed = 4.7f;

        private Rigidbody2D body;
        private SpriteRenderer visuals;
        private bool movementLocked;
        private Vector3 positionBeforeHiding;

        public bool IsHidden { get; private set; }

        public bool IsCatchLocked { get; private set; }

        public bool MovementLocked => movementLocked;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            visuals = GetComponentInChildren<SpriteRenderer>();
        }

        private void FixedUpdate()
        {
            if (movementLocked || IsHidden)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
                return;
            }

            float horizontal = Input.GetAxisRaw("Horizontal");
            bool running = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float speed = running ? runSpeed : walkSpeed;
            body.velocity = new Vector2(horizontal * speed, body.velocity.y);
        }

        public void LockMovement(bool locked)
        {
            movementLocked = locked;
            if (locked)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }
        }

        public void SetCatchLocked(bool locked)
        {
            IsCatchLocked = locked;
        }

        public void EnterHiding(HidingSpot spot)
        {
            if (spot == null)
            {
                return;
            }

            positionBeforeHiding = transform.position;
            IsHidden = true;
            transform.position = spot.HidePoint.position;
            if (visuals != null)
            {
                visuals.enabled = false;
            }

            LockMovement(true);
        }

        public void ExitHiding()
        {
            if (!IsHidden)
            {
                return;
            }

            IsHidden = false;
            transform.position = positionBeforeHiding;
            if (visuals != null)
            {
                visuals.enabled = true;
            }

            LockMovement(false);
        }
    }
}
