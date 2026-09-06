using System;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>지켜보는 것이 부채꼴 안에 있을 때만 아래를 실행하게 하는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class ConeCheckDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("꼭짓점 자리가 담긴 문맥 키이다. 좌표나 Transform이 담겨 있어야 한다.")]
        private string originKey;

        [SerializeField]
        [Tooltip("부채꼴이 향하는 쪽이 담긴 문맥 키이다. 방향이나 Transform이 담겨 있어야 한다.")]
        private string directionKey;

        [SerializeField]
        [Tooltip("안에 있는지 볼 것이 담긴 문맥 키이다.")]
        private string observedKey;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("가운데에서 한쪽으로 벌어진 각(도)이다.")]
        private float halfAngleDegrees = 45f;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(observedKey) ? "ConeCheck" : $"ConeCheck: {observedKey} {halfAngleDegrees:0.#}°";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(originKey)
               || string.IsNullOrWhiteSpace(directionKey)
               || string.IsNullOrWhiteSpace(observedKey)
                ? null
                : new ConeCheckBehaviour(originKey, directionKey, observedKey, halfAngleDegrees);
    }

    /// <summary>지켜보는 것이 처음 방향의 부채꼴을 벗어나면 아래를 막는 자리의 설명이다.</summary>
    [Serializable]
    public sealed class KeepInConeDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("꼭짓점 자리가 담긴 문맥 키이다.")]
        private string originKey;

        [SerializeField]
        [Tooltip("머무는지 볼 것이 담긴 문맥 키이다.")]
        private string observedKey;

        [SerializeField]
        [Range(0f, 180f)]
        [Tooltip("처음 방향에서 한쪽으로 벌어진 각(도)이다.")]
        private float halfAngleDegrees = 45f;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(observedKey) ? "KeepInCone" : $"KeepInCone: {observedKey} {halfAngleDegrees:0.#}°";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(originKey) || string.IsNullOrWhiteSpace(observedKey)
                ? null
                : new KeepInConeBehaviour(originKey, observedKey, halfAngleDegrees);
    }

    /// <summary>유닛이 문맥의 자리에 닿아 있을 때만 아래를 실행하게 하는 자리의 설명이다.</summary>
    /// <remarks>유닛 자신의 자리가 필요하므로 유닛이 없으면 null을 돌린다.</remarks>
    [Serializable]
    public sealed class IsAtLocationDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("자리가 담긴 문맥 키이다. 좌표나 Transform이 담겨 있어야 한다.")]
        private string locationKey;

        [SerializeField]
        [Min(0f)]
        [Tooltip("이 거리 안이면 닿은 것으로 본다(미터).")]
        private float acceptableRadius = 0.5f;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(locationKey) ? "IsAtLocation" : $"IsAtLocation: {locationKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(locationKey) || context.Owner == null
                ? null
                : new IsAtLocationBehaviour(context.Owner.transform, locationKey, acceptableRadius);
    }

    /// <summary>두 자리 사이에 갈 수 있는 길이 있을 때만 아래를 실행하게 하는 자리의 설명이다.</summary>
    /// <remarks>길은 내비게이션 바닥에 묻는다. 유닛 자신의 자리가 필요하므로 유닛이 없으면 null을 돌린다.</remarks>
    [Serializable]
    public sealed class DoesPathExistDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("출발 자리가 담긴 문맥 키이다. 비우면 유닛 자신의 자리에서 출발한다.")]
        private string fromKey;

        [SerializeField]
        [Tooltip("도착 자리가 담긴 문맥 키이다.")]
        private string toKey;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(toKey) ? "DoesPathExist" : $"DoesPathExist: {toKey}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(toKey) || context.Owner == null
                ? null
                : new DoesPathExistBehaviour(context.Owner.transform, fromKey, toKey);
    }
}
