using System;
using HS.Framework.UI.Windows;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>UiWindowStack의 푸시/팝, 모달 차단, 취소 대상 규칙을 검증한다.</summary>
    public sealed class UiWindowStackTests
    {
        [Test]
        public void PushSetsTopAndCount()
        {
            var stack = new UiWindowStack();
            var first = new TestWindow();
            var second = new TestWindow();

            stack.Push(first);
            stack.Push(second);

            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Top, Is.SameAs(second));
            Assert.That(stack.Contains(first), Is.True);
        }

        [Test]
        public void PushExistingWindowMovesItToTop()
        {
            var stack = new UiWindowStack();
            var first = new TestWindow();
            var second = new TestWindow();
            stack.Push(first);
            stack.Push(second);

            stack.Push(first);

            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Top, Is.SameAs(first));
        }

        [Test]
        public void PushNullThrows()
        {
            var stack = new UiWindowStack();

            Assert.Throws<ArgumentNullException>(() => stack.Push(null));
        }

        [Test]
        public void PopReturnsTopAndRemovesIt()
        {
            var stack = new UiWindowStack();
            var first = new TestWindow();
            var second = new TestWindow();
            stack.Push(first);
            stack.Push(second);

            var popped = stack.Pop();

            Assert.That(popped, Is.SameAs(second));
            Assert.That(stack.Top, Is.SameAs(first));
            Assert.That(stack.Pop(), Is.SameAs(first));
            Assert.That(stack.Pop(), Is.Null);
        }

        [Test]
        public void RemoveMiddleWindowKeepsTop()
        {
            var stack = new UiWindowStack();
            var bottom = new TestWindow();
            var middle = new TestWindow();
            var top = new TestWindow();
            stack.Push(bottom);
            stack.Push(middle);
            stack.Push(top);

            var removed = stack.Remove(middle);

            Assert.That(removed, Is.True);
            Assert.That(stack.Count, Is.EqualTo(2));
            Assert.That(stack.Top, Is.SameAs(top));
            Assert.That(stack.Remove(middle), Is.False);
        }

        [Test]
        public void HasModalAndTopModalFindTopmostModal()
        {
            var stack = new UiWindowStack();
            var lowerModal = new TestWindow { IsModal = true };
            var upperModal = new TestWindow { IsModal = true };
            stack.Push(lowerModal);
            stack.Push(upperModal);
            stack.Push(new TestWindow());

            Assert.That(stack.HasModal, Is.True);
            Assert.That(stack.TopModal, Is.SameAs(upperModal));

            stack.Remove(lowerModal);
            stack.Remove(upperModal);
            Assert.That(stack.HasModal, Is.False);
            Assert.That(stack.TopModal, Is.Null);
        }

        [Test]
        public void IsBlockedByModalBlocksOnlyWindowsBelowModal()
        {
            var stack = new UiWindowStack();
            var below = new TestWindow();
            var modal = new TestWindow { IsModal = true };
            var above = new TestWindow();
            stack.Push(below);
            stack.Push(modal);
            stack.Push(above);

            Assert.That(stack.IsBlockedByModal(below), Is.True);
            Assert.That(stack.IsBlockedByModal(modal), Is.False);
            Assert.That(stack.IsBlockedByModal(above), Is.False);
        }

        [Test]
        public void IsBlockedByModalTreatsOutsideWindowAsBlockedWhileModalIsOpen()
        {
            var stack = new UiWindowStack();
            var outside = new TestWindow();

            Assert.That(stack.IsBlockedByModal(outside), Is.False);

            stack.Push(new TestWindow { IsModal = true });
            Assert.That(stack.IsBlockedByModal(outside), Is.True);
            Assert.That(stack.IsBlockedByModal(null), Is.True);
        }

        [Test]
        public void PeekCancelTargetReturnsTopOnlyWhenItAllowsCancel()
        {
            var stack = new UiWindowStack();
            Assert.That(stack.PeekCancelTarget(), Is.Null);

            var closable = new TestWindow { CloseOnCancel = true };
            stack.Push(closable);
            Assert.That(stack.PeekCancelTarget(), Is.SameAs(closable));

            var blocking = new TestWindow { CloseOnCancel = false };
            stack.Push(blocking);
            Assert.That(stack.PeekCancelTarget(), Is.Null);
        }

        [Test]
        public void ClearRemovesAllWindows()
        {
            var stack = new UiWindowStack();
            stack.Push(new TestWindow());
            stack.Push(new TestWindow());

            stack.Clear();

            Assert.That(stack.Count, Is.EqualTo(0));
            Assert.That(stack.Top, Is.Null);
        }

        private sealed class TestWindow : IUiWindow
        {
            public bool IsModal { get; set; }

            public bool CloseOnCancel { get; set; } = true;
        }
    }
}
