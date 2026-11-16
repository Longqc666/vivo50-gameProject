using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.2f;

    [Header("场景左右边界")]
    [SerializeField] private float leftBoundary = -19.5f;
    [SerializeField] private float rightBoundary = 19.5f;

    private Camera cam;
    private float followVelocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        // 正交相机可见画面宽度的一半
        float halfWidth = cam.orthographicSize * cam.aspect;

        float minX = leftBoundary + halfWidth;
        float maxX = rightBoundary - halfWidth;

        float desiredX;

        if (minX > maxX)
        {
            // 画面比场景还宽时，镜头保持在场景中央
            desiredX = (leftBoundary + rightBoundary) * 0.5f;
        }
        else
        {
            desiredX = Mathf.Clamp(target.position.x, minX, maxX);
        }

        float newX = Mathf.SmoothDamp(
            transform.position.x,
            desiredX,
            ref followVelocity,
            smoothTime
        );

        // 保持镜头高度和深度，仅跟随水平方向
        transform.position = new Vector3(
            newX,
            transform.position.y,
            transform.position.z
        );
    }
}