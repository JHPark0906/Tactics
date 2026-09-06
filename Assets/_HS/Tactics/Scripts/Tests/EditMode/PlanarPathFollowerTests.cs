using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>꺾임점 경로를 따라 나아가는 계산의 경계 조건을 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// 이동 적분은 경로의 꺾임점과 끝점을 정확히 처리해야 한다. 여기가 틀리면 화면에는
    /// <b>"가끔 이상한 길로 간다"</b>로만 드러나 원인을 찾기 어렵다.
    /// 경로를 얻는 일은 내비게이션이 하지만 따라가는 규칙은 순수 계산이므로, 그 부분을 여기서 전부 덮는다.
    /// </para>
    /// <para>
    /// 특히 조용히 틀리기 좋은 자리를 겨냥한다 — 한 스텝에 꺾임점을 여러 개 지나는 경우,
    /// 마지막을 지나고도 이동량이 남는 경우, 같은 자리에 꺾임점이 겹친 경우이다.
    /// </para>
    /// </remarks>
    public sealed class PlanarPathFollowerTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void MovingLessThanTheFirstLegStopsPartWay()
        {
            var corners = Corners((0f, 0f), (0f, 10f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 3f);

            Assert.That(result.Position.Z, Is.EqualTo(3f).Within(Tolerance));
            Assert.That(result.CornerIndex, Is.EqualTo(1), "아직 꺾임점에 닿지 않았다.");
            Assert.That(result.ReachedEnd, Is.False);
        }

        [Test]
        public void ReachingACornerAdvancesToTheNextOne()
        {
            var corners = Corners((0f, 0f), (0f, 4f), (5f, 4f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 4f);

            Assert.That(result.Position.Z, Is.EqualTo(4f).Within(Tolerance));
            Assert.That(result.CornerIndex, Is.EqualTo(2));
            Assert.That(result.ReachedEnd, Is.False);
        }

        [Test]
        public void OneStepCanPassSeveralCorners()
        {
            // 스텝이 크거나 꺾임점이 촘촘하면 한 번에 여러 개를 지난다.
            var corners = Corners((0f, 0f), (0f, 1f), (1f, 1f), (2f, 1f), (3f, 1f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 3.5f);

            Assert.That(result.Position.X, Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(result.Position.Z, Is.EqualTo(1f).Within(Tolerance));
            Assert.That(result.CornerIndex, Is.EqualTo(4));
            Assert.That(result.ReachedEnd, Is.False);
        }

        [Test]
        public void LeftoverDistanceAtTheEndIsDiscarded()
        {
            var corners = Corners((0f, 0f), (0f, 2f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 100f);

            Assert.That(
                result.Position.Z,
                Is.EqualTo(2f).Within(Tolerance),
                "경로 끝을 넘어 계속 나아가면 갈 수 없는 곳으로 들어간다.");
            Assert.That(result.ReachedEnd, Is.True);
            Assert.That(result.CornerIndex, Is.EqualTo(corners.Count));
        }

        [Test]
        public void DuplicateCornersAreConsumedWithoutStalling()
        {
            // 같은 자리에 꺾임점이 겹치면 거리가 0이라, 잘못 다루면 그 자리에서 무한히 맴돈다.
            var corners = Corners((0f, 0f), (0f, 1f), (0f, 1f), (0f, 1f), (0f, 3f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 2f);

            Assert.That(result.Position.Z, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(result.CornerIndex, Is.EqualTo(4));
            Assert.That(result.ReachedEnd, Is.False);
        }

        [Test]
        public void ZeroDistanceDoesNotMove()
        {
            var corners = Corners((0f, 0f), (0f, 10f));
            var start = new PlanarPosition(0f, 1f);

            var result = PlanarPathFollower.Advance(start, corners, 1, 0f);

            Assert.That(result.Position, Is.EqualTo(start));
            Assert.That(result.CornerIndex, Is.EqualTo(1));
            Assert.That(result.ReachedEnd, Is.False);
        }

        [Test]
        public void NegativeDistanceDoesNotMoveBackwards()
        {
            var corners = Corners((0f, 0f), (0f, 10f));
            var start = new PlanarPosition(0f, 1f);

            var result = PlanarPathFollower.Advance(start, corners, 1, -5f);

            Assert.That(result.Position, Is.EqualTo(start), "음수 이동량으로 뒤로 가면 안 된다.");
        }

        [Test]
        public void EmptyPathReportsTheEndImmediately()
        {
            var start = new PlanarPosition(2f, 3f);

            var result = PlanarPathFollower.Advance(start, new List<PlanarPosition>(), 0, 5f);

            Assert.That(result.Position, Is.EqualTo(start));
            Assert.That(result.ReachedEnd, Is.True);
        }

        [Test]
        public void NullPathIsTreatedAsAnEmptyPath()
        {
            var start = new PlanarPosition(2f, 3f);

            var result = PlanarPathFollower.Advance(start, null, 0, 5f);

            Assert.That(result.Position, Is.EqualTo(start));
            Assert.That(result.ReachedEnd, Is.True);
        }

        [Test]
        public void AnIndexPastTheEndReportsTheEndWithoutMoving()
        {
            var corners = Corners((0f, 0f), (0f, 10f));
            var start = new PlanarPosition(0f, 10f);

            var result = PlanarPathFollower.Advance(start, corners, corners.Count, 5f);

            Assert.That(result.Position, Is.EqualTo(start));
            Assert.That(result.ReachedEnd, Is.True);
        }

        [Test]
        public void ANegativeIndexStartsFromTheBeginning()
        {
            var corners = Corners((0f, 0f), (0f, 10f));

            var result = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, -3, 0f);

            Assert.That(result.CornerIndex, Is.Zero);
        }

        [Test]
        public void SplittingAStepIntoTwoLandsAtTheSamePlace()
        {
            // 스텝을 쪼개도 결과가 같아야 프레임률이 결과를 바꾸지 않는다.
            var corners = Corners((0f, 0f), (0f, 3f), (4f, 3f));

            var whole = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 5f);
            var firstHalf = PlanarPathFollower.Advance(new PlanarPosition(0f, 0f), corners, 1, 2.5f);
            var secondHalf = PlanarPathFollower.Advance(
                firstHalf.Position, corners, firstHalf.CornerIndex, 2.5f);

            Assert.That(
                PlanarPosition.Distance(whole.Position, secondHalf.Position),
                Is.LessThan(Tolerance),
                "같은 거리라면 몇 번에 나눠 가든 같은 자리에 도착해야 한다.");
            Assert.That(secondHalf.CornerIndex, Is.EqualTo(whole.CornerIndex));
        }

        [Test]
        public void TheRemainingDistanceFollowsTheBendsNotTheStraightLine()
        {
            // 직선으로 재면 꺾인 길이 실제보다 짧아 보인다. 그 값으로 감속을 정하면
            // 너무 일찍 속도를 줄여 남은 길을 기어간다.
            var corners = Corners((0f, 0f), (0f, 3f), (4f, 3f));
            var start = new PlanarPosition(0f, 0f);

            var alongPath = PlanarPathFollower.RemainingDistance(start, corners, 1);
            var straightLine = PlanarPosition.Distance(start, new PlanarPosition(4f, 3f));

            Assert.That(alongPath, Is.EqualTo(7f).Within(Tolerance));
            Assert.That(straightLine, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(alongPath, Is.GreaterThan(straightLine));
        }

        [Test]
        public void AnEmptyPathAndAFinishedPathBothReportZero()
        {
            // 0이 두 가지를 뜻한다는 것을 못 박는다. 길 끝에 닿아도 0이고, 경로를 아직 못 받아도 0이다.
            // 부르는 쪽이 이 둘을 안 가르면 경로를 받기도 전에 도착했다고 판단한다.
            var corners = Corners((0f, 0f), (0f, 10f));

            var finished = PlanarPathFollower.RemainingDistance(
                new PlanarPosition(0f, 10f), corners, corners.Count);
            var noPathYet = PlanarPathFollower.RemainingDistance(
                new PlanarPosition(0f, 0f), new List<PlanarPosition>(), 1);

            Assert.That(finished, Is.Zero);
            Assert.That(
                noPathYet,
                Is.Zero,
                "경로가 없을 때도 0이므로, 이 값만으로는 도착했는지 알 수 없다.");
        }

        [Test]
        public void TheRemainingDistanceShrinksAsTheUnitAdvances()
        {
            var corners = Corners((0f, 0f), (0f, 10f));

            var atStart = PlanarPathFollower.RemainingDistance(new PlanarPosition(0f, 0f), corners, 1);
            var partWay = PlanarPathFollower.RemainingDistance(new PlanarPosition(0f, 4f), corners, 1);

            Assert.That(atStart, Is.EqualTo(10f).Within(Tolerance));
            Assert.That(partWay, Is.EqualTo(6f).Within(Tolerance));
        }

        [Test]
        public void NothingLeftToWalkMeansNoRemainingDistance()
        {
            var corners = Corners((0f, 0f), (0f, 10f));

            Assert.That(
                PlanarPathFollower.RemainingDistance(new PlanarPosition(0f, 10f), corners, corners.Count),
                Is.Zero);
            Assert.That(
                PlanarPathFollower.RemainingDistance(new PlanarPosition(0f, 0f), null, 0),
                Is.Zero);
        }

        /// <summary>좌표 쌍 목록으로 꺾임점 목록을 만든다.</summary>
        /// <param name="points">꺾임점의 x와 z 좌표 쌍이다.</param>
        /// <returns>만든 꺾임점 목록이다.</returns>
        private static List<PlanarPosition> Corners(params (float X, float Z)[] points)
        {
            var corners = new List<PlanarPosition>(points.Length);
            foreach (var point in points)
            {
                corners.Add(new PlanarPosition(point.X, point.Z));
            }

            return corners;
        }
    }
}
