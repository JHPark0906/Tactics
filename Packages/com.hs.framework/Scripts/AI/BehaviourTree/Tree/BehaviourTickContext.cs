using HS.Framework.AI.BehaviourTree;
using HS.Framework.Foundation.Collections;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>한 자리를 실행할 때 그 자리가 필요로 하는 것을 모아 넘긴다.</summary>
    /// <remarks>
    /// 자기가 트리의 어느 자리인지를 함께 넘기는 것이 핵심이다. 그래야 합성 노드가 자식 목록을
    /// 들고 있지 않아도 아래로 내려갈 수 있다.
    /// </remarks>
    public readonly struct BehaviourTickContext
    {
        /// <summary>이 자리를 담은 트리이며, 자식을 실행할 때 쓴다.</summary>
        public BehaviourTreeInstance Tree { get; }

        /// <summary>지금 실행 중인 자리이다.</summary>
        public TreeNode<IBehaviour> Node { get; }

        /// <summary>노드들이 값을 주고받는 곳이다.</summary>
        public IBehaviourContext Context { get; }

        /// <summary>실행에 필요한 것을 모은다.</summary>
        /// <param name="tree">이 자리를 담은 트리이다.</param>
        /// <param name="node">지금 실행 중인 자리이다.</param>
        /// <param name="context">값을 주고받는 곳이다.</param>
        public BehaviourTickContext(BehaviourTreeInstance tree, TreeNode<IBehaviour> node, IBehaviourContext context)
        {
            Tree = tree;
            Node = node;
            Context = context;
        }

        /// <summary>자기 자리의 자식들이며 붙인 순서를 유지한다.</summary>
        /// <remarks>앞에서부터 시도하는 것이 곧 우선순위이다.</remarks>
        public System.Collections.Generic.IReadOnlyList<TreeNode<IBehaviour>> Children => Node.Children;

        /// <summary>자식 하나를 실행한다.</summary>
        /// <param name="child">실행할 자식이다.</param>
        /// <returns>그 자식의 실행 결과이다.</returns>
        public BehaviourStatus TickChild(TreeNode<IBehaviour> child) => Tree.Tick(child, Context);
    }
}
