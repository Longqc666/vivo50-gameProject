using UnityEngine;
using D225.Audio;

namespace D225.UI
{
    /// <summary>
    /// 区域触发：玩家走进门厅时把灯光切到警报、顺手换环境声。
    /// 挂在带 Collider2D（勾 Is Trigger）的空物体上即可，不需要写代码就能摆多个。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [AddComponentMenu("D225/界面/灯光区域触发 (LightingZoneTrigger)")]
    public sealed class LightingZoneTrigger : MonoBehaviour
    {
        [SerializeField] private LightDirector director;
        [SerializeField] private LightingState state = LightingState.HallAlarm;

        [Tooltip("进入后切换的环境声键名，留空表示不换（键名见 SfxLibrary）")]
        [SerializeField] private string ambienceKey = "amb.hall";

        [SerializeField] private bool onlyOnce = true;

        private bool fired;

        private void Reset()
        {
            // 在编辑器里刚挂上脚本时，自动把碰撞体设成触发器，少一步手滑
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (fired && onlyOnce)
            {
                return;
            }

            // 用「有没有玩家脚本」判断，而不是靠 Tag，省得全组去维护 Tag 列表
            if (other.GetComponentInParent<PlayerController>() == null)
            {
                return;
            }

            fired = true;

            if (director != null)
            {
                director.SetState(state);
            }

            if (!string.IsNullOrEmpty(ambienceKey))
            {
                AudioManager.SetAmbienceKey(ambienceKey);
            }
        }
    }
}
