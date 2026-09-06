using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>자리를 나눠 추격하는 자리가 자기가 낸 이동 요청이 살아 있을 때만 멈추는 것을 고정한다.</summary>
    /// <remarks>
    /// 정본 트리에서 전진과 추격은 한 이동 수단을 나눠 쓴다. 대상 문이 닫힐 때 트리는 그 아래를 통째로
    /// 되돌리므로 아직 시작하지 않은 추격 자리도 함께 되돌려지는데, 그 자리가 멈추라고 하면 다른 자리가
    /// 몰던 이동이 끊긴다.
    /// </remarks>
    public sealed class SpreadChaseResetOwnershipTests
    {
        [Test]
        public void AChaseThatNeverStartedDoesNotStopTheMoverWhenReset()
        {
            var mover = new CountingMover();
            var node = new SpreadChaseTargetBehaviour(
                mover,
                UnitBehaviourKeys.Target,
                new ApproachSpreadSettings(5f, 6, 180f),
                () => 0,
                () => ApproachSpreadReference.Resolve(
                    new LaneGeometry(new Vector3(0f, 0f, -50f), new Vector3(0f, 0f, 50f)),
                    LaneAdvanceOrientation.Forward),
                EverythingIsReachable);

            node.Reset();

            Assert.That(mover.StopCount, Is.EqualTo(0), "이동 요청을 낸 적이 없는 자리에는 멈출 것이 없다.");
        }

        /// <summary>어느 자리든 갈 수 있다고 본다.</summary>
        private static bool EverythingIsReachable(Vector3 desiredPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = desiredPosition;
            return true;
        }

        /// <summary>멈춤을 몇 번 받았는지 세는 검사용 이동 수단이다.</summary>
        private sealed class CountingMover : ICharacterMover
        {
            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
