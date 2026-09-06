using System.Collections.Generic;

namespace HS.Framework.AI.Pathfinding
{
    /// <summary>
    /// 경로 탐색이 그래프에게 묻는 것 전부를 담은 계약이다. 노드가 무엇을 싣는지, 좌표가 무엇인지는 모른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>탐색은 좌표를 몰라야 한다.</b> 이웃·비용·휴리스틱을 이 계약 뒤로 감추면 탐색 알고리즘은 노드가
    /// 평면 위의 점인지, 격자 칸인지, 문자열 식별자인지 알 필요가 없다. 게임이 자기 좌표계와 도형으로
    /// 이 계약을 구현해 건네주는 모양이 되어 의존 방향이 프레임워크 → 게임이 아니라 게임 → 프레임워크로 선다.
    /// </para>
    /// <para>
    /// <b>그래프를 미리 다 지어 두지 않아도 된다.</b> <see cref="GetNeighbors"/>가 그때그때 계산해 돌려줘도 되고,
    /// 미리 구운 인접 표를 찾아봐도 된다 — 이 계약은 어느 쪽인지 묻지 않는다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">노드를 식별하는 형식이다. 값이 같으면 같은 노드로 본다.</typeparam>
    public interface IPathGraph<TNode>
    {
        /// <summary>그 노드에서 한 걸음에 갈 수 있는 이웃 전부이다.</summary>
        /// <param name="node">기준 노드이다.</param>
        /// <returns>이웃 노드들이다. 이웃이 없으면 빈 열거이다.</returns>
        IEnumerable<TNode> GetNeighbors(TNode node);

        /// <summary>맞닿은 두 노드 사이를 잇는 데 드는 비용이다.</summary>
        /// <remarks>
        /// <paramref name="to"/>가 <paramref name="from"/>의 이웃이 아닐 때의 결과는 정의하지 않는다.
        /// 탐색은 <see cref="GetNeighbors"/>가 돌려준 쌍에만 이 메서드를 부른다.
        /// </remarks>
        /// <param name="from">출발 노드이다.</param>
        /// <param name="to">도착 노드이다.</param>
        /// <returns>0 이상의 비용이다. 음수를 돌려주면 탐색이 최단 경로를 보장하지 못한다.</returns>
        float GetCost(TNode from, TNode to);

        /// <summary>그 노드에서 목표까지 남은 거리의 어림값이다.</summary>
        /// <remarks>
        /// 실제 남은 비용을 넘어서서 어림하면(허용 가능하지 않으면) 최단 경로를 보장하지 못한다.
        /// 항상 0을 돌려줘도 정확하지만 느린 A*(다익스트라와 같아진다)가 된다.
        /// </remarks>
        /// <param name="node">지금 노드이다.</param>
        /// <param name="goal">목표 노드이다.</param>
        /// <returns>0 이상의 어림값이다.</returns>
        float GetHeuristic(TNode node, TNode goal);
    }
}
