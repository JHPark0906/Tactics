using HS.Framework.Ability.Abilities;
using HS.Framework.Character;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 대상을 마주 볼 때까지 바라보는 방향을 돌리는 어빌리티이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이동과 별개로 돈다.</b> 접근 어빌리티와 동시에 활성일 수 있고, 서로의 값을 읽지 않는다. 이동은 위치를,
    /// 이것은 방향을 쓴다. 끝나는 조건도 각자다 — 이동은 사거리 안, 이것은 허용 각 안이며, 한쪽이 끝나도
    /// 다른 쪽은 이어진다. 그래서 도는 동안에도 걷고, 걷는 동안에도 돈다.
    /// </para>
    /// <para>
    /// <b>회전을 직접 쓰지 않는다.</b> 바라볼 점을 <see cref="ICharacterFacing"/>에 두기만 하고, 실제로 도는 일은
    /// 그 구성요소가 자기 고정 스텝에서 각속도만큼씩 한다. 회전을 쓰는 자리가 하나여야 이동기와 겨루지 않는다.
    /// 어빌리티는 판단이고 구성요소는 손발이라는 것이 이 나눔이다.
    /// </para>
    /// <para>
    /// <b>각속도가 곧 대응 시간이다.</b> 등 뒤에서 온 적은 이미 사거리 안이라 접근이 곧바로 끝나고 조준만 남는다.
    /// 그 시간이 각/각속도이므로, 각속도가 빠른 유닛이 뒤에서 오는 적에 더 빨리 대처한다. 그것이 이 어빌리티를
    /// 따로 둔 까닭이다.
    /// </para>
    /// <para>
    /// <b>끝날 때는 반드시 바라볼 점을 지운다.</b> 스스로 끝나든 사망이나 트리의 끊김으로 취소되든 같다.
    /// 남겨 두면 다음 가지가 걷는 동안에도 그 점을 보느라 정면이 어긋난다.
    /// </para>
    /// </remarks>
    public sealed class AimAbility : GameplayAbility
    {
        /// <summary>대상을 알려 주는 공급자이다.</summary>
        private ICombatTargetSource _targetSource;

        /// <summary>바라보는 방향을 도맡는 구성요소이다.</summary>
        private ICharacterFacing _facing;

        /// <summary>마주 봤다고 볼 허용 각(도)이며 정의에서 읽는다.</summary>
        private float ToleranceDegrees =>
            Definition is AimAbilityDefinition aimDefinition
                ? aimDefinition.ToleranceDegrees
                : AimAbilityDefinition.DefaultToleranceDegrees;

        /// <inheritdoc />
        /// <remarks>
        /// 돌릴 대상과 돌 수단이 있어야 시작한다. 이미 마주 보고 있어도 거절하지 않는다 —
        /// 활성화 직후 첫 판정에서 곧바로 끝나며, 그것이 "조준은 이미 끝났다"를 트리에 성공으로 알리는 길이다.
        /// </remarks>
        public override bool CanActivate()
        {
            return TryResolveOwner() && _targetSource.CurrentTarget != null;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 대상이 사라졌으면 끝낸다. 그 뒤 실제로 쏠지는 사격 조건이 따로 판단하므로 여기서 실패를 만들지 않는다.
        /// </remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            var target = TryResolveOwner() ? _targetSource.CurrentTarget : null;
            if (target == null)
            {
                return GameplayAbilityTickResult.Finished;
            }

            if (_facing.AngleTo(target.position) <= ToleranceDegrees)
            {
                return GameplayAbilityTickResult.Finished;
            }

            _facing.LookAt(target.position);
            return GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        /// <remarks>두었던 점을 놓는다. 이것이 이 어빌리티가 바깥에 남기는 유일한 상태이다.</remarks>
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            _facing?.ClearLookTarget();
        }

        /// <summary>소유 액터에서 대상 공급자와 방향 구성요소를 찾는다.</summary>
        /// <returns>조준할 구성이 갖춰져 있으면 true이다.</returns>
        private bool TryResolveOwner()
        {
            var owner = System != null ? System.Owner : null;
            if (owner == null)
            {
                return false;
            }

            if (_targetSource == null || _facing == null)
            {
                _targetSource = owner.GetComponent<ICombatTargetSource>();
                _facing = owner.GetComponent<ICharacterFacing>();
            }

            return _targetSource != null && _facing != null;
        }
    }
}
