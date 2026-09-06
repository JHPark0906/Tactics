using System;
using System.Collections.Generic;
using HS.Framework.AI.Pathfinding;
using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Pathfinding
{
    /// <summary>
    /// 직사각형 장애물과 전장 경계로부터 가시선 그래프를 짓고, 그 위에서 A*로 경로를 찾는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>굽는 것과 안 굽는 것.</b> 살아 있는 엄폐물과 전장 경계만 그래프에 들어간다. 유닛은 굽지 않는다 —
    /// 유닛은 움직이므로 미리 구운 그래프에 넣으면 매 스텝 다시 구워야 한다. 유닛과의 충돌은 이동이
    /// 스텝마다 따로 푼다. 그래서 이 그래프가 내는 경로는 <b>장애물을 피하는 경로일 뿐, 다른 유닛까지
    /// 피하는 경로는 아니다.</b>
    /// </para>
    /// <para>
    /// <b>유닛 반지름을 모른다.</b> 꼭짓점을 그대로 노드로 쓰므로, 나온 경로는 장애물 모서리에
    /// 바짝 붙어 지날 수 있다. 그 여유는 이동이 스텝마다 하는 충돌 판정(막히면 멈춘다)이 흡수한다.
    /// 그래프가 반지름까지 알면 계산도 무거워지고 「어디까지가 그래프의 몫이고 어디부터가 이동의 몫인가」가
    /// 흐려진다. 유닛이 굽지 않는 것과 같은 이유로 나눠 둔다.
    /// </para>
    /// <para>
    /// <b>경계는 장애물과 다르게 다룬다.</b> 장애물은 안쪽이 못 가는 곳이고 경계는 바깥쪽이 못 가는 곳이다.
    /// 직사각형은 볼록하므로, 두 점이 경계 안에 있으면 그 사이의 직선은 저절로 경계 안에 머문다 —
    /// 그래서 경계는 장애물처럼 시야를 가리는 판정에 넣지 않는다. 경계의 꼭짓점은 다른 장애물이
    /// 경계 구석에 바짝 붙어 있을 때 돌아갈 자리로만 쓰인다.
    /// </para>
    /// <para>
    /// <b>꼭짓점을 살짝 바깥으로 밀어낸다.</b> 장애물 꼭짓점은 그 장애물 자신의 경계 위에 있다.
    /// 그대로 시야 판정을 하면 그 자리에서 바깥으로 나가는 선까지 「닿았다」로 걸려 막힌 것으로 잘못
    /// 판정된다(<see cref="PlanarGeometry.IntersectsSegment"/>는 닿기만 해도 막힌 것으로 본다).
    /// <see cref="CornerNudge"/>만큼 장애물 중심에서 먼 쪽으로 밀어내면, 정말로 장애물을 가로지르는
    /// 선(예: 같은 장애물의 대각선)은 여전히 막히고, 그 장애물에서 바깥으로 나가는 선은 더는 안 막힌다.
    /// 이 값은 유닛 반지름과 무관한 계산상의 여유일 뿐이다.
    /// </para>
    /// </remarks>
    public sealed class ObstacleVisibilityGraph
    {
        /// <summary>
        /// 장애물 꼭짓점을 그 장애물 중심에서 먼 쪽으로 밀어내는 거리(미터)이다.
        /// </summary>
        /// <remarks>
        /// 시야 판정의 닿음 처리를 피하기 위한 값일 뿐이며, 유닛 반지름이나 장애물의 실제 크기와는
        /// 무관하다. 전장 규모(미터 단위)에 비해 무시할 만큼 작되, 부동소수 오차보다는 뚜렷이 크다.
        /// </remarks>
        private const float CornerNudge = 0.01f;

        private readonly IReadOnlyList<PlanarRectangle> _obstacles;
        private readonly PlanarPosition[] _staticNodes;
        private readonly Dictionary<PlanarPosition, List<PlanarPosition>> _staticEdges;

        /// <summary>장애물과 경계로 시야 그래프를 짓는다.</summary>
        /// <remarks>
        /// 정적인 부분(장애물·경계 꼭짓점 사이의 시야)은 여기서 한 번만 계산해 둔다. 시작과 목표는
        /// 매 <see cref="TryFindPath"/> 호출마다 바뀌므로 그때 계산한다.
        /// </remarks>
        /// <param name="obstacles">막는 직사각형 장애물 목록이며 비어 있어도 된다.</param>
        /// <param name="bounds">전장 경계이다. 이 안쪽만 갈 수 있는 곳으로 본다.</param>
        public ObstacleVisibilityGraph(IReadOnlyList<PlanarRectangle> obstacles, PlanarRectangle bounds)
        {
            _obstacles = obstacles ?? Array.Empty<PlanarRectangle>();

            var nodes = new List<PlanarPosition>();
            foreach (var obstacle in _obstacles)
            {
                foreach (var corner in PlanarGeometry.GetCorners(obstacle))
                {
                    var nudged = NudgeOutward(obstacle, corner);
                    if (PlanarGeometry.Contains(bounds, nudged))
                    {
                        nodes.Add(nudged);
                    }
                }
            }

            foreach (var corner in PlanarGeometry.GetCorners(bounds))
            {
                nodes.Add(corner);
            }

            _staticNodes = nodes.ToArray();
            _staticEdges = BuildStaticEdges(_staticNodes);
        }

        /// <summary>
        /// 시작에서 목표까지 장애물을 피하는 경로를 찾는다.
        /// </summary>
        /// <remarks>
        /// 시작이나 목표가 경계 밖이거나 장애물 안이면 찾지 않는다. 그 자리에 이미 있을 수 없는
        /// 위치를 목적지로 받아들이면 나온 경로도 뜻이 없기 때문이다.
        /// </remarks>
        /// <param name="bounds">전장 경계이다. 그래프를 지을 때 쓴 것과 같아야 한다.</param>
        /// <param name="start">시작 좌표이다.</param>
        /// <param name="goal">목표 좌표이다.</param>
        /// <param name="path">
        /// 찾았으면 시작부터 목표까지 순서대로 늘어선 꺾임점 목록(시작과 목표를 포함)이다. 못 찾았으면
        /// 빈 목록이다.
        /// </param>
        /// <param name="excludeFromGoal">
        /// 목표가 이 장애물 안에 있어도 막힌 것으로 치지 않는다. 비워 두면(기본값) 목표가 어느
        /// 장애물이든 그 안이면 갈 수 없는 자리로 본다.
        /// </param>
        /// <returns>경로를 찾았으면 참이다.</returns>
        public bool TryFindPath(
            PlanarRectangle bounds,
            PlanarPosition start,
            PlanarPosition goal,
            out IReadOnlyList<PlanarPosition> path,
            PlanarRectangle? excludeFromGoal = null)
        {
            if (!PlanarGeometry.Contains(bounds, start) || !PlanarGeometry.Contains(bounds, goal)
                || IsInsideAnyObstacle(start)
                || IsBlockedByAnyObstacleOtherThan(goal, excludeFromGoal))
            {
                path = Array.Empty<PlanarPosition>();
                return false;
            }

            var query = new Query(this, start, goal, excludeFromGoal);
            return AStarPathfinder.TryFindPath(query, start, goal, out path, PlanarPositionComparer.Instance);
        }

        /// <summary>주어진 좌표가 장애물 중 하나의 안이면 참이다.</summary>
        private bool IsInsideAnyObstacle(PlanarPosition position)
        {
            foreach (var obstacle in _obstacles)
            {
                if (PlanarGeometry.Contains(obstacle, position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 주어진 좌표가, 뺄 장애물 하나를 빼고 나머지 장애물 중 하나의 안이면 참이다.
        /// </summary>
        /// <param name="position">확인할 좌표이다.</param>
        /// <param name="exclude">판정에서 뺄 장애물이며, 없으면 전부를 본다.</param>
        private bool IsBlockedByAnyObstacleOtherThan(PlanarPosition position, PlanarRectangle? exclude)
        {
            foreach (var obstacle in _obstacles)
            {
                if (exclude.HasValue && obstacle.Equals(exclude.Value))
                {
                    continue;
                }

                if (PlanarGeometry.Contains(obstacle, position))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 주어진 선분이 장애물 중 어느 하나에라도 막히면 참이다.
        /// </summary>
        /// <param name="from">선분의 시작이다.</param>
        /// <param name="to">선분의 끝이다.</param>
        /// <param name="exclude">막힘 판정에서 뺄 장애물이며, 없으면 전부를 본다.</param>
        private bool IsBlocked(PlanarPosition from, PlanarPosition to, PlanarRectangle? exclude = null)
        {
            foreach (var obstacle in _obstacles)
            {
                if (exclude.HasValue && obstacle.Equals(exclude.Value))
                {
                    continue;
                }

                if (PlanarGeometry.IntersectsSegment(obstacle, from, to))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>정적 노드 사이의 시야 간선을 전부 미리 계산한다.</summary>
        /// <param name="nodes">정적 노드(장애물·경계 꼭짓점) 목록이다.</param>
        /// <returns>노드마다 보이는 다른 정적 노드 목록이다.</returns>
        private Dictionary<PlanarPosition, List<PlanarPosition>> BuildStaticEdges(IReadOnlyList<PlanarPosition> nodes)
        {
            var edges = new Dictionary<PlanarPosition, List<PlanarPosition>>(nodes.Count);
            foreach (var node in nodes)
            {
                edges[node] = new List<PlanarPosition>();
            }

            for (var i = 0; i < nodes.Count; i++)
            {
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    if (IsBlocked(nodes[i], nodes[j]))
                    {
                        continue;
                    }

                    edges[nodes[i]].Add(nodes[j]);
                    edges[nodes[j]].Add(nodes[i]);
                }
            }

            return edges;
        }

        /// <summary>장애물의 꼭짓점을 그 중심에서 먼 쪽으로 <see cref="CornerNudge"/>만큼 밀어낸다.</summary>
        /// <param name="obstacle">꼭짓점이 속한 장애물이다.</param>
        /// <param name="corner">밀어낼 꼭짓점의 세계 좌표이다.</param>
        /// <returns>밀어낸 세계 좌표이다.</returns>
        private static PlanarPosition NudgeOutward(PlanarRectangle obstacle, PlanarPosition corner)
        {
            var local = obstacle.ToLocal(corner);
            var pushedLocal = new PlanarPosition(
                local.X + Mathf.Sign(local.X) * CornerNudge,
                local.Z + Mathf.Sign(local.Z) * CornerNudge);
            return obstacle.ToWorld(pushedLocal);
        }

        /// <summary>
        /// 한 번의 경로 탐색 동안만 쓰는 그래프 시야이다. 정적 시야는 바깥 그래프의 것을 그대로 쓰고,
        /// 시작·목표가 보는 것만 이 시야가 새로 계산한다.
        /// </summary>
        private sealed class Query : IPathGraph<PlanarPosition>
        {
            private readonly ObstacleVisibilityGraph _graph;
            private readonly PlanarPosition _start;
            private readonly PlanarPosition _goal;
            private readonly List<PlanarPosition> _startVisible;
            private readonly List<PlanarPosition> _goalVisible;
            private readonly bool _startSeesGoal;

            public Query(
                ObstacleVisibilityGraph graph, PlanarPosition start, PlanarPosition goal,
                PlanarRectangle? excludeFromGoal)
            {
                _graph = graph;
                _start = start;
                _goal = goal;
                _startVisible = VisibleStaticNodesFrom(start, null);
                _goalVisible = VisibleStaticNodesFrom(goal, excludeFromGoal);
                _startSeesGoal = !_graph.IsBlocked(start, goal, excludeFromGoal);
            }

            public IEnumerable<PlanarPosition> GetNeighbors(PlanarPosition node)
            {
                if (node.Equals(_start))
                {
                    return _startSeesGoal ? Append(_startVisible, _goal) : _startVisible;
                }

                if (node.Equals(_goal))
                {
                    return _startSeesGoal ? Append(_goalVisible, _start) : _goalVisible;
                }

                var neighbors = _graph._staticEdges[node];
                if (!_startVisible.Contains(node) && !_goalVisible.Contains(node))
                {
                    return neighbors;
                }

                var extended = new List<PlanarPosition>(neighbors);
                if (_startVisible.Contains(node))
                {
                    extended.Add(_start);
                }

                if (_goalVisible.Contains(node))
                {
                    extended.Add(_goal);
                }

                return extended;
            }

            public float GetCost(PlanarPosition from, PlanarPosition to) => PlanarPosition.Distance(from, to);

            public float GetHeuristic(PlanarPosition node, PlanarPosition goal) => PlanarPosition.Distance(node, goal);

            private List<PlanarPosition> VisibleStaticNodesFrom(PlanarPosition point, PlanarRectangle? exclude)
            {
                var visible = new List<PlanarPosition>();
                foreach (var node in _graph._staticNodes)
                {
                    if (!_graph.IsBlocked(point, node, exclude))
                    {
                        visible.Add(node);
                    }
                }

                return visible;
            }

            private static IEnumerable<PlanarPosition> Append(List<PlanarPosition> list, PlanarPosition extra)
            {
                foreach (var item in list)
                {
                    yield return item;
                }

                yield return extra;
            }
        }
    }
}
