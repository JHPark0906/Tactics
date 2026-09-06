using System;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Behaviour
{
    /// <summary>가지가 도는 동안 적을 다시 찾아 문맥에 담는 서비스이다.</summary>
    /// <remarks>
    /// <para>
    /// 이 일을 컴포넌트가 자기 간격으로 하면 유닛이 무엇을 하고 있든 계속 찾아,
    /// 전진 중에만 찾으면 되는 자리에서도 엄폐 중에 찾는다.
    /// </para>
    /// <para>
    /// 찾는 방법 자체는 그대로 <see cref="EnemyDetector"/>가 안다. 여기서는 언제 찾을지와
    /// 찾은 것을 어디에 담을지만 정한다.
    /// </para>
    /// </remarks>
    public sealed class DetectEnemyService : ServiceBehaviour
    {
        private readonly EnemyDetector _detector;
        private readonly string _targetKey;

        /// <summary>탐지기와 담을 자리를 지정한다.</summary>
        /// <param name="detector">적을 찾는 것이다.</param>
        /// <param name="interval">다시 찾는 간격(초)이다.</param>
        /// <param name="targetKey">찾은 대상을 담을 문맥 키이다.</param>
        /// <param name="timeProvider">지금 시각을 주는 것이다.</param>
        public DetectEnemyService(
            EnemyDetector detector,
            float interval,
            string targetKey = UnitBehaviourKeys.Target,
            Func<float> timeProvider = null)
            : base(interval, timeProvider)
        {
            _detector = detector != null ? detector : throw new ArgumentNullException(nameof(detector));
            if (string.IsNullOrWhiteSpace(targetKey))
            {
                throw new ArgumentException("대상 키는 비어 있을 수 없다.", nameof(targetKey));
            }

            _targetKey = targetKey;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 못 찾았으면 담긴 것을 지운다. 남겨 두면 이미 없는 적을 계속 겨눈다.
        /// 지우는 것도 알림이 나가므로, 그것을 지켜보던 자리가 그 자리에서 되돌아간다.
        /// </remarks>
        protected override void Execute(in BehaviourTickContext context)
        {
            var target = _detector.RefreshTarget();
            if (target != null)
            {
                context.Context.SetValue(_targetKey, target);
            }
            else
            {
                context.Context.RemoveValue(_targetKey);
            }
        }
    }

    /// <summary>가지가 도는 동안 전진 방향을 다시 셈해 문맥에 담는 서비스이다.</summary>
    /// <remarks>
    /// <para>
    /// 목표 좌표 자체는 <see cref="IAdvanceTargetSource"/>가 공급한다. 기본은 씬의 <see cref="BattleLane"/>이며,
    /// 비선형 맵으로 확장할 때는 생성자로 다른 구현을 넣으면 이 서비스도 전진 노드도 행동 트리도 고칠 필요가 없다.
    /// </para>
    /// <para>
    /// 방향을 정할 두 가지 예외를 구분해서 다룬다. 목표를 구하지 못하면 키를 건드리지 않아 전진 노드가
    /// 유닛의 정면을 쓰는 기존 폴백으로 돌아가고, 목표에 도달했으면 <see cref="Vector3.zero"/>를 넣어
    /// 전진 분기가 실패하게 한다. 후자는 레인 끝에서 유닛이 진동하지 않게 하려는 것이다.
    /// </para>
    /// <para>
    /// 값이 실제로 바뀌었을 때만 컨텍스트에 기록한다. 컨텍스트가 값을 object로 담아 Vector3를 넣을 때마다
    /// 박싱이 일어나기 때문이며, 유닛이 많아도 갱신마다 쓰레기가 쌓이지 않게 하려는 것이다.
    /// </para>
    /// <para>
    /// <see cref="RefreshAdvanceDirection"/>은 문맥을 인자로 받아, 트리 없이도 직접 불러 검증할 수 있다.
    /// 가지가 도는 동안의 실제 호출은 <see cref="Execute"/>가 간격마다 대신한다.
    /// </para>
    /// </remarks>
    public sealed class RefreshAdvanceDirectionService : ServiceBehaviour
    {
        /// <summary>이 값보다 방향 변화가 작으면 같은 방향으로 보고 다시 기록하지 않는다.</summary>
        private const float DirectionChangeThresholdSquared = 1e-6f;

        private readonly Transform _self;
        private readonly TeamMember _team;
        private readonly float _arrivalDistance;

        private IAdvanceTargetSource _targetSource;
        private Vector3 _currentDirection;
        private bool _hasDirection;
        private bool _hasWarnedAboutMissingSource;

        /// <summary>전진 방향을 한 번이라도 컨텍스트에 기록했는지 여부이다.</summary>
        public bool HasDirection => _hasDirection;

        /// <summary>거리를 재고 진영을 읽을 유닛, 도달 판정 거리와 간격을 지정한다.</summary>
        /// <param name="self">거리를 잴 기준이 되는 유닛의 Transform이다.</param>
        /// <param name="team">진영을 읽을 구성요소이다.</param>
        /// <param name="arrivalDistance">목표에 이만큼 가까워지면 도달로 보고 더 전진하지 않는다(미터).</param>
        /// <param name="interval">다시 셈하는 간격(초)이다.</param>
        /// <param name="targetSource">
        /// 목표 좌표를 공급할 원본이며, 비워 두면 첫 사용 때 씬에 배치된 <see cref="BattleLane"/>을 찾는다.
        /// 비선형 맵으로 확장할 때 갈아 끼우는 지점이다.
        /// </param>
        /// <param name="timeProvider">지금 시각을 주는 것이다.</param>
        /// <exception cref="ArgumentNullException">유닛 Transform이나 진영 구성요소가 null이면 발생한다.</exception>
        public RefreshAdvanceDirectionService(
            Transform self,
            TeamMember team,
            float arrivalDistance,
            float interval,
            IAdvanceTargetSource targetSource = null,
            Func<float> timeProvider = null)
            : base(interval, timeProvider)
        {
            _self = self != null ? self : throw new ArgumentNullException(nameof(self));
            _team = team != null ? team : throw new ArgumentNullException(nameof(team));
            _arrivalDistance = Mathf.Max(0f, arrivalDistance);
            _targetSource = targetSource;
        }

        /// <summary>
        /// 지금 위치를 기준으로 전진 방향을 다시 계산해 문맥에 반영한다.
        /// 행동 트리의 전진 가지가 <see cref="Execute"/>를 통해 부르며, 그 밖에 즉시 반영이 필요한 쪽도 직접 부를 수 있다.
        /// </summary>
        /// <remarks>
        /// 진영이 아직 없으면 목표를 구할 수 없으므로 키를 건드리지 않는다. 그동안 전진 노드는 유닛의
        /// 정면을 쓰고, 진영이 정해진 뒤의 첫 호출이 방향을 기록한다.
        /// </remarks>
        /// <param name="context">방향을 기록할 문맥이다.</param>
        public void RefreshAdvanceDirection(IBehaviourContext context)
        {
            var source = ResolveTargetSource();
            if (source == null)
            {
                WarnAboutMissingSourceOnce();
                return;
            }

            var position = _self.position;
            if (!source.TryGetAdvanceTarget(_team.TeamId, position, out var target))
            {
                return;
            }

            WriteDirection(context, LaneAdvanceCalculator.GetAdvanceDirection(position, target, _arrivalDistance));
        }

        /// <inheritdoc />
        /// <remarks>실제 계산과 기록은 <see cref="RefreshAdvanceDirection"/>이 하므로 여기서는 문맥만 넘긴다.</remarks>
        protected override void Execute(in BehaviourTickContext context) => RefreshAdvanceDirection(context.Context);

        /// <summary>
        /// 주입된 원본이 없으면 씬의 레인을 찾아 목표 공급 원본으로 삼는다.
        /// 맵을 갈아 끼우면서 원본이 파괴되었으면 붙들고 있던 참조를 버리고 다시 찾는다.
        /// 인터페이스 참조는 파괴된 Unity 객체를 null로 보지 않으므로 형식을 확인해 직접 걸러 낸다.
        /// </summary>
        private IAdvanceTargetSource ResolveTargetSource()
        {
            if (_targetSource is UnityEngine.Object sourceObject && sourceObject == null)
            {
                _targetSource = null;
            }

            if (_targetSource != null)
            {
                return _targetSource;
            }

            var lane = BattleLane.Active;
            if (lane != null)
            {
                _targetSource = lane;
            }

            return _targetSource;
        }

        /// <summary>계산한 방향이 이전과 다를 때만 컨텍스트에 기록한다.</summary>
        private void WriteDirection(IBehaviourContext context, Vector3 direction)
        {
            if (_hasDirection
                && (direction - _currentDirection).sqrMagnitude <= DirectionChangeThresholdSquared)
            {
                return;
            }

            context.SetValue(UnitBehaviourKeys.AdvanceDirection, direction);
            _currentDirection = direction;
            _hasDirection = true;
        }

        /// <summary>레인을 찾지 못했다고 한 번만 알린다. 매 갱신마다 같은 경고가 쌓이지 않게 한다.</summary>
        private void WarnAboutMissingSourceOnce()
        {
            if (_hasWarnedAboutMissingSource)
            {
                return;
            }

            _hasWarnedAboutMissingSource = true;
            Debug.LogWarning(
                $"[RefreshAdvanceDirectionService] 씬에 BattleLane이 없어 {_self.name}이 전진 방향을 정하지 못한다.",
                _self);
        }
    }
}
