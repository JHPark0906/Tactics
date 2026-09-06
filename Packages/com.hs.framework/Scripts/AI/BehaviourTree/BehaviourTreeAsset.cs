using System;
using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.Foundation.Collections;
using UnityEngine;

namespace HS.Framework.AI.BehaviourTree
{
    /// <summary>행동 트리의 모양과 설정을 담아 두는 에셋이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>도는 자리는 담지 않는다.</b> 담는 것은 무엇을 할지에 대한 설명뿐이며, 실제로 도는
    /// 자리는 유닛마다 새로 만든다. 도는 자리는 어디까지 갔는지를 기억하므로, 그것을 여럿이
    /// 나눠 쓰면 유닛들이 서로의 진행을 덮어쓴다.
    /// </para>
    /// <para>
    /// <b>모양은 부모 번호로 담는다.</b> 자리마다 자기 부모가 몇 번째인지를 적어 두며, 뿌리는
    /// <see cref="NoParent"/>이다. 자식 목록을 따로 담지 않는 것은 두 곳에 같은 관계가 있으면
    /// 어긋날 수 있기 때문이고, 담긴 순서가 곧 형제 사이의 순서이다.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "HS Framework/AI/Behaviour Tree", fileName = "BehaviourTree")]
    public sealed class BehaviourTreeAsset : ScriptableObject
    {
        /// <summary>부모가 없음을 뜻하는 번호이다.</summary>
        public const int NoParent = -1;

        [SerializeReference]
        [Tooltip("자리마다 무엇을 할지에 대한 설명이다.")]
        private List<BehaviourNodeDefinition> nodes = new();

        [SerializeField]
        [Tooltip("자리마다 자기 부모가 몇 번째인지이며, 뿌리는 -1이다.")]
        private List<int> parents = new();

        /// <summary>담긴 자리 수이다.</summary>
        public int NodeCount => nodes.Count;

        /// <summary>이 에셋으로 유닛 하나가 쓸 트리를 만든다.</summary>
        /// <remarks>
        /// 담긴 순서대로 만들므로 부모가 자식보다 먼저 나와야 한다. 그렇지 않으면 붙일 부모가
        /// 아직 없어 그 자리부터 아래가 통째로 빠진다.
        /// </remarks>
        /// <param name="context">만들 때 필요한 것들이다.</param>
        /// <returns>만들어진 트리이며, 담긴 것이 없으면 빈 트리이다.</returns>
        public BehaviourTreeInstance CreateRuntimeTree(in BehaviourBuildContext context)
        {
            var tree = new BehaviourTreeInstance();
            Append(tree, null, context, new List<BehaviourTreeAsset>());
            return tree;
        }

        /// <summary>이 에셋의 자리들을 그 트리의 그 자리 아래에 이어 붙인다.</summary>
        /// <remarks>
        /// <para>
        /// 다른 에셋을 끼우는 자리(<see cref="RunBehaviourDefinition"/>)를 만나면 자리 하나를 만드는
        /// 대신 그 에셋의 자리들을 여기 이어 붙인다. 그 에셋의 뿌리가 이 자리에 서고, 그 아래는
        /// 그 에셋이 그린 대로 붙는다.
        /// </para>
        /// <para>
        /// <b>빠지는 자리는 한 번 알린다.</b> 설명이 자리를 비우면 그 가지가 통째로 빠지는데, 그것이
        /// 조용히 일어나면 화면에는 유닛이 가만히 서 있는 것만 남는다. 자리 번호와 이름을 적어
        /// 경고하되, 빠진 자리 아래의 자식들은 다시 알리지 않는다. 부모를 알릴 때 그 가지가
        /// 빠진다고 이미 말했다.
        /// </para>
        /// </remarks>
        /// <param name="tree">이어 붙일 트리이다.</param>
        /// <param name="parent">이 에셋의 뿌리를 붙일 자리이며, null이면 트리의 뿌리로 세운다.</param>
        /// <param name="context">만들 때 필요한 것들이다.</param>
        /// <param name="chain">지금 만들고 있는 에셋들이다.</param>
        /// <returns>이 에셋의 뿌리가 된 자리이며, 만들지 못했으면 null이다.</returns>
        private TreeNode<IBehaviour> Append(
            BehaviourTreeInstance tree,
            TreeNode<IBehaviour> parent,
            in BehaviourBuildContext context,
            List<BehaviourTreeAsset> chain)
        {
            if (!IsValid(out var error))
            {
                Debug.LogWarning($"[BehaviourTreeAsset] {name}: {error} 트리를 만들지 않는다.", this);
                return null;
            }

            chain.Add(this);
            TreeNode<IBehaviour> root = null;
            var created = new TreeNode<IBehaviour>[nodes.Count];
            for (var index = 0; index < nodes.Count; index++)
            {
                var definition = nodes[index];
                var parentIndex = parents[index];
                if (parentIndex != NoParent)
                {
                    if (nodes[parentIndex] is RunBehaviourDefinition)
                    {
                        Warn(index, definition, "부모가 다른 에셋을 끼우는 자리라 붙을 곳이 없다", false);
                        continue;
                    }

                    if (created[parentIndex] == null)
                    {
                        continue;
                    }
                }

                var attachTo = parentIndex == NoParent ? parent : created[parentIndex];
                if (attachTo == null && tree.Root != null)
                {
                    continue;
                }

                var node = Create(tree, attachTo, definition, context, chain, out var reason);
                if (node == null)
                {
                    Warn(index, definition, reason, parentIndex == NoParent && parent == null);
                }

                if (parentIndex == NoParent)
                {
                    root = node;
                }

                // 끼우는 자리는 잎이다. 그 아래에 그린 자식은 붙을 곳이 없다.
                created[index] = definition is RunBehaviourDefinition ? null : node;
            }

            chain.RemoveAt(chain.Count - 1);
            return root;
        }

