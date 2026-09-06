using HS.Framework.Gameplay.Teams;
using HS.Tactics.Lane;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>진영별 진행 방향 판정과 목표를 향한 전진 방향 계산을 검증한다.</summary>
    public sealed class LaneAdvanceCalculatorTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void TheForwardTeamAdvancesTowardTheEndOfTheLane()
        {
            var isResolved = LaneAdvanceCalculator.TryGetOrientation(
                new TeamId(1), new TeamId(1), out var orientation);

            Assert.That(isResolved, Is.True);
            Assert.That(orientation, Is.EqualTo(LaneAdvanceOrientation.Forward));
        }

        [Test]
        public void EveryOtherTeamAdvancesInTheOppositeDirection()
        {
            var isResolved = LaneAdvanceCalculator.TryGetOrientation(
                new TeamId(2), new TeamId(1), out var orientation);

            Assert.That(isResolved, Is.True);
            Assert.That(orientation, Is.EqualTo(LaneAdvanceOrientation.Reverse));
        }

        [Test]
        public void AnUnassignedTeamLeavesTheOrientationUndecided()
        {
            Assert.That(
                LaneAdvanceCalculator.TryGetOrientation(TeamId.None, new TeamId(1), out _),
                Is.False);
            Assert.That(
                LaneAdvanceCalculator.TryGetOrientation(new TeamId(1), TeamId.None, out _),
                Is.False);
        }

        [Test]
        public void TheDirectionPointsAtTheTargetAndIsNormalized()
        {
            var direction = LaneAdvanceCalculator.GetAdvanceDirection(
                new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 20f));

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(Vector3.Distance(direction, Vector3.forward), Is.LessThan(Tolerance));
        }

        [Test]
        public void AnOffAxisUnitTurnsTowardTheTargetInsteadOfFollowingTheLaneAxis()
        {
            var position = new Vector3(6f, 0f, 12f);
            var target = new Vector3(0f, 0f, 20f);

            var direction = LaneAdvanceCalculator.GetAdvanceDirection(position, target);

            var expected = new Vector3(-6f, 0f, 8f).normalized;
            Assert.That(Vector3.Distance(direction, expected), Is.LessThan(Tolerance));
        }

        [Test]
        public void HeightDifferenceDoesNotTiltTheDirection()
        {
            var direction = LaneAdvanceCalculator.GetAdvanceDirection(
                new Vector3(0f, 12f, 0f), new Vector3(0f, 0f, 20f));

            Assert.That(direction.y, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Vector3.Distance(direction, Vector3.forward), Is.LessThan(Tolerance));
        }

        [Test]
        public void ReachingTheTargetStopsTheAdvance()
        {
            var direction = LaneAdvanceCalculator.GetAdvanceDirection(
                new Vector3(0f, 0f, 19.5f), new Vector3(0f, 0f, 20f), 1f);

            Assert.That(direction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void StandingExactlyOnTheTargetStopsTheAdvanceEvenWithoutTolerance()
        {
            var target = new Vector3(4f, 0f, 20f);

            var direction = LaneAdvanceCalculator.GetAdvanceDirection(target, target, 0f);

            Assert.That(direction, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ANegativeToleranceIsTreatedAsNoTolerance()
        {
            var direction = LaneAdvanceCalculator.GetAdvanceDirection(
                new Vector3(0f, 0f, 19.5f), new Vector3(0f, 0f, 20f), -5f);

            Assert.That(Vector3.Distance(direction, Vector3.forward), Is.LessThan(Tolerance));
        }
    }
}
