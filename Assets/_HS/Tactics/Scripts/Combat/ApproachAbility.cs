using HS.Framework.Ability.Abilities;
using HS.Framework.Character;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Combat
{
    /// <summary>
    /// 대상이 사거리 안에 들 때까지 다가가는 어빌리티이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>회전과 별개로 돈다.</b> 조준 어빌리티와 동시에 활성일 수 있고 서로의 값을 읽지 않는다. 이것은 위치만
    /// 쓰고 방향은 건드리지 않으므로, 도는 중에도 걷고 각속도가 걸음을 늦추지 않는다. 끝나는 조건도 각자다 —
    /// 이것은 사거리 안, 조준은 허용 각 안이며, 한쪽이 끝나도 다른 쪽은 이어진다.
    /// </para>
    /// <para>
    /// <b>적의 자리가 아니라 적 주위에 나뉜 자리로 간다.</b> 그래야 같은 적을 노리는 아군들이 한 점에 몰리지 않고
    /// 둘러싸는 모양이 된다. 자리를 고르는 규칙은 <see cref="ApproachSlotResolver"/>가 그대로 맡는다.
    /// 갈 수 없는 자리가 나오면 정해진 차례로 다음 자리를 보고, 끝내 없으면 적의 자리로 간다. 차례가 고정되어
    /// 있으므로 같은 상황에서 언제나 같은 자리를 고른다. 갈 수 있는지는 <see cref="TacticalUnit.IsSlotReachable"/>이
    /// 판정한다 — 경로 계획 서비스가 없으면 어느 자리도 갈 수 없는 것으로 본다.
    /// </para>
    /// <para>
    /// <b>사거리 끝이 아니라 조금 안쪽에서 멈춘다.</b> 사거리 끝에서 멈추면 대상이 한 걸음만 물러나도 사거리를
    /// 벗어나 다시 걷게 된다. 멈출 거리와 자리를 놓을 반지름은 같은 값이라, 자리에 도착한 순간이 곧 멈추려던
    /// 지점이 된다.
    /// </para>
    /// <para>
    /// <b>끝날 때는 몰던 이동을 멈춘다.</b> 스스로 끝나든 사망이나 트리의 끊김으로 취소되든 같다. 다만 이동
    /// 요청을 낸 적이 없으면 멈추지 않는다 — 이동 수단은 유닛에 하나뿐이라 다른 자리가 몰던 이동을 끊게 된다.
    /// </para>
    /// </remarks>
    public sealed class ApproachAbility : GameplayAbility
    {
        /// <summary>대상을 알려 주는 공급자이다.</summary>
        private ICombatTargetSource _targetSource;

        /// <summary>다가갈 이동 수단이다.</summary>
        private ICharacterMover _mover;

        /// <summary>거리를 잴 기준이 되는 유닛의 Transform이다.</summary>
        private Transform _selfTransform;

        /// <summary>자리 번호와 진영을 읽을 유닛이다.</summary>
        private TacticalUnit _unit;

        /// <summary>이번 활성화에서 이동 요청을 낸 적이 있는지 여부이다.</summary>
        private bool _isDrivingMover;

        /// <summary>이 유닛이 멈추려는 거리(미터)이며, 자리를 놓을 반지름과 같은 값이다.</summary>
        /// <remarks>
        /// 공개 프로퍼티라 활성화 전에도 물을 수 있어야 한다. 그래서 읽기 전에 소유 액터를 스스로
        /// 확보한다 — 부여된 순간 <see cref="System"/>은 이미 있으므로, 활성화를 기다리지 않고도
        /// 유닛 정의의 사거리를 읽을 수 있다. 확보하지 않으면 아직 <c>_unit</c>이 없어 사거리를 0으로
        /// 보고, 항상 반환하는 최소값(0.1)이 진짜 값인 척 나온다.
        /// </remarks>
        public float EngageDistance
        {
            get
            {
                TryResolveOwner();
                return Mathf.Max(0.1f, ResolveAttackRange() * ResolveDefinition().RangeRatio);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// 다가갈 대상과 이동 수단이 있어야 시작한다. 이미 사거리 안이어도 거절하지 않는다 —
        /// 활성화 직후 첫 판정에서 곧바로 끝나며, 그것이 "접근은 이미 끝났다"를 트리에 성공으로 알리는 길이다.
        /// 등 뒤에서 온 적이 바로 그 경우이고, 그때 남는 것은 조준뿐이다.
        /// </remarks>
        public override bool CanActivate()
        {
            return TryResolveOwner() && _targetSource.CurrentTarget != null;
        }

        /// <inheritdoc />
        protected override void OnActivate()
        {
            _isDrivingMover = false;
        }

        /// <inheritdoc />
        /// <remarks>
        /// 대상이 움직이므로 매 틱 설 자리를 다시 정한다. 사거리 안에 들면 끝내고, 대상이 사라지거나 길을
        /// 낼 수 없으면 그 자리에서 끝내 다음 판단에 길을 넘긴다.
        /// </remarks>
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            var target = TryResolveOwner() ? _targetSource.CurrentTarget : null;
            if (target == null)
            {
                return GameplayAbilityTickResult.Finished;
            }

            if (Vector3.Distance(_selfTransform.position, target.position) <= EngageDistance)
            {
                return GameplayAbilityTickResult.Finished;
            }

            if (!_mover.MoveTo(ResolveDestination(target.position)))
            {
                return GameplayAbilityTickResult.Finished;
            }

            _isDrivingMover = true;
            return GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        protected override void OnEnd(GameplayAbilityEndReason endReason)
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
            var definition = ResolveDefinition();
            var settings = new ApproachSpreadSettings(EngageDistance, definition.SlotCount, definition.ArcDegrees);
            if (!settings.IsEnabled)
            {
                return targetPosition;
            }

            var targetPlanar = PlanarPosition.FromWorld(targetPosition);
            var reference = ApproachSpreadReference.Resolve(BattleLane.Active, ResolveTeam());
            var slotSeed = ResolveTemporarySlotSeed();

            // 한 바퀴를 다 돌면 처음 자리로 돌아오므로, 그 이상 시도하는 것은 같은 자리를 다시 보는 것이다.
            for (var attempt = 0; attempt < settings.SlotCount; attempt++)
            {
                var slot = ApproachSlotResolver.ResolveSlotPosition(
                    targetPlanar,
                    reference,
                    settings.Radius,
                    slotSeed,
                    settings.SlotCount,
                    settings.ArcDegrees,
                    attempt);

                if (_unit.IsSlotReachable(slot.ToWorld(targetPosition.y), out var reachable))
                {
                    return reachable;
                }
            }

            return targetPosition;
        }

        /// <summary>
        /// 이 유닛의 자리 번호를 임시로 정한다. 인스턴스 번호는 음수일 수 있으므로 부호를 지운다.
        /// </summary>
        /// <remarks>
        /// 한 판 안에서는 안정적이지만 판마다 달라지므로 같은 배치가 같은 자리로 가지 않는다.
        /// 따라서 이 번호는 전투 간 동일한 슬롯 배치를 보장하지 않는다.
        /// </remarks>
        /// <returns>0 이상의 안정적인 번호이다.</returns>
        private int ResolveTemporarySlotSeed() => _unit != null ? _unit.GetInstanceID() & int.MaxValue : 0;

        /// <summary>유닛의 진영을 읽는다. 아직 조립 전이면 지정되지 않은 진영이다.</summary>
        /// <returns>유닛의 진영이다.</returns>
        private TeamId ResolveTeam() => _unit != null && _unit.Team != null ? _unit.Team.TeamId : default;

        /// <summary>유닛 정의의 사거리를 읽는다. 정의가 없으면 다가갈 거리를 모르므로 0이다.</summary>
        /// <returns>사거리(미터)이다.</returns>
        private float ResolveAttackRange()
        {
            var definition = _unit != null ? _unit.Definition : null;
            return definition != null ? definition.AttackRange : 0f;
        }

        /// <summary>이 어빌리티의 정의를 읽는다. 다른 정의로 부여됐으면 기본값을 쓴다.</summary>
        /// <returns>접근 어빌리티 정의이다.</returns>
        private ApproachAbilityDefinition ResolveDefinition()
        {
            return Definition as ApproachAbilityDefinition ?? ApproachAbilityDefinition.Fallback;
        }

        /// <summary>소유 액터에서 접근에 필요한 구성요소를 찾는다.</summary>
        /// <returns>다가갈 구성이 갖춰져 있으면 true이다.</returns>
        private bool TryResolveOwner()
        {
            var owner = System != null ? System.Owner : null;
            if (owner == null)
            {
                return false;
            }

            if (_targetSource == null || _mover == null)
            {
                _targetSource = owner.GetComponent<ICombatTargetSource>();
                _mover = owner.GetComponent<ICharacterMover>();
                _unit = owner.GetComponent<TacticalUnit>();
                _selfTransform = owner.transform;
            }

            return _targetSource != null && _mover != null && _unit != null && ResolveAttackRange() > 0f;
        }
    }
}
