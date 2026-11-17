using NUnit.Framework;
using D225.Quest;

namespace D225.Interact.Tests
{
    /// <summary>4-1/4-3 提示格式：四态 → 统一的中文提示，不可用只给原因。</summary>
    public sealed class InteractionPromptTests
    {
        [Test]
        public void For_AvailableStates_UsesChineseVerb()
        {
            Assert.AreEqual("[E] 拾取 手电筒", InteractionPrompt.For(InteractionState.Pickupable, "手电筒"));
            Assert.AreEqual("[E] 查看 宿舍便笺", InteractionPrompt.For(InteractionState.Inspectable, "宿舍便笺"));
            Assert.AreEqual("[E] 使用 配电箱", InteractionPrompt.For(InteractionState.Usable, "配电箱"));
        }

        [Test]
        public void For_Unavailable_ReturnsReasonWithoutKeyHint()
        {
            string prompt = InteractionPrompt.For(InteractionState.Unavailable, "配电箱缺少「保险丝」");

            Assert.AreEqual("配电箱缺少「保险丝」", prompt);
            StringAssert.DoesNotContain("[E]", prompt);
        }

        [Test]
        public void KeyLabel_CanBeChangedForRebinding()
        {
            string original = InteractionPrompt.KeyLabel;
            try
            {
                InteractionPrompt.KeyLabel = "F";
                Assert.AreEqual("[F] 拾取 手电筒", InteractionPrompt.For(InteractionState.Pickupable, "手电筒"));
            }
            finally
            {
                InteractionPrompt.KeyLabel = original;
            }
        }

        [Test]
        public void Action_BuildsCustomVerbPrompt()
        {
            Assert.AreEqual("[E] 躲进 储物柜", InteractionPrompt.Action("躲进", "储物柜"));
            Assert.AreEqual("[E] 离开 储物柜", InteractionPrompt.Action("离开", "储物柜"));
        }
    }
}
