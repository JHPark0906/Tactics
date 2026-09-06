using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HS.Framework.Foundation.Input;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using R3;
using MessagePipe;
using UnityEngine.SceneManagement;
using VContainer;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>씬 전환 정책과 낮은 수준 로더의 책임 분리를 검증한다.</summary>
    public sealed class SceneTransitionCoordinatorTests
    {
        [Test]
        public void FrameworkRegistrationUsesTheConfiguredRecoverySceneAndRestoresInput()
        {
            var configuration = FrameworkProjectConfiguration.LoadRequired();
            var input = new FakeInputStateController(true);
            var loader = new RecordingSceneLoader { FailedScene = configuration.BootstrapScene };
            var failed = new TestMessageChannel<SceneLoadFailedEvent>();
            loader.BeforeLoad = _ => Assert.That(input.IsUiInputEnabled, Is.False, "실제 엔트리포인트 배선이 전환 전에 입력을 막아야 한다.");
            using var container = BuildRegisteredFrameworkServices(configuration, loader, input, failed);
            var transition = container.Resolve<ISceneTransitionService>();

            Assert.Throws<InvalidOperationException>(() =>
                transition.LoadSceneAsync(configuration.BootstrapScene).GetAwaiter().GetResult());

            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { configuration.BootstrapScene, configuration.MainMenuScene }));
            Assert.That(failed.Published, Has.Count.EqualTo(1));
            Assert.That(failed.Published[0].WasRecovered, Is.True);
            Assert.That(input.LiveBlockCount, Is.Zero);
            Assert.That(input.IsUiInputEnabled, Is.True);
        }

        [Test]
        public void RegisteredReturnFlowKeepsTheSourceSceneAndCanRetryAfterFailure()
        {
            var configuration = FrameworkProjectConfiguration.LoadRequired();
            var input = new FakeInputStateController(true);
            var loader = new RecordingSceneLoader { FailedScene = configuration.MainMenuScene };
            var failed = new TestMessageChannel<SceneLoadFailedEvent>();
            using var container = BuildRegisteredFrameworkServices(configuration, loader, input, failed);
            var flow = container.Resolve<IGameFlowService>();

            Assert.Throws<InvalidOperationException>(() => flow.ReturnToMainMenuAsync().GetAwaiter().GetResult());

            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { configuration.MainMenuScene }),
                "로딩 씬을 먼저 활성화하면 결과 창이 사라져 재시도할 수 없다.");
            Assert.That(failed.Published[0].WasRecovered, Is.False);
            Assert.That(input.LiveBlockCount, Is.Zero);

            loader.FailedScene = null;
            flow.ReturnToMainMenuAsync().GetAwaiter().GetResult();

            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { configuration.MainMenuScene, configuration.MainMenuScene }));
            Assert.That(input.IsUiInputEnabled, Is.True);
            Assert.That(container.Resolve<ISceneTransitionService>().IsLoading, Is.False);
        }

        // 코디네이터를 테스트에서 따로 조립하면 실제 등록에서 recoveryScene을 빠뜨린 결함을 놓친다.
        // 실제 부트스트랩 서비스 등록을 실행하고 Unity의 저수준 로더만 대체한다.
        private static IObjectResolver BuildRegisteredFrameworkServices(
            IProjectSceneCatalog catalog,
            RecordingSceneLoader loader,
            FakeInputStateController input,
            TestMessageChannel<SceneLoadFailedEvent> failed)
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IProjectSceneCatalog>(catalog);
            builder.RegisterInstance<IInputStateController>(input);
            var started = new TestMessageChannel<SceneLoadStartedEvent>();
            var completed = new TestMessageChannel<SceneLoadCompletedEvent>();
            builder.RegisterInstance(started).As<IPublisher<SceneLoadStartedEvent>>().As<ISubscriber<SceneLoadStartedEvent>>();
            builder.RegisterInstance(completed).As<IPublisher<SceneLoadCompletedEvent>>().As<ISubscriber<SceneLoadCompletedEvent>>();
            builder.RegisterInstance(failed).As<IPublisher<SceneLoadFailedEvent>>().As<ISubscriber<SceneLoadFailedEvent>>();
            builder.RegisterInstance<IPublisher<PauseChangedEvent>>(new TestPublisher<PauseChangedEvent>());
            var register = typeof(FrameworkLifetimeScope).GetMethod("RegisterFrameworkServices", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(register, Is.Not.Null);
            register.Invoke(null, new object[] { builder });
            builder.RegisterInstance<ISceneLoader>(loader);
            return builder.Build();
        }

        [Test]
        public void DirectTransitionPublishesEventsAndRetainsLatestProgressState()
        {
            var loader = new RecordingSceneLoader();
            var startedPublisher = new TestPublisher<SceneLoadStartedEvent>();
            var completedPublisher = new TestPublisher<SceneLoadCompletedEvent>();
            var failedPublisher = new TestPublisher<SceneLoadFailedEvent>();
            using var coordinator = new SceneTransitionCoordinator(
                loader,
                new ImmediateDisplayPolicy(),
                startedPublisher,
                completedPublisher,
                failedPublisher);
            var observedStates = new List<SceneTransitionState>();
            using var stateSubscription = coordinator.State.Subscribe(observedStates.Add);
            var destination = SceneFromBuildSettings("Bootstrap.unity");

            coordinator.LoadSceneAsync(destination).GetAwaiter().GetResult();

            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { destination }));
            Assert.That(startedPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(completedPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(coordinator.IsLoading, Is.False);
            var latestState = observedStates[observedStates.Count - 1];
            Assert.That(latestState.Phase, Is.EqualTo(SceneLoadPhase.Completed));
            Assert.That(latestState.Value, Is.EqualTo(1f));
            Assert.That(latestState.OperationId, Is.GreaterThan(0));
            Assert.That(latestState.Destination, Is.SameAs(destination));
        }

        [Test]
        public void FailedTransitionLoadsMainMenuAsRecoveryScene()
        {
            var destination = SceneFromBuildSettings("Bootstrap.unity");
            var mainMenu = SceneFromBuildSettings("MainMenu.unity");
            var loader = new RecordingSceneLoader { FailedScene = destination };
            var startedPublisher = new TestPublisher<SceneLoadStartedEvent>();
            var completedPublisher = new TestPublisher<SceneLoadCompletedEvent>();
            var failedPublisher = new TestPublisher<SceneLoadFailedEvent>();
            using var coordinator = new SceneTransitionCoordinator(
                loader,
                new ImmediateDisplayPolicy(),
                startedPublisher,
                completedPublisher,
                failedPublisher,
                mainMenu);
            var failedEvent = default(SceneLoadFailedEvent);
            var latestState = default(SceneTransitionState);
            using var stateSubscription = coordinator.State.Subscribe(state => latestState = state);

            Assert.Throws<InvalidOperationException>(() =>
                coordinator.LoadSceneAsync(destination).GetAwaiter().GetResult());

            Assert.That(loader.LoadedScenes, Is.EqualTo(new[] { destination, mainMenu }));
            Assert.That(failedPublisher.Published, Has.Count.EqualTo(1));
            failedEvent = failedPublisher.Published[0];
            Assert.That(failedEvent.Destination, Is.SameAs(destination));
            Assert.That(failedEvent.RecoveryScene, Is.SameAs(mainMenu));
            Assert.That(failedEvent.WasRecovered, Is.True);
            Assert.That(failedEvent.RecoveryException, Is.Null);
            Assert.That(latestState.Phase, Is.EqualTo(SceneLoadPhase.Recovered));
            Assert.That(coordinator.IsLoading, Is.False);
        }

        /// <summary>빌드 세팅에 등록된 씬 가운데 파일 이름이 맞는 것을 찾아 참조를 만든다.</summary>
        /// <remarks>
        /// 코디네이터가 실제 빌드 세팅을 확인하므로, 경로를 코드에 적어 두면 씬이 옮겨질 때마다 낡는다.
        /// 등록된 목록에서 찾으면 어느 폴더에 있든 같은 씬을 가리킨다.
        /// </remarks>
        /// <param name="fileName">찾을 씬 파일 이름이다.</param>
        /// <returns>빌드 세팅에 등록된 그 씬의 참조이다.</returns>
        private static SceneReference SceneFromBuildSettings(string fileName)
        {
            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(index);
                if (path.EndsWith(fileName, StringComparison.Ordinal))
                {
                    return SceneReference.Create(path);
                }
            }

            throw new InvalidOperationException($"빌드 세팅에 {fileName} 이 없다.");
        }

        private sealed class RecordingSceneLoader : ISceneLoader
        {
            public bool IsLoading { get; private set; }

            public List<SceneReference> LoadedScenes { get; } = new();

            public SceneReference FailedScene { get; set; }

            public Action<SceneReference> BeforeLoad { get; set; }

            public UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null)
            {
                IsLoading = true;
                LoadedScenes.Add(scene);
                BeforeLoad?.Invoke(scene);
                if (scene.Equals(FailedScene))
                {
                    IsLoading = false;
                    throw new InvalidOperationException("테스트 씬 로딩 실패");
                }

                progress?.Invoke(0.4f);
                progress?.Invoke(1f);
                IsLoading = false;
                return UniTask.CompletedTask;
            }
        }

        private sealed class ImmediateDisplayPolicy : ILoadingSceneDisplayPolicy
        {
            public UniTask WaitForMinimumDisplayAsync() => UniTask.CompletedTask;
        }

    }
}
