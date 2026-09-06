using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>주된 일과 곁 가지를 함께 돌리는 자리의 결과와 마무리를 고정한다.</summary>
    public sealed class SimpleParallelBehaviourTests
    {
        [Test]
        public void TheResultIsAlwaysTheMainTasksResult()
        {
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour());
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Failure));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure),
                "곁 가지가 성공해도 결과는 주된 일의 것이다.");
        }

        [Test]
        public void TheBackgroundIsResetAtOnceWhenTheMainFinishesInImmediateMode()
        {
            var mainStatus = BehaviourStatus.Running;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour(SimpleParallelFinishMode.Immediate));
            tree.AddChild(parallel, new ActionBehaviour(_ => mainStatus));
            tree.AddChild(parallel, new MoveToPositionBehaviour(mover, "destination"));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));

            mainStatus = BehaviourStatus.Success;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(mover.StopCount, Is.EqualTo(1), "바로 끝내면 돌던 곁 가지는 되돌려진다.");
        }

        [Test]
        public void InDelayedModeTheBackgroundIsAllowedToFinishFirst()
        {
            var mainStatus = BehaviourStatus.Running;
            var backgroundStatus = BehaviourStatus.Running;
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour(SimpleParallelFinishMode.Delayed));
            var mainTicks = 0;
            tree.AddChild(parallel, new ActionBehaviour(_ =>
            {
                mainTicks++;
                return mainStatus;
            }));
            tree.AddChild(parallel, new ActionBehaviour(_ => backgroundStatus));
            var context = new BehaviourContext();

            tree.Tick(context);
            mainStatus = BehaviourStatus.Failure;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running), "곁 가지가 끝날 때까지 기다린다.");

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(mainTicks, Is.EqualTo(2), "끝난 주된 일은 기다리는 동안 다시 돌지 않는다.");

            backgroundStatus = BehaviourStatus.Success;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "기다린 뒤에도 결과는 기억해 둔 주된 일의 것이다.");
        }

        [Test]
        public void TheBackgroundRepeatsWhileTheMainRuns()
        {
            var backgroundRuns = 0;
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour());
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Running));
            tree.AddChild(parallel, new ActionBehaviour(_ =>
            {
                backgroundRuns++;
                return BehaviourStatus.Success;
            }));
            var context = new BehaviourContext();

            tree.Tick(context);
            tree.Tick(context);
            tree.Tick(context);

            Assert.That(backgroundRuns, Is.EqualTo(3), "끝난 곁 가지는 되돌려져 다음 실행에서 다시 돈다.");
        }

        [Test]
        public void ARepeatedBackgroundIsResetBetweenRuns()
        {
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour());
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Running));
            var probe = new ResetCountingBehaviour();
            tree.AddChild(parallel, probe);
            var context = new BehaviourContext();

            tree.Tick(context);
            tree.Tick(context);

            Assert.That(probe.ResetCount, Is.EqualTo(2));
        }

        [Test]
        public void WithoutABackgroundOnlyTheMainRuns()
        {
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour());
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void ResettingForgetsTheRememberedMainResult()
        {
            var mainStatus = BehaviourStatus.Success;
            var mainTicks = 0;
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new SimpleParallelBehaviour(SimpleParallelFinishMode.Delayed));
            tree.AddChild(parallel, new ActionBehaviour(_ =>
            {
                mainTicks++;
                return mainStatus;
            }));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Running));
            var context = new BehaviourContext();

            // 첫 실행에서 곁 가지가 돌기 시작하고, 둘째 실행에서 주된 일이 끝나 기다리기로 들어간다.
            mainStatus = BehaviourStatus.Running;
            tree.Tick(context);
            mainStatus = BehaviourStatus.Success;
            tree.Tick(context);
            tree.Reset();
            tree.Tick(context);

            Assert.That(mainTicks, Is.EqualTo(3), "되돌려진 뒤에는 주된 일부터 다시 시작한다.");
        }

        [Test]
        public void TheDefinitionNeedsNothingFromTheOwner()
        {
            var definition = new SimpleParallelDefinition();

            Assert.That(definition.DisplayName, Is.Not.Empty);
            Assert.That(definition.CreateBehaviour(new BehaviourBuildContext(null, new BehaviourContext())),
                Is.TypeOf<SimpleParallelBehaviour>());
        }

        private static BehaviourContext ContextWithDestination()
        {
            var context = new BehaviourContext();
            context.SetValue("destination", new Vector3(1f, 0f, 1f));
            return context;
        }

        private sealed class CountingMover : ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }

        private sealed class ResetCountingBehaviour : IBehaviour
        {
            public int ResetCount { get; private set; }

            public BehaviourStatus Tick(in BehaviourTickContext context) => BehaviourStatus.Success;

            public void Reset() => ResetCount++;
        }
    }
}
