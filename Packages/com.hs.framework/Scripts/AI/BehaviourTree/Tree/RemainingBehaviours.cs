using System;
using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>자식을 몇 갈래로 함께 돌릴 때의 성공 기준이다.</summary>
    public enum ParallelPolicy
    {
        /// <summary>하나라도 성공하면 성공이다.</summary>
        RequireOne,

        /// <summary>모두 성공해야 성공이다.</summary>
        RequireAll
    }

    /// <summary>자식을 한 번에 모두 돌린다.</summary>
    /// <remarks>
    /// <para>
    /// 자식마다 마지막 결과를 기억한다. 끝난 자식은 다시 돌리지 않는다. 그러지 않으면 이미 끝난
    /// 일이 다른 자식이 도는 동안 되풀이된다.
    /// </para>
    /// <para>
    /// <b>일찍 끝날 때 도는 중이던 자식을 되돌린다.</b> 하나가 실패해 전체가 실패하거나 하나가
    /// 성공해 전체가 성공할 때, 아직 돌고 있던 형제는 아무도 끝내 주지 않는다. 되돌리지 않으면
    /// 그 형제가 잡아 둔 것이 그대로 남는다.
    /// </para>
    /// </remarks>
    public sealed class ParallelBehaviour : IBehaviour
    {
        private readonly ParallelPolicy _successPolicy;

        /// <summary>자식마다 마지막 결과이며, 아직 한 번도 돌지 않았으면 null이다.</summary>
        private readonly List<BehaviourStatus?> _lastStatuses = new();

        /// <summary>성공 기준을 지정한다.</summary>
        /// <param name="successPolicy">언제 성공으로 볼지이다.</param>
        public ParallelBehaviour(ParallelPolicy successPolicy = ParallelPolicy.RequireAll)
        {
            _successPolicy = successPolicy;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var children = context.Children;
            while (_lastStatuses.Count < children.Count)
            {
                _lastStatuses.Add(null);
            }

            var anySucceeded = false;
            var anyRunning = false;

            for (var index = 0; index < children.Count; index++)
            {
                var status = TickUnlessFinished(context, index);
                if (status == BehaviourStatus.Running)
                {
                    anyRunning = true;
                    continue;
                }

                if (status == BehaviourStatus.Success)
                {
                    anySucceeded = true;
                    continue;
                }

                if (_successPolicy == ParallelPolicy.RequireAll)
                {
                    return Finish(context, BehaviourStatus.Failure);
                }
            }

            if (_successPolicy == ParallelPolicy.RequireOne && anySucceeded)
            {
                return Finish(context, BehaviourStatus.Success);
            }

            if (anyRunning)
            {
                return BehaviourStatus.Running;
            }

            return Finish(context, anySucceeded ? BehaviourStatus.Success : BehaviourStatus.Failure);
        }

        /// <summary>그 자식이 아직 끝나지 않았으면 한 번 돌리고, 끝났으면 기억한 결과를 돌려준다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <param name="index">자식의 번호이다.</param>
        /// <returns>그 자식의 결과이다.</returns>
        private BehaviourStatus TickUnlessFinished(in BehaviourTickContext context, int index)
        {
            var last = _lastStatuses[index];
            if (last == BehaviourStatus.Success || last == BehaviourStatus.Failure)
            {
                return last.Value;
            }

            var status = context.TickChild(context.Children[index]);
            _lastStatuses[index] = status;
            return status;
        }

        /// <summary>도는 중이던 자식을 되돌리고 기억을 비운 뒤 결과를 돌려준다.</summary>
        /// <param name="context">이 자리의 실행 문맥이다.</param>
        /// <param name="status">이 자리의 결과이다.</param>
        /// <returns>넘겨받은 결과 그대로이다.</returns>
        private BehaviourStatus Finish(in BehaviourTickContext context, BehaviourStatus status)
        {
            var children = context.Children;
            for (var index = 0; index < _lastStatuses.Count && index < children.Count; index++)
            {
                if (_lastStatuses[index] == BehaviourStatus.Running)
                {
                    context.Tree.Reset(children[index]);
                }
            }

            _lastStatuses.Clear();
            return status;
        }

        /// <inheritdoc />
        public void Reset() => _lastStatuses.Clear();
    }

    /// <summary>한 번 끝나면 정해진 시간 동안 아래를 다시 실행하지 않는다.</summary>
    /// <remarks>
    /// <b>되돌려져도 쉬는 시각은 그대로다.</b> 쉬는 시각은 이 자리의 진행이 아니라 아래가 끝났다는
    /// 결과이다. 되돌릴 때 함께 지우면, 끝날 때마다 아래를 되돌리는 되풀이 자리 아래에서 쉬는 시간이
    /// 언제나 0이 된다.
    /// </remarks>
    public sealed class CooldownBehaviour : DecoratorBehaviour
    {
        private readonly float _cooldownSeconds;
        private readonly Func<float> _timeProvider;
        private float? _nextReadyTime;

        /// <summary>쉬어야 할 시간을 지정한다.</summary>
        /// <param name="cooldownSeconds">다시 실행하기까지 쉬는 시간(초)이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이다.</param>
        public CooldownBehaviour(float cooldownSeconds, Func<float> timeProvider = null)
        {
            _cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
            => !_nextReadyTime.HasValue || _timeProvider() >= _nextReadyTime.Value;

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
        {
            if (childStatus != BehaviourStatus.Running)
            {
                _nextReadyTime = _timeProvider() + _cooldownSeconds;
            }

            return childStatus;
        }
    }

    /// <summary>문맥이 가리키는 대상이 보일 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// 판정은 노드 스스로 <see cref="FieldOfViewSensor"/>로 계산한다. 유닛에서 감지 구성요소를 찾지 않으므로
    /// 그런 구성요소가 없어도 선다 — 시야각·거리·가림 레이어는 이 자리를 만드는 정의가 값으로 들고 있다.
    /// </remarks>
    public sealed class CanSeeTargetBehaviour : DecoratorBehaviour
    {
        private readonly FieldOfViewSensor _sensor;
        private readonly Transform _observer;
        private readonly string _targetKey;

        /// <summary>시야 판정에 쓸 것들을 지정한다.</summary>
        /// <param name="sensor">시야각·거리·가림을 판정하는 계산이다.</param>
        /// <param name="observer">보는 쪽의 자리이다.</param>
        /// <param name="targetKey">대상이 담긴 문맥 키이다.</param>
        public CanSeeTargetBehaviour(FieldOfViewSensor sensor, Transform observer, string targetKey)
        {
            _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
            _observer = observer != null ? observer : throw new ArgumentNullException(nameof(observer));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없습니다.", nameof(targetKey));
            }

            _targetKey = targetKey;
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
            => context.Context.TryGetValue<Transform>(_targetKey, out var target)
               && target != null
               && _sensor.CanSee(_observer, target);

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;
    }

    /// <summary>문맥에 담긴 소리 자극이 청취 범위 안일 때만 아래를 실행하게 한다.</summary>
    /// <remarks>
    /// <para>
    /// 판정은 노드 스스로 한다. 소리를 알린 쪽이 <c>stimulusKey</c> 자리에 <see cref="SoundStimulus"/>를
    /// 담아 두면, 이 자리가 자기 위치에서 청취 반경 안인지를 직접 잰다. 유닛에서 감지 구성요소를 찾지
    /// 않으므로 그런 구성요소가 없어도 선다.
    /// </para>
    /// <para>범위 안이면 통과하고 값은 그대로 문맥에 남아 아래가 바로 쓸 수 있다.</para>
    /// </remarks>
    public sealed class HeardSoundBehaviour : DecoratorBehaviour
    {
        private readonly Transform _self;
        private readonly float _hearingRange;
        private readonly string _stimulusKey;
        private readonly bool _consumeOnSuccess;

        /// <summary>소리 감지에 쓸 것들을 지정한다.</summary>
        /// <param name="self">듣는 쪽의 자리이다.</param>
        /// <param name="hearingRange">이 자리가 들을 수 있는 최대 반경(미터)이다.</param>
        /// <param name="stimulusKey">소리 자극이 담긴 문맥 키이다.</param>
        /// <param name="consumeOnSuccess">통과한 뒤 그 소리를 지울지 여부이다.</param>
        public HeardSoundBehaviour(Transform self, float hearingRange, string stimulusKey, bool consumeOnSuccess = false)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _hearingRange = Mathf.Max(0f, hearingRange);
            if (string.IsNullOrWhiteSpace(stimulusKey))
            {
                throw new ArgumentException("자극 키는 비어 있을 수 없습니다.", nameof(stimulusKey));
            }

            _stimulusKey = stimulusKey;
            _consumeOnSuccess = consumeOnSuccess;
        }

        /// <inheritdoc />
        protected override bool CanEnter(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<SoundStimulus>(_stimulusKey, out var stimulus))
            {
                return false;
            }

            var effectiveRange = Mathf.Min(_hearingRange, Mathf.Max(0f, stimulus.Range));
            if (Vector3.Distance(_self.position, stimulus.Position) > effectiveRange)
            {
                return false;
            }

            if (_consumeOnSuccess)
            {
                context.Context.RemoveValue(_stimulusKey);
            }

            return true;
        }

        /// <inheritdoc />
        protected override BehaviourStatus Decorate(BehaviourStatus childStatus, in BehaviourTickContext context)
            => childStatus;
    }
}
