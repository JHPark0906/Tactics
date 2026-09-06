using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Tactics.Character.Movement;
using HS.Tactics.Core;
using HS.Tactics.Cover;
using HS.Tactics.Flow;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Pathfinding;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using UnityEngine;
using VContainer;

namespace HS.Tactics.Units
{
    /// <summary>
    /// 전투에 배치된 유닛의 진입점이며, 유닛 정의를 프레임워크 컴포넌트에 반영하고 행동 트리를 조립한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>유닛 조립 규약.</b> 유닛 프리팹은 이 컴포넌트와 함께 다음을 갖춘다.
    /// 체력은 <see cref="HealthAttributeComponent"/>(값은 어트리뷰트 집합이 갖고 이 컴포넌트는 문이다), 진영은 <see cref="TeamMember"/>,
    /// 이동은 <see cref="ICharacterMover"/> 구현(기본은 <c>PlanarCharacterMover</c>),
    /// AI 실행은 <see cref="BehaviourTreeRunner"/>가 맡는다. 이 컴포넌트는 <see cref="CharacterBase"/>를
    /// 상속하므로 유닛에 붙은 <see cref="ICharacterComponent"/> 구현이 함께 초기화된다.
    /// </para>
    /// <para>
    /// <b>어빌리티.</b> 어빌리티 시스템 컴포넌트는 조립 때 없으면 붙이며, 유닛 정의의 어빌리티 집합을
    /// 그 시스템에 부여한다. 부여를 컴포넌트의 Awake에 맡기지 않는 것은 정의가 스폰 직후에 지정되어
    /// Awake 차례에 따라 부여 여부가 갈리기 때문이다. 어빌리티와 효과의 시간은 이 컴포넌트의
    /// <c>FixedUpdate</c>가 흘린다. 행동 트리와 이동이 같은 고정 스텝 위에서 돌므로 판단과 시간이 한 시계를 본다.
    /// 컴포넌트에 있는 자체 집합 칸은 쓰지 않는다. 유닛의 어빌리티는 정의 하나로 정한다.
    /// </para>
    /// <para>
    /// <b>수치의 투영은 한 자리에서 한 번.</b> 어빌리티 집합의 어트리뷰트 묶음이 체력·공격력·레벨 어트리뷰트를 갖춰 주면,
    /// 조립이 유닛 정의의 수치와 육성 진행(레벨·경험치)을 레벨 곡선으로 곱해 어트리뷰트 기본값에 쓴다.
    /// 그 계산은 <see cref="ProjectDefinitionOntoAttributes"/> 한 곳에만 있고 조립 때 한 번만 한다. 전투 중 레벨업이 없으므로
    /// 최대 체력이 도중에 바뀌는 경로를 두지 않는다. 곡선과 진행은 스코프가 주입하며, 주입되지 않은 조립(검사)은
    /// 정의 수치를 그대로 쓴다.
    /// </para>
    /// <para>
    /// <b>행동 트리.</b> 행동은 연결된 행동 트리 에셋이 정한다. 교전, 엄폐 확보, 접근 분기는
    /// 행동 트리 에셋에 그려진 대로 이 유닛이 쓸 트리를 지어
    /// 우선순위에 맞는 자리에 자동으로 끼워지며, 이 클래스를 고칠 필요가 없다.
    /// </para>
    /// <para>
    /// <b>배치 단계에는 돌지 않는다.</b> 유닛은 배치 중에도 보여야 하므로 스폰과 조립은 그대로 하고,
    /// 행동 트리의 틱에만 「전투가 시작됐는가」를 묻는 문을 건다. 그 판정은
    /// <see cref="UnitPlacementController.IsBattlePhase"/> 한 곳에 있고 유닛은 묻기만 한다. 이동은 트리가
    /// 명령해야 일어나므로 트리가 멈추면 함께 멈춘다. 단계가 바뀌면 트리를 처음으로 되돌려, 전투는 멈춰 있던
    /// 동안의 진행 없이 시작하고 배치로 돌아가면 걷던 자리가 멈춘다. 배치 컨트롤러가 없는 무대에서는 조립 즉시 돈다.
    /// </para>
    /// <para>
    /// <b>의존성 주입.</b> 씬에 놓여 있는 유닛은 <b>주입을 받는다.</b> FrameworkInitializer가 씬이 로드될 때
    /// 그 씬의 모든 컴포넌트에 등록된 의존성을 넣어 주기 때문이다.
    /// </para>
    /// <para>
    /// <b>주입을 놓치는 것은 실행 중에 스폰된 유닛이다.</b> 스폰은 씬 로드보다 뒤에 일어나므로
    /// 그 시점을 이미 지나쳤고, 그래서 <b>스폰하는 쪽이</b>
    /// VContainer의 <c>IObjectResolver.InjectGameObject</c>로 계층 전체에 직접 주입해야
    /// 체력의 이벤트 발행자 같은 서비스가 연결된다.
    /// </para>
    /// <para>
    /// 씬 배치 유닛은 자동 주입을 받고, 실행 중 생성된 유닛은 스폰 경로에서 직접 주입한다.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HealthAttributeComponent))]
    [RequireComponent(typeof(TeamMember))]
    [RequireComponent(typeof(BehaviourTreeRunner))]
    [RequireComponent(typeof(BehaviourTreeDeathStop))]
    [RequireComponent(typeof(DefeatedUnitRetirement))]
    [RequireComponent(typeof(BattleUnitRegistrant))]
    [RequireComponent(typeof(CharacterFacing))]
    public sealed class TacticalUnit : CharacterBase
    {
        [Tooltip("이 유닛의 정의이다. 스폰하는 쪽이 지정하면 그 값이 우선한다.")]
        [SerializeField]
        private UnitDefinition definition;

