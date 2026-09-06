using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using NUnit.Framework;
using R3;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>게임 플로우 서비스가 씬 카탈로그를 씬 전환 요청으로 올바르게 변환하는지 검증한다.</summary>
    public sealed class GameFlowServiceTests
    {
        private static readonly SceneReference LoadingScene = SceneReference.Create("Assets/Scenes/Loading.unity");
        private static readonly SceneReference MainMenuScene = SceneReference.Create("Assets/Scenes/MainMenu.unity");
        private static readonly SceneReference Level1Scene = SceneReference.Create("Assets/Scenes/Level1.unity");
        private static readonly SceneReference Level2Scene = SceneReference.Create("Assets/Scenes/Level2.unity");

        [Test]
        public void StartNewGameLoadsDefaultLevelThroughLoadingScene()
        {
            var catalog = CreateCatalog(defaultLevelId: 2);
            var transitionService = new RecordingTransitionService();
            var gameFlowService = new GameFlowService(catalog, transitionService);

            gameFlowService.StartNewGameAsync().GetAwaiter().GetResult();

            Assert.That(transitionService.Transitions, Has.Count.EqualTo(1));
            Assert.That(transitionService.Transitions[0].LoadingScene, Is.SameAs(LoadingScene));
            Assert.That(transitionService.Transitions[0].Destination, Is.SameAs(Level2Scene));
        }

        [Test]
        public void LoadGameplayLevelRoutesToRegisteredLevelScene()
        {
            var catalog = CreateCatalog(defaultLevelId: 1);
            var transitionService = new RecordingTransitionService();
            var gameFlowService = new GameFlowService(catalog, transitionService);

            gameFlowService.LoadGameplayLevelAsync(1).GetAwaiter().GetResult();

            Assert.That(transitionService.Transitions, Has.Count.EqualTo(1));
            Assert.That(transitionService.Transitions[0].LoadingScene, Is.SameAs(LoadingScene));
            Assert.That(transitionService.Transitions[0].Destination, Is.SameAs(Level1Scene));
        }

        [Test]
        public void LoadGameplayLevelThrowsForUnregisteredLevelWithoutTransition()
        {
            var catalog = CreateCatalog(defaultLevelId: 1);
            var transitionService = new RecordingTransitionService();
            var gameFlowService = new GameFlowService(catalog, transitionService);

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                gameFlowService.LoadGameplayLevelAsync(99).GetAwaiter().GetResult());

            Assert.That(exception.ParamName, Is.EqualTo("levelId"));
            Assert.That(transitionService.Transitions, Is.Empty);
        }

        [Test]
        public void ReturnToMainMenuLoadsDirectlyToKeepTheCurrentRetryUiUntilActivation()
        {
            var catalog = CreateCatalog(defaultLevelId: 1);
            var transitionService = new RecordingTransitionService();
            var gameFlowService = new GameFlowService(catalog, transitionService);

            gameFlowService.ReturnToMainMenuAsync().GetAwaiter().GetResult();

            Assert.That(transitionService.Transitions, Has.Count.EqualTo(1));
            Assert.That(transitionService.Transitions[0].LoadingScene, Is.Null);
            Assert.That(transitionService.Transitions[0].Destination, Is.SameAs(MainMenuScene));
        }

        private static FakeSceneCatalog CreateCatalog(int defaultLevelId)
        {
            return new FakeSceneCatalog(
                defaultLevelId,
                new Dictionary<int, SceneReference>
                {
                    [1] = Level1Scene,
                    [2] = Level2Scene
                });
        }

        private sealed class FakeSceneCatalog : IProjectSceneCatalog
        {
            private readonly Dictionary<int, SceneReference> _gameplayScenes;

            public FakeSceneCatalog(int defaultGameplayLevelId, Dictionary<int, SceneReference> gameplayScenes)
            {
                DefaultGameplayLevelId = defaultGameplayLevelId;
                _gameplayScenes = gameplayScenes;
            }

            public IReadOnlyList<ProjectSceneDefinition> Scenes => Array.Empty<ProjectSceneDefinition>();

            public int DefaultGameplayLevelId { get; }

            public SceneReference BootstrapScene => null;

            public SceneReference LoadingScene => GameFlowServiceTests.LoadingScene;

            public SceneReference MainMenuScene => GameFlowServiceTests.MainMenuScene;

            public bool TryGetGameplayScene(int levelId, out SceneReference scene)
            {
                return _gameplayScenes.TryGetValue(levelId, out scene);
            }

            public bool TryGetGameplayLevelId(string scenePath, out int levelId)
            {
                levelId = default;
                return false;
            }
        }

        private sealed class RecordingTransitionService : ISceneTransitionService
        {
            public List<(SceneReference LoadingScene, SceneReference Destination)> Transitions { get; } = new();

            public bool IsLoading => false;

            public Observable<SceneTransitionState> State => Observable.Empty<SceneTransitionState>();

            public UniTask LoadSceneAsync(SceneReference scene)
            {
                Transitions.Add((null, scene));
                return UniTask.CompletedTask;
            }

            public UniTask LoadSceneAfterLoadingSceneAsync(SceneReference loadingScene, SceneReference destination)
            {
                Transitions.Add((loadingScene, destination));
                return UniTask.CompletedTask;
            }

            public void ReportInitializationProgress(float progress)
            {
            }
        }
    }
}
