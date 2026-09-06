using System;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>아래가 정해진 시간 안에 끝나지 못하면 그것을 되돌리고 실패한다.</summary>
    /// <remarks>
    /// <para>
    /// 시간은 아래가 처음 도는 순간부터 잰다. 아래가 끝나면 시계를 지우므로 다음에 들어올 때
    /// 처음부터 다시 잰다.
    /// </para>
    /// <para>
    /// 시간이 다하면 <b>문을 닫는 것</b>으로 아래를 되돌린다. 문이 닫힐 때 돌던 자식을 되돌리는
    /// 일은 <see cref="DecoratorBehaviour"/>가 하므로 여기서 따로 되돌리지 않는다.
    /// </para>
    /// </remarks>
    public sealed class TimeLimitBehaviour : DecoratorBehaviour
    {
        private readonly float _limitSeconds;
        private readonly Func<float> _timeProvider;
        private float? _startTime;

        /// <summary>허용할 시간을 지정한다.</summary>
        /// <param name="limitSeconds">아래에 허용하는 시간(초)이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이며, 비우면 <see cref="Time.time"/>을 쓴다.</param>
        public TimeLimitBehaviour(float limitSeconds, Func<float> timeProvider = null)
        {
            _limitSeconds = Mathf.Max(0f, limitSeconds);
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <summary>아래에 허용하는 시간(초)이다.</summary>
        public float LimitSeconds => _limitSeconds;

        /// <inheritdoc />
        /// <remarks>허용한 시간을 꼭 채운 순간까지는 들어갈 수 있다. 그 시각에 끝나는 아래를 실패로 만들지 않는다.</remarks>
        protected override bool CanEnter(in BehaviourTickContext context)
        {
            if (!_startTime.HasValue || _timeProvider() - _startTime.Value <= _limitSeconds)
            {
                return true;
            }

            _startTime = null;
            return false;
        }

        /// <inheritdoc />
        protected override void OnBeforeChildTick(in BehaviourTickContext context) => _startTime ??= _timeProvider();

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
        {
            if (childStatus != BehaviourStatus.Running)
            {
                _startTime = null;
            }

            return childStatus;
        }

        /// <inheritdoc />
        protected override void OnReset() => _startTime = null;
    }

    /// <summary>문맥의 키에 적힌 시각이 지나기 전에는 아래를 실행하지 않고, 아래가 끝나면 그 시각을 미룬다.</summary>
    /// <remarks>
    /// <para>
    /// 쉬는 시각을 자기 안에 두지 않고 문맥에 둔다. 그래서 같은 키를 쓰는 자리들이 하나의 쉬는
    /// 시간을 나눠 쓴다. 다른 가지에서 같은 일을 했으면 여기서도 쉰다.
    /// </para>
    /// <para>
    /// 아직 쉬는 중에 아래가 다시 끝났을 때, 남은 시간에 더할지 지금부터 다시 셀지를 고른다.
    /// </para>
    /// </remarks>
    public sealed class ContextCooldownBehaviour : DecoratorBehaviour
    {
        private readonly string _key;
        private readonly float _cooldownSeconds;
        private readonly bool _addsToExistingDuration;
        private readonly Func<float> _timeProvider;

        /// <summary>쉬는 시각을 둘 키와 쉬는 시간을 지정한다.</summary>
        /// <param name="key">쉬는 시각을 둘 문맥 키이다.</param>
        /// <param name="cooldownSeconds">다시 실행하기까지 쉬는 시간(초)이다.</param>
        /// <param name="addsToExistingDuration">아직 쉬는 중이면 남은 시간에 더할지 여부이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이며, 비우면 <see cref="Time.time"/>을 쓴다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public ContextCooldownBehaviour(
            string key,
            float cooldownSeconds,
            bool addsToExistingDuration = false,
            Func<float> timeProvider = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", nameof(key));
            }

            _key = key;
            _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            _addsToExistingDuration = addsToExistingDuration;
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
            => !ContextValueReader.TryGetNumber(context.Context, _key, out var readyTime)
               || _timeProvider() >= readyTime;

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
        {
            if (childStatus == BehaviourStatus.Running)
            {
                return childStatus;
            }

            var now = _timeProvider();
            var from = now;
            if (_addsToExistingDuration
                && ContextValueReader.TryGetNumber(context.Context, _key, out var existing)
                && existing > now)
            {
                from = (float)existing;
            }

            context.Context.SetValue(_key, from + _cooldownSeconds);
            return childStatus;
        }
    }

    /// <summary>문맥의 키에 적힌 만큼의 시간이 지날 때까지 기다린다.</summary>
    /// <remarks>기다릴 시간은 시작하는 순간 한 번 읽는다. 값이 없거나 수가 아니면 실패한다.</remarks>
    public sealed class WaitContextTimeBehaviour : IBehaviour
    {
        private readonly string _key;
        private readonly Func<float> _timeProvider;
        private float? _startTime;
        private float _duration;

        /// <summary>기다릴 시간을 읽을 키를 지정한다.</summary>
        /// <param name="key">기다릴 시간(초)이 담긴 문맥 키이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이며, 비우면 <see cref="Time.time"/>을 쓴다.</param>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public WaitContextTimeBehaviour(string key, Func<float> timeProvider = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("키는 비어 있을 수 없습니다.", nameof(key));
            }

            _key = key;
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var now = _timeProvider();
            if (!_startTime.HasValue)
            {
                if (!ContextValueReader.TryGetNumber(context.Context, _key, out var seconds))
                {
                    return BehaviourStatus.Failure;
                }

                _duration = Mathf.Max(0f, (float)seconds);
                _startTime = now;
            }

            if (now - _startTime.Value < _duration)
            {
                return BehaviourStatus.Running;
            }

            Reset();
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        public void Reset() => _startTime = null;
    }
}
