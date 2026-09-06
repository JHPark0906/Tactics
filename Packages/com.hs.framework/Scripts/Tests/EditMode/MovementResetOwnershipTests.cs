using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>이동 자리가 자기가 낸 이동 요청이 살아 있을 때만 멈추는 것을 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 이동 수단은 유닛에 하나뿐이라 트리의 이동 자리들이 나눠 쓴다. 아무것도 몰지 않던 자리가
    /// 되돌려지며 멈추라고 하면, 다른 자리가 몰던 이동이 끊기거나 멈춤이 두 번 간다.
    /// </para>
    /// <para>
    /// 트리가 끊기에서 실행 경로 밖의 가지를 되돌리지 않더라도, 문 닫힘과 병렬의 되돌림은 서브트리
    /// 통째라 시작하지 않은 잎도 되돌려진다. 그래서 잎이 스스로 지켜야 한다.
    /// </para>
    /// </remarks>
    public sealed class MovementResetOwnershipTests
    {
        private const string DestinationKey = "destination";
        private const string TargetKey = "target";

        [Test]
        public void AMoveThatNeverStartedDoesNotStopTheMoverWhenReset()
        {
            var mover = new CountingMover();
            var behaviour = new MoveToPositionBehaviour(mover, DestinationKey);

            behaviour.Reset();

            Assert.That(mover.StopCount, Is.EqualTo(0), "이동 요청을 낸 적이 없는 자리에는 멈출 것이 없다.");
        }

        [Test]
        public void AChaseThatNeverStartedDoesNotStopTheMoverWhenReset()
        {
            var mover = new CountingMover();
            var behaviour = new ChaseTargetBehaviour(mover, TargetKey);

            behaviour.Reset();

            Assert.That(mover.StopCount, Is.EqualTo(0), "이동 요청을 낸 적이 없는 자리에는 멈출 것이 없다.");
        }

        [Test]
        public void AMoveThatWasRunningStopsTheMoverOnceNoMatterHowOftenItIsReset()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var move = tree.SetRoot(new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            tree.Reset(move);
            tree.Reset(move);

            Assert.That(mover.StopCount, Is.EqualTo(1), "한 번 되돌려진 이동 요청은 다시 멈출 것이 없다.");
        }

        [Test]
        public void AMoveStopsTheMoverAgainOnlyAfterItHasMovedAgain()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var move = tree.SetRoot(new MoveToPositionBehaviour(mover, DestinationKey));

            tree.Tick(context);
            tree.Reset(move);
            tree.Tick(context);
            tree.Reset(move);

            Assert.That(mover.StopCount, Is.EqualTo(2), "다시 걷기 시작한 뒤의 되돌림은 다시 멈춤을 낸다.");
        }

        /// <remarks>
        /// 문이 닫히면 트리는 그 아래를 통째로 되돌린다. 걷던 자리와 아직 시작하지 않은 자리가 한 이동
        /// 수단을 나눠 쓰면, 멈춤은 걷던 자리의 것 하나여야 한다.
        /// </remarks>
        [Test]
        public void AClosedGateResetsTheWholeBranchButStopsTheSharedMoverOnce()
        {
            var open = true;
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var gate = tree.SetRoot(new ConditionBehaviour(_ => open));
            var selector = tree.AddChild(gate, new SelectorBehaviour());
            tree.AddChild(selector, new MoveToPositionBehaviour(mover, DestinationKey));
            tree.AddChild(selector, new MoveToPositionBehaviour(mover, DestinationKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            open = false;
            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            Assert.That(mover.StopCount, Is.EqualTo(1), "걷던 자리 하나만 멈춤을 받아야 한다.");
        }

        /// <remarks>
        /// 앞 가지의 이동 자리는 아직 시작하지 않았고 뒤 가지의 이동 자리가 걷는 중이다. 뒤 가지를
        /// 끊어 부모가 되돌려질 때, 멈춤은 걷던 자리의 것 하나여야 한다.
        /// </remarks>
        [Test]
        public void CuttingOffALowerPrioritySiblingStopsTheSharedMoverOnce()
        {
            var mover = new CountingMover();
            var context = ContextWithDestination();
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(new SelectorBehaviour());
            var guard = tree.AddChild(root,
                new ContextValueConditionBehaviour(TargetKey, true, BehaviourAbortScope.LowerPriority));
            var guarded = tree.AddChild(guard, new MoveToPositionBehaviour(mover, DestinationKey));
            var advance = tree.AddChild(root, new MoveToPositionBehaviour(mover, DestinationKey));

            tree.Tick(context);
            Assert.That(tree.IsRunning(advance), Is.True);

            context.SetValue(TargetKey, 1);
            tree.Tick(context);

            Assert.That(mover.StopCount, Is.EqualTo(1), "걷던 자리 하나만 멈춤을 받아야 한다.");
            Assert.That(tree.IsRunning(advance), Is.False);
            Assert.That(tree.IsRunning(guarded), Is.True);
        }

        private static BehaviourContext ContextWithDestination()
        {
            var context = new BehaviourContext();
            context.SetValue(DestinationKey, new Vector3(1f, 0f, 1f));
            return context;
        }

        /// <summary>멈춤을 몇 번 받았는지 세는 검사용 이동 수단이다. 목적지에는 닿지 않는다.</summary>
        private sealed class CountingMover : ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
