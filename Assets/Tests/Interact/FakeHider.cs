using UnityEngine;

namespace D225.Interact.Tests
{
    /// <summary>
    /// 测试替身：模拟成员1 未来实现的 <see cref="IHidableActor"/>。
    /// 只记录进入/离开调用，不做真实移动锁定。
    /// </summary>
    public sealed class FakeHider : MonoBehaviour, IHidableActor
    {
        public bool IsHidden { get; private set; }

        public bool IsCatchLocked { get; set; }

        public HidingSpot CurrentSpot { get; private set; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public void EnterHiding(HidingSpot spot)
        {
            IsHidden = true;
            CurrentSpot = spot;
            EnterCount++;
        }

        public void ExitHiding()
        {
            IsHidden = false;
            CurrentSpot = null;
            ExitCount++;
        }
    }
}
