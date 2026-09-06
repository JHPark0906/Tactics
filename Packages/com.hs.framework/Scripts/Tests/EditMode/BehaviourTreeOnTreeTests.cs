using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>구조를 트리가 들고 행동이 자식을 모르는 구성을 고정한다.</summary>
    public sealed class BehaviourTreeOnTreeTests
    {
        [Test]
        public void AnEmptyTreeFails()
        {
            var tree = new BehaviourTreeInstance();

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void ASequenceSucceedsOnlyWhenEveryChildSucceeds()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            tree.AddChild(root, Always(BehaviourStatus.Success));
            tree.AddChild(root, Always(BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void ASequenceStopsAtTheFirstFailingChild()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var visited = new List<string>();
            tree.AddChild(root, Record(visited, "a", BehaviourStatus.Success));
            tree.AddChild(root, Record(visited, "b", BehaviourStatus.Failure));
            tree.AddChild(root, Record(visited, "c", BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(visited, Is.EqualTo(new[] { "a", "b" }), "실패한 뒤의 자식은 실행되지 않는다.");
        }

        [Test]
        public void ASequenceResumesAtTheChildThatWasStillRunning()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var visited = new List<string>();
            tree.AddChild(root, Record(visited, "a", BehaviourStatus.Success));
            tree.AddChild(root, Record(visited, "b", BehaviourStatus.Running));

            tree.Tick(new BehaviourContext());
            visited.Clear();
            tree.Tick(new BehaviourContext());

            Assert.That(visited, Is.EqualTo(new[] { "b" }), "이미 끝난 자식을 다시 하지 않는다.");
        }

        [Test]
        public void ASelectorTakesTheFirstChildThatSucceeds()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var visited = new List<string>();
            tree.AddChild(root, Record(visited, "a", BehaviourStatus.Failure));
            tree.AddChild(root, Record(visited, "b", BehaviourStatus.Success));
            tree.AddChild(root, Record(visited, "c", BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(visited, Is.EqualTo(new[] { "a", "b" }), "성공한 뒤의 자식은 시도하지 않는다.");
        }

        [Test]
        public void ChildOrderIsPriorityAndMovingAChildChangesIt()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var visited = new List<string>();
            tree.AddChild(root, Record(visited, "low", BehaviourStatus.Success));
            tree.AddChild(root, Record(visited, "high", BehaviourStatus.Success));

            tree.MoveChild(root, 1, 0);
            tree.Tick(new BehaviourContext());

            Assert.That(visited, Is.EqualTo(new[] { "high" }), "앞에 놓인 자식이 곧 높은 우선순위이다.");
        }

        [Test]
        public void CompositesDoNotHoldTheirOwnChildren()
        {
            var tree = new BehaviourTreeInstance();
            var composite = new SequenceBehaviour();
            var root = tree.SetRoot(composite);
            tree.AddChild(root, Always(BehaviourStatus.Success));

            Assert.That(root.Children.Count, Is.EqualTo(1),
                "구조는 트리가 든다. 같은 행동 객체를 다른 자리에 두어도 그 자리의 자식을 본다.");
        }

        [Test]
        public void ResettingRunsDownTheWholeBranch()
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SequenceBehaviour());
            var deep = new CountingBehaviour(BehaviourStatus.Running);
            var branch = tree.AddChild(root, new SequenceBehaviour());
            tree.AddChild(branch, deep);

            tree.Reset();

            Assert.That(deep.ResetCount, Is.EqualTo(1), "아래로 내려가는 일은 트리가 한다.");
        }

        private static IBehaviour Always(BehaviourStatus status) => new CountingBehaviour(status);

        private static IBehaviour Record(ICollection<string> log, string name, BehaviourStatus status)
            => new ActionBehaviour(_ =>
            {
                log.Add(name);
                return status;
            });

        private sealed class CountingBehaviour : IBehaviour
        {
            private readonly BehaviourStatus _status;

            public int ResetCount { get; private set; }

            public CountingBehaviour(BehaviourStatus status) => _status = status;

            public BehaviourStatus Tick(in BehaviourTickContext context) => _status;

            public void Reset() => ResetCount++;
        }
    }
}
