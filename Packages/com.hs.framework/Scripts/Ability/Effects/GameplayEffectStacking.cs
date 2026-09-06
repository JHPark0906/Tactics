using System;
using UnityEngine;

namespace HS.Framework.Ability.Effects
{
    /// <summary>
    /// 같은 효과를 다시 적용했을 때 어떻게 쌓을지 정한다.
    /// </summary>
    /// <remarks>
    /// <b>쌓임은 즉시 효과와 무관하다.</b> 즉시 효과는 남지 않으므로 쌓을 것이 없고, 지속·무한 효과만 쌓인다.
    /// 쌓이는 효과는 유지 중인 기록 하나가 층수를 세며, 층마다 수정자 한 벌씩 얹히고 주기 실행은 층수만큼 반복된다.
    /// 부여 태그는 층수와 무관하게 한 번만 부여된다.
    /// </remarks>
    public enum GameplayEffectStackingPolicy
    {
        /// <summary>쌓지 않는다. 다시 적용하면 별개의 기록이 하나 더 생긴다.</summary>
        None = 0,

        /// <summary>대상마다 하나로 쌓는다. 누가 걸었든 같은 정의는 같은 기록에 층을 더한다.</summary>
        AggregateByTarget = 1,

        /// <summary>건 것마다 따로 쌓는다. 같은 정의라도 건 것이 다르면 별개의 기록이다.</summary>
        AggregateBySource = 2
    }

    /// <summary>층이 더해질 때 남은 지속 시간을 어떻게 할지 정한다.</summary>
    public enum GameplayEffectStackDurationRefreshPolicy
    {
        /// <summary>층이 더해지거나 한도에 닿아 더하지 못했을 때 지속 시간을 처음부터 다시 센다.</summary>
        RefreshOnSuccessfulApplication = 0,

        /// <summary>지속 시간을 건드리지 않는다. 처음 걸린 시각 기준으로 만료된다.</summary>
        NeverRefresh = 1
    }

    /// <summary>층이 더해질 때 주기 실행 타이머를 어떻게 할지 정한다.</summary>
    public enum GameplayEffectStackPeriodResetPolicy
    {
        /// <summary>층이 더해지면 다음 주기까지의 시간을 처음부터 다시 센다.</summary>
        ResetOnSuccessfulApplication = 0,

        /// <summary>주기 타이머를 건드리지 않는다.</summary>
        NeverReset = 1
    }

    /// <summary>지속 시간이 다했을 때 쌓인 층을 어떻게 할지 정한다.</summary>
    public enum GameplayEffectStackExpirationPolicy
    {
        /// <summary>층수와 상관없이 한 번에 전부 걷는다.</summary>
        ClearEntireStack = 0,

        /// <summary>한 층만 걷고 지속 시간을 다시 센다. 층마다 지속 시간만큼 살아남는다.</summary>
        RemoveSingleStackAndRefreshDuration = 1,

        /// <summary>층을 걷지 않고 지속 시간만 다시 센다. 바깥에서 제거하기 전까지 사라지지 않는다.</summary>
        RefreshDuration = 2
    }

    /// <summary>
    /// 효과 하나의 쌓임 규칙 묶음이다.
    /// </summary>
    /// <remarks>
    /// 기본값은 쌓지 않는 것이다. 쌓기로 하면 한도 0은 무제한이고, 한도에 닿은 적용은 층을 더하지 못하되
    /// 지속 시간과 주기 타이머는 정책에 따라 갱신된다.
    /// </remarks>
    [Serializable]
    public struct GameplayEffectStackingSettings
    {
        [Tooltip("같은 효과를 다시 적용했을 때 쌓는 방식이다.")]
        [SerializeField]
        private GameplayEffectStackingPolicy policy;

        [Tooltip("쌓을 수 있는 최대 층수이다. 0이면 무제한이다.")]
        [SerializeField]
        [Min(0)]
        private int limit;

        [Tooltip("층이 더해질 때 남은 지속 시간을 어떻게 할지이다.")]
        [SerializeField]
        private GameplayEffectStackDurationRefreshPolicy durationRefreshPolicy;

        [Tooltip("층이 더해질 때 주기 실행 타이머를 어떻게 할지이다.")]
        [SerializeField]
        private GameplayEffectStackPeriodResetPolicy periodResetPolicy;

        [Tooltip("지속 시간이 다했을 때 쌓인 층을 어떻게 할지이다.")]
        [SerializeField]
        private GameplayEffectStackExpirationPolicy expirationPolicy;

        /// <summary>쌓임 규칙을 생성한다.</summary>
        /// <param name="policy">쌓는 방식이다.</param>
        /// <param name="limit">최대 층수이며 0이면 무제한이다.</param>
        /// <param name="durationRefreshPolicy">층이 더해질 때 지속 시간을 다루는 방식이다.</param>
        /// <param name="periodResetPolicy">층이 더해질 때 주기 타이머를 다루는 방식이다.</param>
        /// <param name="expirationPolicy">지속 시간이 다했을 때 층을 다루는 방식이다.</param>
        public GameplayEffectStackingSettings(
            GameplayEffectStackingPolicy policy,
            int limit = 0,
            GameplayEffectStackDurationRefreshPolicy durationRefreshPolicy =
                GameplayEffectStackDurationRefreshPolicy.RefreshOnSuccessfulApplication,
            GameplayEffectStackPeriodResetPolicy periodResetPolicy =
                GameplayEffectStackPeriodResetPolicy.ResetOnSuccessfulApplication,
            GameplayEffectStackExpirationPolicy expirationPolicy = GameplayEffectStackExpirationPolicy.ClearEntireStack)
        {
            this.policy = policy;
            this.limit = Mathf.Max(0, limit);
            this.durationRefreshPolicy = durationRefreshPolicy;
            this.periodResetPolicy = periodResetPolicy;
            this.expirationPolicy = expirationPolicy;
        }

        /// <summary>쌓지 않는 규칙이다.</summary>
        public static GameplayEffectStackingSettings None => default;

        /// <summary>쌓는 방식이다.</summary>
        public GameplayEffectStackingPolicy Policy => policy;

        /// <summary>최대 층수이며 0이면 무제한이다.</summary>
        public int Limit => Mathf.Max(0, limit);

        /// <summary>층이 더해질 때 지속 시간을 다루는 방식이다.</summary>
        public GameplayEffectStackDurationRefreshPolicy DurationRefreshPolicy => durationRefreshPolicy;

        /// <summary>층이 더해질 때 주기 타이머를 다루는 방식이다.</summary>
        public GameplayEffectStackPeriodResetPolicy PeriodResetPolicy => periodResetPolicy;

        /// <summary>지속 시간이 다했을 때 층을 다루는 방식이다.</summary>
        public GameplayEffectStackExpirationPolicy ExpirationPolicy => expirationPolicy;

        /// <summary>쌓는 규칙인지 여부이다.</summary>
        public bool IsStacking => policy != GameplayEffectStackingPolicy.None;

        /// <summary>지정한 층수에서 층을 더 쌓을 수 있는지 확인한다.</summary>
        /// <param name="currentCount">지금 층수이다.</param>
        /// <returns>더 쌓을 수 있으면 true이다.</returns>
        public bool CanGrow(int currentCount)
        {
            return Limit <= 0 || currentCount < Limit;
        }
    }
}
