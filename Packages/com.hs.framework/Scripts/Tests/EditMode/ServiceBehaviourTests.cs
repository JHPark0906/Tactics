using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>서비스가 가지에 있을 때만, 정해진 간격으로 도는 것을 고정한다.</summary>
    public sealed class ServiceBehaviourTests
    {
        [Test]
        public void AServiceRunsOnceWhenItsBranchIsFirstEntered()
        {
            var now = 0f;
            var ran = new List<int>();
            var tree = Build(1f, () => now, ran, out _);

            tree.Tick(new BehaviourContext());

            Assert.That(ran.Count, Is.EqualTo(1));
        }

        [Test]
        public void AServiceWaitsOutItsIntervalBeforeRunningAgain()
        {
            var now = 0f;
            var ran = new List<int>();
            var tree = Build(1f, () => now, ran, out _);

            tree.Tick(new BehaviourContext());
            now = 0.5f;
            tree.Tick(new BehaviourContext());

            Assert.That(ran.Count, Is.EqualTo(1), "간격이 차기 전에는 다시 돌지 않는다.");

            now = 1.5f;
            tree.Tick(new BehaviourContext());

            Assert.That(ran.Count, Is.EqualTo(2));
        }

        [Test]
        public void AServiceDoesNotRunWhileAnotherBranchIsRunning()
        {
            var now = 0f;
            var ran = new List<int>();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Running));
            var service = tree.AddChild(root, new ActionServiceBehaviour(0f, _ => ran.Add(1), () => now));
            tree.AddChild(service, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(new BehaviourContext());

            Assert.That(ran, Is.Empty,
                "가지가 도는 동안에만 일한다. 컴포넌트로 붙여 두면 유닛이 무엇을 하든 계속 돈다.");
        }

        [Test]
        public void AServicePassesItsChildResultThroughUnchanged()
        {
            var now = 0f;
            var ran = new List<int>();
            var tree = Build(0f, () => now, ran, out _);

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success),
                "결과를 바꾸지 않으므로 트리 모양에 끼워 넣어도 흐름이 달라지지 않는다.");
        }

        [Test]
        public void AServiceRunsRightAwayWhenItsBranchIsEnteredAgain()
        {
            var now = 0f;
            var ran = new List<int>();
            var tree = Build(10f, () => now, ran, out var serviceNode);

            tree.Tick(new BehaviourContext());
            tree.Reset(serviceNode);
            now = 0.1f;
            tree.Tick(new BehaviourContext());

            Assert.That(ran.Count, Is.EqualTo(2), "다시 들어오면 간격을 기다리지 않는다.");
        }

        private static BehaviourTreeInstance Build(
            float interval,
            System.Func<float> timeProvider,
            ICollection<int> ran,
            out HS.Framework.Foundation.Collections.TreeNode<IBehaviour> serviceNode)
        {
            var tree = new BehaviourTreeInstance();
            serviceNode = tree.SetRoot(new ActionServiceBehaviour(interval, _ => ran.Add(1), timeProvider));
            tree.AddChild(serviceNode, new ActionBehaviour(_ => BehaviourStatus.Success));
            return tree;
        }
    }
}
