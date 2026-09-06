using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Collections;

namespace HS.Framework.AI.Pathfinding
{
    /// <summary>
    /// <see cref="IPathGraph{TNode}"/> 위에서 A* 로 최단 경로를 찾는다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>좌표를 모른다.</b> 노드가 무엇을 싣는지는 <see cref="IPathGraph{TNode}"/> 구현만 알고, 이 탐색은
    /// 이웃·비용·휴리스틱을 그 계약으로만 묻는다. 그래서 어느 게임이나 자기 좌표계와 도형으로 계약을
    /// 구현해 건네줄 수 있다.
    /// </para>
    /// <para>
    /// <b>실패는 예외가 아니라 반환값이다.</b> 경로가 없는 것은 흔한 결과(막힌 전장, 동떨어진 목표)이지
    /// 프로그램 오류가 아니다. <see cref="TryFindPath{TNode}"/>는 찾았는지를 반환값으로 알리고, 못 찾았으면
    /// <paramref name="path"/>는 빈 목록이다.
    /// </para>
    /// <para>
    /// <b>같은 그래프·시작·목표면 언제나 같은 경로가 나온다.</b> 열린 목록에서 우선순위가 같은 노드가
    /// 여럿이면 <paramref name="tieBreaker"/>(또는 노드 자체의 <see cref="IComparable{T}"/>)로 가르며,
    /// 삽입 순서나 컬렉션의 열거 순서에는 기대지 않는다. 노드 형식이 비교 가능하지 않은데 동점이 실제로
    /// 생기면 그 자리에서 예외가 난다 — 조용히 실행마다 다른 경로를 내주는 것보다 낫다.
    /// </para>
    /// </remarks>
    public static class AStarPathfinder
    {
        /// <summary>시작에서 목표까지의 최단 경로를 찾는다.</summary>
        /// <param name="graph">이웃·비용·휴리스틱을 답하는 그래프 계약이다.</param>
        /// <param name="start">시작 노드이다.</param>
        /// <param name="goal">목표 노드이다.</param>
        /// <param name="path">
        /// 찾았으면 시작부터 목표까지 순서대로 늘어선 노드 목록(시작과 목표를 포함)이며, 시작과 목표가 같으면
        /// 노드 하나짜리 목록이다. 못 찾았으면 빈 목록이다.
        /// </param>
        /// <param name="tieBreaker">
        /// 우선순위가 같은 노드가 여럿일 때 순서를 가를 비교자이다. 생략하면 노드 형식의 기본 비교(주로
        /// <see cref="IComparable{T}"/>)를 쓴다.
        /// </param>
        /// <returns>경로를 찾았으면 참이다.</returns>
        public static bool TryFindPath<TNode>(
            IPathGraph<TNode> graph,
            TNode start,
            TNode goal,
            out IReadOnlyList<TNode> path,
            IComparer<TNode> tieBreaker = null)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var equality = EqualityComparer<TNode>.Default;
            if (equality.Equals(start, goal))
            {
                path = new List<TNode> { start };
                return true;
            }

            var comparer = tieBreaker ?? Comparer<TNode>.Default;
            var open = new BinaryMinHeap<TNode>(comparer);
            var cameFrom = new Dictionary<TNode, TNode>(equality);
            var costSoFar = new Dictionary<TNode, float>(equality) { [start] = 0f };
            var closed = new HashSet<TNode>(equality);

            open.Push(start, graph.GetHeuristic(start, goal));

            while (open.Count > 0)
            {
                var current = open.Pop();
                if (!closed.Add(current))
                {
                    // 더 싼 경로로 이미 닫힌 노드의 낡은 항목이다. 느슨한 삭제이므로 여기서 걸러낸다.
                    continue;
                }

                if (equality.Equals(current, goal))
                {
                    path = ReconstructPath(cameFrom, current);
                    return true;
                }

                foreach (var neighbor in graph.GetNeighbors(current))
                {
                    var tentativeCost = costSoFar[current] + graph.GetCost(current, neighbor);
                    if (costSoFar.TryGetValue(neighbor, out var knownCost) && tentativeCost >= knownCost)
                    {
                        continue;
                    }

                    cameFrom[neighbor] = current;
                    costSoFar[neighbor] = tentativeCost;
                    // 허용 가능하지만 일관적이지 않은 휴리스틱이면 닫힌 뒤 더 싼 길이 발견될 수 있다.
                    closed.Remove(neighbor);
                    open.Push(neighbor, tentativeCost + graph.GetHeuristic(neighbor, goal));
                }
            }

            path = Array.Empty<TNode>();
            return false;
        }

        /// <summary>목표에서 시작까지 역추적한 뒤 시작→목표 순서로 뒤집는다.</summary>
        private static IReadOnlyList<TNode> ReconstructPath<TNode>(
            IReadOnlyDictionary<TNode, TNode> cameFrom, TNode goal)
        {
            var reversed = new List<TNode> { goal };
            var current = goal;
            while (cameFrom.TryGetValue(current, out var previous))
            {
                reversed.Add(previous);
                current = previous;
            }

            reversed.Reverse();
            return reversed;
        }
    }
}
