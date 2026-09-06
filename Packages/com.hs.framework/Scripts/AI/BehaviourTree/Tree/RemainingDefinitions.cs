using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자식을 한 번에 모두 돌리는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class ParallelDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("언제 성공으로 볼지이다. 하나만 성공해도 되는지, 모두 성공해야 하는지를 고른다.")]
        private ParallelPolicy successPolicy = ParallelPolicy.RequireAll;

        /// <inheritdoc />
        public override string DisplayName => $"Parallel ({successPolicy})";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new ParallelBehaviour(successPolicy);
    }

    /// <summary>한 번 끝나면 정해진 시간 동안 아래를 다시 실행하지 않는 자리의 설명이다.</summary>
    /// <remarks>시각은 <see cref="Time.time"/>에서 읽는다.</remarks>
    [Serializable]
    public sealed class CooldownDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("다시 실행하기까지 쉬는 시간(초)이다.")]
        private float cooldownSeconds = 1f;

        /// <inheritdoc />
        public override string DisplayName => $"Cooldown {cooldownSeconds:0.##}s";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new CooldownBehaviour(cooldownSeconds);
    }

    /// <summary>문맥의 대상이 보일 때만 아래를 실행하게 하는 자리의 설명이다.</summary>
    /// <remarks>
    /// 시야 판정은 이 정의가 값으로 들고 있는 시야각·거리·가림 레이어로 노드가 스스로 계산한다.
    /// 유닛에서 감지 구성요소를 찾지 않으므로 유닛만 있으면 선다. 보는 쪽의 자리는 유닛 자신이다.
    /// </remarks>
    [Serializable]
    public sealed class CanSeeTargetDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("대상이 담긴 문맥 키이다.")]
        private string targetKey;

        [SerializeField]
        [Range(0f, 360f)]
        [Tooltip("이 각(도) 안이면 보이는 것으로 본다.")]
        private float viewAngle = 90f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이 거리(미터) 안이면 보이는 것으로 본다.")]
        private float maxDistance = 20f;

        [SerializeField]
        [Tooltip("시야를 가리는 것으로 볼 레이어이다.")]
        private LayerMask occlusionMask = ~0;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(targetKey) ? "CanSee" : $"CanSee: {targetKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || context.Owner == null)
            {
                return null;
            }

            var sensor = new FieldOfViewSensor(viewAngle, maxDistance, occlusionMask);
            return new CanSeeTargetBehaviour(sensor, context.Owner.transform, targetKey);
        }
    }

    /// <summary>문맥의 소리 자극이 청취 범위 안일 때만 아래를 실행하며 그 자극을 문맥에 남기는 자리의 설명이다.</summary>
    /// <remarks>
    /// 청취 판정은 이 정의가 값으로 들고 있는 청취 반경으로 노드가 스스로 계산한다.
    /// 유닛에서 감지 구성요소를 찾지 않으므로 유닛만 있으면 선다.
    /// </remarks>
    [Serializable]
    public sealed class HeardSoundDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("소리 자극이 담긴 문맥 키이다. 소리를 알린 쪽이 이 키에 자극을 담아 둔다.")]
        private string stimulusKey;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이 자리가 들을 수 있는 최대 반경(미터)이다.")]
        private float hearingRange = 15f;

        [SerializeField]
        [Tooltip("통과한 뒤 그 소리를 지울지 여부이다. 켜면 같은 소리로 두 번 통과하지 않는다.")]
        private bool consumeOnSuccess;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(stimulusKey) ? "HeardSound" : $"HeardSound: {stimulusKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(stimulusKey) || context.Owner == null)
            {
                return null;
            }

            return new HeardSoundBehaviour(context.Owner.transform, hearingRange, stimulusKey, consumeOnSuccess);
        }
    }
}
