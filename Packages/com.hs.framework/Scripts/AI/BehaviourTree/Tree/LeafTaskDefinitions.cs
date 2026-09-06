using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>정해진 결과를 돌려주는 잎의 설명이다.</summary>
    [Serializable]
    public sealed class FinishWithResultDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("언제나 돌려줄 결과이다.")]
        private BehaviourStatus result = BehaviourStatus.Success;

        /// <inheritdoc />
        public override string DisplayName => $"Finish: {result}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new FinishWithResultBehaviour(result);
    }

    /// <summary>유닛을 문맥의 자리 쪽으로 돌리는 잎의 설명이다.</summary>
    /// <remarks>
    /// 유닛 자신을 돌리므로 유닛이 없으면 null을 돌린다. 유닛에 <see cref="ICharacterFacing"/>을 구현한
    /// 구성요소가 있으면 그것을 통해 유닛의 각속도로 돌고, 여기 적힌 속도는 쓰지 않는다. 없으면 여기 적힌
    /// 속도로 직접 돌린다. 허용 각은 「조준을 마쳤다」의 규칙이므로 유닛 값이 아니라 에셋 값이다.
    /// </remarks>
    [Serializable]
    public sealed class RotateToFaceDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("마주 볼 자리가 담긴 문맥 키이다. 좌표나 Transform이 담겨 있어야 한다.")]
        private string targetKey;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이 각(도) 안이면 마주 본 것으로 본다. 0이면 영영 마칠 수 없다.")]
        private float precisionDegrees = 10f;

        [SerializeField]
        [Tooltip("유닛에 방향 계약이 없을 때 도는 속도(도/초)이다. 계약이 있으면 유닛의 각속도를 쓴다. 0 이하이면 한 번에 돈다.")]
        private float degreesPerSecond = 360f;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(targetKey) ? "RotateToFace" : $"RotateToFace: {targetKey} ±{precisionDegrees:0.#}°";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(targetKey) || context.Owner == null)
            {
                return null;
            }

            context.Owner.TryGetComponent<ICharacterFacing>(out var facing);
            return new RotateToFaceBehaviour(context.Owner.transform, targetKey, precisionDegrees, degreesPerSecond, facing: facing);
        }
    }

    /// <summary>길을 찾지 않고 문맥의 자리로 곧장 걸어가는 잎의 설명이다.</summary>
    /// <remarks>유닛 자신을 옮기므로 유닛이 없으면 null을 돌린다.</remarks>
    [Serializable]
    public sealed class MoveDirectlyTowardDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("목표 자리가 담긴 문맥 키이다. 좌표나 Transform이 담겨 있어야 한다.")]
        private string targetKey;

        [SerializeField]
        [Min(0f)]
        [Tooltip("걷는 속도(미터/초)이다.")]
        private float speed = 3.5f;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이 거리 안이면 닿은 것으로 본다(미터).")]
        private float acceptableRadius = 0.5f;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(targetKey) ? "MoveDirectly" : $"MoveDirectly: {targetKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(targetKey) || context.Owner == null
                ? null
                : new MoveDirectlyTowardBehaviour(context.Owner.transform, targetKey, speed, acceptableRadius);
    }

    /// <summary>문맥의 방향으로 한 걸음 나아간 좌표를 문맥에 적는 잎의 설명이다.</summary>
    /// <remarks>유닛 자신의 자리가 필요하므로 유닛이 없으면 null을 돌린다.</remarks>
    [Serializable]
    public sealed class StepAlongDirectionDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0.1f)]
        [Tooltip("한 걸음의 거리(미터)이다.")]
        private float stepDistance = StepAlongDirectionBehaviour.DefaultStepDistance;

        [SerializeField]
        [Tooltip("셈한 좌표를 적을 문맥 키이다.")]
        private string destinationKey;

        [SerializeField]
        [Tooltip("방향을 읽을 문맥 키이다. 값이 없으면 유닛이 바라보는 쪽을 쓴다.")]
        private string directionKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(destinationKey) ? "Step Along Direction" : $"Step {stepDistance:0.#}m → {destinationKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(destinationKey) || string.IsNullOrWhiteSpace(directionKey) || context.Owner == null
                ? null
                : new StepAlongDirectionBehaviour(context.Owner.transform, destinationKey, directionKey, stepDistance);
    }

    /// <summary>유닛의 자리에서 소리 파일을 한 번 트는 잎의 설명이다.</summary>
    /// <remarks>틀 소리가 없거나 유닛이 없으면 null을 돌린다.</remarks>
    [Serializable]
    public sealed class PlaySoundDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("틀 소리이다.")]
        private AudioClip clip;

        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("소리 크기이다.")]
        private float volume = 1f;

        /// <inheritdoc />
        public override string DisplayName => clip == null ? "PlaySound" : $"PlaySound: {clip.name}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => clip == null || context.Owner == null
                ? null
                : new PlaySoundBehaviour(clip, context.Owner.transform, volume);
    }
}
