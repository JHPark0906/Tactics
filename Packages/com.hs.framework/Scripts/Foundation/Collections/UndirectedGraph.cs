using System;
using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>방향 없는 그래프이다.</summary>
    /// <remarks>
    /// 간선 하나가 두 노드에 함께 붙는다. 간선 객체가 하나뿐이므로 실은 값도 한 벌이며,
    /// 양쪽에서 본 값이 어긋날 자리가 없다.
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    /// <typeparam name="TEdge">간선이 싣는 값이다.</typeparam>
    public class UndirectedGraph<TNode, TEdge> : Graph<TNode, TEdge>
    {
        /// <inheritdoc />
        /// <remarks>방향이 없으므로 붙은 간선 전부를 돌려준다.</remarks>
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> OutgoingEdges(GraphNode<TNode> node)
        {
            return _edges.FindAll(edge => edge.Source == node || edge.Target == node);
        }

        /// <inheritdoc />
        /// <remarks>방향이 없으므로 <see cref="OutgoingEdges"/>와 같은 것을 돌려준다.</remarks>
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> IncomingEdges(GraphNode<TNode> node)
            => OutgoingEdges(node);

        /// <inheritdoc />
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> EdgesBetween(
            GraphNode<TNode> from, GraphNode<TNode> to)
        {
            return _edges.FindAll(edge => edge.Source == from && edge.Target == to || edge.Source == to && edge.Target == from);
        }


    }

    /// <summary>가중치를 싣지 않는 방향 없는 그래프이다.</summary>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public sealed class UndirectedGraph<TNode> : UndirectedGraph<TNode, Unweighted>
    {
    }
}
