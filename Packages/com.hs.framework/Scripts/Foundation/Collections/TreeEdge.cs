using System;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>트리에서 부모와 자식을 잇는 자리를 가리킨다.</summary>
    /// <remarks>
    /// <para>
    /// 트리는 부모가 하나뿐이라 자식 하나가 곧 간선 하나다. 그래서 간선 객체를 따로 두지 않고
    /// 자식을 담아 가리키기만 한다.
    /// </para>
    /// <para>
    /// <b>자식 노드를 그대로 쓰지 않고 감싼 까닭이 있다.</b> 노드와 간선을 같은 형식으로 가리키면
    /// <c>Contains</c>가 하나로 합쳐져 두 물음을 구별할 수 없게 된다. 루트는 노드이지만 간선이
    /// 아니므로 그 둘은 서로 다른 답이어야 한다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public readonly struct TreeEdge<TNode> : IEquatable<TreeEdge<TNode>>
    {
        /// <summary>이 간선이 내려가 닿는 자식이다.</summary>
        public TreeNode<TNode> Child { get; }

        /// <summary>자식이 가리키는 간선을 만든다.</summary>
        /// <param name="child">간선이 닿는 자식이다.</param>
        public TreeEdge(TreeNode<TNode> child)
        {
            Child = child;
        }

        /// <summary>같은 자식을 가리키는지 본다.</summary>
        /// <param name="other">견줄 간선이다.</param>
        /// <returns>같은 자식이면 참이다.</returns>
        public bool Equals(TreeEdge<TNode> other) => ReferenceEquals(Child, other.Child);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TreeEdge<TNode> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            Child == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Child);
    }
}
