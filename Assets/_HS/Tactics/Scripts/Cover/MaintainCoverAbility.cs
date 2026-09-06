using HS.Framework.Ability.Abilities;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Cover
{
    /// <summary>
    /// 확보한 엄폐를 유지하면서 위협과 사거리 조건을 확인한다.
    /// 위협이 없어지거나 확보한 자리에서 교전할 수 없으면 예약을 해제하고 종료한다.
    /// 엄폐 확보와 이동은 TakeCoverAbility가 맡는다.
    /// </summary>
    public sealed class MaintainCoverAbility : GameplayAbility
    {
        /// <summary>이 유닛의 엄폐 상태이다.</summary>
        private UnitCoverState _coverState;

        /// <summary>대상을 알려 주는 공급자이다.</summary>
        private ICombatTargetSource _targetSource;

        /// <summary>이 유닛의 공격 사거리이다.</summary>
        private float _attackRange;

        /// <inheritdoc />
        /// <remarks>지킬 자리가 있고 그 자리가 지금도 쓸 만할 때만 활성화한다.</remarks>
        public override bool CanActivate()
        {
            return TryResolveOwner() && _coverState.HasCoverClaim && IsCoverStillWorthKeeping();
        }

        /// <inheritdoc />
        /// <remarks>
        /// 자리가 아직 쓸 만한지 매 틱 다시 본다. 아니면 예약을 놓고 끝내 접근 분기에 길을 넘긴다.
        /// </remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            if (IsCoverStillWorthKeeping())
            {
                return GameplayAbilityTickResult.Running;
            }

            ReleaseAbandonedCover();
            return GameplayAbilityTickResult.Finished;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 취소로 끝난 경우에는 예약을 놓지 않는다. 가로채인 것과 자리를 포기한 것은 다르며,
        /// 포기 판단은 <see cref="OnTick"/>이 이미 내리고 그 자리에서 놓았다.
        /// </remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
        }

        /// <summary>지금 이 자리를 계속 지킬 만한지 판단한다.</summary>
        /// <returns>지킬 만하면 true이다.</returns>
        private bool IsCoverStillWorthKeeping()
        {
            if (!TryResolveOwner() || !_coverState.HasCoverClaim)
            {
                return false;
            }

            var target = _targetSource.CurrentTarget;
            if (target == null)
            {
                return false;
            }

            var threatPosition = target.position;
            if (!_coverState.IsInCover)
            {
                return false;
            }

            // 쏠 수 없는 거리에서 앉아 있으면 교착이 되므로 자리를 내주고 접근에 길을 넘긴다.
            return Vector3.Distance(_coverState.transform.position, threatPosition) <= _attackRange;
        }

        /// <summary>더는 쓰지 않기로 한 엄폐 예약을 놓아 준다.</summary>
        private void ReleaseAbandonedCover()
        {
            if (_coverState != null && _coverState.HasCoverClaim)
            {
                _coverState.ReleaseCover();
            }
        }

        /// <summary>소유 액터에서 유지 판단에 필요한 구성요소와 수치를 찾는다.</summary>
        /// <returns>판단할 수 있는 구성이 갖춰져 있으면 true이다.</returns>
        private bool TryResolveOwner()
        {
            var owner = System != null ? System.Owner : null;
            if (owner == null)
            {
                return false;
            }

            if (_coverState == null)
            {
                _coverState = owner.GetComponent<UnitCoverState>();
                _targetSource = owner.GetComponent<ICombatTargetSource>();
            }

            if (_coverState == null || _targetSource == null)
            {
                return false;
            }

            // 사거리를 모르면 유지 조건을 판단할 수 없다. 0이면 곧바로 자리를 놓아 붙들고 있지 않게 한다.
            var definition = owner.GetComponent<TacticalUnit>()?.Definition;
            _attackRange = definition != null ? definition.AttackRange : 0f;
            return true;
        }
    }
}
