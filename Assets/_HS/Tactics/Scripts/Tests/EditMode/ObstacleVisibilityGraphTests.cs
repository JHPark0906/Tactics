using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Pathfinding;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 장애물과 전장 경계로 지은 시야 그래프 위에서 A*가 옳은 경로를 내는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 시계도 씬도 쓰지 않는 순수 계산이다. 여기서 보는 것은 「장애물 목록 + 시작 + 끝을 주면
    /// 그것을 피하는 꺾임점이 나오는가」와 「같은 입력이면 언제나 같은 경로가 나오는가」이다.
    /// </para>
    /// <para>
    /// <b>확인하지 않는 것</b>: 유닛이 실제로 그 꺾임점을 걸을 때 장애물에 부딪히지 않는지는 여기서
    /// 보지 않는다. 그래프는 유닛 반지름을 모르므로 그 여유는 이동이 스텝마다 흡수한다.
    /// 다른 유닛을 피하는 것도 여기서 보지 않는다 — 유닛은 이 그래프에 굽지 않는다.
    /// </para>
    /// </remarks>
    public sealed class ObstacleVisibilityGraphTests
    {
        private const float Tolerance = 0.0001f;
        private static readonly PlanarRectangle WideBounds =
            new(PlanarPosition.Zero, new PlanarPosition(50f, 50f), new PlanarPosition(0f, 1f));

        [Test]
        public void WithNoObstaclesTheDirectLineIsTheWholePath()
        {
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle>(), WideBounds);
            var start = new PlanarPosition(-10f, 0f);
            var goal = new PlanarPosition(10f, 0f);

            var found = graph.TryFindPath(WideBounds, start, goal, out var path);

            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.EqualTo(2), "막는 것이 없으면 시작과 끝, 둘뿐이어야 한다.");
            AssertPosition(path[0], start);
            AssertPosition(path[path.Count - 1], goal);
        }

        [Test]
        public void ANullObstacleListBehavesLikeAnEmptyOne()
        {
            var graph = new ObstacleVisibilityGraph(null, WideBounds);

            var found = graph.TryFindPath(WideBounds, new PlanarPosition(-5f, 0f), new PlanarPosition(5f, 0f), out var path);

            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.EqualTo(2));
        }

        [Test]
        public void StartEqualToGoalGivesASingleNodePath()
        {
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle>(), WideBounds);
            var point = new PlanarPosition(3f, 4f);

            var found = graph.TryFindPath(WideBounds, point, point, out var path);

            Assert.That(found, Is.True);
            Assert.That(path.Count, Is.EqualTo(1));
            AssertPosition(path[0], point);
        }

        [Test]
        public void AnObstacleBetweenStartAndGoalForcesADetourThatAvoidsIt()
        {
            // 🔴 이 무대는 장애물 자신의 인접한 두 꼭짓점을 잇는 변이 실제로 필요하다 — 시작은 가까운
            // 꼭짓점만 보고, 그 꼭짓점은 목표를 못 보며, 먼 꼭짓점을 거쳐야만 목표에 닿는다. 꼭짓점을
            // 밖으로 밀어내지 않으면 같은 장애물의 인접한 꼭짓점끼리도 "닿았다"로 막힌 것이 되어
            // 이 경로 자체가 없어진다 — 그러면 이 검사는 found가 거짓이 되어 실패한다.
            var obstacle = new PlanarRectangle(
                PlanarPosition.Zero, new PlanarPosition(2f, 2f), new PlanarPosition(0f, 1f));
            var obstacles = new List<PlanarRectangle> { obstacle };
            var graph = new ObstacleVisibilityGraph(obstacles, WideBounds);
            var start = new PlanarPosition(-10f, 0f);
            var goal = new PlanarPosition(10f, 0f);

            var found = graph.TryFindPath(WideBounds, start, goal, out var path);

            Assert.That(found, Is.True);
            Assert.That(
                path.Count, Is.EqualTo(4),
                "시작·장애물의 가까운 꼭짓점·먼 꼭짓점·목표, 넷을 거쳐야 하는 무대다.");
            AssertPathAvoids(path, obstacles);
        }

        [Test]
        public void AGoalInsideAnObstacleIsNotReachable()
        {
            var obstacle = new PlanarRectangle(
                new PlanarPosition(5f, 5f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle> { obstacle }, WideBounds);

            var found = graph.TryFindPath(
                WideBounds, new PlanarPosition(-5f, -5f), new PlanarPosition(5f, 5f), out var path);

            Assert.That(found, Is.False, "목표가 장애물 안이면 애초에 있을 수 없는 자리다.");
            Assert.That(path.Count, Is.Zero);
        }

        [Test]
        public void AGoalOutsideTheBoundaryIsNotReachable()
        {
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle>(), WideBounds);

            var found = graph.TryFindPath(
                WideBounds, PlanarPosition.Zero, new PlanarPosition(1000f, 1000f), out var path);

            Assert.That(found, Is.False, "목표가 전장 경계 밖이면 갈 수 없는 자리다.");
        }

        [Test]
        public void ASymmetricDetourStillPicksTheSamePathEveryTime()
        {
            // 장애물이 시작-목표 축 위에 정확히 놓여, 왼쪽으로 돌든 오른쪽으로 돌든 비용이 같다.
            // 동점을 가르는 비교자가 없으면 이 상황에서 예외가 난다.
            var obstacle = new PlanarRectangle(
                PlanarPosition.Zero, new PlanarPosition(2f, 2f), new PlanarPosition(0f, 1f));
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle> { obstacle }, WideBounds);
            var start = new PlanarPosition(-10f, 0f);
            var goal = new PlanarPosition(10f, 0f);

            graph.TryFindPath(WideBounds, start, goal, out var first);
            for (var attempt = 0; attempt < 5; attempt++)
            {
                graph.TryFindPath(WideBounds, start, goal, out var repeat);

                Assert.That(repeat.Count, Is.EqualTo(first.Count));
                for (var index = 0; index < first.Count; index++)
                {
                    AssertPosition(repeat[index], first[index]);
                }
            }
        }

        /// <summary>경로의 이어진 구간마다 어느 장애물도 가로지르지 않는지 확인한다.</summary>
        /// <param name="path">확인할 꺾임점 목록이다.</param>
        /// <param name="obstacles">피해야 하는 장애물 목록이다.</param>
        private static void AssertPathAvoids(IReadOnlyList<PlanarPosition> path, IReadOnlyList<PlanarRectangle> obstacles)
        {
            for (var index = 0; index < path.Count - 1; index++)
            {
                foreach (var obstacle in obstacles)
                {
                    Assert.That(
                        PlanarGeometry.IntersectsSegment(obstacle, path[index], path[index + 1]),
                        Is.False,
                        $"{index}번째 구간이 장애물을 가로지른다.");
                }
            }
        }

        private static void AssertPosition(PlanarPosition actual, PlanarPosition expected)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance));
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Tolerance));
        }
    }
}
