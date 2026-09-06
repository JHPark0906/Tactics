using System;
using HS.Framework.AI.Behaviour;
using HS.Framework.Character;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Lane;
using HS.Tactics.Units;
using UnityEngine;

namespace HS.Tactics.Behaviour
{
    /// <summary>
    /// 게임 자리 정의들이 함께 쓰는 규칙이다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>필요한 것이 없으면 자리를 만들지 않는다.</b> 이동 수단이나 탐지기가 없는 유닛에 그것을 쓰는
    /// 자리를 억지로 만들면, 실행 중에 null을 만나 트리가 그 자리에서 멈춘다. 만들지 않으면
    /// 에셋을 짓는 쪽이 빠진 자리를 알 수 있다.
    /// </para>
    /// <para>
    /// <b>사거리는 유닛 정의에서 읽는다.</b> 같은 트리 에셋을 사거리가 다른 유닛들이 나눠 쓰므로,
    /// 에셋에 숫자를 적으면 한 유닛에만 맞는 트리가 된다.
    /// </para>
    /// </remarks>
    internal static class UnitBehaviourDefinitionSupport
    {
        /// <summary>유닛 정의가 없을 때 쓰는 사거리(미터)이며 임시값이다.</summary>
        internal const float FallbackAttackRange = 8f;

        /// <summary>트리를 쓸 유닛의 사거리를 읽는다.</summary>
        /// <param name="owner">트리를 쓸 유닛이다.</param>
        /// <returns>유닛 정의의 사거리이며, 정의가 없으면 임시값이다.</returns>
        internal static float ResolveAttackRange(GameObject owner)
        {
            var definition = owner != null && owner.TryGetComponent<TacticalUnit>(out var unit)
                ? unit.Definition
                : null;
            return definition != null ? definition.AttackRange : FallbackAttackRange;
        }
    }

    /// <summary>대상이 이 유닛의 사거리 안에 들어오면 멈추는 자리를 만든다.</summary>
    /// <remarks>
    /// <para>
    /// 멈출 거리는 사거리에 비율을 곱해 정한다. 사거리 끝에서 멈추면 대상이 한 걸음만 물러나도
    /// 사거리를 벗어나므로, 조금 안쪽에서 멈춰 여유를 둔다.
    /// </para>
    /// <para>
    /// 멈추는 일은 프레임워크의 <see cref="HoldWithinRangeBehaviour"/>가 하고, 여기서는 거리를 에셋이
    /// 아니라 유닛 정의에서 읽어 넘길 뿐이다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class HoldWithinAttackRangeDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("사거리에 이 비율을 곱한 거리에서 멈춘다. 1이면 사거리 끝에서 멈춘다.")]
        [Range(0.1f, 1f)]
        private float rangeRatio = 0.9f;

        [SerializeField]
        [Tooltip("대상 Transform이 담긴 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        /// <inheritdoc />
        public override string DisplayName => "Hold Within Attack Range";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            if (owner == null || !owner.TryGetComponent<ICharacterMover>(out var mover))
            {
                return null;
            }

            var holdDistance = Mathf.Max(
                0.1f, UnitBehaviourDefinitionSupport.ResolveAttackRange(owner) * rangeRatio);
            return new HoldWithinRangeBehaviour(owner.transform, mover, targetKey, holdDistance);
        }
    }

    /// <summary>적 주위에 나뉜 자리 하나로 다가가는 자리를 만든다.</summary>
    /// <remarks>
    /// <para>
    /// 자리는 멈출 거리에 놓는다. 그래야 자리에 도착한 순간이 곧 멈추려던 지점이 된다.
    /// 반원을 그릴 기준 방향은 레인과 진영에서 구하며, 레인이 없으면 고정된 방향으로 물러난다.
    /// </para>
    /// <para>
    /// <b>자리 번호는 임시다.</b> 지금은 유닛 인스턴스 번호를 쓴다 —
    /// 한 판 안에서는 안정적이지만 <b>판마다 달라지므로 같은 배치가 같은 자리로 가지 않는다.</b>
    /// 판을 건너 같은 값을 주는 출처는 없다.
    /// 따라서 자리 배치는 전투 간 재현성을 보장하지 않는다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SpreadChaseTargetDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("사거리에 이 비율을 곱한 거리에 자리를 놓는다. 사거리 유지 자리와 같은 값을 쓴다.")]
        [Range(0.1f, 1f)]
        private float rangeRatio = 0.9f;

