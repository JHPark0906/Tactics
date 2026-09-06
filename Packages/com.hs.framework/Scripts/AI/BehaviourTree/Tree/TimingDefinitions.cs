using System;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>아래가 정해진 시간 안에 끝나지 못하면 되돌리고 실패하는 자리의 설명이다.</summary>
    /// <remarks>시각은 <see cref="Time.time"/>에서 읽는다.</remarks>
    [Serializable]
    public sealed class TimeLimitDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Min(0f)]
        [Tooltip("아래에 허용하는 시간(초)이다. 이 시간 안에 끝나지 못하면 되돌리고 실패한다.")]
        private float limitSeconds = 1f;

        /// <inheritdoc />
        public override string DisplayName => $"TimeLimit {limitSeconds:0.##}s";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => new TimeLimitBehaviour(limitSeconds);
    }

    /// <summary>문맥의 키에 적힌 시각까지 쉬고, 끝나면 그 시각을 미루는 자리의 설명이다.</summary>
    /// <remarks>같은 키를 쓰는 자리들이 하나의 쉬는 시간을 나눠 쓴다. 시각은 <see cref="Time.time"/>에서 읽는다.</remarks>
    [Serializable]
    public sealed class ContextCooldownDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("쉬는 시각을 둘 문맥 키이다. 같은 키를 쓰는 자리들이 쉬는 시간을 나눠 쓴다.")]
        private string key;

        [SerializeField]
        [Min(0f)]
        [Tooltip("다시 실행하기까지 쉬는 시간(초)이다.")]
        private float cooldownSeconds = 1f;

        [SerializeField]
        [Tooltip("아직 쉬는 중에 아래가 다시 끝나면 남은 시간에 더한다. 끄면 지금부터 다시 센다.")]
        private bool addsToExistingDuration;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(key) ? "SharedCooldown" : $"SharedCooldown: {key} {cooldownSeconds:0.##}s";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(key)
                ? null
                : new ContextCooldownBehaviour(key, cooldownSeconds, addsToExistingDuration);
    }

    /// <summary>문맥의 키에 적힌 만큼 기다리는 자리의 설명이다.</summary>
    /// <remarks>시각은 <see cref="Time.time"/>에서 읽는다.</remarks>
    [Serializable]
    public sealed class WaitContextTimeDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("기다릴 시간(초)이 담긴 문맥 키이다.")]
        private string key;

        /// <inheritdoc />
        public override string DisplayName
            => string.IsNullOrWhiteSpace(key) ? "WaitContextTime" : $"Wait: {key}";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
            => string.IsNullOrWhiteSpace(key) ? null : new WaitContextTimeBehaviour(key);
    }
}