        [Tooltip("이 유닛이 쓸 행동 트리 에셋이다. 비어 있으면 행동 트리를 시작하지 않는다.")]
        [SerializeField]
        private BehaviourTreeAsset behaviourTree;

        private readonly BehaviourContext _behaviourContext = new();
        private HealthAttributeComponent _health;
        private TeamMember _team;
        private BehaviourTreeRunner _behaviourTreeRunner;
        private ICharacterMover _mover;
        private bool _isUnitInitialized;
        private GameplayAbilitySystemComponent _abilitySystem;
        private UnitProgressionService _progression;
        private UnitLevelCurve _levelCurve;
        private int? _explicitLevel;
        private UnitPlacementController _placement;
        private IUnitSpatialRegistry _spatialRegistry;
        private BattlePathfindingService _pathfindingService;
        private CoverSensor _coverSensor;
        private readonly List<TeamMember> _nearbyTeamBuffer = new();

        /// <summary>유닛의 어빌리티 시스템 컴포넌트이며 조립 전에는 null이다.</summary>
        public GameplayAbilitySystemComponent AbilitySystem => _abilitySystem;

        /// <summary>이 유닛의 정의이며 아직 지정되지 않았으면 null이다.</summary>
        public UnitDefinition Definition => definition;

        /// <summary>행동 트리 노드가 공유하는 컨텍스트이다.</summary>
        public IBehaviourContext BehaviourContext => _behaviourContext;

        /// <summary>유닛 조립이 끝났는지 여부이다.</summary>
        public bool IsUnitInitialized => _isUnitInitialized;

        /// <summary>유닛의 체력 문이며 조립 전에는 null이다. 값은 어트리뷰트 집합이 갖는다.</summary>
        public HealthAttributeComponent Health => _health;

        /// <summary>유닛의 진영 구성요소이며 조립 전에는 null이다.</summary>
        public TeamMember Team => _team;

        /// <inheritdoc />
        protected override void Awake()
        {
            base.Awake();
            InitializeUnit();
        }

        /// <summary>
        /// 공간 레지스트리에 자신을 등록한다. 이미 주입을 받은 뒤에 켜졌을 수도 있으므로
        /// 활성화 때마다 시도하며, 아직 레지스트리가 없으면 조용히 넘어간다.
        /// </summary>
        private void OnEnable()
        {
            RegisterSpatialPosition();
        }

        /// <summary>
        /// 공간 레지스트리에서 자신을 해제한다.
        /// 비활성 유닛을 탐지 대상으로 남겨 두지 않기 위함이다 — 죽어서 꺼진 유닛이 계속 겨눠지면 안 된다.
        /// </summary>
        private void OnDisable()
        {
            DetachSpatialRegistry();
        }

