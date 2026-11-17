using UnityEngine;

namespace D225.Sandbox
{
    /// <summary>联调演示用镜头：水平跟随并限制在关卡范围内（正式镜头属于成员1）。</summary>
    [AddComponentMenu("D225/联调/演示镜头 (SandboxCameraFollow)")]
    public sealed class SandboxCameraFollow : MonoBehaviour
    {
        [SerializeField]
        private Transform target;

        [SerializeField]
        private float minX = 4.8f;

        [SerializeField]
        private float maxX = 14.4f;

        [SerializeField]
        private float fixedY;

        [SerializeField]
        private float smooth = 6f;

        public void Configure(Transform followTarget, float min, float max, float y = 0f)
        {
            target = followTarget;
            minX = min;
            maxX = max;
            fixedY = y;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            float x = Mathf.Clamp(target.position.x, minX, maxX);
            Vector3 goal = new Vector3(x, fixedY, transform.position.z);
            float t = 1f - Mathf.Exp(-smooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, goal, t);
        }
    }
}
