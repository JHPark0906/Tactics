using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>고치지 않고 읽기만 하는 그래프 계약이다.</summary>
    /// <remarks>
    /// <para>
    /// 노드와 간선을 무엇으로 가리키는지를 인자로 연다. 그래프는 노드 객체로, 트리는 자기 노드로
    /// 가리키며, 둘 다 이 계약을 구현한다. 그래야 그리거나 훑는 코드를 한 벌만 두면 된다.
    /// </para>
    /// <para>
    /// <b>간선의 끝은 그래프가 푼다.</b> 가리키는 것이 무엇인지 밖에서 모르므로 간선에게 직접
    /// 물을 수 없다. 트리처럼 간선 객체가 따로 없는 구현도 이 두 메서드로 답할 수 있다.
    /// </para>
    /// <para>
    /// 값을 꺼내는 자리는 두지 않았다. 값까지 열려면 인자가 넷이 되고 읽기가 나빠지는데,
    /// 그리는 쪽은 구체 형식을 알고 있으므로 지금은 필요가 없다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNodeRef">노드를 가리키는 형식이다.</typeparam>
    /// <typeparam name="TEdgeRef">간선을 가리키는 형식이다.</typeparam>
    public interface IReadOnlyGraph<TNodeRef, TEdgeRef>
    {
        /// <summary>노드 수이다.</summary>
        int NodeCount { get; }

        /// <summary>간선 수이며, 같은 두 노드를 잇는 간선이 여럿이면 각각을 센다.</summary>
        int EdgeCount { get; }

        /// <summary>모든 노드이며, 넣은 순서를 유지한다.</summary>
        IReadOnlyList<TNodeRef> Nodes { get; }

        /// <summary>모든 간선이며, 넣은 순서를 유지한다.</summary>
        IReadOnlyList<TEdgeRef> Edges { get; }

        /// <summary>그 노드가 이 그래프에 있는지 본다.</summary>
        /// <param name="node">확인할 노드이다.</param>
        /// <returns>있으면 참이다.</returns>
        bool Contains(TNodeRef node);

        /// <summary>그 간선이 이 그래프에 있는지 본다.</summary>
        /// <param name="edge">확인할 간선이다.</param>
        /// <returns>있으면 참이다.</returns>
        bool Contains(TEdgeRef edge);

        /// <summary>그 간선이 나가는 노드이다.</summary>
        /// <param name="edge">기준 간선이다.</param>
        /// <returns>나가는 쪽 노드이며, 방향이 없는 구현에서는 두 끝 가운데 하나이다.</returns>
        TNodeRef SourceOf(TEdgeRef edge);

        /// <summary>그 간선이 들어가는 노드이다.</summary>
        /// <param name="edge">기준 간선이다.</param>
        /// <returns>들어가는 쪽 노드이다.</returns>
        TNodeRef TargetOf(TEdgeRef edge);

        /// <summary>그 노드에서 나가는 간선이다.</summary>
        /// <remarks>
        /// 이웃이 아니라 간선을 돌려준다. 다중 간선을 허용하는 구현에서는 같은 이웃이 여러 번
        /// 나올 수 있고, 이웃만 받으면 그것이 몇 번째 간선인지 알 수 없다.
        /// </remarks>
        /// <param name="node">기준 노드이다.</param>
        /// <returns>나가는 간선 목록이며 넣은 순서를 유지한다.</returns>
        IReadOnlyList<TEdgeRef> OutgoingEdges(TNodeRef node);

        /// <summary>그 노드로 들어오는 간선이다.</summary>
        /// <param name="node">기준 노드이다.</param>
        /// <returns>들어오는 간선 목록이며 넣은 순서를 유지한다.</returns>
        IReadOnlyList<TEdgeRef> IncomingEdges(TNodeRef node);

        /// <summary>두 노드를 잇는 간선을 전부 돌려준다.</summary>
        /// <param name="from">한쪽 노드이다.</param>
        /// <param name="to">다른 쪽 노드이다.</param>
        /// <returns>두 노드를 잇는 간선 목록이며, 없으면 빈 목록이다.</returns>
        IReadOnlyList<TEdgeRef> EdgesBetween(TNodeRef from, TNodeRef to);
    }
}