        /// <summary>VContainer를 사용하는 주입 경로이다. 육성 진행과 레벨 곡선, 배치 컨트롤러를 받는다.</summary>
        /// <remarks>
        /// 곡선과 배치 컨트롤러는 스코프가 등록하지 않았거나 씬에 없을 수 있으므로 해석기에서 있으면 받는다.
        /// 곡선이 없으면 레벨이 수치에 반영되지 않으며, 진행은 있는데 곡선이 없는 것은 잘못된 구성이므로 조립 때
        /// 경고를 남긴다. 배치 컨트롤러가 없으면 배치 단계가 없는 무대이므로 트리는 조립 즉시 돈다.
        /// </remarks>
        /// <param name="progression">유닛 종류별 육성 진행이다.</param>
        /// <param name="resolver">레벨 곡선과 배치 컨트롤러, 공간 레지스트리, 경로 계획 서비스를 찾을 해석기이다.</param>
        [Inject]
        public void InjectRuntimeDependencies(UnitProgressionService progression, IObjectResolver resolver)
        {
            UnitLevelCurve levelCurve = null;
            UnitPlacementController placement = null;
            IUnitSpatialRegistry spatialRegistry = null;
            BattlePathfindingService pathfindingService = null;
            if (resolver != null)
            {
                resolver.TryResolve(out IUnitProgressionRules rules);
                levelCurve = rules?.UnitLevelCurve;
                resolver.TryResolve(out placement);
                resolver.TryResolve(out spatialRegistry);
                resolver.TryResolve(out pathfindingService);
            }

            ConfigureProgression(progression, levelCurve);
            ConfigurePlacement(placement);
            ConfigureSpatialRegistry(spatialRegistry);
            ConfigurePathfindingService(pathfindingService);
        }

        /// <summary>
        /// 전투 시작을 알려 줄 배치 컨트롤러를 지정한다. 없으면 행동 트리는 조립 즉시 돈다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 조립 앞뒤 어느 때 불려도 된다. 스폰된 유닛은 조립 전에 주입을 받지만, 씬에 놓인 유닛은 깨어나 조립을
        /// 마친 뒤에 주입을 받는다. 그래서 이미 도는 트리에도 문이 걸리고, 아직 전투 전이면 그동안 돈 것을
        /// 처음으로 되돌려 걷던 자리가 멈추게 한다.
        /// </para>
        /// <para>
        /// 유닛은 「전투가 시작됐는가」를 스스로 해석하지 않고
        /// <see cref="UnitPlacementController.IsBattlePhase"/>에 묻는다. 그 판정이 한 곳에 있어야 유닛마다
        /// 다르게 굴지 않는다.
        /// </para>
        /// </remarks>
        /// <param name="placement">배치 단계를 아는 컨트롤러이며, 배치 단계가 없는 무대에서는 null이다.</param>
        public void ConfigurePlacement(UnitPlacementController placement)
        {
            if (ReferenceEquals(_placement, placement))
            {
                return;
            }

            DetachPlacement();
            _placement = placement;
            if (_placement == null)
            {
                return;
            }

            _placement.PhaseChanged += OnPlacementPhaseChanged;
            if (!_placement.IsBattlePhase)
            {
                ResetBehaviourTree();
            }
        }

        /// <summary>행동 트리를 지금 돌려도 되는지 답한다. 배치 단계가 없는 무대이거나 전투가 시작됐으면 참이다.</summary>
        private bool MayTickBehaviourTree() => _placement == null || _placement.IsBattlePhase;

        /// <summary>
        /// 단계가 바뀌면 트리를 처음으로 되돌린다. 전투에 들어갈 때는 멈춰 있던 동안의 진행 없이 시작하고,
        /// 전투에서 나올 때는 걷던 자리가 멈춤을 낸다.
        /// </summary>
        /// <param name="phase">바뀐 배치 단계이다.</param>
        private void OnPlacementPhaseChanged(PlacementPhase phase) => ResetBehaviourTree();

        /// <summary>행동 트리가 지어져 있으면 처음으로 되돌린다.</summary>
        private void ResetBehaviourTree()
        {
            if (_behaviourTreeRunner != null)
            {
                _behaviourTreeRunner.ResetTree();
            }
        }

        /// <summary>배치 컨트롤러의 단계 알림 구독을 푼다. 파괴된 컨트롤러에서도 풀어야 하므로 참조로만 본다.</summary>
        private void DetachPlacement()
        {
            if (_placement is null)
            {
                return;
            }

            _placement.PhaseChanged -= OnPlacementPhaseChanged;
            _placement = null;
        }

