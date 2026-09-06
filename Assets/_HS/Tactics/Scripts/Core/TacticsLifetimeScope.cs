using UnityEngine;
using HS.Framework.Foundation.Patterns;
using HS.Tactics.Pathfinding;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using HS.Tactics.Flow;
using HS.Framework.Persistence;
using MessagePipe;
using VContainer;
using VContainer.Unity;
using HS.Framework.Runtime;

namespace HS.Tactics.Core
{
    /// <summary>
    /// Tactics 게임 레이어의 의존성과 메시지 broker를 Framework Scope에 추가한다.
    /// </summary>
    public sealed class TacticsLifetimeScope : FrameworkLifetimeScope
    {
        [Tooltip("스테이지 경험치 표와 레벨 곡선을 읽는 진행 규칙 컴포넌트이다. 비워 두면 보상을 주지 않는다.")]
        [SerializeField] private TacticsProgressionRules progressionRules;

        protected override void ConfigureProjectServices(
            IContainerBuilder builder,
            MessagePipeOptions messagePipeOptions)
        {
            builder.RegisterMessageBroker<BattleOutcomeDecidedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<PlacementCompletedEvent>(messagePipeOptions);

            // 씬을 오가도 유지되는 프로젝트 상태는 Scope 수명으로 승격한다.
            builder.Register<StageProgressionService>(Lifetime.Singleton);
            builder.Register<UnitProgressionService>(Lifetime.Singleton);

            // MainMenu의 배치 패널이 확정한 선택을 게임플레이 씬 로드 너머로 들고 가야 하므로
            // 씬 전환에도 살아남는 Singleton으로 등록한다. 위 두 서비스와 같은 이유다.
            builder.Register<PartySelectionService>(Lifetime.Singleton);
            builder.Register<StageRewardService>(resolver => new StageRewardService(
                resolver.Resolve<UnitProgressionService>().Levels,
                progressionRules != null ? progressionRules.StageExperienceTable : null,
                progressionRules != null ? progressionRules.UnitLevelCurve : null), Lifetime.Singleton);

            // 레벨 곡선은 유닛 조립(TacticalUnit)이 있으면 받고 없으면 곱하지 않는다 — 인스펙터가 비어 있으면 등록하지 않는다.
            // 구체 ScriptableObject 타입이 아니라 계약으로 등록한다 — 구체 타입을 토큰으로 쓰면 다른 등록자가
            // 같은 타입을 또 등록했을 때 어느 쪽이 이겼는지 알 길이 없다.
            if (progressionRules != null)
            {
                builder.RegisterInstance<IUnitProgressionRules>(progressionRules);
            }
            builder.RegisterInstance<ISaveDataStorage>(new FileSaveDataStorage());

            // 승패 집계는 전투 씬마다 새로 놓이는 컴포넌트라 Transient여야 한다.
            // Singleton이면 첫 씬의 서비스가 컨테이너에 붙들려 다음 스테이지의 유닛이 죽은 서비스에 등록된다.
            // 씬 안에서의 재사용은 확인자가 자기 캐시로 맡는다.
            // 비활성인 서비스는 사망 이벤트를 구독하지 않으므로 찾지 않는다 — 받아 봐야 아무것도 집계되지 않는다.
            var battleUnitRegistryLocator =
                new SceneComponentLocator<BattleOutcomeService>(FindObjectsInactive.Exclude);
            builder.Register<IBattleUnitRegistry>(
                _ => battleUnitRegistryLocator.Resolve(),
                Lifetime.Transient);

            // 배치 컨트롤러도 스테이지 씬마다 새로 놓이므로 같은 방식으로 Transient로 넘긴다.
            // 유닛은 이것에 전투가 시작됐는지 묻고, 씬에 없으면(배치 단계가 없는 무대) null을 받아 곧바로 돈다.
            // 비활성인 컨트롤러도 찾는다 — 잠시 꺼져 있어도 그 씬의 단계 출처는 그것 하나이며,
            // 흐름 컨트롤러도 같은 방식으로 찾는다.
            var placementControllerLocator =
                new SceneComponentLocator<UnitPlacementController>(FindObjectsInactive.Include);
            builder.Register<UnitPlacementController>(
                _ => placementControllerLocator.Resolve(),
                Lifetime.Transient);

            // 공간 레지스트리도 스테이지 씬마다 새로 놓인다. Singleton으로 승격하면 승패 집계와 같은
            // 이유로 다음 스테이지의 유닛이 이전 씬의 죽은 레지스트리에 등록되는 사고가 난다.
            var spatialRegistryLocator =
                new SceneComponentLocator<UnitSpatialRegistry>(FindObjectsInactive.Include);
            builder.Register<IUnitSpatialRegistry>(
                _ => spatialRegistryLocator.Resolve(),
                Lifetime.Transient);

            // 경로 계획 서비스도 스테이지 씬마다 새로 놓인다. 같은 이유로 Singleton이 아니라
            // Transient여야 한다 — 그러지 않으면 다음 스테이지의 유닛이 이전 씬의 죽은 그래프에 경로를 묻는다.
            var pathfindingServiceLocator =
                new SceneComponentLocator<BattlePathfindingService>(FindObjectsInactive.Include);
            builder.Register<BattlePathfindingService>(
                _ => pathfindingServiceLocator.Resolve(),
                Lifetime.Transient);
            builder.Register<SaveOrchestrator>(resolver => new SaveOrchestrator(
                resolver.Resolve<ISaveDataStorage>(),
                resolver.Resolve<IPublisher<SaveAllCompletedEvent>>(),
                resolver.Resolve<IPublisher<LoadAllCompletedEvent>>()), Lifetime.Singleton);
            builder.RegisterEntryPoint<TacticsPersistenceInitializer>();
        }
    }

    /// <summary>
    /// Tactics Scope에서 진행도 저장 참여자를 연결하고 초기 복원을 수행한다.
    /// </summary>
    public sealed class TacticsPersistenceInitializer : IStartable
    {
        private readonly StageProgressionService _progressionService;
        private readonly UnitProgressionService _unitProgressionService;
        private readonly SaveOrchestrator _saveOrchestrator;

        public TacticsPersistenceInitializer(
            StageProgressionService progressionService,
            UnitProgressionService unitProgressionService,
            SaveOrchestrator saveOrchestrator)
        {
            _progressionService = progressionService;
            _saveOrchestrator = saveOrchestrator;
            _unitProgressionService = unitProgressionService;
        }

        /// <summary>저장 참여자를 등록하고 저장 데이터를 복원한다.</summary>
        public void Start()
        {
            _saveOrchestrator.Register(_progressionService.Saveable);
            _saveOrchestrator.Register(_unitProgressionService.Saveable);
            _saveOrchestrator.LoadAll();
        }
    }
}
