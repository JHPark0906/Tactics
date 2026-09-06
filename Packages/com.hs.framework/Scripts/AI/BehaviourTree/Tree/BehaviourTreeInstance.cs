using HS.Framework.AI.BehaviourTree;
using System;
using System.Collections.Generic;
using HS.Framework.Foundation.Collections;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>구조는 트리가 들고, 각 자리가 무엇을 하는지는 노드가 싣는 행동 트리이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>자식은 <see cref="Tree{TNode}"/>가 든다.</b> 합성 노드가 자식 배열을 직접 들면
    /// 구조가 노드마다 흩어져 있어 편집기가 트리를 통째로 다루기 어렵고
    /// 자식 순서를 바꾸는 일도 노드마다 달라진다.
    /// </para>
    /// <para>
    /// 실행 상태는 유닛마다 별도로 생성한 행동 객체가 소유한다. 실행 객체를 여러 유닛이 공유하지 않는다.
    /// </para>
    /// </remarks>
    public sealed class BehaviourTreeInstance
    {
        private readonly Tree<IBehaviour> _tree = new();
        private readonly List<TreeNode<IBehaviour>> _activePath = new();
        private readonly List<AbortRequest> _pendingAborts = new();
        private readonly List<TreeNode<IBehaviour>> _relevant = new();
        private readonly List<TreeNode<IBehaviour>> _nextRelevant = new();

        private readonly struct AbortRequest
        {
            public AbortRequest(TreeNode<IBehaviour> node, BehaviourAbortScope scope)
            {
                Node = node;
                Scope = scope;
            }

            public TreeNode<IBehaviour> Node { get; }

            public BehaviourAbortScope Scope { get; }
        }

        /// <summary>트리의 뿌리이며, 비어 있으면 null이다.</summary>
        public TreeNode<IBehaviour> Root => _tree.Root;

        /// <summary>자리 수이다.</summary>
        public int Count => _tree.NodeCount;

        /// <summary>지금 실행 중인 자리들이며, 뿌리에서 가장 깊은 곳까지 이어진다.</summary>
        /// <remarks>
        /// 어느 가지가 도는 중인지를 알아야 "실행 중인 것을 끊는다"를 말할 수 있다.
        /// 한 번 실행이 끝나면 진행 중으로 남은 자리들만 여기 남는다.
        /// </remarks>
        public IReadOnlyList<TreeNode<IBehaviour>> ActivePath => _activePath;

        /// <summary>그 자리가 지금 실행 중인 경로에 있는지 본다.</summary>
        /// <param name="node">확인할 자리이다.</param>
        /// <returns>실행 중이면 참이다.</returns>
        public bool IsRunning(TreeNode<IBehaviour> node) => node != null && _activePath.Contains(node);

        /// <summary>읽기 전용으로 트리를 본다. 그리거나 훑는 코드가 쓴다.</summary>
        public IReadOnlyGraph<TreeNode<IBehaviour>, TreeEdge<IBehaviour>> AsGraph => _tree;

        /// <summary>뿌리를 세운다.</summary>
        /// <param name="behaviour">뿌리가 할 일이다.</param>
        /// <returns>만들어진 자리이다.</returns>
        public TreeNode<IBehaviour> SetRoot(IBehaviour behaviour)
        {
            if (behaviour == null)
            {
                throw new ArgumentNullException(nameof(behaviour));
            }

            return _tree.SetRoot(behaviour);
        }

        /// <summary>어떤 자리 아래에 자식을 붙인다.</summary>
        /// <remarks>붙인 순서가 곧 시도하는 순서이다.</remarks>
        /// <param name="parent">붙일 자리이다.</param>
        /// <param name="behaviour">자식이 할 일이다.</param>
        /// <returns>만들어진 자리이다.</returns>
        public TreeNode<IBehaviour> AddChild(TreeNode<IBehaviour> parent, IBehaviour behaviour)
        {
            if (behaviour == null)
            {
                throw new ArgumentNullException(nameof(behaviour));
            }

            return _tree.AddChild(parent, behaviour);
        }

        /// <summary>자식의 순서를 바꾼다.</summary>
        /// <param name="parent">순서를 바꿀 자리이다.</param>
        /// <param name="from">옮길 자식의 지금 자리이다.</param>
        /// <param name="to">옮겨 갈 자리이다.</param>
        public void MoveChild(TreeNode<IBehaviour> parent, int from, int to) => _tree.MoveChild(parent, from, to);

        /// <summary>그 자리와 아래 가지를 함께 뺀다.</summary>
        /// <param name="node">뺄 자리이다.</param>
        /// <returns>실제로 뺐으면 참이다.</returns>
        public bool Remove(TreeNode<IBehaviour> node) => _tree.RemoveNode(node);

        /// <summary>뿌리부터 한 번 실행한다.</summary>
        /// <remarks>
        /// <para>
        /// 순서가 뜻을 가진다. <b>걸어 둔 끊기를 먼저 적용</b>하고, 실행 경로를 비운 뒤 뿌리를 돌리고,
        /// <b>끝난 뒤에 지켜볼 자리를 다시 셈한다.</b> 끊기는 지난 실행에서 어디가 돌고 있었는지를
        /// 보고 판단하므로 경로를 비우기 전에 해야 하고, 지켜볼 자리는 이번 실행에서 어디가 돌게
        /// 되었는지로 정해지므로 뿌리가 돌고 난 뒤에 셈해야 한다.
        /// </para>
        /// <para>
        /// 지켜볼 자리를 실행 앞에서 셈하면 경로가 비어 있어 뿌리만 지켜보게 되고, 뿌리 아래의
        /// 조건 자리는 값이 바뀌어도 알림을 받지 못한다.
        /// </para>
        /// </remarks>
        /// <param name="context">값을 주고받는 곳이다.</param>
        /// <returns>실행 결과이며, 트리가 비어 있으면 실패이다.</returns>
        public BehaviourStatus Tick(IBehaviourContext context)
        {
            ApplyPendingAborts();
            _activePath.Clear();
            var status = Root == null ? BehaviourStatus.Failure : Tick(Root, context);
            RefreshRelevance(context);
            return status;
        }

        /// <summary>그 자리를 한 번 실행한다.</summary>
        /// <remarks>합성 노드가 자식으로 내려갈 때도 이 자리를 지난다.</remarks>
        /// <param name="node">실행할 자리이다.</param>
        /// <param name="context">값을 주고받는 곳이다.</param>
        /// <returns>실행 결과이며, 자리나 할 일이 없으면 실패이다.</returns>
        public BehaviourStatus Tick(TreeNode<IBehaviour> node, IBehaviourContext context)
        {
            if (node?.Value == null)
            {
                return BehaviourStatus.Failure;
            }

            var depth = _activePath.Count;
            _activePath.Add(node);

            var status = node.Value.Tick(new BehaviourTickContext(this, node, context));

            if (status != BehaviourStatus.Running)
            {
                _activePath.RemoveRange(depth, _activePath.Count - depth);
            }

            return status;
        }

        /// <summary>그 자리와 아래 가지의 진행을 모두 처음으로 되돌린다.</summary>
        /// <remarks>
        /// 아래로 내려가는 일을 트리가 맡는다. 각 행동이 자식을 되돌리게 하면 자식을 모르는
        /// 계약과 어긋난다.
        /// </remarks>
        /// <param name="node">되돌릴 자리이며, 비우면 뿌리부터 한다.</param>
        public void Reset(TreeNode<IBehaviour> node = null)
        {
            var start = node ?? Root;
            if (start == null)
            {
                return;
            }

            var pending = new System.Collections.Generic.Stack<TreeNode<IBehaviour>>();
            pending.Push(start);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                current.Value?.Reset();
                foreach (var child in current.Children)
                {
                    pending.Push(child);
                }
            }
        }

        /// <summary>값이 바뀌었으니 끊어 달라고 걸어 둔다.</summary>
        /// <remarks>
        /// <b>그 자리에서 바로 끊지 않는다.</b> 알림은 노드가 실행 중인 도중에도 오므로, 그때
        /// 트리를 흔들면 지금 돌던 자리가 자기 밑이 사라진 채로 이어진다. 다음 실행 앞에서 적용한다.
        /// </remarks>
        /// <param name="node">끊어 달라고 하는 자리이다.</param>
        /// <param name="scope">무엇을 끊을지이다.</param>
        public void RequestAbort(TreeNode<IBehaviour> node, BehaviourAbortScope scope)
        {
            if (node == null || scope == BehaviourAbortScope.None)
            {
                return;
            }

            _pendingAborts.Add(new AbortRequest(node, scope));
        }

        /// <summary>걸어 둔 끊기를 실행 앞에서 적용한다.</summary>
        /// <remarks>
        /// <para>
        /// <b>무엇을 되돌리는지가 범위마다 다르다.</b> 뒤엣 형제를 끊을 때는 부모의 진행과 그 아래에서
        /// 도는 가지를 되돌린다. 부모가 다시 앞에서부터 고르므로 자기 차례가 그 자리에서 돌아온다.
        /// 자기를 끊을 때는 <b>자기 아래만</b> 되돌린다. 부모까지 되돌리면 이미 끝난 형제의 진행이
        /// 함께 지워져, 순차 자리 아래라면 끝난 일을 다시 하고 나서야 이 자리의 실패가 위로 올라간다.
        /// </para>
        /// <para>
        /// 자기 아래만 되돌리면 부모는 여전히 이 자리를 가리키고 있고, 다음 실행에서 조건이 막아
        /// 실패를 돌려주므로 부모가 그것을 보통의 실패로 다룬다.
        /// </para>
        /// </remarks>
        private void ApplyPendingAborts()
        {
            if (_pendingAborts.Count == 0)
            {
                return;
            }

            for (var index = 0; index < _pendingAborts.Count; index++)
            {
                var request = _pendingAborts[index];
                var node = request.Node;
                var scope = request.Scope;

                var abortsLower = scope == BehaviourAbortScope.LowerPriority || scope == BehaviourAbortScope.Both;
                if (abortsLower && IsLowerPrioritySiblingRunning(node))
                {
                    ResetRunningBranches(node.Parent);
                    continue;
                }

                var abortsSelf = scope == BehaviourAbortScope.Self || scope == BehaviourAbortScope.Both;
                if (abortsSelf && IsRunning(node))
                {
                    ResetRunningBranches(node);
                }
            }

            _pendingAborts.Clear();
        }

        /// <summary>그 자리의 진행과, 그 아래에서 실행 중인 가지만 되돌린다.</summary>
        /// <remarks>
        /// 실행 중이 아닌 자식은 되돌릴 진행이 없으므로 건드리지 않는다. 그런 자리까지 되돌리면
        /// 끝나 있던 자리의 <see cref="IBehaviour.Reset"/>이 남기는 부작용이 실제로 돌던 가지의 것에
        /// 겹친다. 같은 이동 수단을 쓰는 두 이동 자리가 있으면 돌지도 않던 쪽이 멈춤을 한 번 더 보낸다.
        /// 끊기는 실행 중인 가지를 가로채는 것이지 나무를 통째로 처음으로 돌리는 것이 아니다.
        /// </remarks>
        /// <param name="node">되돌릴 자리이다.</param>
        private void ResetRunningBranches(TreeNode<IBehaviour> node)
        {
            node.Value?.Reset();
            var children = node.Children;
            for (var index = 0; index < children.Count; index++)
            {
                if (IsRunning(children[index]))
                {
                    ResetRunningBranches(children[index]);
                }
            }
        }

        /// <summary>그 자리보다 뒤에 있는 형제가 실행 중인지 본다.</summary>
        /// <param name="node">기준이 되는 자리이다.</param>
        /// <returns>뒤엣 형제 가운데 실행 중인 것이 있으면 참이다.</returns>
        private bool IsLowerPrioritySiblingRunning(TreeNode<IBehaviour> node)
        {
            if (node.Parent == null)
            {
                return false;
            }

            var siblings = node.Parent.Children;
            var own = -1;
            for (var index = 0; index < siblings.Count; index++)
            {
                if (ReferenceEquals(siblings[index], node))
                {
                    own = index;
                    break;
                }
            }

            for (var index = own + 1; own >= 0 && index < siblings.Count; index++)
            {
                if (IsRunning(siblings[index]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>지켜볼 자리를 다시 셈한다.</summary>
        /// <remarks>
        /// 실행 중인 자리의 자식들이 지켜볼 자리이다. 자기가 도는 중이든 뒤엣 형제가 도는 중이든
        /// 부모가 실행 중일 때 일어나므로, 그 범위로 걸고 푼다.
        /// </remarks>
        /// <param name="context">노드들이 값을 주고받는 곳이다.</param>
        private void RefreshRelevance(IBehaviourContext context)
        {
            _nextRelevant.Clear();
            if (Root != null)
            {
                _nextRelevant.Add(Root);
                for (var index = 0; index < _activePath.Count; index++)
                {
                    var children = _activePath[index].Children;
                    for (var childIndex = 0; childIndex < children.Count; childIndex++)
                    {
                        _nextRelevant.Add(children[childIndex]);
                    }
                }
            }

            for (var index = 0; index < _relevant.Count; index++)
            {
                var node = _relevant[index];
                if (!_nextRelevant.Contains(node) && node.Value is IObservingBehaviour ceasing)
                {
                    ceasing.OnCeaseRelevant();
                }
            }

            for (var index = 0; index < _nextRelevant.Count; index++)
            {
                var node = _nextRelevant[index];
                if (!_relevant.Contains(node) && node.Value is IObservingBehaviour becoming)
                {
                    becoming.OnBecomeRelevant(new BehaviourTickContext(this, node, context));
                }
            }

            _relevant.Clear();
            _relevant.AddRange(_nextRelevant);
        }
    }
}
