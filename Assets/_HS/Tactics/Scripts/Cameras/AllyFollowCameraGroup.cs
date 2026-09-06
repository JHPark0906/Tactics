using System;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using MessagePipe;
using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Cameras
{
    /// <summary>배치되는 아군 유닛을 Cinemachine 대상 그룹의 추적 멤버로 잇는다.</summary>
    /// <remarks>
    /// <para>
    /// <b>카메라 자체의 추적·프레이밍은 인스펙터 설정만으로 된다.</b> <c>CinemachineTargetGroup</c>을
    /// Tracking Target으로 문 카메라에 <c>CinemachineGroupFraming</c> 확장을 붙이면 그 그룹을 화면 안에
    /// 담는 일은 Cinemachine이 알아서 한다. 이 컴포넌트가 하는 일은 그 그룹의 멤버 목록 하나뿐이다 —
    /// 유닛은 배치 단계에서 동적으로 스폰되고 전투 중 죽어 사라지므로, 멤버를 실시간으로 넣고 빼는 것만큼은
    /// 코드가 있어야 한다.
    /// </para>
    /// <para>
    /// <b>넣는 시점과 빼는 시점이 서로 다른 발행자에서 온다.</b> 배치·회수는
    /// <see cref="UnitPlacementController.UnitPlaced"/>·<see cref="UnitPlacementController.UnitRecalled"/>
    /// (평범한 C# 이벤트)가 알리고, 전투 중 죽음은 프레임워크의 <see cref="DeathEvent"/>(MessagePipe)가 알린다.
    /// <see cref="UnitPlacementController"/>가 이미 내보내는 이 두 이벤트를 쓰는 것은, 배치 완료 신호
    /// (<see cref="PlacementCompletedEvent"/>)에는 실린 것이 없고 그 시점의 배치 계획도 정의(어떤 유닛
    /// 종류인지)만 담아 실제로 스폰된 Transform을 주지 않기 때문이다.
    /// </para>
    /// <para>
    /// <b>유닛 자체에는 아무것도 붙이지 않는다.</b> 이 컴포넌트 하나를 씬에 두면 되므로 유닛 프리팹을
    /// 건드릴 필요가 없다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AllyFollowCameraGroup : MonoBehaviour
    {
        [Tooltip("아군을 따라갈 Cinemachine 대상 그룹이다.")]
        [SerializeField] private CinemachineTargetGroup targetGroup;

        [Tooltip("추적할 진영이다. 배치 컨트롤러는 플레이어 진영만 다루므로 사실상 걸러지지만, 방어적으로 한 번 더 확인한다.")]
        [SerializeField] private TeamId allyTeamId = new(1);

        [Tooltip("대상 그룹 안에서 유닛 하나가 갖는 가중치이다.")]
        [SerializeField] [Min(0f)] private float memberWeight = 1f;

        [Tooltip("대상 그룹 안에서 유닛 하나가 갖는 반경(미터)이다.")]
        [SerializeField] [Min(0f)] private float memberRadius = 1f;

        private UnitPlacementController _placementController;
        private ISubscriber<DeathEvent> _deathSubscriber;
        private IDisposable _deathSubscription;

        /// <summary>배치 컨트롤러와 사망 이벤트 구독자를 주입받는다.</summary>
        /// <remarks>
        /// 배치 컨트롤러의 이벤트는 MessagePipe가 아니라 평범한 C# 이벤트이므로 인스턴스 참조를 직접 들고
        /// 구독·해제한다. 주입이 다시 오면 기존 구독을 먼저 떼어 중복 구독이 쌓이지 않게 한다.
        /// </remarks>
        /// <param name="placementController">배치·회수를 알려 줄 컨트롤러이다.</param>
        /// <param name="deathSubscriber">전투 중 죽음을 알려 줄 프레임워크 이벤트 구독자이다.</param>
        [Inject]
        public void InjectDependencies(
            UnitPlacementController placementController,
            ISubscriber<DeathEvent> deathSubscriber)
        {
            DetachPlacementEvents();
            _placementController = placementController;
            _deathSubscriber = deathSubscriber;
            AttachPlacementEvents();
            RefreshDeathSubscription();
        }

        private void OnEnable()
        {
            AttachPlacementEvents();
            RefreshDeathSubscription();
        }

        private void OnDisable()
        {
            DetachPlacementEvents();
            DetachDeathSubscription();
        }

        private void OnDestroy()
        {
            DetachPlacementEvents();
            DetachDeathSubscription();
            _placementController = null;
            _deathSubscriber = null;
        }

        /// <summary>컨트롤러가 있고 활성 상태일 때만 배치·회수 이벤트를 구독한다.</summary>
        private void AttachPlacementEvents()
        {
            if (_placementController == null || !isActiveAndEnabled)
            {
                return;
            }

            _placementController.UnitPlaced += OnUnitPlaced;
            _placementController.UnitRecalled += OnUnitRecalled;
        }

        /// <summary>부착된 배치·회수 이벤트 구독을 해제한다.</summary>
        private void DetachPlacementEvents()
        {
            if (_placementController == null)
            {
                return;
            }

            _placementController.UnitPlaced -= OnUnitPlaced;
            _placementController.UnitRecalled -= OnUnitRecalled;
        }

        /// <summary>구독자가 준비되고 활성 상태일 때만 사망 이벤트를 구독한다.</summary>
        private void RefreshDeathSubscription()
        {
            DetachDeathSubscription();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _deathSubscription = _deathSubscriber?.Subscribe(OnDeath);
        }

        /// <summary>부착된 사망 이벤트 구독을 해제한다.</summary>
        private void DetachDeathSubscription()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
        }

        /// <summary>배치된 유닛이 아군이면 대상 그룹에 넣는다. 이미 들어 있으면 다시 넣지 않는다.</summary>
        /// <param name="unit">배치된 유닛이다.</param>
        private void OnUnitPlaced(TacticalUnit unit)
        {
            if (targetGroup == null || unit == null || !IsAlly(unit)
                || targetGroup.FindMember(unit.transform) >= 0)
            {
                return;
            }

            targetGroup.AddMember(unit.transform, memberWeight, memberRadius);
        }

        /// <summary>회수된 유닛을 대상 그룹에서 뺀다.</summary>
        /// <param name="unit">회수된 유닛이다.</param>
        private void OnUnitRecalled(TacticalUnit unit)
        {
            if (unit != null)
            {
                targetGroup?.RemoveMember(unit.transform);
            }
        }

        /// <summary>죽은 유닛을 대상 그룹에서 뺀다. 그룹에 없던 대상이면 아무 일도 하지 않는다.</summary>
        /// <param name="deathEvent">프레임워크가 발행한 사망 이벤트이다.</param>
        private void OnDeath(DeathEvent deathEvent)
        {
            if (deathEvent.Target != null)
            {
                targetGroup?.RemoveMember(deathEvent.Target.transform);
            }
        }

        /// <summary>유닛이 추적할 진영인지 확인한다.</summary>
        /// <param name="unit">확인할 유닛이다.</param>
        /// <returns>진영이 일치하면 true이다.</returns>
        private bool IsAlly(TacticalUnit unit)
        {
            var team = unit.Team;
            return team != null && team.TeamId.Equals(allyTeamId);
        }
    }
}