        [SerializeField]
        [Tooltip("적 주위에 나눌 자리의 수이다. 1 이하이면 나누지 않고 적의 자리로 곧장 간다.")]
        [Min(1)]
        private int slotCount = 6;

        [SerializeField]
        [Tooltip("자리를 펼칠 각도의 폭(도)이다. 180이면 우리 쪽을 향한 반원이다.")]
        [Range(0f, 360f)]
        private float arcDegrees = 180f;

        [SerializeField]
        [Tooltip("대상 Transform이 담긴 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        /// <inheritdoc />
        public override string DisplayName => "Spread Chase Target";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            if (owner == null
                || !owner.TryGetComponent<ICharacterMover>(out var mover)
                || !owner.TryGetComponent<TacticalUnit>(out var unit))
            {
                return null;
            }

            var radius = Mathf.Max(0.1f, UnitBehaviourDefinitionSupport.ResolveAttackRange(owner) * rangeRatio);
            var slotSeed = ResolveTemporarySlotSeed(unit);
            return new SpreadChaseTargetBehaviour(
                mover,
                targetKey,
                new ApproachSpreadSettings(radius, slotCount, arcDegrees),
                () => slotSeed,
                () => ApproachSpreadReference.Resolve(BattleLane.Active, ResolveTeam(unit)),
                unit.IsSlotReachable);
        }

        /// <summary>
        /// 이 유닛의 자리 번호를 임시로 정한다. 인스턴스 번호는 음수일 수 있으므로 부호를 지운다.
        /// </summary>
        /// <param name="unit">번호를 정할 유닛이다.</param>
        /// <returns>0 이상의 안정적인 번호이다.</returns>
        private static int ResolveTemporarySlotSeed(TacticalUnit unit) => unit.GetInstanceID() & int.MaxValue;

        /// <summary>유닛의 진영을 읽는다. 아직 조립 전이면 지정되지 않은 진영이다.</summary>
        /// <param name="unit">진영을 읽을 유닛이다.</param>
        /// <returns>유닛의 진영이다.</returns>
        private static HS.Framework.Gameplay.Teams.TeamId ResolveTeam(TacticalUnit unit)
            => unit.Team != null ? unit.Team.TeamId : default;
    }

