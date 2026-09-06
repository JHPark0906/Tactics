using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Tactics.Foundation.Geometry;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>자리를 나눌 기준 방향과 그 자리로 다가가는 노드를 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// 여기서 지키려는 것은 하나다. <b>같은 적을 노리는 유닛들이 세계 좌표에서 서로 다른 자리에 선다.</b>
    /// 그러려면 기준 방향이 모든 유닛에게 같아야 한다. 유닛마다 기준이 다르면 각자 자기 반원 안에서는
    /// 잘 흩어지지만 <b>두 반원이 겹치는 것은 아무도 보고 있지 않다</b> — 북쪽에서 온 유닛의 +30° 자리와
    /// 남쪽에서 온 유닛의 +150° 자리가 세계 좌표에서 같은 곳이 될 수 있다.
    /// </para>
    /// <para>
    /// 그래서 기준을 구하는 계산에 <b>유닛의 위치가 인자로 들어가지 않는다</b>는 것과,
    /// 레인이 없을 때의 대체도 모두에게 같다는 것을 함께 못 박는다.
    /// </para>
    /// </remarks>
    public sealed class ApproachSpreadTests
    {
        private const float Tolerance = 0.001f;

        private static readonly TeamId Ally = new(1);
        private static readonly TeamId Enemy = new(2);

        private static LaneGeometry NorthwardLane =>
            new(new Vector3(0f, 0f, -50f), new Vector3(0f, 0f, 50f));

        [Test]
        public void EveryUnitOnATeamGetsTheSameReferenceNoMatterWhereItStands()
        {
            // 기준을 구하는 계산에는 유닛의 위치가 들어갈 인자 자체가 없다.
            // 그 사실을 결과로도 못 박는다. 인자가 없다는 것만으로는 나중에 누가 추가하는 것을 막지 못한다.
            var first = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward);
            var second = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SlotsNeverCollideWhenEveryoneSharesOneReference()
        {
            // 공통 기준 방향은 슬롯 번호를 세계 좌표의 자리로 대응시킨다. 기준이 하나면 자리 번호가 곧 세계 좌표의 자리가 된다.
            var reference = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward);
            var target = new PlanarPosition(0f, 10f);
            var positions = new List<PlanarPosition>();

            for (var seed = 0; seed < 6; seed++)
            {
                positions.Add(ApproachSlotResolver.ResolveSlotPosition(target, reference, 5f, seed, 6));
            }

            for (var left = 0; left < positions.Count; left++)
            {
                for (var right = left + 1; right < positions.Count; right++)
                {
                    Assert.That(
                        PlanarPosition.Distance(positions[left], positions[right]),
                        Is.GreaterThan(Tolerance),
                        $"{left}번과 {right}번이 세계 좌표에서 같은 자리에 선다.");
                }
            }
        }

        [Test]
        public void PerUnitReferencesWouldPutTwoUnitsInTheSamePlace()
        {
            // 왜 기준이 하나여야 하는지를 실패로 보여 둔다. 각자 접근해 온 방향을 기준으로 삼으면
            // 서로 다른 자리 번호를 받고도 세계 좌표에서 같은 곳에 선다.
            var target = new PlanarPosition(0f, 0f);
            var fromNorth = new PlanarPosition(0f, 1f);
            var fromSouth = new PlanarPosition(0f, -1f);

            var northUnit = ApproachSlotResolver.ResolveSlotPosition(target, fromNorth, 5f, 4, 6, 360f);
            var southUnit = ApproachSlotResolver.ResolveSlotPosition(target, fromSouth, 5f, 1, 6, 360f);

            Assert.That(
                PlanarPosition.Distance(northUnit, southUnit),
                Is.LessThan(Tolerance),
                "이 겹침이 기준을 하나로 모아야 하는 이유이다. 겹치지 않게 되었다면 이 설명을 고쳐야 한다.");
        }

        [Test]
        public void TheReferencePointsBackFromTheEnemyTowardsUs()
        {
            // 전진 방향은 적을 향한다. 기준은 그 반대여야 자리가 우리 쪽에 생기고, 유닛이 적을 돌아가지 않는다.
            var reference = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward);

            Assert.That(reference.Z, Is.EqualTo(-1f).Within(Tolerance));
            Assert.That(reference.X, Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void OpposingTeamsFaceEachOther()
        {
            var forward = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward);
            var reverse = ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Reverse);

            Assert.That(
                PlanarPosition.Dot(forward, reverse),
                Is.EqualTo(-1f).Within(Tolerance),
                "두 진영은 서로 반대쪽에서 다가온다.");
        }

        [Test]
        public void AnInvalidLaneFallsBackToOneFixedDirection()
        {
            // 길이가 없는 레인이다. 방향을 정할 수 없어도 모두가 같은 값을 얻어야 한다.
            var degenerate = new LaneGeometry(Vector3.zero, Vector3.zero);

            var reference = ApproachSpreadReference.Resolve(degenerate, LaneAdvanceOrientation.Forward);

            Assert.That(reference, Is.EqualTo(ApproachSpreadReference.FallbackDirection));
        }

        [Test]
        public void HeightAloneNeverMakesALaneUsable()
        {
            // 위아래로만 뻗은 레인은 평면에서 길이가 0이다. 높이가 방향을 만들어내면 안 된다.
            var vertical = new LaneGeometry(Vector3.zero, new Vector3(0f, 50f, 0f));

            var reference = ApproachSpreadReference.Resolve(vertical, LaneAdvanceOrientation.Forward);

            Assert.That(reference, Is.EqualTo(ApproachSpreadReference.FallbackDirection));
        }

        [Test]
        public void WithoutALaneEveryoneStillGetsTheSameDirection()
        {
            var ally = ApproachSpreadReference.Resolve(null, Ally);
            var enemy = ApproachSpreadReference.Resolve(null, Enemy);

            Assert.That(ally, Is.EqualTo(ApproachSpreadReference.FallbackDirection));
            Assert.That(
                enemy,
                Is.EqualTo(ally),
                "레인이 없을 때 진영마다 다른 방향을 주면 그때만 자리가 겹친다.");
        }

        [Test]
        public void AnUnassignedTeamFallsBackWithoutThrowing()
        {
            // 레인은 제대로 갖춰져 있고 진영만 지정되지 않은 상태를 만든다.
            // 레인이 없어서 물러난 것과 구별하려면 레인 쪽은 온전해야 한다.
            var laneObject = new GameObject("Lane");
            try
            {
                var start = new GameObject("Start").transform;
                var end = new GameObject("End").transform;
                start.SetParent(laneObject.transform);
                end.SetParent(laneObject.transform);
                start.position = new Vector3(0f, 0f, -50f);
                end.position = new Vector3(0f, 0f, 50f);

                var lane = laneObject.AddComponent<BattleLane>();
                lane.SetLane(start, end, Ally);
                Assert.That(lane.IsConfigured, Is.True, "레인 쪽 문제로 물러나면 이 검사가 뜻을 잃는다.");

                var reference = ApproachSpreadReference.Resolve(lane, default);

                Assert.That(reference, Is.EqualTo(ApproachSpreadReference.FallbackDirection));
            }
            finally
            {
                Object.DestroyImmediate(laneObject);
            }
        }

        [Test]
        public void TheNodeWalksToItsOwnSlotInsteadOfTheEnemy()
        {
            var mover = new FakeCharacterMover();
            var node = CreateNode(mover, slotSeed: 2);

            var status = node.Tick(CreateContextWithTarget(new Vector3(0f, 0f, 10f), out var targetObject));

            try
            {
                Assert.That(status, Is.EqualTo(BehaviourStatus.Running));
                Assert.That(mover.LastDestination.HasValue, Is.True);
                Assert.That(
                    Vector3.Distance(mover.LastDestination.Value, targetObject.transform.position),
                    Is.EqualTo(5f).Within(Tolerance),
                    "적의 자리로 그대로 가면 자리를 나눈 것이 아니다.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void AnUnreachableSlotMovesOnToTheNextOneInAFixedOrder()
        {
            // 첫 자리를 막고 두 번 돌린다. 두 번 모두 같은 대체 자리로 가야 한다.
            var blockedSlots = new List<Vector3>();
            var first = RunWithFirstSlotBlocked(blockedSlots);
            var second = RunWithFirstSlotBlocked(blockedSlots);

            Assert.That(
                Vector3.Distance(first, second),
                Is.LessThan(Tolerance),
                "대체 차례가 흔들리면 같은 상황에서 다른 곳으로 간다.");
            Assert.That(
                Vector3.Distance(first, blockedSlots[0]),
                Is.GreaterThan(Tolerance),
                "막힌 자리를 그대로 쓰면 대체가 일어나지 않은 것이다.");
        }

        [Test]
        public void WhenNoSlotIsReachableTheEnemyPositionIsUsed()
        {
            var mover = new FakeCharacterMover();
            var node = CreateNode(mover, slotSeed: 0, isReachable: NothingIsReachable);
            var context = CreateContextWithTarget(new Vector3(3f, 0f, 10f), out var targetObject);

            try
            {
                node.Tick(context);

                Assert.That(
                    mover.LastDestination.Value,
                    Is.EqualTo(targetObject.transform.position),
                    "도달할 수 있는 슬롯이 없으면 대상 위치로 다가간다.");
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void TheNodeFailsWithoutATarget()
        {
            var mover = new FakeCharacterMover();
            var node = CreateNode(mover, slotSeed: 0);

            var status = node.Tick(new BehaviourContext());

            Assert.That(status, Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(mover.LastDestination.HasValue, Is.False);
        }

        [Test]
        public void ResettingStopsTheMover()
        {
            // 상위 우선순위에 가로채였을 때 이동이 남아 있으면 유닛이 엉뚱한 곳으로 계속 걸어간다.
            var mover = new FakeCharacterMover();
            var node = CreateNode(mover, slotSeed: 0);
            var context = CreateContextWithTarget(new Vector3(0f, 0f, 10f), out var targetObject);

            try
            {
                Assert.That(node.Tick(context), Is.EqualTo(BehaviourStatus.Running));
                node.Reset();

                Assert.That(mover.StopCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void DisabledSettingsSendTheUnitStraightToTheEnemy()
        {
            var mover = new FakeCharacterMover();
            var node = new SpreadChaseTargetBehaviour(
                mover,
                UnitBehaviourKeys.Target,
                ApproachSpreadSettings.Disabled,
                () => 3,
                () => ApproachSpreadReference.FallbackDirection,
                EverythingIsReachable);
            var context = CreateContextWithTarget(new Vector3(0f, 0f, 10f), out var targetObject);

            try
            {
                node.Tick(context);

                Assert.That(mover.LastDestination.Value, Is.EqualTo(targetObject.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        /// <summary>첫 자리를 막아 둔 채 노드를 한 번 돌리고 목적지를 얻는다.</summary>
        /// <param name="blockedSlots">막힌 자리를 모아 둘 목록이며 첫 호출에서 채워진다.</param>
        /// <returns>노드가 고른 목적지이다.</returns>
        private static Vector3 RunWithFirstSlotBlocked(List<Vector3> blockedSlots)
        {
            var mover = new FakeCharacterMover();
            var attempt = 0;
            var node = CreateNode(mover, slotSeed: 2, isReachable: (Vector3 desired, out Vector3 resolved) =>
            {
                resolved = desired;
                if (attempt++ != 0)
                {
                    return true;
                }

                if (blockedSlots.Count == 0)
                {
                    blockedSlots.Add(desired);
                }

                return false;
            });

            var context = CreateContextWithTarget(new Vector3(0f, 0f, 10f), out var targetObject);
            try
            {
                node.Tick(context);
                return mover.LastDestination.Value;
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        /// <summary>검증에 쓸 자리 나누기 노드를 만든다.</summary>
        /// <param name="mover">이동 요청을 받을 구성요소이다.</param>
        /// <param name="slotSeed">이 유닛의 고정된 번호이다.</param>
        /// <param name="isReachable">자리에 갈 수 있는지 보는 방법이며 기본은 모두 갈 수 있다고 본다.</param>
        /// <returns>만든 노드이다.</returns>
        private static SpreadChaseTargetBehaviour CreateNode(
            ICharacterMover mover,
            int slotSeed,
            SlotReachabilityCheck isReachable = null)
        {
            return new SpreadChaseTargetBehaviour(
                mover,
                UnitBehaviourKeys.Target,
                new ApproachSpreadSettings(5f, 6, 180f),
                () => slotSeed,
                () => ApproachSpreadReference.Resolve(NorthwardLane, LaneAdvanceOrientation.Forward),
                isReachable ?? EverythingIsReachable);
        }

        /// <summary>대상이 담긴 행동 컨텍스트를 만든다.</summary>
        /// <param name="targetPosition">대상을 놓을 세계 좌표이다.</param>
        /// <param name="targetObject">만든 대상 오브젝트이며 검사가 끝나면 지워야 한다.</param>
        /// <returns>대상이 담긴 컨텍스트이다.</returns>
        private static IBehaviourContext CreateContextWithTarget(
            Vector3 targetPosition,
            out GameObject targetObject)
        {
            targetObject = new GameObject("Target");
            targetObject.transform.position = targetPosition;

            var context = new BehaviourContext();
            context.SetValue(UnitBehaviourKeys.Target, targetObject.transform);
            return context;
        }

        /// <summary>어느 자리든 갈 수 있다고 본다.</summary>
        private static bool EverythingIsReachable(Vector3 desiredPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = desiredPosition;
            return true;
        }

        /// <summary>어느 자리에도 갈 수 없다고 본다.</summary>
        private static bool NothingIsReachable(Vector3 desiredPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = desiredPosition;
            return false;
        }

        /// <summary>이동과 정지 요청을 기록하는 테스트용 이동 구성요소이다.</summary>
        private sealed class FakeCharacterMover : ICharacterMover
        {
            /// <summary>도착했는지 여부이다.</summary>
            public bool HasReachedDestination { get; set; }

            /// <summary>마지막으로 받은 목적지이며 없으면 값이 없다.</summary>
            public Vector3? LastDestination { get; private set; }

            /// <summary>정지 요청을 받은 횟수이다.</summary>
            public int StopCount { get; private set; }

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination)
            {
                LastDestination = destination;
                return true;
            }

            /// <inheritdoc />
            public void Stop()
            {
                StopCount++;
            }
        }
    }
}