        private void OnDestroy()
        {
            DetachPlacement();
            DetachSpatialRegistry();
        }

        /// <summary>
        /// 공간 레지스트리를 지정한다. 이미 같은 레지스트리가 물려 있으면 아무 일도 하지 않는다.
        /// </summary>
        /// <param name="registry">공간 레지스트리이며, 씬에 없으면 null이 온다.</param>
        private void ConfigureSpatialRegistry(IUnitSpatialRegistry registry)
        {
            if (ReferenceEquals(_spatialRegistry, registry))
            {
                return;
            }

            DetachSpatialRegistry();
            _spatialRegistry = registry;
            RegisterSpatialPosition();
        }

        /// <summary>
        /// 레지스트리와 진영 구성요소가 모두 준비되고 활성 상태일 때 자신을 등록한다.
        /// 주입과 활성화 중 어느 쪽이 먼저 와도 되도록 둘 다에서 이 메서드를 부른다.
        /// </summary>
        private void RegisterSpatialPosition()
        {
            if (_spatialRegistry == null || !isActiveAndEnabled || _team == null)
            {
                return;
            }

            _spatialRegistry.Register(_team, transform, CollisionRadius());
        }

        /// <summary>
        /// 경로 계획 서비스를 지정하고, 이동기가 이미 캐시되어 있으면 곧바로 다시 배선한다.
        /// </summary>
        /// <remarks>
        /// 주입과 조립(<see cref="CacheComponents"/>) 중 어느 쪽이 먼저 와도 되도록 둘 다에서
        /// <see cref="WireMover"/>를 부른다 — 공간 레지스트리와 같은 이유다.
        /// </remarks>
        /// <param name="pathfindingService">경로 계획 서비스이며, 씬에 없으면 null이 온다.</param>
        private void ConfigurePathfindingService(BattlePathfindingService pathfindingService)
        {
            _pathfindingService = pathfindingService;
            WireMover();
            WireCoverSensor();
        }

        /// <summary>등록되어 있으면 공간 레지스트리에서 자신을 해제한다.</summary>
        private void DetachSpatialRegistry()
        {
            if (_spatialRegistry == null || _team == null)
            {
                return;
            }

            _spatialRegistry.Unregister(_team);
        }

        /// <summary>
        /// 육성 진행과 레벨 곡선을 코드에서 지정한다. 조립 전에 호출해야 투영에 반영된다.
        /// </summary>
        /// <param name="progression">유닛 종류별 육성 진행이며 없으면 null이다.</param>
        /// <param name="levelCurve">레벨 곡선이며 없으면 null이다.</param>
        public void ConfigureProgression(UnitProgressionService progression, UnitLevelCurve levelCurve)
        {
            if (_isUnitInitialized)
            {
                Debug.LogWarning($"[TacticalUnit] {name}은 이미 조립되어 육성 진행을 바꿔도 반영되지 않는다.", this);
                return;
            }

            _progression = progression;
            _levelCurve = levelCurve;
        }

        /// <summary>
        /// 이 유닛의 레벨을 코드에서 직접 지정한다. 조립 전에 호출해야 투영에 반영되며, 곡선은 주입된 것을 그대로 쓴다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b><see cref="ConfigureProgression"/>과 나란히 선 별개의 통로다.</b> 육성 진행(<see cref="UnitProgressionService"/>)은
        /// 플레이어 유닛 종류만 추적하므로, 적처럼 육성 대상이 아닌 유닛의 레벨은 전투를 구성하는 쪽이 정한다. 육성
        /// 진행과 별개로 레벨 값만 받아 두고, 배율을 계산하는 자리에서 명시 레벨이 있으면 그것을, 없으면 육성 진행을
        /// 본다. <b>육성 진행이 함께 있어도 이 값이 우선한다</b> — 스테이지가 놓은 유닛의 레벨은 스테이지 데이터가
        /// 권위이고, 플레이어 유닛은 명시 레벨을 받을 일이 없다.
        /// </para>
        /// <para>
        /// <b>레벨 값만 받는다.</b> 곡선은 주입된 규칙(<see cref="IUnitProgressionRules"/>)에서만 오므로, 이 호출은
        /// 주입 전후 어디서 불러도 된다. 조립이 이미 끝났으면 <see cref="ConfigureProgression"/>과 같은 규약으로 경고만
        /// 남기고 반영하지 않는다 — 수치의 투영은 조립 때 한 번만 하기 때문이다.
        /// </para>
        /// </remarks>
        /// <param name="level">지정할 레벨이며 시작 레벨보다 낮으면 시작 레벨로 올린다.</param>
        public void ConfigureExplicitLevel(int level)
        {
            if (_isUnitInitialized)
            {
                Debug.LogWarning($"[TacticalUnit] {name}은 이미 조립되어 레벨을 바꿔도 반영되지 않는다.", this);
                return;
            }

            _explicitLevel = Mathf.Max(UnitLevelProgress.StartingLevel, level);
        }

