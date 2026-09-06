using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Framework.AI.Behaviour
{
    /// <summary>문맥가 가리키는 좌표로 걸어간다.</summary>
    /// <remarks>
    /// <para>어디로 갈지는 정하지 않는다. 그 자리를 채우는 일은 다른 자리가 한다.</para>
    /// <para>
    /// <b>자기가 낸 이동 요청이 살아 있을 때만 멈춘다.</b> 이동 수단은 유닛에 하나뿐이라 트리의
    /// 이동 자리들이 그것을 나눠 쓴다. 문이 닫히거나 병렬이 끝날 때 트리는 그 아래를 통째로 되돌리므로
    /// 시작하지 않은 자리도 되돌려지는데, 아무것도 몰지 않던 자리가 멈추라고 하면 다른 자리가 몰던
    /// 이동이 끊긴다.
    /// </para>
    /// </remarks>
    public sealed class MoveToPositionBehaviour : IBehaviour
    {
        private readonly ICharacterMover _mover;
        private readonly string _destinationKey;
        private bool _isDrivingMover;

        /// <summary>이동 수단과 목표를 읽어 올 키를 지정한다.</summary>
        /// <param name="mover">유닛을 움직이는 것이다.</param>
        /// <param name="destinationKey">목표 좌표가 담긴 문맥 키이다.</param>
        public MoveToPositionBehaviour(ICharacterMover mover, string destinationKey)
        {
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            if (string.IsNullOrWhiteSpace(destinationKey))
            {
                throw new ArgumentException("목표 좌표 키는 비어 있을 수 없습니다.", nameof(destinationKey));
            }

            _destinationKey = destinationKey;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<Vector3>(_destinationKey, out var destination))
            {
                return BehaviourStatus.Failure;
            }

            if (!_mover.MoveTo(destination))
            {
                return BehaviourStatus.Failure;
            }

            _isDrivingMover = true;
            return _mover.HasReachedDestination ? BehaviourStatus.Success : BehaviourStatus.Running;
        }

        /// <inheritdoc />
        /// <remarks>이 자리가 낸 이동 요청이 없으면 멈출 것도 없다.</remarks>
        public void Reset()
        {
            if (!_isDrivingMover)
            {
                return;
            }

            _isDrivingMover = false;
            _mover.Stop();
        }
    }

    /// <summary>문맥가 가리키는 대상을 쫓아간다.</summary>
    /// <remarks>
    /// 대상이 움직이므로 매 틱 지금 자리를 다시 목표로 삼는다. 멈추는 규칙은
    /// <see cref="MoveToPositionBehaviour"/>와 같아서, 자기가 낸 이동 요청이 살아 있을 때만 멈춘다.
    /// </remarks>
    public sealed class ChaseTargetBehaviour : IBehaviour
    {
        private readonly ICharacterMover _mover;
        private readonly string _targetKey;
        private bool _isDrivingMover;

        /// <summary>이동 수단과 대상을 읽어 올 키를 지정한다.</summary>
        /// <param name="mover">유닛을 움직이는 것이다.</param>
        /// <param name="targetKey">쫓을 대상이 담긴 문맥 키이다.</param>
        public ChaseTargetBehaviour(ICharacterMover mover, string targetKey)
        {
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없습니다.", nameof(targetKey));
            }

            _targetKey = targetKey;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<Transform>(_targetKey, out var target) || target == null)
            {
                return BehaviourStatus.Failure;
            }

            if (!_mover.MoveTo(target.position))
            {
                return BehaviourStatus.Failure;
            }

            _isDrivingMover = true;
            return _mover.HasReachedDestination ? BehaviourStatus.Success : BehaviourStatus.Running;
        }

        /// <inheritdoc />
        /// <remarks>이 자리가 낸 이동 요청이 없으면 멈출 것도 없다.</remarks>
        public void Reset()
        {
            if (!_isDrivingMover)
            {
                return;
            }

            _isDrivingMover = false;
            _mover.Stop();
        }
    }

    /// <summary>정해진 시간이 지날 때까지 기다린다.</summary>
    public sealed class WaitBehaviour : IBehaviour
    {
        private readonly float _duration;
        private readonly Func<float> _timeProvider;
        private float? _startTime;

        /// <summary>기다릴 시간을 지정한다.</summary>
        /// <param name="duration">기다릴 시간(초)이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이며, 비우면 <see cref="Time.time"/>을 쓴다.</param>
        public WaitBehaviour(float duration, Func<float> timeProvider = null)
        {
            _duration = Mathf.Max(0f, duration);
            _timeProvider = timeProvider ?? (static () => Time.time);
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            var now = _timeProvider();
            _startTime ??= now;

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
