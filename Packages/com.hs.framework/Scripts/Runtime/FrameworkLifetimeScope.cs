using HS.Framework.Ability;
using HS.Framework.Foundation.Input;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Triggers;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Persistence;
using HS.Framework.ProjectManagement;
using HS.Framework.Settings;
using HS.Framework.Scene;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace HS.Framework.Runtime
{
    public class FrameworkLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            var projectConfiguration = FrameworkProjectConfiguration.LoadRequired();
            var messagePipeOptions = builder.RegisterMessagePipe();

            // 같은 인스턴스를 여러 타입으로 등록할 때는 RegisterInstance를 따로따로 부르면 안 된다 —
            // VContainer의 레지스트리는 등록을 구체 타입 기준으로 관리해서, 같은 인스턴스를 호출마다
            // 새로 등록하면 "같은 키가 이미 있다"는 예외로 죽는다. 반드시 한 번만 등록하고 .As<>()로
            // 필요한 인터페이스를 체이닝한다.
            // 어빌리티 시스템·효과 실행기·행동 트리 실행기는 IGameplayTickModeSource만 알고 IProjectConfiguration을
            // 직접 참조하지 않는다. 참조 방향을 ProjectManagement → Ability 한쪽으로만 유지하기 위해서다.
            builder.RegisterInstance(projectConfiguration)
                .As<IProjectConfiguration>()
                .As<IProjectSceneCatalog>()
                .As<IGameplayTickModeSource>();
            builder.Register<IInputStateController>(_ => InputSettingsService.Current, Lifetime.Singleton);
            builder.RegisterInstance<ITeamRelationPolicy>(DefaultTeamRelationPolicy.Instance);

            builder.RegisterMessageBroker<DeathEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<DamageAppliedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<GameTriggerEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<PauseChangedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SceneLoadStartedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SceneLoadCompletedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SceneLoadFailedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SaveAllCompletedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<LoadAllCompletedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SaveGameLoadedEvent>(messagePipeOptions);
            builder.RegisterMessageBroker<SaveGameSavedEvent>(messagePipeOptions);
            ConfigureProjectServices(builder, messagePipeOptions);

            RegisterFrameworkServices(builder);
            builder.RegisterEntryPoint<FrameworkInitializer>();

            builder.RegisterBuildCallback(
                resolver => GlobalMessagePipe.SetProvider(
                    resolver.AsServiceProvider()));
        }

        /// <summary>
        /// 게임 프로젝트가 자신의 broker와 서비스를 추가하는 확장 지점이다.
        /// </summary>
        protected virtual void ConfigureProjectServices(
            IContainerBuilder builder,
            MessagePipeOptions messagePipeOptions)
        {
        }
        
        private static void RegisterFrameworkServices(IContainerBuilder builder)
        {
            builder.Register<SceneLoader>(Lifetime.Singleton)
                .As<ISceneLoader>();
            builder.Register<LoadingSceneDisplayPolicy>(Lifetime.Singleton)
                .As<ILoadingSceneDisplayPolicy>();
            builder.Register<ISceneTransitionService>(resolver =>
                new SceneTransitionCoordinator(
                    resolver.Resolve<ISceneLoader>(),
                    resolver.Resolve<ILoadingSceneDisplayPolicy>(),
                    resolver.Resolve<IPublisher<SceneLoadStartedEvent>>(),
                    resolver.Resolve<IPublisher<SceneLoadCompletedEvent>>(),
                    resolver.Resolve<IPublisher<SceneLoadFailedEvent>>(),
                    resolver.Resolve<IProjectSceneCatalog>().MainMenuScene),
                Lifetime.Singleton);
            builder.Register<IGameFlowService>(resolver => new GameFlowService(
                    resolver.Resolve<IProjectSceneCatalog>(),
                    resolver.Resolve<ISceneTransitionService>()),
                Lifetime.Singleton);
            // 둘 다 씬 전환 메시지를 구독해야 일을 하는데 해석하는 소비자가 없거나 드물다.
            // 엔트리포인트로 등록해야 컨테이너가 만들고 IInitializable.Initialize로 구독을 건다.
            // RegisterEntryPoint는 구현한 인터페이스를 모두 노출하므로 IPauseService도 여기서 해석된다.
            builder.RegisterEntryPoint<PauseService>();
            builder.RegisterEntryPoint<SceneInputGate>();
        }

        protected override void Awake()
        {
            base.Awake();
            
            DontDestroyOnLoad(gameObject);
        }
    }
}
