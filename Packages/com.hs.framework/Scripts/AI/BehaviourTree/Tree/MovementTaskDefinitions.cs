using System;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥의 좌표로 걸어가는 자리의 설명이다.</summary>
    /// <remarks>
    /// 이동 수단은 유닛마다 다르므로 에셋에 담지 않고 만들 때 유닛에서 찾는다. 유닛에
    /// <see cref="ICharacterMover"/>를 구현한 구성요소가 없으면 만들 수 없고, 그때는 null을
    /// 돌려 그 자리가 트리에서 빠진다.
    /// </remarks>
    [Serializable]
    public sealed class MoveToPositionDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("목표 좌표가 담긴 문맥 키이다.")]
        private string destinationKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(destinationKey) ? "MoveTo" : $"MoveTo: {destinationKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(destinationKey))
            {
                return null;
            }

            return context.Owner != null && context.Owner.TryGetComponent<ICharacterMover>(out var mover)
                ? new MoveToPositionBehaviour(mover, destinationKey)
                : null;
        }
    }

    /// <summary>문맥의 대상을 쫓아가는 자리의 설명이다.</summary>
    /// <remarks>
    /// 이동 수단은 만들 때 유닛에서 찾는다. 유닛에 <see cref="ICharacterMover"/>를 구현한
    /// 구성요소가 없으면 null을 돌린다.
    /// </remarks>
    [Serializable]
    public sealed class ChaseTargetDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("쫓을 대상이 담긴 문맥 키이다.")]
        private string targetKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(targetKey) ? "Chase" : $"Chase: {targetKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                return null;
            }

            return context.Owner != null && context.Owner.TryGetComponent<ICharacterMover>(out var mover)
                ? new ChaseTargetBehaviour(mover, targetKey)
                : null;
        }
    }

    /// <summary>정해진 시간을 기다리는 자리의 설명이다.</summary>
    /// <remarks>
    /// 유닛에서 찾을 것이 없으므로 언제나 만들 수 있다. 시각은 <see cref="Time.time"/>에서 읽으며,
    /// 다른 시계를 쓰는 것은 에셋으로 정할 수 있는 일이 아니다.
    /// </remarks>
    [Serializable]
    public sealed class WaitDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("기다릴 시간(초)이다.")]
        private float durationSeconds = 1f;

        /// <inheritdoc />
        public override string DisplayName => $"Wait {durationSeconds:0.##}s";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new WaitBehaviour(durationSeconds);
    }
}