        /// <summary>자리 하나를 빼면서 그 사실을 알린다.</summary>
        /// <param name="index">뺀 자리의 번호이다.</param>
        /// <param name="definition">뺀 자리의 설명이다.</param>
        /// <param name="reason">뺀 까닭이다.</param>
        /// <param name="isTreeRoot">이 자리가 트리의 뿌리였으면 참이다. 그러면 트리가 빈다.</param>
        private void Warn(int index, BehaviourNodeDefinition definition, string reason, bool isTreeRoot)
        {
            var consequence = isTreeRoot ? "뿌리라 트리가 빈다" : "그 가지를 뺀다";
            Debug.LogWarning(
                $"[BehaviourTreeAsset] {name}: {index}번째 자리 '{definition.DisplayName}'을 만들 수 없어 {consequence}. 까닭: {reason}.",
                this);
        }

        /// <summary>설명 하나로 자리를 만들어 붙인다.</summary>
        /// <remarks>
        /// <b>되도는 참조는 끊는다.</b> 자기 자신이나 만들고 있는 조상 에셋을 다시 끼우면 끝이 없으므로,
        /// 지금 만들고 있는 에셋들 안에 있는 에셋은 이어 붙이지 않는다. 같은 에셋을 서로 다른 두
        /// 자리에 끼우는 것은 되도는 것이 아니므로 막지 않는다.
        /// </remarks>
        /// <param name="tree">붙일 트리이다.</param>
        /// <param name="attachTo">붙일 자리이며, null이면 뿌리로 세운다.</param>
        /// <param name="definition">만들 설명이다.</param>
        /// <param name="context">만들 때 필요한 것들이다.</param>
        /// <param name="chain">지금 만들고 있는 에셋들이다.</param>
        /// <param name="reason">만들지 못한 까닭이며, 만들었으면 null이다.</param>
        /// <returns>만들어진 자리이며, 만들지 못했으면 null이다.</returns>
        private static TreeNode<IBehaviour> Create(
            BehaviourTreeInstance tree,
            TreeNode<IBehaviour> attachTo,
            BehaviourNodeDefinition definition,
            in BehaviourBuildContext context,
            List<BehaviourTreeAsset> chain,
            out string reason)
        {
            if (definition is RunBehaviourDefinition run)
            {
                if (run.Subtree == null)
                {
                    reason = "끼울 에셋이 비어 있다";
                    return null;
                }

                if (chain.Contains(run.Subtree))
                {
                    reason = $"끼울 에셋 '{run.Subtree.name}'이 자기 자신이거나 조상이라 끝없이 되돈다";
                    return null;
                }

                var subtreeRoot = run.Subtree.Append(tree, attachTo, context, chain);
                reason = subtreeRoot == null ? $"끼울 에셋 '{run.Subtree.name}'의 뿌리를 만들 수 없다" : null;
                return subtreeRoot;
            }

            var behaviour = definition.CreateBehaviour(context);
            if (behaviour == null)
            {
                reason = "설명이 자리를 비웠다. 유닛에 필요한 것이 없거나 설정이 비어 있다";
                return null;
            }

            reason = null;
            return attachTo == null ? tree.SetRoot(behaviour) : tree.AddChild(attachTo, behaviour);
        }

        /// <summary>담긴 것이 트리를 이룰 수 있는지 본다.</summary>
        /// <remarks>
        /// <b>순환과 끊긴 자리는 검사하지 않는다.</b> 부모 번호가 자기보다 앞을 가리키게만 하면
        /// 순환이 생길 수 없고, 뿌리가 아닌 자리는 반드시 부모가 있으므로 끊길 수도 없다.
        /// 막을 수 있는 것을 검사로 잡으려 하면 잡히지 않는 경우가 남는다.
        /// </remarks>
        /// <param name="error">어긋난 까닭이며, 옳으면 비어 있다.</param>
        /// <returns>트리를 이룰 수 있으면 참이다.</returns>
        public bool IsValid(out string error)
        {
            if (nodes.Count != parents.Count)
            {
                error = "자리 수와 부모 수가 다르다.";
                return false;
            }

            if (nodes.Count == 0)
            {
                error = "담긴 자리가 없다.";
                return false;
            }

            var rootCount = 0;
            for (var index = 0; index < nodes.Count; index++)
            {
                if (nodes[index] == null)
                {
                    error = $"{index}번째 자리에 무엇을 할지가 비어 있다.";
                    return false;
                }

                var parent = parents[index];
                if (parent == NoParent)
                {
                    rootCount++;
                    continue;
                }

                if (parent < 0 || parent >= index)
                {
                    error = $"{index}번째 자리의 부모 번호가 자기보다 앞을 가리키지 않는다.";
                    return false;
                }
            }

            if (rootCount != 1)
            {
                error = rootCount == 0 ? "뿌리가 없다." : "뿌리가 둘 이상이다.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
