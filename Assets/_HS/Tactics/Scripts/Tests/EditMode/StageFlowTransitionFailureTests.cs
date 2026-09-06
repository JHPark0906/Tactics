using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using HS.Framework.UI.Windows;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 결과 이벤트 한 번을 실제 결과 창과 버튼으로 처리하고, 실패 뒤 같은 버튼으로 재시도할 수 있는지 검증한다.
    /// 재시도가 보상·저장을 반복하지 않는지와 모달 스택이 복구되는지도 함께 확인한다.
    /// </summary>
    /// <remarks>
    /// 전환 실패는 UniTask의 미관찰 예외로, 저장 실패는 결과 창을 유지하면서 예외 로그로 기록한다.
    /// 저장 참여자와 실제 버튼을 연결해 부분 저장 성공 뒤 재시도가 중복 정산을 만들지 않는지도 확인한다.
    /// </remarks>
    public sealed class StageFlowTransitionFailureTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new();
        private readonly List<Exception> _unobservedExceptions = new();
        private StageFlowController _controller;
        private FakeGameFlowService _gameFlow;
        private TestMessageChannel<BattleOutcomeDecidedEvent> _outcomeChannel;
        private BattleResultWindow _window;
        private UiWindowManager _windowManager;
        private Button _proceedButton;
        private UnitLevelProgress _levels;
        private TestPublisher<SaveAllCompletedEvent> _saves;
        private MemoryStorage _storage;
        private StageProgressionService _progression;
        private UnitLevelSaveable _unitSaveable;
        private TMP_Text _messageText;
        private TMP_Text _buttonLabel;

        [SetUp]
        public void SetUp()
        {
            UniTaskScheduler.UnobservedTaskException += RecordUnobservedException;

            _gameFlow = new FakeGameFlowService();
            _outcomeChannel = new TestMessageChannel<BattleOutcomeDecidedEvent>();
            var catalog = new FakeSceneCatalog(1, FakeSceneCatalog.Gameplay(1), FakeSceneCatalog.Gameplay(2));

            var controllerObject = new GameObject("StageFlowController");
            _createdObjects.Add(controllerObject);
            _controller = controllerObject.AddComponent<StageFlowController>();
            _window = CreateObject("BattleResultWindow").AddComponent<BattleResultWindow>();
            _proceedButton = CreateObject("ProceedButton").AddComponent<Button>();
            SetReference(_window, "proceedButton", _proceedButton);
            _messageText = CreateText("ResultMessage");
            _buttonLabel = CreateText("ProceedButtonLabel");
            SetReference(_window, "messageText", _messageText);
            SetReference(_window, "proceedButtonLabel", _buttonLabel);
            MonoBehaviourLifecycle.InvokeAwake(_window);
            _windowManager = CreateObject("UiWindowManager").AddComponent<UiWindowManager>();
            SetReference(_controller, "resultWindow", _window);
            SetReference(_controller, "windowManager", _windowManager);

            var placement = CreateObject("UnitPlacementController").AddComponent<UnitPlacementController>();
            placement.BeginPlacement();
            placement.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
            SetReference(_controller, "placementController", placement);

            var table = ScriptableObject.CreateInstance<StageExperienceTable>();
            _createdObjects.Add(table);
            JsonUtility.FromJsonOverwrite("{\"entries\":[{\"stageId\":1,\"experience\":100}]}", table);
            var curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            _createdObjects.Add(curve);
            JsonUtility.FromJsonOverwrite("{\"maxLevel\":5,\"xpToNext\":[1000,2000,3000,4000]}", curve);
            _levels = new UnitLevelProgress();
            _saves = new TestPublisher<SaveAllCompletedEvent>();
            _storage = new MemoryStorage();
            _progression = new StageProgressionService(catalog);
            _unitSaveable = new UnitLevelSaveable(_levels);
            var saveOrchestrator = new SaveOrchestrator(_storage, _saves, new TestPublisher<LoadAllCompletedEvent>());
            saveOrchestrator.Register(_progression.Saveable);
            saveOrchestrator.Register(_unitSaveable);
            _controller.InjectRuntimeDependencies(
                _outcomeChannel,
                _gameFlow,
                catalog,
                _progression,
                saveOrchestrator,
                new StageRewardService(_levels, table, curve));

            var definition = UnitDefinition.CreateRuntime("RewardTester", 100, id: "unit.test");
            _createdObjects.Add(definition);
            Assert.That(placement.Plan.TryPlaceAtCell(definition, 0, out _), Is.EqualTo(PlacementResult.Success));
            Assert.That(placement.TryStartBattle(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            UniTaskScheduler.UnobservedTaskException -= RecordUnobservedException;
            _unobservedExceptions.Clear();

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
        public void ASynchronousThrowReopensTheResultAndTheSameButtonCanRetry()
        {
            _gameFlow.ThrowOnReturn = true;
            PublishDefeat();
            _proceedButton.onClick.Invoke();

            Assert.That(_unobservedExceptions, Has.Count.EqualTo(1), "실패는 예외로 기록되어야 한다.");
            Assert.That(_controller.IsTransitionRequested, Is.False, "실패한 전환이 요청 표시를 잠근 채 두면 다음 전환이 막힌다.");
            AssertRetryWindowIsUsable();

            _gameFlow.ThrowOnReturn = false;
            _proceedButton.onClick.Invoke();

            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(2));
            Assert.That(_outcomeChannel.Published, Has.Count.EqualTo(1), "재시도에 결과 이벤트를 다시 발행하지 않는다.");
            Assert.That(_window.IsOpen, Is.False);
        }

        [Test]
        public void AFaultedTransitionReopensTheResultAndTheSameButtonCanRetry()
        {
            _gameFlow.PendingReturn = new UniTaskCompletionSource();
            PublishDefeat();
            _proceedButton.onClick.Invoke();
            Assert.That(_controller.IsTransitionRequested, Is.True, "전환이 진행 중인 동안은 요청 표시가 잠겨 있어야 한다.");

            _gameFlow.PendingReturn.TrySetException(new InvalidOperationException("시험용 전환 실패이다."));

            Assert.That(_unobservedExceptions, Has.Count.EqualTo(1), "실패는 예외로 기록되어야 한다.");
            Assert.That(_controller.IsTransitionRequested, Is.False);
            AssertRetryWindowIsUsable();

            _gameFlow.PendingReturn = null;
            _proceedButton.onClick.Invoke();

            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(2));
            Assert.That(_outcomeChannel.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void ASecondClickWhileATransitionIsInFlightIsIgnored()
        {
            _gameFlow.PendingReturn = new UniTaskCompletionSource();
            PublishDefeat();
            _proceedButton.onClick.Invoke();
            _proceedButton.onClick.Invoke();

            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(1), "진행 중인 전환 위에 전환을 겹치지 않는다.");

            _gameFlow.PendingReturn.TrySetResult();
            Assert.That(_controller.IsTransitionRequested, Is.False);
        }

        [Test]
        public void ClosingThroughTheManagerCanReopenAfterASynchronousFailure()
        {
            _gameFlow.ThrowOnReturn = true;
            PublishDefeat();

            _windowManager.CloseTopWindow();

            AssertRetryWindowIsUsable();
            _gameFlow.ThrowOnReturn = false;
            _proceedButton.onClick.Invoke();
            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(2));
        }

        [Test]
        public void RetryAndDuplicateOutcomeDoNotAwardExperienceOrSaveTwice()
        {
            _gameFlow.ThrowOnReturn = true;
            var victory = new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0);
            _outcomeChannel.Publish(victory);
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_saves.Published, Has.Count.EqualTo(1));

            _proceedButton.onClick.Invoke();
            _outcomeChannel.Publish(victory);
            AssertRetryWindowIsUsable();
            _gameFlow.ThrowOnReturn = false;
            _proceedButton.onClick.Invoke();

            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(2));
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_saves.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void FailedSaveCanRetryWithoutRepeatingRewardsOrAlreadySavedParticipants()
        {
            _storage.FailedKey = UnitLevelSaveable.DefaultSaveKey;
            var victory = new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0);
            ExpectSaveFailure();
            _outcomeChannel.Publish(victory);

            AssertPendingSaveIsUsable();
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_progression.Progression.IsCleared(1), Is.True);
            Assert.That(_progression.Saveable.IsDirty, Is.False, "먼저 기록한 스테이지 참여자는 성공을 유지한다.");
            Assert.That(_unitSaveable.IsDirty, Is.True);
            Assert.That(_storage.SuccessfulWrites[StageProgressSaveable.DefaultSaveKey], Is.EqualTo(1));
            Assert.That(_saves.Published, Is.Empty);

            _outcomeChannel.Publish(victory);
            ExpectSaveFailure();
            _proceedButton.onClick.Invoke();

            AssertPendingSaveIsUsable();
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_storage.SuccessfulWrites[StageProgressSaveable.DefaultSaveKey], Is.EqualTo(1));

            _storage.FailedKey = null;
            _proceedButton.onClick.Invoke();

            Assert.That(_controller.HasPendingSave, Is.False);
            Assert.That(_window.IsOpen, Is.False);
            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_storage.SuccessfulWrites[StageProgressSaveable.DefaultSaveKey], Is.EqualTo(1));
            Assert.That(_storage.SuccessfulWrites[UnitLevelSaveable.DefaultSaveKey], Is.EqualTo(1));
            Assert.That(_unitSaveable.IsDirty, Is.False);
            Assert.That(_saves.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void ClosingTheWindowCannotBypassAFailedSave()
        {
            _storage.FailedKey = StageProgressSaveable.DefaultSaveKey;
            ExpectSaveFailure();
            PublishDefeat();

            ExpectSaveFailure();
            _windowManager.CloseTopWindow();

            AssertPendingSaveIsUsable();
            Assert.That(_storage.SuccessfulWrites, Is.Empty);

            _storage.FailedKey = null;
            _windowManager.CloseTopWindow();

            Assert.That(_controller.HasPendingSave, Is.False);
            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(_saves.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void TransitionFailureAfterASaveRetryUsesTheExistingTransitionRetry()
        {
            _storage.FailedKey = UnitLevelSaveable.DefaultSaveKey;
            ExpectSaveFailure();
            _outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));
            _storage.FailedKey = null;
            _gameFlow.ThrowOnReturn = true;

            _proceedButton.onClick.Invoke();

            Assert.That(_controller.HasPendingSave, Is.False);
            AssertRetryWindowIsUsable();
            Assert.That(_messageText.text, Does.Contain("돌아가지 못했다"));
            Assert.That(_buttonLabel.text, Is.EqualTo("스테이지 선택"));
            Assert.That(_unobservedExceptions, Has.Count.EqualTo(1));

            _gameFlow.ThrowOnReturn = false;
            _proceedButton.onClick.Invoke();

            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.EqualTo(2));
            Assert.That(_levels.GetExperience("unit.test"), Is.EqualTo(100));
            Assert.That(_storage.SuccessfulWrites[StageProgressSaveable.DefaultSaveKey], Is.EqualTo(1));
            Assert.That(_storage.SuccessfulWrites[UnitLevelSaveable.DefaultSaveKey], Is.EqualTo(1));
        }

        private void AssertPendingSaveIsUsable()
        {
            Assert.That(_controller.HasPendingSave, Is.True);
            Assert.That(_controller.IsTransitionRequested, Is.False);
            Assert.That(_gameFlow.ReturnToMainMenuCount, Is.Zero, "저장하지 못한 결과로 씬을 떠나면 안 된다.");
            AssertRetryWindowIsUsable();
            Assert.That(_messageText.text, Does.Contain("저장하지 못했다"));
            Assert.That(_buttonLabel.text, Is.EqualTo("저장 후 스테이지 선택"));
        }

        private static void ExpectSaveFailure()
        {
            LogAssert.Expect(LogType.Exception, new Regex(MemoryStorage.FailureMessage));
        }

        private void AssertRetryWindowIsUsable()
        {
            Assert.That(_window.IsOpen, Is.True);
            Assert.That(_window.gameObject.activeSelf, Is.True);
            Assert.That(_windowManager.TopWindow, Is.SameAs(_window));
            Assert.That(_windowManager.OpenWindowCount, Is.EqualTo(1));
        }

        private GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            _createdObjects.Add(created);
            return created;
        }

        private TMP_Text CreateText(string name)
        {
            var created = new GameObject(name, typeof(RectTransform));
            _createdObjects.Add(created);
            created.SetActive(false);
            return created.AddComponent<TextMeshProUGUI>();
        }

        private static void SetReference(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class MemoryStorage : ISaveDataStorage
        {
            public const string FailureMessage = "시험용 진행도 저장 실패이다.";
            private readonly Dictionary<string, string> _values = new();
            public readonly Dictionary<string, int> SuccessfulWrites = new();
            public string FailedKey { get; set; }
            public bool Exists(string key) => _values.ContainsKey(key);
            public bool TryRead(string key, out string value) => _values.TryGetValue(key, out value);
            public void Write(string key, string value)
            {
                if (key == FailedKey)
                {
                    throw new IOException(FailureMessage);
                }

                _values[key] = value;
                SuccessfulWrites.TryGetValue(key, out var count);
                SuccessfulWrites[key] = count + 1;
            }
            public void Delete(string key) => _values.Remove(key);
        }

        private void PublishDefeat()
        {
            _outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 0));
        }

        private void RecordUnobservedException(Exception exception)
        {
            _unobservedExceptions.Add(exception);
        }
    }
}