    /// <summary>쓸 수 있는 엄폐 지점을 골라 확보하고 그 좌표를 문맥에 적는 자리를 만든다.</summary>
    /// <remarks>그 자리에서 위협을 쏠 수 있는 엄폐만 고르므로 사거리를 유닛 정의에서 읽어 넘긴다.</remarks>
    [Serializable]
    public sealed class SelectCoverDestinationDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("위협 대상을 읽을 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        [SerializeField]
        [Tooltip("고른 엄폐 좌표를 담을 문맥 키이다.")]
        private string destinationKey = UnitBehaviourKeys.Destination;

        /// <inheritdoc />
        public override string DisplayName => "Select Cover Destination";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            if (owner == null
                || !owner.TryGetComponent<CoverSensor>(out var sensor)
                || !owner.TryGetComponent<UnitCoverState>(out var coverState))
            {
                return null;
            }

            return new SelectCoverDestinationBehaviour(
                sensor,
                coverState,
                UnitBehaviourDefinitionSupport.ResolveAttackRange(owner),
                targetKey,
                destinationKey);
        }
    }

    /// <summary>확보한 엄폐가 아직 유효한지 지키는 자리를 만든다.</summary>
    [Serializable]
    public sealed class MaintainCoverDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("위협 대상을 읽을 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        /// <inheritdoc />
        public override string DisplayName => "Maintain Cover";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            if (owner == null || !owner.TryGetComponent<UnitCoverState>(out var coverState))
            {
                return null;
            }

            return new MaintainCoverBehaviour(
                coverState, UnitBehaviourDefinitionSupport.ResolveAttackRange(owner), targetKey);
        }
    }

    /// <summary>가지가 도는 동안 적을 다시 찾아 문맥에 담는 서비스를 만든다.</summary>
    [Serializable]
    public sealed class DetectEnemyServiceDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("다시 찾는 간격(초)이다. 0이면 실행할 때마다 찾는다.")]
        [Min(0f)]
        private float interval = 0.2f;

        [SerializeField]
        [Tooltip("찾은 대상을 담을 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        /// <inheritdoc />
        public override string DisplayName => "Service: Detect Enemy";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            return owner == null || !owner.TryGetComponent<EnemyDetector>(out var detector)
                ? null
                : new DetectEnemyService(detector, interval, targetKey);
        }
    }

    /// <summary>가지가 도는 동안 전진 방향을 다시 셈해 문맥에 담는 서비스를 만든다.</summary>
    [Serializable]
    public sealed class RefreshAdvanceDirectionDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("다시 셈하는 간격(초)이다. 0이면 실행할 때마다 셈한다.")]
        [Min(0f)]
        private float interval = 0.25f;

        [SerializeField]
        [Tooltip("목표에 이만큼 가까워지면 도달로 보고 더 전진하지 않는다(미터).")]
        [Min(0f)]
        private float arrivalDistance = LaneAdvanceCalculator.DefaultArrivalDistance;

        /// <inheritdoc />
        public override string DisplayName => "Service: Refresh Advance Direction";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            return owner == null || !owner.TryGetComponent<TeamMember>(out var team)
                ? null
                : new RefreshAdvanceDirectionService(owner.transform, team, arrivalDistance, interval);
        }
    }

    /// <summary>가지가 도는 동안 대상이 이 유닛의 사거리 안인지 재서 표식을 두는 서비스를 만든다.</summary>
    /// <remarks>
    /// <para>
    /// 사거리 안에서 멈추는 조건이 이 표식을 지켜보게 두면, 대상이 들어오는 순간 추격이 끊긴다.
    /// 이 서비스가 없으면 선택 자리가 진행 중인 추격을 기억해 유닛이 적에게 달라붙는다.
    /// </para>
    /// <para>
    /// 재는 일은 프레임워크의 <see cref="TargetInRangeService"/>가 하고, 여기서는 거리를 에셋이 아니라
    /// 유닛 정의에서 읽어 넘길 뿐이다. 거리를 에셋에 적는 프레임워크 설명과는 그 점만 다르다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class TargetInAttackRangeServiceDefinition : BehaviourNodeDefinition
    {
        [SerializeField]
        [Tooltip("다시 재는 간격(초)이다. 0이면 실행할 때마다 잰다. 임시값이다.")]
        [Min(0f)]
        private float interval = 0.1f;

        [SerializeField]
        [Tooltip("대상 Transform이 담긴 문맥 키이다.")]
        private string targetKey = UnitBehaviourKeys.Target;

        [SerializeField]
        [Tooltip("사거리 안일 때 표식을 둘 문맥 키이다.")]
        private string inRangeKey = UnitBehaviourKeys.TargetInRange;

        /// <inheritdoc />
        public override string DisplayName => "Service: Target In Range";

        /// <inheritdoc />
        public override IBehaviour CreateBehaviour(in BehaviourBuildContext context)
        {
            var owner = context.Owner;
            return owner == null
                ? null
                : new TargetInRangeService(
                    owner.transform,
                    UnitBehaviourDefinitionSupport.ResolveAttackRange(owner),
                    interval,
                    targetKey,
                    inRangeKey);
        }
    }
}
