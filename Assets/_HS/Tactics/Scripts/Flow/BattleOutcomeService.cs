using System;
using HS.Framework.Gameplay;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Flow
{
    /// <summary>
    /// 전투 씬에 배치되어 유닛 등록을 받고 사망 이벤트로 승패를 판정해 게임 레이어 이벤트로 알린다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 판정 자체는 순수 클래스인 <see cref="BattleOutcomeTracker"/>가 수행하고, 이 컴포넌트는
    /// MessagePipe 연결과 수명주기 관리만 맡으며 정적 접근자를 쓰지 않는다.
    /// </para>
    /// <para>
    /// 유닛을 집계에 넣는 경로는 <see cref="IBattleUnitRegistry"/>에 정의되어 있으며,
    /// 배치한 유닛의 등록 구성요소가 이 컴포넌트를 그 계약으로 주입받아 사용한다.
    /// </para>
    /// <para>
    /// <see cref="HS.Framework.Gameplay.GameState"/>를 상속해 <see cref="PlacementCompletedEvent"/>를
    /// 구독한다. 배치 컨트롤러를 직접 참조하지 않으므로, 배치 쪽 구현이 바뀌어도 이 컴포넌트는
    /// 이벤트의 모양만 지키면 된다. 자세한 반응은 <see cref="HasBattleBegun"/>을 본다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattleOutcomeService : GameState,
        IBattleUnitRegistry
    {
        [Tooltip("플레이어가 조작하는 진영이다. 이 진영과 적대하는 유닛이 적으로 집계된다.")]
        [SerializeField]
        private TeamId playerTeamId = new(1);

        private BattleOutcomeTracker _tracker;
        private ISubscriber<DeathEvent> _deathSubscriber;
        private IPublisher<BattleOutcomeDecidedEvent> _outcomePublisher;
        private ITeamRelationPolicy _relationPolicy;
        private ISubscriber<PlacementCompletedEvent> _placementSubscriber;
        private IDisposable _deathSubscription;
        private IDisposable _placementSubscription;

        /// <summary>확정된 전투 결과이며, 아직 확정 전이면 <see cref="BattleOutcome.Undecided"/>이다.</summary>
        public BattleOutcome Outcome => Tracker.Outcome;

        /// <summary>
        /// 배치 완료 신호를 받아 전투가 시작되었는지 보관한다.
        /// 양 진영의 등록 완료를 나타내는 HasBattleStarted와 별개의 조건이며, 배치 완료 전에는 사망을 반영하지 않는다.
        /// </summary>
        public bool HasBattleBegun { get; private set; }

        /// <summary>살아 있는 아군 유닛 수이다.</summary>
        public int FriendlyAliveCount => Tracker.FriendlyAliveCount;

        /// <summary>살아 있는 적 유닛 수이다.</summary>
        public int HostileAliveCount => Tracker.HostileAliveCount;

        /// <summary>
        /// 판정에 사용하는 추적기이며 첫 사용 시점에 만들어진다.
        /// 생성 시점에 주입된 피아 판정 규칙이 고정되므로, 규칙을 교체하려면 유닛 등록 전에 주입해야 한다.
        /// </summary>
        private BattleOutcomeTracker Tracker => _tracker ??= new BattleOutcomeTracker(playerTeamId, _relationPolicy);

        /// <summary>
        /// 사망 이벤트 구독자, 결과 발행자, 피아 판정 규칙, 배치 완료 구독자를 주입받고
        /// 활성 상태면 두 이벤트를 구독한다. 이미 유닛이 등록된 뒤에 규칙이 오면 집계 기준이
        /// 도중에 바뀌므로 규칙만 무시하고 경고를 남긴다.
        /// </summary>
        /// <param name="deathSubscriber">프레임워크 사망 이벤트의 구독자이다.</param>
        /// <param name="outcomePublisher">전투 결과 확정 이벤트의 발행자이다.</param>
        /// <param name="relationPolicy">진영 관계 판정 규칙이다.</param>
        /// <param name="placementSubscriber">배치 완료 이벤트의 구독자이다.</param>
        [Inject]
        public void InjectMessagePipeDependencies(
            ISubscriber<DeathEvent> deathSubscriber,
            IPublisher<BattleOutcomeDecidedEvent> outcomePublisher,
            ITeamRelationPolicy relationPolicy,
            ISubscriber<PlacementCompletedEvent> placementSubscriber)
        {
            _deathSubscriber = deathSubscriber ?? throw new ArgumentNullException(nameof(deathSubscriber));
            _outcomePublisher = outcomePublisher ?? throw new ArgumentNullException(nameof(outcomePublisher));
            _placementSubscriber =
                placementSubscriber ?? throw new ArgumentNullException(nameof(placementSubscriber));
            if (_tracker != null)
            {
                Debug.LogWarning(
                    $"[BattleOutcomeService] {name}은 이미 유닛을 집계하고 있어 진영 관계 규칙 교체를 무시한다.", this);
            }
            else
            {
                _relationPolicy = relationPolicy;
            }

            RefreshSubscription();
        }

        /// <inheritdoc />
        public bool RegisterUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return false;
            }

            var teamMember = unit.Team != null ? unit.Team : unit.GetComponent<TeamMember>();
            if (teamMember == null)
            {
                Debug.LogWarning($"[BattleOutcomeService] {unit.name}에 진영 정보가 없어 승패 집계에 넣지 않는다.", unit);
                return false;
            }

            // 유닛에 주입된 규칙으로 판정한 관계를 그대로 쓴다. 유닛과 판정기가 서로 다른 규칙을 보지 않게 하기 위함이다.
            return Tracker.Register(unit.gameObject, teamMember.GetRelationTo(playerTeamId));
        }

        /// <inheritdoc />
        public bool RegisterUnit(GameObject unitObject, TeamId teamId)
        {
            return Tracker.Register(unitObject, teamId);
        }

        /// <inheritdoc />
        public bool UnregisterUnit(GameObject unitObject)
        {
            return Tracker.Unregister(unitObject);
        }

        private void OnEnable()
        {
            RefreshSubscription();
        }

        private void OnDisable()
        {
            DetachSubscription();
        }

        private void OnDestroy()
        {
            DetachSubscription();
            _deathSubscriber = null;
            _outcomePublisher = null;
            _placementSubscriber = null;
        }

        /// <summary>
        /// 구독자가 준비되고 컴포넌트가 활성 상태일 때만 사망·배치 완료 이벤트를 구독한다.
        /// 주입이 여러 번 도착해도 중복 구독이 쌓이지 않도록 기존 구독을 먼저 해제한다.
        /// </summary>
        private void RefreshSubscription()
        {
            DetachSubscription();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _deathSubscription = _deathSubscriber?.Subscribe(OnDeath);
            _placementSubscription = _placementSubscriber?.Subscribe(OnPlacementCompleted);
        }

        /// <summary>부착된 사망·배치 완료 이벤트 구독을 해제한다.</summary>
        private void DetachSubscription()
        {
            _deathSubscription?.Dispose();
            _deathSubscription = null;
            _placementSubscription?.Dispose();
            _placementSubscription = null;
        }

        /// <summary>배치가 끝났다는 신호를 받아 자기 상태를 갱신한다.</summary>
        /// <param name="placementCompletedEvent">배치 컨트롤러가 발행한 배치 완료 이벤트이다.</param>
        private void OnPlacementCompleted(PlacementCompletedEvent placementCompletedEvent)
        {
            HasBattleBegun = true;
        }

        // 배치 완료 신호 전에는 사망을 집계에 반영하지 않는다.
        private void OnDeath(DeathEvent deathEvent)
        {
            if (!HasBattleBegun)
            {
                return;
            }

            var wasDecided = Tracker.IsDecided;
            if (!Tracker.NotifyDeath(deathEvent.Target) || wasDecided || !Tracker.IsDecided)
            {
                return;
            }

            var outcomeEvent = new BattleOutcomeDecidedEvent(
                Tracker.Outcome, Tracker.FriendlyAliveCount, Tracker.HostileAliveCount);
            _outcomePublisher?.Publish(outcomeEvent);
        }
    }
}
