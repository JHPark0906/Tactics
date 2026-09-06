using System;
using System.Collections.Generic;
using System.Linq;
using HS.Framework.AI.Pathfinding;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// A* 가 5×5 격자 그래프 위에서 최단 경로를 찾고, 갈 수 없으면 실패를 알리며, 어느 실행에서나 같은 경로를
    /// 내놓는지 검증한다. 그래프는 좌표를 문자열 노드 뒤에 감춰, 탐색이 좌표를 몰라도 되는지도 함께 본다.
    /// </summary>
    public sealed class AStarPathfinderTests
    {
        [Test]
        public void ReopensAClosedNodeWhenAnAdmissibleHeuristicFindsACheaperRouteLater()
        {
            var found = AStarPathfinder.TryFindPath(new InconsistentHeuristicGraph(), "S", "G", out var path);

            Assert.That(found, Is.True);
            Assert.That(path, Is.EqualTo(new[] { "S", "B", "A", "G" }),
                "A를 먼저 닫아도 B를 거친 비용 3.5의 길을 찾아야 한다. S-A-G는 비용 4이다.");
        }

        private sealed class InconsistentHeuristicGraph : IPathGraph<string>
        {
            public IEnumerable<string> GetNeighbors(string node) => node switch
            {
                "S" => new[] { "A", "B" },
                "B" => new[] { "A" },
                "A" => new[] { "G" },
                _ => Array.Empty<string>()
            };

            public float GetCost(string from, string to) => (from, to) switch
            {
                ("S", "A") => 2f,
                ("S", "B") => 1f,
                ("B", "A") => 0.5f,
                ("A", "G") => 2f,
                _ => throw new ArgumentException("연결되지 않은 노드이다.")
            };

            // B의 실제 남은 비용은 2.5이므로 2는 허용 가능한 어림값이다.
            public float GetHeuristic(string node, string goal) => node == "B" ? 2f : 0f;
        }

        [Test]
        public void FindsTheShortestPathOnAnOpenGrid()
        {
            var graph = new GridGraph(5, 5);

            var found = AStarPathfinder.TryFindPath(graph, GridGraph.NodeAt(0, 0), GridGraph.NodeAt(0, 4), out var path);

            Assert.That(found, Is.True);
            Assert.That(
                path,
                Is.EqualTo(new[] { "0,0", "0,1", "0,2", "0,3", "0,4" }),
                "시작과 목표가 같은 열에 있으면 최단 경로는 그 열을 따라가는 한 갈래뿐이다.");
        }

        [Test]
        public void ReportsFailureWhenNoPathExists()
        {
            var blockedColumn = Enumerable.Range(0, 5).Select(y => GridGraph.NodeAt(2, y));
            var graph = new GridGraph(5, 5, blockedColumn);

            var found = AStarPathfinder.TryFindPath(graph, GridGraph.NodeAt(0, 0), GridGraph.NodeAt(4, 4), out var path);

            Assert.That(found, Is.False, "가운데 열 전체가 막혀 있으면 4방향 격자에서는 반대편으로 건널 수 없다.");
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void StartEqualToGoalReturnsTheSingleNodePath()
        {
            var graph = new GridGraph(5, 5);

            var found = AStarPathfinder.TryFindPath(graph, GridGraph.NodeAt(2, 2), GridGraph.NodeAt(2, 2), out var path);

            Assert.That(found, Is.True);
            Assert.That(path, Is.EqualTo(new[] { "2,2" }), "시작과 목표가 같으면 그 자리 하나로 된 경로를 돌려준다. 빈 경로는 실패와 헷갈린다.");
        }

        [Test]
        public void RoutesAroundAnObstacleThroughTheOnlyGap()
        {
            var blockedRowExceptOneGap = Enumerable.Range(0, 4).Select(x => GridGraph.NodeAt(x, 2));
            var graph = new GridGraph(5, 5, blockedRowExceptOneGap);

            var found = AStarPathfinder.TryFindPath(graph, GridGraph.NodeAt(0, 0), GridGraph.NodeAt(0, 4), out var path);

            Assert.That(found, Is.True);
            Assert.That(path.First(), Is.EqualTo("0,0"));
            Assert.That(path.Last(), Is.EqualTo("0,4"));
            Assert.That(
                path,
                Has.None.Matches<string>(node => node is "0,2" or "1,2" or "2,2" or "3,2"),
                "막힌 칸을 지나가면 안 된다.");
            Assert.That(
                path,
                Has.Count.EqualTo(13),
                "가운데 행이 (4,2) 한 칸만 열려 있으므로 최단 우회는 그 틈까지 갔다가 되돌아오는 12걸음(13개 노드)이다.");
        }

        [Test]
        public void ProducesTheSameShortestPathOnRepeatedRuns()
        {
            var graph = new GridGraph(5, 5);
            var start = GridGraph.NodeAt(0, 0);
            var goal = GridGraph.NodeAt(2, 2);

            AStarPathfinder.TryFindPath(graph, start, goal, out var first);
            var results = Enumerable.Range(0, 5)
                .Select(_ =>
                {
                    AStarPathfinder.TryFindPath(graph, start, goal, out var path);
                    return path;
                })
                .ToList();

            Assert.That(
                results,
                Has.All.EqualTo(first),
                "격자 대각 이동에는 길이가 같은 경로가 여럿이라 동점 처리가 실행마다 갈리면 이 검사가 흔들린다.");
        }

        /// <summary>
        /// x,y 좌표를 "x,y" 문자열로 감춘 4방향 격자 그래프이다. 탐색에게는 좌표가 아니라 이 문자열만 보인다.
        /// </summary>
        private sealed class GridGraph : IPathGraph<string>
        {
            private static readonly (int Dx, int Dy)[] Directions = { (1, 0), (-1, 0), (0, 1), (0, -1) };

            private readonly int _width;
            private readonly int _height;
            private readonly HashSet<string> _blocked;

            internal GridGraph(int width, int height, IEnumerable<string> blocked = null)
            {
                _width = width;
                _height = height;
                _blocked = blocked != null ? new HashSet<string>(blocked) : new HashSet<string>();
            }

            internal static string NodeAt(int x, int y) => $"{x},{y}";

            public IEnumerable<string> GetNeighbors(string node)
            {
                var (x, y) = Parse(node);
                foreach (var (dx, dy) in Directions)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx < 0 || nx >= _width || ny < 0 || ny >= _height)
                    {
                        continue;
                    }

                    var candidate = NodeAt(nx, ny);
                    if (!_blocked.Contains(candidate))
                    {
                        yield return candidate;
                    }
                }
            }

            public float GetCost(string from, string to) => 1f;

            public float GetHeuristic(string node, string goal)
            {
                var (nx, ny) = Parse(node);
                var (gx, gy) = Parse(goal);
                return Math.Abs(nx - gx) + Math.Abs(ny - gy);
            }

            private static (int X, int Y) Parse(string node)
            {
                var parts = node.Split(',');
                return (int.Parse(parts[0]), int.Parse(parts[1]));
            }
        }
    }
}
