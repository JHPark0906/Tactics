using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HS.Framework.Persistence;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>전투 결과가 스테이지 이동과 진행도 저장으로 이어지는 흐름을 검증한다.</summary>
    /// <remarks>
    /// EditMode에서는 MonoBehaviour 수명주기 콜백을 기대할 수 없으므로,
    /// 모든 의존성을 공개 주입 진입점으로 직접 연결해 실제 실행 경로를 그대로 태운다.
    /// </remarks>
    public sealed class StageFlowControllerTests
    {
        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();

        }

        [Test]
        public void MissingProgressionServiceFallsBackToTheSceneCatalog()
        {
            var controllerObject = CreateObject("StageFlowController");
            var controller = controllerObject.AddComponent<StageFlowController>();
            var sceneCatalog = new FakeSceneCatalog(stageCount: 3);
            var gameFlow = new FakeGameFlowService();
            var outcomeChannel = new TestMessageChannel<BattleOutcomeDecidedEvent>();
            var saveOrchestrator = CreateSaveOrchestrator();
            controller.InjectRuntimeDependencies(
                outcomeChannel,
                gameFlow,
                sceneCatalog,
                null,
                saveOrchestrator,
                null);

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 2));

            Assert.That(controller.ProgressionService, Is.Not.Null);
            Assert.That(gameFlow.ReturnToMainMenuCount, Is.EqualTo(1), "임시 진행 상태로도 스테이지 선택으로 돌아간다.");
            Assert.That(gameFlow.LoadedLevelIds, Is.Empty);
        }

        private static SaveOrchestrator CreateSaveOrchestrator()
        {
            return new SaveOrchestrator(
                new InMemorySaveDataStorage(),
                new TestPublisher<SaveAllCompletedEvent>(),
                new TestPublisher<LoadAllCompletedEvent>());
        }

        /// <summary>결과 창을 만들어 컨트롤러의 직렬화 필드에 연결한다.</summary>
        /// <param name="controller">연결 대상 컨트롤러이다.</param>
        /// <returns>연결한 결과 창이다.</returns>
        private BattleResultWindow AttachResultWindow(StageFlowController controller)
        {
            var window = CreateObject("BattleResultWindow").AddComponent<BattleResultWindow>();
            var serializedController = new SerializedObject(controller);
            serializedController.FindProperty("resultWindow").objectReferenceValue = window;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            return window;
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>컨트롤러와 협력자를 함께 보관하는 테스트 문맥이다.</summary>
        private sealed class TestContext
        {
            public TestContext(
                StageFlowController controller,
                StageProgressionService progressionService,
                FakeGameFlowService gameFlow,
                TestMessageChannel<BattleOutcomeDecidedEvent> outcomeChannel,
                SaveOrchestrator saveOrchestrator)
            {
                Controller = controller;
                ProgressionService = progressionService;
                GameFlow = gameFlow;
                OutcomeChannel = outcomeChannel;
                SaveOrchestrator = saveOrchestrator;
            }

            public StageFlowController Controller { get; }

            public StageProgressionService ProgressionService { get; }

            public StageProgression Progression => ProgressionService.Progression;

            public FakeGameFlowService GameFlow { get; }

            public TestMessageChannel<BattleOutcomeDecidedEvent> OutcomeChannel { get; }

            public SaveOrchestrator SaveOrchestrator { get; }

            /// <summary>확정된 전투 결과를 이벤트 버스로 발행한다.</summary>
            /// <param name="outcome">발행할 전투 결과이다.</param>
            public void PublishOutcome(BattleOutcome outcome)
            {
                OutcomeChannel.Publish(new BattleOutcomeDecidedEvent(outcome, 0, 0));
            }
        }


        /// <summary>요청한 씬 전환을 기록하는 테스트용 게임 흐름 서비스이다.</summary>
        private sealed class FakeGameFlowService : IGameFlowService
        {
            /// <summary>요청된 Gameplay 레벨 ID 목록이다.</summary>
            public List<int> LoadedLevelIds { get; } = new();

            /// <summary>메인 메뉴 복귀 요청 횟수이다.</summary>
            public int ReturnToMainMenuCount { get; private set; }

            public UniTask StartNewGameAsync()
            {
                return LoadGameplayLevelAsync(1);
            }

            public UniTask LoadGameplayLevelAsync(int levelId)
            {
                LoadedLevelIds.Add(levelId);
                return UniTask.CompletedTask;
            }

            public UniTask ReturnToMainMenuAsync()
            {
                ReturnToMainMenuCount++;
                return UniTask.CompletedTask;
            }
        }

        /// <summary>1부터 지정한 개수까지의 Gameplay 레벨만 가진 테스트용 씬 카탈로그이다.</summary>
        private sealed class FakeSceneCatalog : IProjectSceneCatalog
        {
            private readonly int _stageCount;

            public FakeSceneCatalog(int stageCount)
            {
                _stageCount = stageCount;
            }

            public IReadOnlyList<ProjectSceneDefinition> Scenes { get; } = Array.Empty<ProjectSceneDefinition>();

            public int DefaultGameplayLevelId => 1;

            public SceneReference BootstrapScene { get; } = SceneReference.Create("Assets/Scenes/Bootstrap.unity");

            public SceneReference LoadingScene { get; } = SceneReference.Create("Assets/Scenes/Loading.unity");

            public SceneReference MainMenuScene { get; } = SceneReference.Create("Assets/Scenes/MainMenu.unity");

            public bool TryGetGameplayScene(int levelId, out SceneReference scene)
            {
                if (levelId < 1 || levelId > _stageCount)
                {
                    scene = null;
                    return false;
                }

                scene = SceneReference.Create($"Assets/Scenes/Stage{levelId}.unity");
                return true;
            }

            /// <summary>
            /// 테스트 오브젝트가 속한 씬은 스테이지 씬이 아니므로 항상 실패로 답한다.
            /// 이렇게 해야 컨트롤러가 진행 상태를 씬 경로로 덮어쓰지 않고 주입된 값을 그대로 쓴다.
            /// </summary>
            public bool TryGetGameplayLevelId(string scenePath, out int levelId)
            {
                levelId = default;
                return false;
            }
        }

        /// <summary>메모리 사전에 기록하는 테스트용 저장소 백엔드이다.</summary>
        private sealed class InMemorySaveDataStorage : ISaveDataStorage
        {
            private readonly Dictionary<string, string> _values = new();

            public bool Exists(string key)
            {
                return _values.ContainsKey(key);
            }

            public bool TryRead(string key, out string value)
            {
                return _values.TryGetValue(key, out value);
            }

            public void Write(string key, string value)
            {
                _values[key] = value;
            }

            public void Delete(string key)
            {
                _values.Remove(key);
            }
        }
    }
}