        /// <summary>
        /// 스폰 직후 사용할 유닛 정의를 지정한다.
        /// 조립 전에 호출해야 정의가 반영되며, 이미 조립된 뒤에는 아무 일도 하지 않는다.
        /// </summary>
        /// <param name="unitDefinition">적용할 유닛 정의이다.</param>
        public void SetDefinition(UnitDefinition unitDefinition)
        {
            if (_isUnitInitialized)
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}은 이미 조립되어 유닛 정의를 바꿔도 반영되지 않는다.", this);
                return;
            }

            definition = unitDefinition;
        }

        /// <summary>
        /// 유닛 정의를 컴포넌트에 반영하고 행동 트리를 조립한다.
        /// 여러 번 호출해도 처음 한 번만 수행하며, 스폰한 쪽이 Awake보다 먼저 조립하고 싶을 때 직접 호출한다.
        /// </summary>
        public void InitializeUnit()
        {
            if (_isUnitInitialized)
            {
                return;
            }

            Initialize();
            CacheComponents();
            ApplyDefinition();
            BuildBehaviourTree();
            _isUnitInitialized = true;
        }

        /// <summary>조립에 사용할 컴포넌트를 찾아 두고, 어빌리티 시스템과 체력 문은 없으면 붙인다.</summary>
        private void CacheComponents()
        {
            _team = GetComponent<TeamMember>();
            _behaviourTreeRunner = GetComponent<BehaviourTreeRunner>();
            _mover = GetComponent<ICharacterMover>();
            _coverSensor = GetComponent<CoverSensor>();
            WireMover();
            WireCoverSensor();

            EnsureAbilitySystem();
            EnsureHealthAttributeComponent();
        }

        /// <summary>
        /// 이동기에 경로 계획·다른 유닛 회피·장애물을 끼운다. 조립과 경로 계획 서비스 주입 중 어느 쪽이
        /// 먼저 와도 되도록 둘 다에서 부르므로, <see cref="_mover"/>가 아직 없으면 조용히 넘어간다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 경로 계획 서비스는 전투 씬의 필수 인프라다 — <see cref="Units.UnitSpatialRegistry"/>와 같은
        /// 자리에 있다. 없으면(씬에 없거나 아직 주입 전이면) <see cref="PlanPath"/>가 그대로 실패로
        /// 답하고, 그 결과 이 유닛은 안 걷는다 — 걷되 장애물을 무시하는 임시 대체는 두지 않는다.
        /// </para>
        /// <para>
        /// 다른 유닛을 피하는 데 쓸 자신의 반지름은 서비스가 들고 있는 살아 있는 엄폐물 목록
        /// (<see cref="BattlePathfindingService.LiveObstacles"/>)과 함께 끼운다. 서비스가 아직 없으면 목록도 null이며 경로 요청은 실패한다. 서비스가 나중에 주입되면 이 메서드가
        /// 다시 불려 그 시점의 살아 있는 목록으로 갈아 끼운다.
        /// </para>
        /// </remarks>
        private void WireMover()
        {
            if (_mover is not PlanarCharacterMover navMeshMover)
            {
                return;
            }

            navMeshMover.SetPathPlanner(PlanPath);
            navMeshMover.SetNearbyUnitsQuery(CollectNearbyUnitCircles);
            navMeshMover.SetObstacles(_pathfindingService?.LiveObstacles, CollisionRadius());
        }

        /// <summary>
        /// 엄폐 지점을 고를 때 쓸 이동 거리 측정자를 끼운다. <see cref="WireMover"/>와 같은 이유로
        /// 조립과 서비스 주입 어느 쪽이 먼저 와도 다시 불린다.
        /// </summary>
        /// <remarks>
        /// 서비스가 없으면 <see cref="CoverSensor"/>는 모든 후보를 "못 간다"로 본다 — 이동과 같은 등급의
        /// 물러섬이다. 이 유닛에 <see cref="CoverSensor"/>가 없으면(엄폐를 안 쓰는 유닛) 조용히 넘어간다.
        /// </remarks>
        private void WireCoverSensor()
        {
            if (_coverSensor == null)
            {
                return;
            }

            _coverSensor.SetTravelDistance(_pathfindingService != null ? MeasureCoverTravelDistance : null);
        }

        private bool MeasureCoverTravelDistance(Vector3 from, Vector3 to, out float distance)
        {
            return _pathfindingService.TryMeasureTravelDistance(from, to, out distance, CollisionRadius());
        }

        /// <summary>지금 정의가 아는 이 유닛의 평면 반지름이다. 정의가 아직 없으면 등록용 기본값과 같다.</summary>
        private float CollisionRadius() => definition != null ? definition.Radius : 0.5f;

        /// <summary>
        /// <see cref="PathPlanner"/>로 이동기에 끼우는 경로 계획이다.
        /// </summary>
        /// <remarks>
        /// 경로 계획 서비스가 없으면(씬에 없거나 아직 주입 전이면) 그대로 실패한다 — 물러설 대체가 없다.
        /// </remarks>
        private bool PlanPath(Vector3 from, Vector3 destination, List<Vector3> corners)
        {
            corners.Clear();
            return _pathfindingService != null && _pathfindingService.Plan(from, destination, corners, CollisionRadius());
        }

        /// <summary>
        /// <c>SlotReachabilityCheck</c> 계약에 맞는 자리 판정이다. <c>SpreadChaseTargetBehaviour</c>가
        /// 자리를 나눠 다가갈 때 기본으로 쓴다(<c>UnitBehaviourDefinitions</c> 참고).
        /// </summary>
        /// <remarks>경로 계획 서비스가 없으면 어느 자리도 갈 수 없는 것으로 본다.</remarks>
        internal bool IsSlotReachable(Vector3 desiredPosition, out Vector3 resolvedPosition)
        {
            resolvedPosition = desiredPosition;
            return _pathfindingService != null && _pathfindingService.TryResolveReachable(desiredPosition, out resolvedPosition, CollisionRadius());
        }

        /// <summary>
        /// 이번 스텝이 훑는 자리와 겹칠 만한 다른 유닛의 원을 공간 레지스트리에서 모은다.
        /// </summary>
        /// <remarks>
        /// <see cref="NearbyUnitsQuery"/>로 이동기에 끼우는 방법이다. 레지스트리가 아직 안 붙었으면
        /// 빈 결과를 낸다 — 그동안은 다른 유닛의 충돌 원을 조회하지 않는다. 자기 자신은 결과에서 뺀다.
        /// </remarks>
        private void CollectNearbyUnitCircles(PlanarCircle sweep, List<PlanarCircle> results)
        {
            results.Clear();
            if (_spatialRegistry == null || _team == null)
            {
                return;
            }

            _nearbyTeamBuffer.Clear();
            _spatialRegistry.CollectOverlapping(sweep, _nearbyTeamBuffer);
            foreach (var candidate in _nearbyTeamBuffer)
            {
                if (candidate == _team)
                {
                    continue;
                }

                if (_spatialRegistry.TryGetCircle(candidate, out var circle))
                {
                    results.Add(circle);
                }
            }
        }

        /// <summary>
        /// 체력 문을 확보한다. 요구 컴포넌트가 적용되기 전에 만들어진 프리팹에는 없을 수 있으므로 없으면 붙인다.
        /// </summary>
        private void EnsureHealthAttributeComponent()
        {
            if (!TryGetComponent(out _health))
            {
                _health = gameObject.AddComponent<HealthAttributeComponent>();
            }
        }

        /// <summary>
        /// 어빌리티 시스템 컴포넌트를 확보하고 부여와 시간 공급을 이 유닛이 맡도록 돌린다.
        /// </summary>
        /// <remarks>
        /// 프리팹에 붙이지 않았다고 어빌리티가 조용히 빠지지 않도록 없으면 붙인다.
        /// 붙이는 순간 그 컴포넌트의 Awake가 먼저 돌 수 있으나 집합 칸이 비어 있어 아무것도 부여하지 않는다.
        /// </remarks>
        private void EnsureAbilitySystem()
        {
            if (!TryGetComponent(out _abilitySystem))
            {
                _abilitySystem = gameObject.AddComponent<GameplayAbilitySystemComponent>();
            }

            _abilitySystem.ConfigureManualControl();
        }

        /// <summary>
        /// 유닛 정의의 수치를 각 컴포넌트에 반영한다.
        /// 진영은 이미 지정되어 있으면 배치 결과를 존중해 덮어쓰지 않는다.
        /// </summary>
        private void ApplyDefinition()
        {
            if (definition == null)
            {
                Debug.LogWarning($"[TacticalUnit] {name}에 유닛 정의가 없어 기본 수치로 동작한다.", this);
                return;
            }

            if (!_team.TeamId.IsAssigned)
            {
                _team.SetTeam(definition.DefaultTeam);
            }

            if (_mover is PlanarCharacterMover navMeshMover)
            {
                navMeshMover.SetSpeed(definition.MoveSpeed);
            }

            // 바라보는 방향은 이 구성요소가 도맡는다(요구 구성요소라 언제나 있다). 각속도의 출처를
            // 이동 속도와 같이 정의 하나로 둔다.
            if (TryGetComponent<CharacterFacing>(out var facing))
            {
                facing.SetTurnSpeed(definition.TurnSpeed);
            }

            GrantAbilitySet();
            ProjectDefinitionOntoAttributes();
        }

        /// <summary>
        /// 유닛 정의의 수치와 육성 진행을 어트리뷰트에 투영한다. 이 계산은 여기 한 곳에만 있고 조립 때 한 번만 한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 최대 체력 = 정의의 최대 체력 × 곡선의 체력 배수(레벨), 공격력 = 정의의 공격력 × 곡선의 공격 배수(레벨),
        /// 레벨과 경험치 = 진행 데이터의 값이다. 명시 레벨(<see cref="ConfigureExplicitLevel"/>)이 있으면 레벨은 그 값이고
        /// 경험치는 시작값이다. 현재 체력은 최대 체력으로 채운다. 배수는 레벨 1의 값까지 곡선 데이터에서 오며 코드에는
        /// 예외가 없다. 곡선이 주입되지 않았으면 곱하지 않고 정의 수치를 그대로 쓴다.
        /// </para>
        /// <para>
        /// 어빌리티 집합이 체력 어트리뷰트를 갖추지 않았으면 체력을 조립하지 않고 경고를 남긴다.
        /// 그 유닛은 체력 문이 정의를 몰라 피해를 받지 못하며, 그것이 오류로 드러난다.
        /// </para>
        /// </remarks>
        private void ProjectDefinitionOntoAttributes()
        {
            var attributes = _abilitySystem.System.Attributes;
            if (!HealthAttributeWiring.TryConfigure(_health, attributes))
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}의 어빌리티 집합이 체력 어트리뷰트({UnitAttributeIds.Health}·{UnitAttributeIds.MaxHealth})를 " +
                    "갖추지 않아 체력을 조립하지 않는다. 유닛 정의의 어빌리티 집합에 어트리뷰트 묶음을 연결해야 한다.",
                    this);
                return;
            }

            // 명시 레벨(전투를 구성하는 쪽이 정한 유닛)이 있으면 그것이 권위이고, 없으면 플레이어의 육성 진행을 본다.
            // 명시 레벨 유닛은 육성 대상이 아니므로 경험치도 육성 진행에서 읽지 않는다 — 같은 종류를 플레이어가 키우고
            // 있어도 그 경험치가 적의 Xp로 새지 않는다.
            var level = _explicitLevel
                        ?? (_progression != null ? _progression.Levels.GetLevel(definition) : UnitLevelProgress.StartingLevel);
            var experience = _explicitLevel.HasValue
                ? UnitLevelProgress.StartingExperience
                : _progression != null
                    ? _progression.Levels.GetExperience(definition)
                    : UnitLevelProgress.StartingExperience;

            var maxHealth = definition.MaxHealth;
            float attackPower = definition.AttackDamage;
            if (_levelCurve != null)
            {
                maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * _levelCurve.GetHealthMultiplier(level)));
                attackPower *= _levelCurve.GetAttackMultiplier(level);
            }
            else if (_progression != null || _explicitLevel.HasValue)
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}의 레벨은 정해졌지만 레벨 곡선이 없어 레벨 {level}이 수치에 반영되지 않는다. " +
                    "스코프가 UnitLevelCurve를 등록해야 한다.",
                    this);
            }

            var context = new AttributeChangeContext(this);
            _health.SetMaxHealth(maxHealth);
            if (attributes.TryFindDefinition(UnitAttributeIds.AttackPower, out var attackPowerDefinition))
            {
                attributes.SetBaseValue(attackPowerDefinition, attackPower, context);
            }

            if (attributes.TryFindDefinition(UnitAttributeIds.Level, out var levelDefinition))
            {
                attributes.SetBaseValue(levelDefinition, level, context);
            }

            if (attributes.TryFindDefinition(UnitAttributeIds.Xp, out var experienceDefinition))
            {
                attributes.SetBaseValue(experienceDefinition, experience, context);
            }
        }

        /// <summary>
        /// 유닛 정의의 어빌리티 집합을 어빌리티 시스템에 부여한다. 집합이 없으면 아무것도 부여하지 않는다.
        /// </summary>
        /// <remarks>
        /// 컴포넌트가 자기 집합을 먼저 부여해 두었다면 경고를 남긴다. 유닛의 어빌리티는 정의 하나로 정하므로
        /// 두 집합이 겹치면 어느 쪽이 실제 구성인지 읽을 수 없게 된다.
        /// </remarks>
        private void GrantAbilitySet()
        {
            var abilitySet = definition.AbilitySet;
            if (abilitySet == null)
            {
                return;
            }

            if (_abilitySystem.HasGrantedAbilitySet)
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}의 어빌리티 시스템 컴포넌트가 자기 집합을 먼저 부여했다. " +
                    "유닛의 어빌리티는 유닛 정의의 집합으로 정하므로 컴포넌트의 집합 칸은 비워 두어야 한다.",
                    this);
            }

            abilitySet.GrantTo(_abilitySystem.System);
        }

        /// <summary>
        /// 고정 스텝마다 효과 실행기와 어빌리티 시스템에 시간을 흘린다.
        /// </summary>
        /// <remarks>
        /// 둘을 따로 부른다. 효과 실행기는 활성 어빌리티가 없어도 흘러야 하고(사격 간격을 여기서 센다),
        /// 어빌리티 시스템의 틱은 활성 어빌리티가 없으면 곧바로 돌아간다. 한쪽 안에 다른 쪽을 넣어 묶으면
        /// 간격이 활성 중에만 흐르게 된다. 실행기를 먼저 흘리는 것은 이 스텝에 걷힌 간격을
        /// 같은 스텝의 사격 판단이 보게 하기 위해서이다.
        /// </remarks>
        private void FixedUpdate()
        {
            if (_abilitySystem == null)
            {
                return;
            }

            var system = _abilitySystem.System;
            system.Effects.Tick(Time.fixedDeltaTime);
            system.Tick(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 행동 트리 에셋으로 이 유닛이 쓸 트리를 지어 실행기에 넘기고, 전투가 시작됐는지 묻는 문을 건다.
        /// </summary>
        /// <remarks>
        /// 에셋이 없으면 실행기를 시작하지 않는다. 무엇을 할지 적힌 것이 없는데 돌리면
        /// 유닛이 아무것도 안 하는 채로 매 틱 트리를 훑는다. 문은 배치 컨트롤러가 없으면 늘 열려 있다.
        /// </remarks>
        private void BuildBehaviourTree()
        {
            if (behaviourTree == null)
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}에 행동 트리 에셋이 없어 행동 트리를 시작하지 않는다.", this);
                return;
            }

            var buildContext = new BehaviourBuildContext(gameObject, _behaviourContext);
            var tree = behaviourTree.CreateRuntimeTree(buildContext);
            if (tree.Root == null)
            {
                Debug.LogWarning(
                    $"[TacticalUnit] {name}의 행동 트리 에셋이 비어 있어 행동 트리를 시작하지 않는다.", this);
                return;
            }

            _behaviourTreeRunner.Initialize(tree, _behaviourContext);
            _behaviourTreeRunner.SetTickGate(MayTickBehaviourTree);
        }
    }
}
