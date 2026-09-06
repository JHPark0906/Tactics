using HS.Tactics.Lane;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>레인 기하가 길이와 방향, 진영별 목표 끝점을 바르게 계산하는지 검증한다.</summary>
    public sealed class LaneGeometryTests
    {
        private const float Tolerance = 1e-4f;

        [Test]
        public void ForwardPointsFromStartToEnd()
        {
            var lane = new LaneGeometry(Vector3.zero, new Vector3(0f, 0f, 20f));

            Assert.That(lane.IsValid, Is.True);
            Assert.That(lane.Length, Is.EqualTo(20f).Within(Tolerance));
            AssertVector(lane.Forward, Vector3.forward);
        }

        [Test]
        public void HeightDifferenceIsIgnoredWhenMeasuringTheLane()
        {
            var lane = new LaneGeometry(new Vector3(0f, 5f, 0f), new Vector3(0f, -3f, 20f));

            Assert.That(lane.Length, Is.EqualTo(20f).Within(Tolerance));
            AssertVector(lane.Forward, Vector3.forward);
        }

        [Test]
        public void ALaneShorterThanTheMinimumHasNoDirection()
        {
            var lane = new LaneGeometry(Vector3.zero, new Vector3(0f, 10f, 0f));

            Assert.That(lane.IsValid, Is.False);
            AssertVector(lane.Forward, Vector3.zero);
            AssertVector(lane.GetDirection(LaneAdvanceOrientation.Reverse), Vector3.zero);
        }

        [Test]
        public void EachOrientationTakesTheOppositeEndAsItsGoal()
        {
            var start = new Vector3(1f, 0f, 2f);
            var end = new Vector3(1f, 0f, 22f);
            var lane = new LaneGeometry(start, end);

            AssertVector(lane.GetGoal(LaneAdvanceOrientation.Forward), end);
            AssertVector(lane.GetGoal(LaneAdvanceOrientation.Reverse), start);
        }

        [Test]
        public void EachOrientationAdvancesInTheOppositeDirection()
        {
            var lane = new LaneGeometry(Vector3.zero, new Vector3(0f, 0f, 20f));

            AssertVector(lane.GetDirection(LaneAdvanceOrientation.Forward), Vector3.forward);
            AssertVector(lane.GetDirection(LaneAdvanceOrientation.Reverse), Vector3.back);
        }

        [Test]
        public void TheGoalKeepsTheHeightOfThePlacedPoint()
        {
            var lane = new LaneGeometry(Vector3.zero, new Vector3(0f, 4f, 20f));

            Assert.That(lane.GetGoal(LaneAdvanceOrientation.Forward).y, Is.EqualTo(4f).Within(Tolerance));
        }

        /// <summary>두 벡터가 허용 오차 안에서 같은지 확인한다.</summary>
        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance),
                $"기대한 값은 {expected}이고 실제 값은 {actual}이다.");
        }
    }
}
