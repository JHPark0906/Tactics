using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>방향 있는 그래프이다.</summary>
    /// <remarks>
    /// 간선은 <see cref="GraphEdge{TNode,TEdge}.Source"/>에서 <see cref="GraphEdge{TNode,TEdge}.Target"/>
    /// 으로만 간다. 위상 정렬처럼 방향을 요구하는 알고리즘은 이 형식을 받으면 된다.
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    /// <typeparam name="TEdge">간선이 싣는 값이다.</typeparam>
    public class DirectedGraph<TNode, TEdge> : Graph<TNode, TEdge>
    {
        /// <inheritdoc />
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> OutgoingEdges(GraphNode<TNode> node)
        {
            return _edges.FindAll(edge => edge.Source == node);
        }

        /// <inheritdoc />
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> IncomingEdges(GraphNode<TNode> node)
        {
            return _edges.FindAll(edge => edge.Target == node);
        }

        /// <inheritdoc />
        public override IReadOnlyList<GraphEdge<TNode, TEdge>> EdgesBetween(
            GraphNode<TNode> from, GraphNode<TNode> to)
        {
            return _edges.FindAll(edge => edge.Source == from && edge.Target == to);
        }
    }

    /// <summary>가중치를 싣지 않는 방향 있는 그래프이다.</summary>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public sealed class DirectedGraph<TNode> : DirectedGraph<TNode, Unweighted>
    {
    }
}
