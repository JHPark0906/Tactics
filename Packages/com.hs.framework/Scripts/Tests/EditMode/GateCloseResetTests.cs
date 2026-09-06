using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>문이 닫히거나 함께 돌던 형제가 먼저 끝날 때, 도는 중이던 자리가 되돌려지는 것을 고정한다.</summary>
    /// <remarks>
    /// <see cref="IBehaviour.Reset"/>의 계약은 "바깥에 남는 것을 잡았으면 여기서 푼다"이다. 그 계약은
    /// 누군가 되돌려 줄 때만 지켜진다. 문이 닫힌 것을 트리는 실패로만 보므로, 아래를 되돌리는 일은
    /// 문과 병렬 자리가 맡아야 한다. 이동 자리가 멈춤을 받는지로 그것을 본다.
    /// </remarks>
    public sealed class GateCloseResetTests
    {
        private const string DestinationKey = "destination";

        [Test]
        public void AClosedGateResetsTheChildThatWasRunning()
        {
            var open = true;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(new ConditionBehaviour(_ => open));
            tree.AddChild(gate, new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));

            open = false;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            Assert.That(mover.StopCount, Is.EqualTo(1), "문이 닫히면 걷던 자리는 멈춤을 받아야 한다.");
        }

        [Test]
        public void AClosedGateResetsTheRunningChildOnlyOnce()
        {
            var open = true;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(new ConditionBehaviour(_ => open));
            tree.AddChild(gate, new MoveToPositionBehaviour(mover, DestinationKey));

            tree.Tick(context);
            open = false;
            tree.Tick(context);
            tree.Tick(context);

            Assert.That(mover.StopCount, Is.EqualTo(1), "이미 되돌린 자식을 닫힌 문이 다시 되돌리지 않는다.");
        }

        [Test]
        public void AClosedGateDoesNotResetAChildThatHadFinished()
        {
            var open = true;
            var mover = new CountingMover { HasReachedDestination = true };
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(new ConditionBehaviour(_ => open));
            tree.AddChild(gate, new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));

            open = false;
            tree.Tick(context);

            Assert.That(mover.StopCount, Is.EqualTo(0), "끝난 자식에는 되돌릴 진행이 없다.");
        }

        [Test]
        public void AClosedGateResetsTheWholeBranchBelowIt()
        {
            var open = true;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(new ConditionBehaviour(_ => open));
            var sequence = tree.AddChild(gate, new SequenceBehaviour());
            tree.AddChild(sequence, new ActionBehaviour(_ => BehaviourStatus.Success));
            tree.AddChild(sequence, new MoveToPositionBehaviour(mover, DestinationKey));

            tree.Tick(context);
            open = false;
            tree.Tick(context);

            Assert.That(mover.StopCount, Is.EqualTo(1), "아래로 내려가는 일은 트리가 하므로 손자까지 되돌려진다.");
        }

        [Test]
        public void AParallelThatFailsEarlyResetsItsRunningSiblings()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new ParallelBehaviour(ParallelPolicy.RequireAll));
            tree.AddChild(parallel, new MoveToPositionBehaviour(mover, DestinationKey));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Failure));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(mover.StopCount, Is.EqualTo(1), "형제 하나가 실패해 전체가 끝나면 돌던 형제는 멈춰야 한다.");
        }

        [Test]
        public void AParallelThatSucceedsEarlyResetsItsRunningSiblings()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new ParallelBehaviour(ParallelPolicy.RequireOne));
            tree.AddChild(parallel, new MoveToPositionBehaviour(mover, DestinationKey));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Success));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(mover.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void AParallelDoesNotResetAChildThatNeverStarted()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new ParallelBehaviour(ParallelPolicy.RequireAll));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Failure));
            tree.AddChild(parallel, new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(mover.StopCount, Is.EqualTo(0), "시작하지 않은 자리에는 되돌릴 진행이 없다.");
        }

        [Test]
        public void AParallelResetsARunningSiblingThatWasNotReachedInTheFinalTick()
        {
            var mover = new CountingMover();
            var failNow = false;
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new ParallelBehaviour(ParallelPolicy.RequireAll));
            tree.AddChild(parallel, new ActionBehaviour(_ => failNow ? BehaviourStatus.Failure : BehaviourStatus.Running));
            tree.AddChild(parallel, new MoveToPositionBehaviour(mover, DestinationKey));

            tree.Tick(context);
            failNow = true;
            tree.Tick(context);

            Assert.That(mover.StopCount, Is.EqualTo(1),
                "앞엣 형제가 실패해 뒤엣것을 돌리기 전에 끝나도, 지난 실행에서 돌던 뒤엣것은 되돌려진다.");
        }

        [Test]
        public void AParallelDoesNotTickAChildThatAlreadyFinished()
        {
            var visited = new List<string>();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var parallel = tree.SetRoot(new ParallelBehaviour(ParallelPolicy.RequireAll));
            tree.AddChild(parallel, new ActionBehaviour(_ =>
            {
                visited.Add("done");
                return BehaviourStatus.Success;
            }));
            tree.AddChild(parallel, new ActionBehaviour(_ => BehaviourStatus.Running));

            tree.Tick(context);
            tree.Tick(context);

            Assert.That(visited, Is.EqualTo(new[] { "done" }), "끝난 자식은 다른 자식이 도는 동안 되풀이되지 않는다.");
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

            public bool HasReachedDestination { get; set; }

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
