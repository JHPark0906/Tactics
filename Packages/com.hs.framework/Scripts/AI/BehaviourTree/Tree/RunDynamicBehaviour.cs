using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥의 키에 담긴 트리 에셋을 그때그때 지어 아래에서 돌리는 잎이다.</summary>
    /// <remarks>
    /// <para>
    /// 에셋에 그려 둘 때 정해지는 <see cref="RunBehaviourDefinition"/>과 달리, 어느 에셋을 돌릴지를
    /// 실행 중에 바꿀 수 있다. 문맥의 키에 다른 에셋을 담으면 돌던 것을 되돌리고 새것을 짓는다.
    /// </para>
    /// <para>
    /// 지은 트리는 이 자리 안에서 따로 돈다. 끊기와 지켜보기도 그 안에서만 이어지므로, 바깥 조건이
    /// 이 자리를 끊으면 안의 트리는 통째로 되돌려진다.
    /// </para>
    /// </remarks>
    public sealed class RunDynamicBehaviour : IBehaviour
    {
        private readonly string _assetKey;
        private readonly BehaviourBuildContext _buildContext;
        private BehaviourTreeAsset _currentAsset;
        private BehaviourTreeInstance _subtree;

        /// <summary>에셋을 읽을 키와 트리를 지을 때 쓸 것들을 지정한다.</summary>
        /// <param name="assetKey">돌릴 <see cref="BehaviourTreeAsset"/>이 담긴 문맥 키이다.</param>
        /// <param name="buildContext">안의 트리를 지을 때 쓸 것들이다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public RunDynamicBehaviour(string assetKey, in BehaviourBuildContext buildContext)
        {
            if (string.IsNullOrWhiteSpace(assetKey))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", nameof(assetKey));
            }

            _assetKey = assetKey;
            _buildContext = buildContext;
        }

        /// <summary>지금 돌리고 있는 에셋이며, 없으면 null이다.</summary>
        public BehaviourTreeAsset CurrentAsset => _currentAsset;

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<BehaviourTreeAsset>(_assetKey, out var asset) || asset == null)
            {
                Discard();
                return BehaviourStatus.Failure;
            }

            if (!ReferenceEquals(asset, _currentAsset))
            {
                Discard();
                _subtree = asset.CreateRuntimeTree(_buildContext);
                _currentAsset = asset;
            }

            return _subtree.Root == null ? BehaviourStatus.Failure : _subtree.Tick(context.Context);
        }

        /// <inheritdoc />
        /// <remarks>안의 트리를 통째로 되돌린다. 지은 것은 버리지 않으므로 다시 들어오면 짓지 않고 돈다.</remarks>
        public void Reset() => _subtree?.Reset();

        /// <summary>돌던 트리를 되돌리고 버린다.</summary>
        private void Discard()
        {
            _subtree?.Reset();
            _subtree = null;
            _currentAsset = null;
        }
    }

    /// <summary>문맥의 키에 담긴 트리 에셋을 그때그때 지어 돌리는 잎의 설명이다.</summary>
    [Serializable]
    public sealed class RunDynamicBehaviourDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("돌릴 트리 에셋이 담긴 문맥 키이다.")]
        private string assetKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(assetKey) ? "RunDynamic" : $"RunDynamic: {assetKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(assetKey) ? null : new RunDynamicBehaviour(assetKey, context);
    }
}
