using HS.Framework.AI.Behaviour;
using System;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Tactics.Foundation.Geometry;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 자리가 갈 수 있는 곳인지 판정하고, 갈 수 있으면 실제로 설 자리를 돌려준다.
    /// </summary>
    /// <param name="desiredPosition">서고 싶은 자리이다.</param>
    /// <param name="resolvedPosition">실제로 설 자리이며, 갈 수 없으면 값이 없다.</param>
    /// <returns>그 자리에 설 수 있으면 true이다.</returns>
    public delegate bool SlotReachabilityCheck(Vector3 desiredPosition, out Vector3 resolvedPosition);

    /// <summary>
    /// 대상 주위에 유닛별 자리를 나누어 추격한다.
    /// 슬롯 도달 판정은 주입된 수단으로 수행하고, 갈 수 있는 자리가 없으면 대상 위치를 목적지로 쓴다.
    /// 자기가 시작한 이동이 있을 때만 Reset에서 정지를 요청한다.
    /// </summary>
    public sealed class SpreadChaseTargetBehaviour : IBehaviour
    {
        private readonly ICharacterMover _mover;
        private readonly string _targetKey;
        private readonly ApproachSpreadSettings _settings;
        private readonly Func<int> _resolveSlotSeed;
        private readonly Func<PlanarPosition> _resolveReferenceDirection;
        private readonly SlotReachabilityCheck _isReachable;
        private bool _isDrivingMover;

        /// <summary>
        /// 자리를 나눠 다가가는 노드를 만든다.
        /// </summary>
        /// <param name="mover">이동을 맡을 구성요소이다.</param>
        /// <param name="targetKey">대상 Transform이 담긴 컨텍스트 키이다.</param>
        /// <param name="settings">자리를 어떻게 나눌지에 대한 설정이다.</param>
        /// <param name="resolveSlotSeed">이 유닛의 고정된 번호를 구하는 함수이다.</param>
        /// <param name="resolveReferenceDirection">반원을 그릴 기준 방향을 구하는 함수이다.</param>
        /// <param name="isReachable">자리에 갈 수 있는지 보는 방법이다.</param>
        /// <exception cref="ArgumentNullException">이동 구성요소나 함수가 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">대상 키가 비어 있으면 발생한다.</exception>
        public SpreadChaseTargetBehaviour(
            ICharacterMover mover,
            string targetKey,
            ApproachSpreadSettings settings,
            Func<int> resolveSlotSeed,
            Func<PlanarPosition> resolveReferenceDirection,
            SlotReachabilityCheck isReachable)
        {
            _mover = mover ?? throw new ArgumentNullException(nameof(mover));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없습니다.", nameof(targetKey));
            }

            _targetKey = targetKey;
            _settings = settings;
            _resolveSlotSeed = resolveSlotSeed ?? throw new ArgumentNullException(nameof(resolveSlotSeed));
            _resolveReferenceDirection = resolveReferenceDirection
                                         ?? throw new ArgumentNullException(nameof(resolveReferenceDirection));
            _isReachable = isReachable ?? throw new ArgumentNullException(nameof(isReachable));
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (!context.Context.TryGetValue<Transform>(_targetKey, out var target) || target == null)
            {
                return BehaviourStatus.Failure;
            }

            var destination = ResolveDestination(target.position);
            if (!_mover.MoveTo(destination))
            {
                return BehaviourStatus.Failure;
            }

            _isDrivingMover = true;
            return _mover.HasReachedDestination
                ? BehaviourStatus.Success
                : BehaviourStatus.Running;
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

        /// <summary>
        /// 이 유닛이 설 자리를 정한다. 갈 수 없는 자리는 정해진 차례로 넘기고, 끝내 없으면 적의 자리를 쓴다.
        /// </summary>
        /// <param name="targetPosition">다가갈 적의 세계 좌표이다.</param>
        /// <returns>이동 목적지로 쓸 세계 좌표이다.</returns>
        private Vector3 ResolveDestination(Vector3 targetPosition)
        {
            if (!_settings.IsEnabled)
            {
                return targetPosition;
            }

            var targetPlanar = PlanarPosition.FromWorld(targetPosition);
            var reference = _resolveReferenceDirection();
            var slotSeed = _resolveSlotSeed();

            // 한 바퀴를 다 돌면 처음 자리로 돌아오므로, 그 이상 시도하는 것은 같은 자리를 다시 보는 것이다.
            for (var attempt = 0; attempt < _settings.SlotCount; attempt++)
            {
                var slot = ApproachSlotResolver.ResolveSlotPosition(
                    targetPlanar,
                    reference,
                    _settings.Radius,
                    slotSeed,
                    _settings.SlotCount,
                    _settings.ArcDegrees,
                    attempt);

                if (_isReachable(slot.ToWorld(targetPosition.y), out var reachable))
                {
                    return reachable;
                }
            }

            return targetPosition;
        }
    }
}
