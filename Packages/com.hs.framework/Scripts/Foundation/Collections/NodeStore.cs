using System.Collections;
using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>노드를 담아 두는 자리이며, 그래프와 트리가 함께 쓴다.</summary>
    /// <remarks>
    /// <para>
    /// 노드가 무엇인지는 모른다. 그래프의 노드든 트리의 노드든 담고, 아래 두 약속만 지킨다.
    /// 담긴 것들 사이의 관계는 담는 쪽이 자기 방식으로 따로 든다.
    /// </para>
    /// <para>
    /// <b>넣은 순서를 유지한다.</b> 같은 조작을 같은 순서로 하면 열거 순서가 언제나 같다.
    /// 해시 기반 컨테이너로 바꾸면 순서가 보장되지 않으며, 그러면 순회에 기대는 결과가
    /// 실행마다 달라진다.
    /// </para>
    /// <para>
    /// <b>참조가 정체성이다.</b> 값이 같은 노드 둘은 서로 다른 노드이며, 담긴 것을 빼도 남이
    /// 들고 있던 참조가 다른 노드를 가리키게 되지 않는다.
    /// </para>
    /// <para>
    /// 상속이 아니라 담기로 나눈 것은, 물려주는 자리를 두면 시간이 지나며 관계까지 위로
    /// 올라오기 때문이다. 이 이름은 무엇을 담는지만 말하므로 그 이상은 들어올 자리가 없다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">담을 노드의 형식이며 참조 형식이어야 한다.</typeparam>
    public sealed class NodeStore<TNode> : IReadOnlyList<TNode>
        where TNode : class
    {
        private readonly List<TNode> _nodes = new();

        /// <summary>담긴 노드 수이다.</summary>
        public int Count => _nodes.Count;

        /// <summary>넣은 순서에서 그 자리의 노드이다.</summary>
        /// <param name="index">찾을 자리이다.</param>
        public TNode this[int index] => _nodes[index];

        /// <summary>담긴 노드 전부이며 넣은 순서를 유지한다.</summary>
        public IReadOnlyList<TNode> Nodes => _nodes;

        /// <summary>그 노드가 여기 담겨 있는지 본다.</summary>
        /// <param name="node">확인할 노드이다.</param>
        /// <returns>담겨 있으면 참이다.</returns>
        public bool Contains(TNode node) => node != null && _nodes.Contains(node);

        /// <summary>노드를 담는다.</summary>
        /// <param name="node">담을 노드이다.</param>
        public void Add(TNode node) => _nodes.Add(node);

        /// <summary>노드를 뺀다.</summary>
        /// <remarks>
        /// 그 노드에 얽힌 관계를 정리하는 것은 담는 쪽의 몫이다. 여기서는 담긴 것에서 빼기만 한다.
        /// 그래프는 붙은 간선을, 트리는 자식을 어떻게 할지 각자 정하며 그 뜻이 서로 다르다.
        /// </remarks>
        /// <param name="node">뺄 노드이다.</param>
        /// <returns>실제로 뺐으면 참이다.</returns>
        public bool Remove(TNode node) => node != null && _nodes.Remove(node);

        /// <inheritdoc />
        public IEnumerator<TNode> GetEnumerator() => _nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
