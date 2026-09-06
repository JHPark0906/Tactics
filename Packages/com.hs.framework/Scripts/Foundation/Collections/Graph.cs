using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>방향 있는 그래프와 없는 그래프가 함께 쓰는 저장이다.</summary>
    /// <remarks>
    /// <para>
    /// 방향성은 간선이 아니라 그래프가 정한다. 간선마다 방향 여부를 두면 알고리즘이 간선마다
    /// 갈라져야 하고, 방향 있는 간선과 없는 간선이 섞인 그래프를 만들 수 있게 된다.
    /// </para>
    /// <para>
    /// 가중치는 타입을 나누지 않고 <typeparamref name="TEdge"/>로 싣는다. 가중치가 없는 그래프는
    /// <see cref="Unweighted"/>를 쓴다. 방향성은 무엇이 유효한지를 바꾸지만 가중치는 무엇을 쓸 수
    /// 있는지만 바꾸므로, 앞은 타입이고 뒤는 인자다.
    /// </para>
    /// <para>
    /// 노드를 담아 두는 일은 <see cref="NodeStore{TNode}"/>에 맡긴다. 트리도 같은 것을 쓰므로
    /// 넣은 순서와 참조 정체성이라는 약속이 한 곳에만 있다. 따로 두면 한쪽만 바뀌어도 아무 신호가 없다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    /// <typeparam name="TEdge">간선이 싣는 값이다.</typeparam>
    public abstract class Graph<TNode, TEdge> : IGraph<GraphNode<TNode>, GraphEdge<TNode, TEdge>>,
        IReadOnlyCollection<GraphNode<TNode>>
    {
        private readonly NodeStore<GraphNode<TNode>> _nodes = new();
        protected readonly List<GraphEdge<TNode, TEdge>> _edges = new();

        /// <inheritdoc />
        public int NodeCount => _nodes.Count;

        /// <inheritdoc />
        public int EdgeCount => _edges.Count;

        /// <inheritdoc />
        public int Count => _nodes.Count;

        /// <inheritdoc />
        public IReadOnlyList<GraphNode<TNode>> Nodes => _nodes.Nodes;

        /// <inheritdoc />
        public IReadOnlyList<GraphEdge<TNode, TEdge>> Edges => _edges;

        /// <inheritdoc />
        public bool Contains(GraphNode<TNode> node) => _nodes.Contains(node);

        /// <inheritdoc />
        public bool Contains(GraphEdge<TNode, TEdge> edge) => _edges.Contains(edge);

        /// <inheritdoc />
        public GraphNode<TNode> SourceOf(GraphEdge<TNode, TEdge> edge) => edge.Source;

        /// <inheritdoc />
        public GraphNode<TNode> TargetOf(GraphEdge<TNode, TEdge> edge) => edge.Target;

        /// <summary>값을 싣는 노드를 새로 만들어 넣는다.</summary>
        /// <remarks>계약에 두지 않은 것은 무엇을 실을지가 구체 형식만 아는 값이기 때문이다.</remarks>
        /// <param name="value">노드가 실을 값이다.</param>
        /// <returns>만들어진 노드이다.</returns>
        public GraphNode<TNode> AddNode(TNode value = default)
        {
            var node = new GraphNode<TNode>(value);
            _nodes.Add(node);
            return node;
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>붙어 있던 간선을 함께 지운다.</b> 노드가 사라졌는데 그 노드를 끝으로 삼는 간선이 남으면
        /// 어디에도 닿지 않는 간선이 되어, 순회에서는 안 보이는데 목록에는 남는다.
        /// </remarks>
        public bool RemoveNode(GraphNode<TNode> node)
        {
            if (!Contains(node))
            {
                return false;
            }

            foreach (var edge in _edges.ToList().Where(edge => edge.Source == node || edge.Target == node))
            {
                _edges.Remove(edge);
            }
            
            _nodes.Remove(node);
            return true;
        }

        /// <summary>두 노드를 잇는 간선을 새로 만들어 넣는다.</summary>
        /// <remarks>만들어진 간선을 돌려준다. 다중 간선이라 노드 쌍으로는 다시 찾을 수 없다.</remarks>
        /// <param name="from">나가는 노드이다.</param>
        /// <param name="to">들어가는 노드이다.</param>
        /// <param name="value">간선이 실을 값이다.</param>
        /// <returns>만들어진 간선이다.</returns>
        public GraphEdge<TNode, TEdge> AddEdge(
            GraphNode<TNode> from, GraphNode<TNode> to, TEdge value = default)
        {
            if (!Contains(from) || !Contains(to))
            {
                throw new ArgumentException("from or to is not in this graph");
            }
            
            var edge = new GraphEdge<TNode, TEdge>(from, to, value);
            _edges.Add(edge);
            return edge;
        }

        /// <inheritdoc />
        public bool RemoveEdge(GraphEdge<TNode, TEdge> edge)
        {
            if (!_edges.Contains(edge))
            {
                return false;
            }

            _edges.Remove(edge);
            return true;

        }

        /// <inheritdoc />
        public abstract IReadOnlyList<GraphEdge<TNode, TEdge>> OutgoingEdges(GraphNode<TNode> node);

        /// <inheritdoc />
        public abstract IReadOnlyList<GraphEdge<TNode, TEdge>> IncomingEdges(GraphNode<TNode> node);

        /// <inheritdoc />
        public abstract IReadOnlyList<GraphEdge<TNode, TEdge>> EdgesBetween(
            GraphNode<TNode> from, GraphNode<TNode> to);


        /// <inheritdoc />
        public IEnumerator<GraphNode<TNode>> GetEnumerator() => _nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
