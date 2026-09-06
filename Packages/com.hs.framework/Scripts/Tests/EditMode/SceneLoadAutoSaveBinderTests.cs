using System.Collections.Generic;
using HS.Framework.Bootstrap;
using HS.Framework.Persistence;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>씬 로딩 완료 자동 저장 배선의 부착·해제 규칙을 검증한다.</summary>
    public sealed class SceneLoadAutoSaveBinderTests
    {
        private readonly List<GameObject> _createdGameObjects = new();
        private TestSubscriber<SceneLoadCompletedEvent> _sceneCompleted;
        private TestPublisher<SaveAllCompletedEvent> _savePublisher;
        private TestPublisher<LoadAllCompletedEvent> _loadPublisher;
        private InMemorySaveDataStorage _storage;
        private List<SaveAllCompletedEvent> _saveEvents;

        [SetUp]
        public void SetUp()
        {
            _sceneCompleted = new TestSubscriber<SceneLoadCompletedEvent>();
            _savePublisher = new TestPublisher<SaveAllCompletedEvent>();
            _loadPublisher = new TestPublisher<LoadAllCompletedEvent>();
            _storage = new InMemorySaveDataStorage();
            _saveEvents = _savePublisher.Published;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in _createdGameObjects)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }

            _createdGameObjects.Clear();
        }

        [Test]
        public void RepeatedInjectionDoesNotStackDuplicateTriggers()
        {
            var orchestrator = CreateOrchestrator(out _);
            var binder = CreateBinder();
            binder.InjectMessagePipeDependencies(orchestrator, _sceneCompleted);

            binder.InjectMessagePipeDependencies(orchestrator, _sceneCompleted);
            PublishSceneLoadCompleted();

            Assert.That(_saveEvents, Has.Count.EqualTo(1), "재주입으로 구독이 중복되면 안 된다.");
        }

        [Test]
        public void DisablingTheBinderDetachesTheTriggerAndReenablingRestoresIt()
        {
            var orchestrator = CreateOrchestrator(out _);
            var binder = CreateBinder();
            binder.InjectMessagePipeDependencies(orchestrator, _sceneCompleted);

            MonoBehaviourLifecycle.InvokeOnDisable(binder);
            Assert.That(binder.IsAttached, Is.False);
            PublishSceneLoadCompleted();
            Assert.That(_saveEvents, Is.Empty);

            MonoBehaviourLifecycle.InvokeOnEnable(binder);
            Assert.That(binder.IsAttached, Is.True);
            PublishSceneLoadCompleted();
            Assert.That(_saveEvents, Has.Count.EqualTo(1));
        }

        [Test]
        public void DestroyingTheBinderDetachesTheTrigger()
        {
            var orchestrator = CreateOrchestrator(out _);
            var binder = CreateBinder();
            binder.InjectMessagePipeDependencies(orchestrator, _sceneCompleted);

            MonoBehaviourLifecycle.InvokeOnDestroy(binder);
            PublishSceneLoadCompleted();

            Assert.That(_saveEvents, Is.Empty, "파괴된 뒤에는 자동 저장이 실행되면 안 된다.");
        }

        private void PublishSceneLoadCompleted()
        {
            _sceneCompleted.Publish(new SceneLoadCompletedEvent(SceneReference.Create("Assets/Scenes/Level1.unity")));
        }

        private SaveOrchestrator CreateOrchestrator(out FakeSaveable saveable)
        {
            var orchestrator = new SaveOrchestrator(_storage, _savePublisher, _loadPublisher);
            saveable = new FakeSaveable("player") { IsDirty = true, State = "player-state" };
            orchestrator.Register(saveable);
            return orchestrator;
        }

        private SceneLoadAutoSaveBinder CreateBinder()
        {
            var gameObject = new GameObject(nameof(SceneLoadAutoSaveBinder));
            _createdGameObjects.Add(gameObject);
            return gameObject.AddComponent<SceneLoadAutoSaveBinder>();
        }

        /// <summary>저장 호출 횟수를 관찰하기 위해 dirty 상태를 유지하는 테스트용 참여자이다.</summary>
        private sealed class FakeSaveable : ISaveable
        {
            public FakeSaveable(string saveKey)
            {
                SaveKey = saveKey;
            }

            public string SaveKey { get; }

            public bool IsDirty { get; set; }

            public string State { get; set; }

            public string CaptureState()
            {
                return State;
            }

            public void RestoreState(string serializedState)
            {
                State = serializedState;
            }

            public void AcknowledgeSaved()
            {
            }
        }

        /// <summary>메모리에 키와 값을 보관하는 테스트용 저장소이다.</summary>
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
