using System.Collections.Generic;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>스테이지 진행도의 저장 참여 규약과 dirty 판정을 검증한다.</summary>
    public sealed class StageProgressSaveableTests
    {
        [Test]
        public void FreshProgressIsDirtySoTheFirstSaveIsRecorded()
        {
            var saveable = new StageProgressSaveable(new StageProgression());

            Assert.That(saveable.IsDirty, Is.True);
            Assert.That(saveable.SaveKey, Is.EqualTo(StageProgressSaveable.DefaultSaveKey));
        }

        [Test]
        public void AcknowledgeSavedClearsDirty()
        {
            var saveable = new StageProgressSaveable(new StageProgression());

            saveable.CaptureState();
            saveable.AcknowledgeSaved();

            Assert.That(saveable.IsDirty, Is.False);
        }

        [Test]
        public void ProgressChangeMakesItDirtyAgain()
        {
            var progression = new StageProgression();
            var saveable = new StageProgressSaveable(progression);
            saveable.CaptureState();
            saveable.AcknowledgeSaved();

            progression.ApplyOutcome(BattleOutcome.Victory);

            Assert.That(saveable.IsDirty, Is.True);
        }

        [Test]
        public void AcknowledgeKeepsDirtyWhenProgressChangedAfterCapture()
        {
            var progression = new StageProgression();
            var saveable = new StageProgressSaveable(progression);

            saveable.CaptureState();
            progression.ApplyOutcome(BattleOutcome.Victory);
            saveable.AcknowledgeSaved();

            Assert.That(saveable.IsDirty, Is.True, "캡처 이후의 변경은 저장되지 않았으므로 dirty가 유지되어야 한다.");
        }

        [Test]
        public void RestoreStateAppliesSavedProgressAndClearsDirty()
        {
            // 승리는 클리어를 기록할 뿐 열린 스테이지를 옮기지 않는다. 다음 스테이지는 플레이어가 스테이지 선택에서 고른다.
            var sourceProgression = new StageProgression();
            sourceProgression.ApplyOutcome(BattleOutcome.Victory);
            sourceProgression.SetCurrentStage(2);
            var serializedState = new StageProgressSaveable(sourceProgression).CaptureState();

            var targetProgression = new StageProgression();
            var targetSaveable = new StageProgressSaveable(targetProgression);
            targetSaveable.RestoreState(serializedState);

            Assert.That(targetProgression.CurrentStageId, Is.EqualTo(2), "플레이어가 고른 스테이지가 복원되어야 한다.");
            Assert.That(targetProgression.HighestClearedStageId, Is.EqualTo(1), "1번을 깬 기록이 복원되어야 한다.");
            Assert.That(targetSaveable.IsDirty, Is.False);
        }

        [Test]
        public void RestoreStateIgnoresEmptyPayload()
        {
            var progression = new StageProgression();
            var saveable = new StageProgressSaveable(progression);

            saveable.RestoreState(null);
            saveable.RestoreState(string.Empty);

            Assert.That(progression.CurrentStageId, Is.EqualTo(1));
            Assert.That(progression.HighestClearedStageId, Is.EqualTo(0));
        }

        [Test]
        public void OrchestratorRoundTripsProgressAcrossSessions()
        {
            var storage = new InMemorySaveDataStorage();
            // 승리는 클리어를 기록할 뿐 열린 스테이지를 옮기지 않으므로, 2번은 플레이어가 고른 뒤 깬다.
            var savedProgression = new StageProgression();
            savedProgression.ApplyOutcome(BattleOutcome.Victory);
            savedProgression.SetCurrentStage(2);
            savedProgression.ApplyOutcome(BattleOutcome.Victory);
            var saveOrchestrator = new SaveOrchestrator(storage, new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            saveOrchestrator.Register(new StageProgressSaveable(savedProgression));

            Assert.That(saveOrchestrator.SaveAll(SaveKind.Automatic), Is.EqualTo(1));

            var loadedProgression = new StageProgression();
            var loadOrchestrator = new SaveOrchestrator(storage, new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            loadOrchestrator.Register(new StageProgressSaveable(loadedProgression));

            Assert.That(loadOrchestrator.LoadAll(), Is.EqualTo(1));
            Assert.That(loadedProgression.HighestClearedStageId, Is.EqualTo(2), "2번까지 깬 기록이 세션을 건너 살아남아야 한다.");
            Assert.That(loadedProgression.CurrentStageId, Is.EqualTo(2), "마지막으로 열려 있던 스테이지가 세션을 건너 살아남아야 한다.");
        }

        [Test]
        public void AutomaticSaveSkipsUnchangedProgress()
        {
            var storage = new InMemorySaveDataStorage();
            var orchestrator = new SaveOrchestrator(storage, new TestPublisher<SaveAllCompletedEvent>(), new TestPublisher<LoadAllCompletedEvent>());
            orchestrator.Register(new StageProgressSaveable(new StageProgression()));

            Assert.That(orchestrator.SaveAll(SaveKind.Automatic), Is.EqualTo(1));
            Assert.That(orchestrator.SaveAll(SaveKind.Automatic), Is.EqualTo(0));
        }

        [Test]
        public void MissingProgressionIsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(() => new StageProgressSaveable(null));
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
