using HS.Framework.Ability.Abilities;
using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Units;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 교전할 수 있는 엄폐 후보를 선택하고 예약한 뒤 그 자리로 이동한다.
    /// 도착하면 확보를 마치며, 종료할 때 자신이 시작한 이동을 정리한다.
    /// 도착하지 못한 예약은 해제하고, 도착한 예약의 유지는 별도 어빌리티가 맡는다.
    /// </summary>
    public sealed class TakeCoverAbility : GameplayAbility
    {
        /// <summary>엄폐 후보를 찾는 센서이다.</summary>
        private CoverSensor _sensor;

        /// <summary>이 유닛의 엄폐 상태이다.</summary>
        private UnitCoverState _coverState;

        /// <summary>대상을 알려 주는 공급자이다.</summary>
        private ICombatTargetSource _targetSource;

        /// <summary>엄폐 지점으로 이동시킬 이동 구성요소이다.</summary>
        private ICharacterMover _mover;

        /// <summary>이 유닛의 공격 사거리이다.</summary>
        private float _attackRange;

        /// <summary>활성화 판정에서 찾아 둔 엄폐 후보이다.</summary>
        private CoverPoint _candidate;

        /// <summary>이번 활성화에서 이동을 시작했는지 여부이다.</summary>
        private bool _hasStartedMove;

        /// <summary>마지막으로 예약해 이동한 엄폐 지점이며, 없으면 null이다.</summary>
        public CoverPoint ClaimedCoverPoint => _coverState != null ? _coverState.ClaimedCover : null;

        /// <inheritdoc />
        /// <remarks>
        /// 지금 새 엄폐를 확보할 이유가 있는지 확인한다.
        /// 이미 위협에 대해 엄폐가 되어 있으면 확보할 이유가 없으므로 거부해 다음 우선순위로 넘긴다.
        /// </remarks>
        public override bool CanActivate()
        {
            _candidate = null;
            if (!TryResolveOwner() || _attackRange <= 0f)
            {
                return false;
            }

            var target = _targetSource.CurrentTarget;
            if (target == null)
            {
                return false;
            }

            var threatPosition = target.position;
            if (_coverState.IsInCover)
            {
                return false;
            }

            if (!_sensor.TryFindCover(threatPosition, out var coverPoint, _attackRange))
            {
                return false;
            }

            _candidate = coverPoint;
            return true;
        }

        /// <inheritdoc />
        /// <remarks>찾아 둔 지점을 예약하고 그 자리로 이동을 시작한다.</remarks>
        protected override void OnActivate()
        {
            _hasStartedMove = false;
            if (_candidate == null || !_coverState.ClaimCover(_candidate))
            {
                return;
            }

            _hasStartedMove = _mover.MoveTo(_candidate.Position);
        }

        /// <inheritdoc />
        /// <remarks>
        /// 도착할 때까지 이어 가되, 매 틱 이동이 아직 성립하는지 다시 본다.
        /// 예약을 못 했거나 이동을 시작하지 못했으면 그 자리에서 끝내 다음 우선순위에 길을 넘긴다.
        /// </remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            if (!_hasStartedMove || _mover == null || _coverState == null || !_coverState.HasCoverClaim)
            {
                return GameplayAbilityTickResult.Finished;
            }

            return _mover.HasReachedDestination
                ? GameplayAbilityTickResult.Finished
                : GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 아직 자리에 들어가지 못한 예약만 놓는다. 이미 엄폐에 서 있으면 그 자리는 유닛이 쓰고 있으므로 유지한다.
        /// 스스로 끝났든 가로채여 취소되었든 같은 규칙을 쓴다. 자신이 시작한 이동은 어느 종료 경로든 멈춘다.
        /// </remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            var hasStartedMove = _hasStartedMove;
            _hasStartedMove = false;
            _candidate = null;
            if (hasStartedMove)
            {
                _mover.Stop();
            }

            if (_coverState != null && _coverState.HasCoverClaim && !_coverState.IsInCover)
            {
                _coverState.ReleaseCover();
            }
        }

        /// <summary>소유 액터에서 엄폐 확보에 필요한 구성요소와 수치를 찾는다.</summary>
        /// <returns>확보를 시도할 수 있는 구성이 갖춰져 있으면 true이다.</returns>
        private bool TryResolveOwner()
        {
            var owner = System != null ? System.Owner : null;
            if (owner == null)
            {
                return false;
            }

            if (_sensor == null)
            {
                _sensor = owner.GetComponent<CoverSensor>();
                _coverState = owner.GetComponent<UnitCoverState>();
                _targetSource = owner.GetComponent<ICombatTargetSource>();
                _mover = owner.GetComponent<ICharacterMover>();
            }

            if (_sensor == null || _coverState == null || _targetSource == null || _mover == null)
            {
                return false;
            }

            // 사거리를 모르면 쏠 수 있는 자리인지 가릴 수 없다. 그때는 엄폐를 아예 쓰지 않는다.
            var definition = owner.GetComponent<TacticalUnit>()?.Definition;
            _attackRange = definition != null ? definition.AttackRange : 0f;
            return true;
        }
    }
}
