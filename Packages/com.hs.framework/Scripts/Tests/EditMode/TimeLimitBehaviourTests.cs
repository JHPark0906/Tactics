using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>시간 제한 자리가 시간이 다하면 아래를 되돌리고 실패하는 것을 고정한다.</summary>
    public sealed class TimeLimitBehaviourTests
    {
        private const string DestinationKey = "destination";

        [Test]
        public void TheChildIsCutOffAndResetWhenTheLimitPasses()
        {
            var now = 0f;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var limit = tree.SetRoot(new TimeLimitBehaviour(1f, () => now));
            tree.AddChild(limit, new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            now = 1f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "허용한 시간을 꼭 채운 순간까지는 돈다.");
            now = 1.01f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "시간이 다하면 실패한다.");
            Assert.That(mover.StopCount, Is.EqualTo(1), "잘린 아래는 되돌려져 멈춤을 받는다.");
        }

        [Test]
        public void AChildThatFinishesInTimePassesItsResultThrough()
        {
            var now = 0f;
            var tree = new BehaviourTreeInstance();
            var limit = tree.SetRoot(new TimeLimitBehaviour(1f, () => now));
            tree.AddChild(limit, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));

            var failing = new BehaviourTreeInstance();
            var failingLimit = failing.SetRoot(new TimeLimitBehaviour(1f, () => now));
            failing.AddChild(failingLimit, new ActionBehaviour(_ => BehaviourStatus.Failure));

            Assert.That(failing.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void TheClockStartsOverWhenTheChildIsEnteredAgain()
        {
            var now = 0f;
            var tree = new BehaviourTreeInstance();
            var limit = tree.SetRoot(new TimeLimitBehaviour(1f, () => now));
            tree.AddChild(limit, new ActionBehaviour(_ => BehaviourStatus.Running));
            var context = new BehaviourContext();

            tree.Tick(context);
            now = 1.5f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            now = 1.6f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "다시 들어오면 처음부터 다시 잰다.");
            now = 2.5f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            now = 2.7f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void TheClockStartsOverAfterTheChildFinishes()
        {
            var now = 0f;
            var status = BehaviourStatus.Running;
            var tree = new BehaviourTreeInstance();
            var limit = tree.SetRoot(new TimeLimitBehaviour(1f, () => now));
            tree.AddChild(limit, new ActionBehaviour(_ => status));
            var context = new BehaviourContext();

            tree.Tick(context);
            now = 0.9f;
            status = BehaviourStatus.Success;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));

            status = BehaviourStatus.Running;
            now = 1.5f;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "끝난 뒤 다시 들어오면 시계가 새로 시작한다.");
        }

        [Test]
        public void ResettingClearsTheClock()
        {
            var now = 0f;
            var tree = new BehaviourTreeInstance();
            var limit = tree.SetRoot(new TimeLimitBehaviour(1f, () => now));
            tree.AddChild(limit, new ActionBehaviour(_ => BehaviourStatus.Running));
            var context = new BehaviourContext();

            tree.Tick(context);
            tree.Reset();
            now = 1.5f;

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "되돌려진 자리는 시계도 처음이다.");
        }

        [Test]
        public void TheDefinitionNeedsNothingFromTheOwner()
        {
            var definition = new TimeLimitDefinition();

            Assert.That(definition.DisplayName, Is.Not.Empty);
            Assert.That(definition.CreateBehaviour(new BehaviourBuildContext(null, new BehaviourContext())),
                Is.TypeOf<TimeLimitBehaviour>());
        }

        private static BehaviourContext ContextWithDestination()
        {
            var context = new BehaviourContext();
            context.SetValue(DestinationKey, new Vector3(1f, 0f, 1f));
            return context;
        }

        private sealed class CountingMover : ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
