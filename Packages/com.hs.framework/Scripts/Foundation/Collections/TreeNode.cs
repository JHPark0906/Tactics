using System.Collections.Generic;

namespace HS.Framework.Foundation.Collections
{
    /// <summary>트리의 노드이며, 자기 자신이 정체성이다.</summary>
    /// <remarks>
    /// <para>
    /// 부모와 자식을 직접 든다. 간선을 훑어 자식을 찾으면 자식 하나를 얻는 데 간선 전체를 봐야
    /// 하는데, 트리는 매 틱 자식을 훑는 쪽이라 그 비용이 그대로 쌓인다.
    /// </para>
    /// <para>
    /// <b>자식 순서가 뜻을 가진다.</b> 앞에서부터 시도하는 것이 곧 우선순위이므로 넣은 순서를
    /// 유지하며, 순서를 바꾸는 일은 <see cref="Tree{TNode}"/>가 내준다.
    /// </para>
    /// <para>
    /// 붙이고 떼는 자리를 밖으로 열지 않은 것은, 부모와 자식이 서로를 가리키는 두 값이라 한쪽만
    /// 고쳐지면 어긋나기 때문이다. 트리만 두 값을 함께 고칠 수 있다.
    /// </para>
    /// </remarks>
    /// <typeparam name="TNode">노드가 싣는 값이다.</typeparam>
    public sealed class TreeNode<TNode>
    {
        private readonly List<TreeNode<TNode>> _children = new();

        /// <summary>이 노드가 싣는 값이다.</summary>
        public TNode Value { get; set; }

        /// <summary>부모이며, 루트이면 null이다.</summary>
        public TreeNode<TNode> Parent { get; internal set; }

        /// <summary>자식이며 넣은 순서를 유지한다.</summary>
        public IReadOnlyList<TreeNode<TNode>> Children => _children;

        /// <summary>루트인지 여부이며, 부모가 없으면 참이다.</summary>
        public bool IsRoot => Parent == null;

        /// <summary>주어진 값을 싣는 노드를 만든다.</summary>
        /// <param name="value">노드가 실을 값이다.</param>
        public TreeNode(TNode value = default)
        {
            Value = value;
        }

        /// <summary>자식을 끝에 붙인다.</summary>
        /// <param name="child">붙일 자식이다.</param>
        internal void AddChild(TreeNode<TNode> child)
        {
            _children.Add(child);
            child.Parent = this;
        }

        /// <summary>자식을 뗀다.</summary>
        /// <param name="child">뗄 자식이다.</param>
        /// <returns>실제로 뗐으면 참이다.</returns>
        internal bool RemoveChild(TreeNode<TNode> child)
        {
            if (!_children.Remove(child))
            {
                return false;
            }

            child.Parent = null;
            return true;
        }

        /// <summary>자식을 다른 자리로 옮긴다.</summary>
        /// <param name="from">옮길 자식의 지금 자리이다.</param>
        /// <param name="to">옮겨 갈 자리이다.</param>
        internal void MoveChild(int from, int to)
        {
            var child = _children[from];
            _children.RemoveAt(from);
            _children.Insert(to, child);
        }
    }
}
