using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Pathfinding;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 목표를 막는 장애물 하나를 제외하는 excludeFromGoal 인자를 검증한다.
    /// 엄폐물 중심처럼 목표가 지정 장애물 안에 있을 때 해당 장애물만 제외한다.
    /// 인자를 생략하면 모든 장애물을 검사하며, 다른 장애물은 제외 인자와 무관하게 목표를 막는다.
    /// </summary>
    public sealed class ObstacleVisibilityGraphSelfObstacleTests
    {
        private static readonly PlanarRectangle WideBounds =
            new(PlanarPosition.Zero, new PlanarPosition(50f, 50f), new PlanarPosition(0f, 1f));

        [Test]
        public void AGoalInsideTheObstacleItselfIsReachableWhenThatObstacleIsExcluded()
        {
            var obstacle = new PlanarRectangle(
                new PlanarPosition(5f, 5f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle> { obstacle }, WideBounds);

            var found = graph.TryFindPath(
                WideBounds, new PlanarPosition(-5f, -5f), obstacle.Center, out var path, obstacle);

            Assert.That(found, Is.True, "목적지가 속한 장애물 하나를 뺐으므로 그 자리에 닿을 수 있어야 한다.");
            Assert.That(path[path.Count - 1].X, Is.EqualTo(obstacle.Center.X).Within(0.0001f));
            Assert.That(path[path.Count - 1].Z, Is.EqualTo(obstacle.Center.Z).Within(0.0001f));
        }

        [Test]
        public void ExcludingTheWrongObstacleStillBlocksTheGoal()
        {
            // 목표가 걸리는 장애물과 뺀 장애물이 다르면, 뺀 것은 아무 소용이 없어 막혀야 한다.
            var blockingObstacle = new PlanarRectangle(
                new PlanarPosition(5f, 5f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));
            var unrelatedObstacle = new PlanarRectangle(
                new PlanarPosition(-20f, -20f), new PlanarPosition(1f, 1f), new PlanarPosition(0f, 1f));
            var graph = new ObstacleVisibilityGraph(
                new List<PlanarRectangle> { blockingObstacle }, WideBounds);

            var found = graph.TryFindPath(
                WideBounds, new PlanarPosition(-5f, -5f), blockingObstacle.Center, out var path,
                unrelatedObstacle);

            Assert.That(found, Is.False, "목표를 막는 장애물이 빠진 것과 다르면 여전히 갈 수 없는 자리다.");
        }

        [Test]
        public void ExcludingOneObstacleStillRespectsAnotherThatAlsoCoversTheGoal()
        {
            // 두 장애물이 겹쳐 같은 자리를 함께 막으면, 하나만 빼도 나머지 하나가 여전히 막는다.
            var first = new PlanarRectangle(
                new PlanarPosition(5f, 5f), new PlanarPosition(2f, 2f), new PlanarPosition(0f, 1f));
            var second = new PlanarRectangle(
                new PlanarPosition(5.5f, 5f), new PlanarPosition(2f, 2f), new PlanarPosition(0f, 1f));
            var graph = new ObstacleVisibilityGraph(new List<PlanarRectangle> { first, second }, WideBounds);

            var found = graph.TryFindPath(
                WideBounds, new PlanarPosition(-5f, -5f), first.Center, out _, first);

            Assert.That(found, Is.False, "겹친 다른 장애물이 여전히 그 자리를 막고 있어야 한다.");
        }
    }
}
