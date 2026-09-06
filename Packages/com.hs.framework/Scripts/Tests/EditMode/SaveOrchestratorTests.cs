using System.Collections.Generic;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using MessagePipe;
using NUnit.Framework;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>저장 조정자의 등록, 일괄 저장·복원, 자동 저장 훅을 검증한다.</summary>
    public sealed class SaveOrchestratorTests
    {
        [Test]
        public void RegisterRejectsDuplicateSaveKey()
        {
            var orchestrator = CreateOrchestrator(new InMemorySaveDataStorage());
            orchestrator.Register(new FakeSaveable("player"));

            Assert.That(
                () => orchestrator.Register(new FakeSaveable("player")),
                Throws.ArgumentException);
        }

        [Test]
        public void AutomaticSaveSkipsCleanParticipants()
        {
            var storage = new InMemorySaveDataStorage();
            var orchestrator = CreateOrchestrator(storage);
            var dirtySaveable = new FakeSaveable("dirty") { IsDirty = true, State = "dirty-state" };
            var cleanSaveable = new FakeSaveable("clean") { IsDirty = false, State = "clean-state" };
            orchestrator.Register(dirtySaveable);
            orchestrator.Register(cleanSaveable);

            var savedCount = orchestrator.SaveAll(SaveKind.Automatic);

            Assert.That(savedCount, Is.EqualTo(1));
            Assert.That(storage.TryRead("dirty", out var savedState), Is.True);
            Assert.That(savedState, Is.EqualTo("dirty-state"));
            Assert.That(storage.Exists("clean"), Is.False);
            Assert.That(dirtySaveable.AcknowledgeCount, Is.EqualTo(1));
            Assert.That(cleanSaveable.AcknowledgeCount, Is.EqualTo(0));
        }

        [Test]
        public void ManualSaveIncludesCleanParticipants()
        {
            var storage = new InMemorySaveDataStorage();
            var orchestrator = CreateOrchestrator(storage);
            orchestrator.Register(new FakeSaveable("dirty") { IsDirty = true, State = "dirty-state" });
            orchestrator.Register(new FakeSaveable("clean") { IsDirty = false, State = "clean-state" });

            var savedCount = orchestrator.SaveAll(SaveKind.Manual);

            Assert.That(savedCount, Is.EqualTo(2));
            Assert.That(storage.Exists("dirty"), Is.True);
            Assert.That(storage.Exists("clean"), Is.True);
        }

        [Test]
        public void LoadAllRestoresOnlyStoredParticipants()
        {
            var storage = new InMemorySaveDataStorage();
            storage.Write("stored", "stored-state");
            var orchestrator = CreateOrchestrator(storage);
            var storedSaveable = new FakeSaveable("stored");
            var missingSaveable = new FakeSaveable("missing");
            orchestrator.Register(storedSaveable);
            orchestrator.Register(missingSaveable);

            var restoredCount = orchestrator.LoadAll();

            Assert.That(restoredCount, Is.EqualTo(1));
            Assert.That(storedSaveable.RestoredState, Is.EqualTo("stored-state"));
            Assert.That(missingSaveable.RestoredState, Is.Null);
        }

        [Test]
        public void UnregisterStopsSaving()
        {
            var storage = new InMemorySaveDataStorage();
            var orchestrator = CreateOrchestrator(storage);
            var saveable = new FakeSaveable("player") { IsDirty = true, State = "state" };
            orchestrator.Register(saveable);

            Assert.That(orchestrator.Unregister(saveable), Is.True);
            Assert.That(orchestrator.SaveAll(SaveKind.Manual), Is.EqualTo(0));
            Assert.That(storage.Exists("player"), Is.False);
        }

        [Test]
        public void SaveAllPublishesCompletedEventWithKind()
        {
            var savePublisher = new TestPublisher<SaveAllCompletedEvent>();
            var loadPublisher = new TestPublisher<LoadAllCompletedEvent>();
            var orchestrator = new SaveOrchestrator(new InMemorySaveDataStorage(), savePublisher, loadPublisher);
            orchestrator.Register(new FakeSaveable("player") { IsDirty = true, State = "state" });

            orchestrator.SaveAll(SaveKind.Automatic);

            Assert.That(savePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(savePublisher.Published[0].Kind, Is.EqualTo(SaveKind.Automatic));
            Assert.That(savePublisher.Published[0].SavedCount, Is.EqualTo(1));
        }

        [Test]
        public void AttachedAutoSaveTriggerSavesDirtyParticipantsOnEvent()
        {
            var savePublisher = new TestPublisher<SaveAllCompletedEvent>();
            var loadPublisher = new TestPublisher<LoadAllCompletedEvent>();
            var triggerSubscriber = new TestSubscriber<TestTriggerEvent>();
            var storage = new InMemorySaveDataStorage();
            var orchestrator = new SaveOrchestrator(storage, savePublisher, loadPublisher);
            var saveable = new FakeSaveable("player") { IsDirty = true, State = "state" };
            orchestrator.Register(saveable);
            using var trigger = orchestrator.AttachAutoSaveTrigger<TestTriggerEvent>(triggerSubscriber);

            triggerSubscriber.Publish(new TestTriggerEvent());

            Assert.That(storage.Exists("player"), Is.True);
            Assert.That(saveable.AcknowledgeCount, Is.EqualTo(1));
        }

        [Test]
        public void DetachedAutoSaveTriggerNoLongerSaves()
        {
            var savePublisher = new TestPublisher<SaveAllCompletedEvent>();
            var loadPublisher = new TestPublisher<LoadAllCompletedEvent>();
            var triggerSubscriber = new TestSubscriber<TestTriggerEvent>();
            var storage = new InMemorySaveDataStorage();
            var orchestrator = new SaveOrchestrator(storage, savePublisher, loadPublisher);
            orchestrator.Register(new FakeSaveable("player") { IsDirty = true, State = "state" });
            var trigger = orchestrator.AttachAutoSaveTrigger<TestTriggerEvent>(triggerSubscriber);
            trigger.Dispose();

            triggerSubscriber.Publish(new TestTriggerEvent());

            Assert.That(storage.Exists("player"), Is.False);
        }

        /// <summary>자동 저장 훅 테스트에 사용하는 트리거 이벤트이다.</summary>
        private readonly struct TestTriggerEvent
        {
        }

        private static SaveOrchestrator CreateOrchestrator(ISaveDataStorage storage)
        {
            return new SaveOrchestrator(
                storage,
                new TestPublisher<SaveAllCompletedEvent>(),
                new TestPublisher<LoadAllCompletedEvent>());
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

        /// <summary>호출 기록을 남기는 테스트용 저장 참여자이다.</summary>
        private sealed class FakeSaveable : ISaveable
        {
            public FakeSaveable(string saveKey)
            {
                SaveKey = saveKey;
            }

            public string SaveKey { get; }

            public bool IsDirty { get; set; }

            public string State { get; set; }

            public string RestoredState { get; private set; }

            public int AcknowledgeCount { get; private set; }

            public string CaptureState()
            {
                return State;
            }

            public void RestoreState(string serializedState)
            {
                RestoredState = serializedState;
                IsDirty = false;
            }

            public void AcknowledgeSaved()
            {
                AcknowledgeCount++;
                IsDirty = false;
            }
        }
    }
}
