using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문맥 알림과 실행 경로 기록을 고정한다.</summary>
    /// <remarks>이 둘이 있어야 "값이 바뀌면 실행 중인 가지를 끊는다"를 말할 수 있다.</remarks>
    public sealed class ContextAndActivePathTests
    {
        [Test]
        public void ChangingAValueTellsWhoeverIsWatchingThatKey()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            using var subscription = context.Observe("target", seen.Add);

            context.SetValue("target", 1);

            Assert.That(seen, Is.EqualTo(new[] { "target" }));
        }

        [Test]
        public void WritingTheSameValueTellsNobody()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            context.SetValue("target", 1);
            using var subscription = context.Observe("target", seen.Add);

            context.SetValue("target", 1);

            Assert.That(seen, Is.Empty,
                "바뀐 것이 없는데 깨우면 아무 일도 없었는데 실행 중인 가지가 끊긴다.");
        }

        [Test]
        public void RemovingAValueAlsoTells()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            context.SetValue("target", 1);
            using var subscription = context.Observe("target", seen.Add);

            context.RemoveValue("target");

            Assert.That(seen, Is.EqualTo(new[] { "target" }));
        }

        [Test]
        public void WatchingOneKeyDoesNotWakeOnAnother()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            using var subscription = context.Observe("target", seen.Add);

            context.SetValue("destination", 1);

            Assert.That(seen, Is.Empty);
        }

        [Test]
        public void ThrowingAwayTheSubscriptionStopsTheNotifications()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            var subscription = context.Observe("target", seen.Add);

            subscription.Dispose();
            context.SetValue("target", 1);

            Assert.That(seen, Is.Empty, "가지에서 벗어난 자리가 계속 받으면 실행 중이지도 않은 자리가 트리를 끊는다.");
        }

        [Test]
        public void WatchersAreToldInTheOrderTheyWereAdded()
        {
            var context = new BehaviourContext();
            var order = new List<string>();
            using var first = context.Observe("target", _ => order.Add("first"));
            using var second = context.Observe("target", _ => order.Add("second"));

            context.SetValue("target", 1);

            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void ASubscriptionDroppedDuringNotificationIsNotCalled()
        {
            var context = new BehaviourContext();
            var seen = new List<string>();
            System.IDisposable second = null;
            using var first = context.Observe("target", _ => second.Dispose());
            second = context.Observe("target", seen.Add);

            context.SetValue("target", 1);

            Assert.That(seen, Is.Empty, "알리는 도중에 끊긴 것은 건너뛴다.");
        }

        [Test]
        public void TheActivePathHoldsWhateverIsStillRunning()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var branch = tree.AddChild(root, new SequenceBehaviour());
            var running = tree.AddChild(branch, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(new BehaviourContext());

            Assert.That(tree.ActivePath, Is.EqualTo(new[] { root, branch, running }),
                "뿌리에서 가장 깊은 곳까지 이어져야 어디를 끊을지 정할 수 있다.");
            Assert.That(tree.IsRunning(running), Is.True);
        }

        [Test]
        public void NothingIsLeftOnThePathWhenTheTreeFinishes()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));

            tree.Tick(new BehaviourContext());

            Assert.That(tree.ActivePath, Is.Empty);
        }

        [Test]
        public void ChildrenThatFailedDoNotStayOnThePath()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var failing = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Failure));
            var running = tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(new BehaviourContext());

            Assert.That(tree.IsRunning(failing), Is.False);
            Assert.That(tree.ActivePath, Is.EqualTo(new[] { root, running }));
        }
    }
}
