namespace HS.Framework.Foundation.Collections
{
    /// <summary>그래프의 노드이며, 자기 자신이 정체성이다.</summary>
    /// <remarks>
    /// 참조가 곧 정체성이라 인덱스를 쓰지 않는다. 노드나 간선을 지워도 남이 들고 있던 참조가
    /// 다른 것을 가리키게 되는 일이 없다.
    /// 붙은 간선은 노드가 아니라 그래프가 안다. 노드에 목록을 들리면 그 목록과 그래프의 목록이
    /// 두 벌이 되어 어긋날 수 있고, 어긋나도 아무 신호가 나지 않는다.
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public sealed class GraphNode<TNode>
    {
        /// <summary>이 노드가 싣는 값이다.</summary>
        public TNode Value { get; set; }

        /// <summary>주어진 값을 싣는 노드를 만든다.</summary>
        /// <param name="value">노드가 실을 값이다.</param>
        public GraphNode(TNode value = default)
        {
            Value = value;
        }
    }
}
