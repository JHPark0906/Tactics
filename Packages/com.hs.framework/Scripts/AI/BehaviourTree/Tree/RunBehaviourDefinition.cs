using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>다른 트리 에셋을 이 자리에 서브트리로 끼우는 설명이다.</summary>
    /// <remarks>
    /// <para>
    /// <b>자리 하나를 만들지 않는다.</b> 에셋이 트리를 지을 때 이 자리를 만나면, 여기 적힌 에셋의
    /// 자리들을 이 자리에 이어 붙인다. 그래서 만들어진 트리에는 이 자리가 따로 없고 그 에셋의
    /// 뿌리가 그 자리에 선다. 끊기와 지켜보기가 경계 없이 이어지는 것은 그 때문이다.
    /// </para>
    /// <para>
    /// <b>잎이다.</b> 이 자리 아래에 그린 자식은 붙을 곳이 없어 빠진다. 아래에 무언가를 두고 싶으면
    /// 끼우는 에셋 안에 그린다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class RunBehaviourDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("이 자리에 이어 붙일 트리 에셋이다.")]
        private BehaviourTreeAsset subtree;

        /// <summary>이 자리에 이어 붙일 트리 에셋이며, 비어 있으면 null이다.</summary>
        public BehaviourTreeAsset Subtree => subtree;

        /// <inheritdoc />
        public override string DisplayName => subtree == null ? "Run" : $"Run: {subtree.name}";

        /// <inheritdoc />
        /// <remarks>언제나 null이다. 자리를 만드는 대신 에셋이 서브트리를 이어 붙인다.</remarks>
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context) => null;
    }
}
