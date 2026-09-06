using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 각도 한계 안에서 이동 방향을 바라보는 평면 회전 계산을 검증한다.
    /// 이 계산은 이미 정한 방향을 회전으로 표현하며 경로 자체는 바꾸지 않는다.
    /// 회전 속도, 짧은 회전 방향, 정지 및 퇴화 방향의 처리를 확인한다.
    /// </summary>
    public sealed class PlanarFacingTests
    {
        private const float Tolerance = 0.0001f;

        private static readonly PlanarPosition North = new(0f, 1f);
        private static readonly PlanarPosition East = new(1f, 0f);
        private static readonly PlanarPosition South = new(0f, -1f);

        [Test]
        public void ASmallTurnLandsExactlyOnTheTarget()
        {
            var result = PlanarFacing.Advance(North, East, maxDegrees: 180f);

            Assert.That(PlanarPosition.Distance(result, East), Is.LessThan(Tolerance));
        }

        [Test]
        public void ABigTurnIsCutToTheLimit()
        {
            // 한 번에 다 돌면 방향이 튄다. 각속도 한계가 그것을 막는다.
            var result = PlanarFacing.Advance(North, East, maxDegrees: 30f);

            Assert.That(
                Mathf.Abs(PlanarFacing.SignedAngleDegrees(North, result)),
                Is.EqualTo(30f).Within(0.01f));
            Assert.That(
                Mathf.Abs(PlanarFacing.SignedAngleDegrees(result, East)),
                Is.EqualTo(60f).Within(0.01f),
                "남은 각도만큼은 아직 안 돌아 있어야 한다.");
        }

        [Test]
        public void TurningTakesTheShorterWay()
        {
            // 서쪽으로 조금 도는 것이 동쪽으로 크게 도는 것보다 짧다. 먼 쪽으로 돌면 눈에 띄게 어색하다.
            var west = new PlanarPosition(-1f, 0f);
            var slightlyWest = PlanarFacing.Advance(North, west, maxDegrees: 10f);

            Assert.That(slightlyWest.X, Is.LessThan(0f), "가까운 쪽으로 돌아야 한다.");
        }

        [Test]
        public void TheResultIsAlwaysAUnitDirection()
        {
            var result = PlanarFacing.Advance(new PlanarPosition(0f, 5f), new PlanarPosition(7f, 0f), 20f);

            Assert.That(result.Magnitude, Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void WithoutAMovementDirectionTheFacingIsKept()
        {
            // 멈춰 있으면 돌 이유가 없다. 여기서 방향을 잃으면 멈출 때마다 유닛이 홱 돌아간다.
            var result = PlanarFacing.Advance(East, PlanarPosition.Zero, 90f);

            Assert.That(PlanarPosition.Distance(result, East), Is.LessThan(Tolerance));
        }

        [Test]
        public void WithoutACurrentFacingTheTargetIsTakenAtOnce()
        {
            // 아직 바라보는 방향이 없으면 돌 것도 없다.
            var result = PlanarFacing.Advance(PlanarPosition.Zero, East, maxDegrees: 1f);

            Assert.That(PlanarPosition.Distance(result, East), Is.LessThan(Tolerance));
        }

        [Test]
        public void AZeroLimitDoesNotTurn()
        {
            var result = PlanarFacing.Advance(North, East, maxDegrees: 0f);

            Assert.That(PlanarPosition.Distance(result, North), Is.LessThan(Tolerance));
        }

        [Test]
        public void TurningBackwardsPicksOneSideAndStaysThere()
        {
            // 정반대 방향은 어느 쪽으로 돌아도 같은 거리다. 매번 다른 쪽을 고르면 유닛이 떤다.
            var first = PlanarFacing.Advance(North, South, maxDegrees: 45f);
            var second = PlanarFacing.Advance(North, South, maxDegrees: 45f);

            Assert.That(PlanarPosition.Distance(first, second), Is.LessThan(Tolerance));
        }

        [Test]
        public void RepeatedStepsReachTheTarget()
        {
            // 한계가 작아도 여러 스텝이면 도착해야 한다. 도착하지 못하면 유닛이 영원히 비스듬히 걷는다.
            var facing = North;
            for (var step = 0; step < 20; step++)
            {
                facing = PlanarFacing.Advance(facing, East, maxDegrees: 10f);
            }

            Assert.That(PlanarPosition.Distance(facing, East), Is.LessThan(Tolerance));
        }

        [Test]
        public void TheSignedAngleTellsWhichWayToTurn()
        {
            Assert.That(PlanarFacing.SignedAngleDegrees(North, North), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(
                Mathf.Abs(PlanarFacing.SignedAngleDegrees(North, East)),
                Is.EqualTo(90f).Within(0.01f));
            Assert.That(
                PlanarFacing.SignedAngleDegrees(North, East),
                Is.Not.EqualTo(PlanarFacing.SignedAngleDegrees(East, North)).Within(0.01f),
                "방향이 반대면 부호도 반대여야 한다.");
        }

        [Test]
        public void RotatingByTheMeasuredAngleLandsOnTheTarget()
        {
            // 각도를 재는 것과 그 각도만큼 도는 것이 같은 규칙을 써야 한다. 어긋나면 목표를 지나친다.
            var angle = PlanarFacing.SignedAngleDegrees(North, East);

            var rotated = PlanarFacing.Rotate(North, angle);

            Assert.That(PlanarPosition.Distance(rotated, East), Is.LessThan(Tolerance));
        }
    }
}
