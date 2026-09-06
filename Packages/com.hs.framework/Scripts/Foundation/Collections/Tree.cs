using System;
using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>부모가 하나뿐이고 순환이 없는 트리이다.</summary>
    /// <remarks>
    /// <para>
    /// <b><see cref="IGraph{TNodeRef,TEdgeRef}"/>는 구현하지 않는다.</b> 그것을 구현하면 간선을
    /// 임의로 이어 붙일 수 있게 되어 순환이나 두 번째 부모를 만들 문이 열린다. 읽기 계약만 구현해
    /// 그리고 훑는 코드는 함께 쓰되, 붙이는 일은 <see cref="AddChild"/> 하나로만 연다.
    /// </para>
    /// <para>
    /// <b>뜨거운 쪽과 차가운 쪽이 다르다.</b> 자식을 훑는 일은 노드에서 바로 하므로 값이 들지
    /// 않는다. 그래프 계약으로 도는 쪽은 간선을 감싸느라 목록을 새로 만들지만, 그리거나 편집할
    /// 때만 지나는 길이라 그 비용을 낸다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public sealed class Tree<TNode> : IReadOnlyGraph<TreeNode<TNode>, TreeEdge<TNode>>
    {
        private readonly NodeStore<TreeNode<TNode>> _nodes = new();

        /// <summary>루트이며, 비어 있으면 null이다.</summary>
        public TreeNode<TNode> Root { get; private set; }

        /// <inheritdoc />
        public int NodeCount => _nodes.Count;

        /// <inheritdoc />
        /// <remarks>루트를 뺀 노드마다 부모로 가는 간선이 하나씩이다.</remarks>
        public int EdgeCount => _nodes.Count == 0 ? 0 : _nodes.Count - 1;

        /// <inheritdoc />
        public IReadOnlyList<TreeNode<TNode>> Nodes => _nodes.Nodes;

        /// <inheritdoc />
        /// <remarks>루트가 아닌 노드가 곧 간선이므로 그때 목록을 만들어 돌려준다.</remarks>
        public IReadOnlyList<TreeEdge<TNode>> Edges
        {
            get
            {
                var edges = new List<TreeEdge<TNode>>(EdgeCount);
                foreach (var node in _nodes)
                {
                    if (!node.IsRoot)
                    {
                        edges.Add(new TreeEdge<TNode>(node));
                    }
                }

                return edges;
            }
        }

        /// <inheritdoc />
        public bool Contains(TreeNode<TNode> node) => _nodes.Contains(node);

        /// <inheritdoc />
        /// <remarks>루트는 노드이지만 간선이 아니므로, 자식이 부모를 가진 경우만 참이다.</remarks>
        public bool Contains(TreeEdge<TNode> edge) => edge.Child != null && !edge.Child.IsRoot
                                                      && _nodes.Contains(edge.Child);

        /// <inheritdoc />
        public TreeNode<TNode> SourceOf(TreeEdge<TNode> edge) => edge.Child?.Parent;

        /// <inheritdoc />
        public TreeNode<TNode> TargetOf(TreeEdge<TNode> edge) => edge.Child;

        /// <inheritdoc />
        /// <remarks>자식으로 내려가는 간선이므로 자식마다 하나씩이다.</remarks>
        public IReadOnlyList<TreeEdge<TNode>> OutgoingEdges(TreeNode<TNode> node)
        {
            if (node == null)
            {
                return Array.Empty<TreeEdge<TNode>>();
            }

            var edges = new List<TreeEdge<TNode>>(node.Children.Count);
            foreach (var child in node.Children)
            {
                edges.Add(new TreeEdge<TNode>(child));
            }

            return edges;
        }

        /// <inheritdoc />
        /// <remarks>부모가 하나뿐이므로 답은 하나이거나 없다.</remarks>
        public IReadOnlyList<TreeEdge<TNode>> IncomingEdges(TreeNode<TNode> node)
        {
            if (node == null || node.IsRoot)
            {
                return Array.Empty<TreeEdge<TNode>>();
            }

            return new[] { new TreeEdge<TNode>(node) };
        }

        /// <inheritdoc />
        /// <remarks>부모와 자식 사이에만 간선이 있으므로 답은 하나이거나 없다.</remarks>
        public IReadOnlyList<TreeEdge<TNode>> EdgesBetween(TreeNode<TNode> from, TreeNode<TNode> to)
        {
            if (to == null || !ReferenceEquals(to.Parent, from))
            {
                return Array.Empty<TreeEdge<TNode>>();
            }

            return new[] { new TreeEdge<TNode>(to) };
        }

        /// <summary>루트를 세운다.</summary>
        /// <remarks>
        /// <b>이미 루트가 있으면 거부한다.</b> 갈아치우면 아래 가지가 통째로 트리 밖에 남는데,
        /// 그것을 조용히 하면 어디에도 닿지 않는 노드가 저장에 남는다. 바꾸려면 먼저 비워야 한다.
        /// </remarks>
        /// <param name="value">루트가 실을 값이다.</param>
        /// <returns>만들어진 루트이다.</returns>
        /// <exception cref="InvalidOperationException">이미 루트가 있으면 발생한다.</exception>
        public TreeNode<TNode> SetRoot(TNode value = default)
        {
            if (Root != null)
            {
                throw new InvalidOperationException("이미 루트가 있다. 바꾸려면 먼저 그것을 빼야 한다.");
            }

            Root = new TreeNode<TNode>(value);
            _nodes.Add(Root);
            return Root;
        }

        /// <summary>부모 아래에 자식을 만들어 붙인다.</summary>
        /// <remarks>붙이는 문이 이것 하나뿐이라 순환도 두 번째 부모도 생기지 않는다.</remarks>
        /// <param name="parent">붙일 부모이다.</param>
        /// <param name="value">자식이 실을 값이다.</param>
        /// <returns>만들어진 자식이다.</returns>
        /// <exception cref="ArgumentException">부모가 이 트리의 노드가 아니면 발생한다.</exception>
        public TreeNode<TNode> AddChild(TreeNode<TNode> parent, TNode value = default)
        {
            if (!Contains(parent))
            {
                throw new ArgumentException("parent is not in this tree", nameof(parent));
            }

            var child = new TreeNode<TNode>(value);
            parent.AddChild(child);
            _nodes.Add(child);
            return child;
        }

        /// <summary>그 노드와 아래 가지를 함께 뺀다.</summary>
        /// <remarks>
        /// <b>아래 가지를 함께 지운다.</b> 자식을 부모에게 올리면 그 자리에 있던 뜻이 달라진다.
        /// 자식 순서가 곧 우선순위이므로, 중간 노드가 사라진 자리에 손자들이 끼어들면 형제들의
        /// 우선순위가 조용히 밀린다. 루트를 빼면 트리가 빈다.
        /// </remarks>
        /// <param name="node">뺄 노드이다.</param>
        /// <returns>실제로 뺐으면 참이다.</returns>
        public bool RemoveNode(TreeNode<TNode> node)
        {
            if (!Contains(node))
            {
                return false;
            }

            node.Parent?.RemoveChild(node);

            var pending = new Stack<TreeNode<TNode>>();
            pending.Push(node);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var child in current.Children)
                {
                    pending.Push(child);
                }

                _nodes.Remove(current);
            }

            if (ReferenceEquals(node, Root))
            {
                Root = null;
            }

            return true;
        }

        /// <summary>자식의 순서를 바꾼다.</summary>
        /// <remarks>자식 순서가 곧 우선순위이므로 편집기가 이 자리를 쓴다.</remarks>
        /// <param name="parent">순서를 바꿀 부모이다.</param>
        /// <param name="from">옮길 자식의 지금 자리이다.</param>
        /// <param name="to">옮겨 갈 자리이다.</param>
        /// <exception cref="ArgumentException">부모가 이 트리의 노드가 아니면 발생한다.</exception>
        /// <exception cref="ArgumentOutOfRangeException">자리가 자식 수를 벗어나면 발생한다.</exception>
        public void MoveChild(TreeNode<TNode> parent, int from, int to)
        {
            if (!Contains(parent))
            {
                throw new ArgumentException("parent is not in this tree", nameof(parent));
            }

            if (from < 0 || from >= parent.Children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(from));
            }

            if (to < 0 || to >= parent.Children.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(to));
            }

            parent.MoveChild(from, to);
        }
    }
}
