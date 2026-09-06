using HS.Framework.AI.Behaviour;
using System;
using HS.Framework.AI.BehaviourTree;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 적을 기준으로 쓸 수 있는 엄폐 지점을 골라 확보하고 그 좌표를 컨텍스트에 기록하는 노드이다.
    /// 이동은 하지 않으며 뒤따르는 이동 노드가 이 좌표를 읽어 움직인다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이미 유효한 엄폐 중이면 실패를 반환한다. 이 노드는 엄폐를 새로 확보하는 일만 맡고,
    /// 확보한 엄폐를 유지하는 판단은 <see cref="MaintainCoverBehaviour"/>가 담당한다.
    /// </para>
    /// <para>
    /// <b>확보와 해제의 짝.</b> 확보는 다른 유닛이 같은 지점을 고르지 못하게 하는 예약이므로,
    /// 그 지점에 자리 잡지 못한 채 분기가 끝나면 반드시 풀어야 한다. 예약한 노드가 해제까지 책임지도록
    /// <see cref="Reset"/>에서 아직 도착하지 않은 예약을 놓아 준다. 그래서 엄폐로 이동하던 중
    /// 교전 같은 상위 우선순위에 가로채여도 아무도 쓰지 않는 지점이 영구 점유되지 않는다.
    /// </para>
    /// </remarks>
    public sealed class SelectCoverDestinationBehaviour : IBehaviour
    {
        private readonly CoverSensor _sensor;
        private readonly UnitCoverState _coverState;
        private readonly float _attackRange;
        private readonly string _targetKey;
        private readonly string _destinationKey;

        /// <summary>엄폐 지점을 고를 노드를 생성한다.</summary>
        /// <param name="sensor">엄폐 지점을 찾을 센서이다.</param>
        /// <param name="coverState">확보 상태를 기록할 컴포넌트이다.</param>
        /// <param name="attackRange">
        /// 이 유닛의 공격 사거리이며, 그 자리에서 위협을 쏠 수 있는 엄폐만 고르는 데 쓴다.
        /// 0 이하이면 쏠 수 없는 유닛으로 보고 엄폐를 고르지 않는다.
        /// </param>
        /// <param name="targetKey">위협 대상을 읽을 컨텍스트 키이다.</param>
        /// <param name="destinationKey">고른 좌표를 기록할 컨텍스트 키이다.</param>
        /// <exception cref="ArgumentNullException">센서나 엄폐 상태가 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public SelectCoverDestinationBehaviour(
            CoverSensor sensor,
            UnitCoverState coverState,
            float attackRange = float.PositiveInfinity,
            string targetKey = UnitBehaviourKeys.Target,
            string destinationKey = UnitBehaviourKeys.Destination)
        {
            _sensor = sensor != null ? sensor : throw new ArgumentNullException(nameof(sensor));
            _coverState = coverState != null ? coverState : throw new ArgumentNullException(nameof(coverState));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없다.", nameof(targetKey));
            }

            if (string.IsNullOrWhiteSpace(destinationKey))
            {
                throw new ArgumentException("목표 좌표 키는 비어 있을 수 없다.", nameof(destinationKey));
            }

            _attackRange = attackRange;
            _targetKey = targetKey;
            _destinationKey = destinationKey;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (_sensor == null || _coverState == null)
            {
                return BehaviourStatus.Failure;
            }

            // 쏠 수 없는 유닛은 엄폐에 앉아 자리만 차지하므로 아예 고르지 않는다.
            if (_attackRange <= 0f)
            {
                return BehaviourStatus.Failure;
            }

            if (!context.Context.TryGetValue<Transform>(_targetKey, out var target) || target == null)
            {
                return BehaviourStatus.Failure;
            }

            var threatPosition = target.position;
            if (_coverState.IsInCover)
            {
                return BehaviourStatus.Failure;
            }

            if (!_sensor.TryFindCover(threatPosition, out var coverPoint, _attackRange)
                || !_coverState.ClaimCover(coverPoint))
            {
                return BehaviourStatus.Failure;
            }

            context.Context.SetValue(_destinationKey, coverPoint.Position);
            return BehaviourStatus.Success;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 아직 엄폐 지점에 자리 잡지 못한 예약만 놓아 준다.
        /// 이미 그 자리에서 엄폐 중이면 유닛이 실제로 쓰고 있는 것이므로 예약을 유지한다.
        /// </remarks>
        public void Reset()
        {
            if (_coverState != null && _coverState.HasCoverClaim && !_coverState.IsInCover)
            {
                _coverState.ReleaseCover();
            }
        }
    }

    /// <summary>
    /// 이미 확보한 엄폐가 지금 적에게 유효한지 확인하는 노드이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 유효하면 실행 중을 반환한다. 엄폐를 지키는 것은 한 틱에 끝나는 일이 아니라
    /// 계속되는 상태이므로 실행 중이 그 뜻에 맞는다. 루트가 매 틱 상위 우선순위부터 다시 평가하는
    /// 선택 노드라, 실행 중을 반환해도 교전 같은 상위 분기가 굶지 않는다.
    /// 성공을 돌려주면 상위 선택 노드가 이 분기를 "완료"로 보고 매 틱 하위 노드를 초기화하는데,
    /// 그러면 엄폐를 유지하는 동안에도 이동 중단과 예약 점검이 헛되이 반복된다.
    /// </para>
    /// <para>
    /// <b>유지에는 종료 조건이 있다.</b> 대상이 사거리 밖이면 유지를 실패시킨다.
    /// 엄폐는 쏠 수 있을 때 의미가 있고, 쏠 수 없는데 앉아 있는 것은 엄폐가 아니라 정지이기 때문이다.
    /// 이 조건이 없으면 양측이 서로를 발견하고도 각자 사거리 밖 엄폐에 앉아 아무도 쏘지 않는
    /// 영구 교착이 생긴다. 유지가 실패하면 아래 우선순위인 접근이 실행되어 거리를 좁히고,
    /// 사거리에 들어가면 교전이 잡아 사격이 시작되므로 전투가 반드시 끝난다.
    /// </para>
    /// <para>
    /// <b>포기한 자리는 놓아 준다.</b> 유지를 그만두는 순간 그 엄폐 지점을 해제한다.
    /// 확보는 다른 유닛이 같은 자리를 고르지 못하게 하는 예약이므로, 떠나면서 쥐고 있으면
    /// 아무도 쓰지 않는 자리가 영구 점유된다. 아직 도착하지 않은 예약은
    /// <see cref="SelectCoverDestinationBehaviour.Reset"/>이 풀지만, 도착해서 쓰다가 떠나는 경우는
    /// 그 경로로 풀리지 않으므로(실패한 분기는 초기화를 받지 않는다) 여기서 직접 놓아 준다.
    /// </para>
    /// </remarks>
    public sealed class MaintainCoverBehaviour : IBehaviour
    {
        private readonly UnitCoverState _coverState;
        private readonly float _attackRange;
        private readonly string _targetKey;

        /// <summary>엄폐 유지 여부를 판단할 노드를 생성한다.</summary>
        /// <param name="coverState">확인할 엄폐 상태 컴포넌트이다.</param>
        /// <param name="attackRange">
        /// 이 유닛의 공격 사거리이며, 대상이 이 거리 안에 있을 때만 엄폐를 유지한다.
        /// 0 이하이면 쏠 수 없는 유닛으로 보고 유지하지 않는다.
        /// </param>
        /// <param name="targetKey">위협 대상을 읽을 컨텍스트 키이다.</param>
        /// <exception cref="ArgumentNullException">엄폐 상태가 null이면 발생한다.</exception>
        /// <exception cref="ArgumentException">키가 비어 있으면 발생한다.</exception>
        public MaintainCoverBehaviour(
            UnitCoverState coverState,
            float attackRange = float.PositiveInfinity,
            string targetKey = UnitBehaviourKeys.Target)
        {
            _coverState = coverState != null ? coverState : throw new ArgumentNullException(nameof(coverState));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없다.", nameof(targetKey));
            }

            _attackRange = attackRange;
            _targetKey = targetKey;
        }

        /// <inheritdoc />
        public BehaviourStatus Tick(in BehaviourTickContext context)
        {
            if (_coverState == null
                || !context.Context.TryGetValue<Transform>(_targetKey, out var target)
                || target == null)
            {
                ReleaseAbandonedCover();
                return BehaviourStatus.Failure;
            }

            if (!_coverState.IsInCover)
            {
                ReleaseAbandonedCover();
                return BehaviourStatus.Failure;
            }

            // 쏠 수 없는 거리에서 앉아 있으면 교착이 되므로 자리를 내주고 접근에 길을 넘긴다.
            if (Vector3.Distance(_coverState.transform.position, target.position) > _attackRange)
            {
                ReleaseAbandonedCover();
                return BehaviourStatus.Failure;
            }

            return BehaviourStatus.Running;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 상위 우선순위에 가로채인 것뿐일 수도 있으므로 여기서는 예약을 풀지 않는다.
        /// 교전이 이 유닛을 데려가는 동안에도 자리는 계속 이 유닛의 것이어야 하며,
        /// 실제로 자리를 포기하는 판단은 <see cref="Tick"/>이 내린다.
        /// </remarks>
        public void Reset()
        {
        }

        /// <summary>
        /// 더는 쓰지 않기로 한 엄폐 예약을 놓아 준다.
        /// </summary>
        private void ReleaseAbandonedCover()
        {
            if (_coverState != null && _coverState.HasCoverClaim)
            {
                _coverState.ReleaseCover();
            }
        }
    }
}
