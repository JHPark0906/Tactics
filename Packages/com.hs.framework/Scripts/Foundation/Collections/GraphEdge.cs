namespace HS.Framework.Foundation.Collections
{
    /// <summary>그래프의 간선이며, 자기 자신이 정체성이다.</summary>
    /// <remarks>
    /// 다중 간선을 허용하므로 노드 쌍으로는 간선을 가리킬 수 없다. 같은 두 노드를 잇는
    /// 간선이 여럿일 수 있고, 그 각각은 서로 다른 객체다.
    /// 지우고 넣는 일이 잦아도 남이 들고 있던 간선이 다른 간선을 가리키게 되지 않는다.
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    /// <typeparam name="TEdge">간선이 싣는 값이며, 가중치가 필요 없으면 <see cref="Unweighted"/>를 쓴다.</typeparam>
    public sealed class GraphEdge<TNode, TEdge>
    {
        /// <summary>간선이 나가는 노드이다.</summary>
        /// <remarks>방향 없는 그래프에서는 두 끝 가운데 하나일 뿐이며 방향을 뜻하지 않는다.</remarks>
        public GraphNode<TNode> Source { get; }

        /// <summary>간선이 들어가는 노드이다.</summary>
        public GraphNode<TNode> Target { get; }

        /// <summary>간선이 싣는 값이다.</summary>
        /// <remarks>
        /// 한 곳에만 있다. 방향 없는 간선도 객체 하나라, 양쪽에서 본 값이 어긋날 자리가 없다.
        /// </remarks>
        public TEdge Value { get; set; }

        /// <summary>두 노드를 잇는 간선을 만든다.</summary>
        /// <param name="source">나가는 노드이다.</param>
        /// <param name="target">들어가는 노드이다.</param>
        /// <param name="value">간선이 실을 값이다.</param>
        public GraphEdge(GraphNode<TNode> source, GraphNode<TNode> target, TEdge value = default)
        {
            Source = source;
            Target = target;
            Value = value;
        }

        /// <summary>주어진 노드의 반대편 끝을 돌려준다.</summary>
        /// <param name="node">한쪽 끝 노드이다.</param>
        /// <returns>반대편 노드이며, 자기 순환이면 같은 노드이다.</returns>
        public GraphNode<TNode> Opposite(GraphNode<TNode> node)
        {
            if (node == Source)
            {
                return Target;
            }

            if (node == Target)
            {
                return Source;
            }

            throw new System.ArgumentException("node is not an end of this edge", nameof(node));
        }
    }

    /// <summary>값을 싣지 않는 자리에 쓰는 빈 형식이다.</summary>
    /// <remarks>가중치가 없는 그래프는 간선 값으로 이것을 쓴다. 타입을 따로 만들지 않기 위함이다.</remarks>
    public readonly struct Unweighted
    {
    }
}
