#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Persistence;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Settings;
using HS.Framework.UI.Windows;
using HS.Tactics.Core;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.UI;
using HS.Tactics.Units;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>실제 씬·프리팹·주입·사망 이벤트·결과 버튼을 지나 저장과 보상의 중복을 검사한다.</summary>
    public sealed class TacticsActualSceneFlowTests
    {
        private const string BootstrapPath = "Assets/_HS/Tactics/Scenes/Bootstrap.unity";
        private const string MainMenuPath = "Assets/_HS/Tactics/Scenes/MainMenu.unity";
        private const string LevelPath = "Assets/_HS/Tactics/Scenes/Level1.unity";
        private const int TestExperience = 25;
        private readonly List<Object> _testObjects = new();
        private readonly List<IDisposable> _subscriptions = new();
        private readonly List<string> _missingScripts = new();
        private ISaveDataStorage _previousSettingsStorage;
        private TacticsLifetimeScope _scope;
        private MemoryStorage _progressStorage;
        private FailOnceMenuLoader _loader;
        private float _previousTimeScale;
        private bool _settingsAreIsolated;

        [UnityTest]
        public IEnumerator VictoryKeepsTheRealResultWindowForRetryAndAwardsExactlyOnce()
            => ExerciseVictoryFlow(failSave: false);

        [UnityTest]
        public IEnumerator VictoryRetriesPartialSavingBeforeLeavingAndAwardsExactlyOnce()
            => ExerciseVictoryFlow(failSave: true);

        private IEnumerator ExerciseVictoryFlow(bool failSave)
        {
            Assert.That(Object.FindObjectsByType<TacticsLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                Is.Empty, "실행 중인 게임 스코프가 없는 격리된 PlayMode에서 실행해야 한다.");
            _missingScripts.Clear();
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            _previousSettingsStorage = SettingsStorage.Current;
            SettingsStorage.Current = new MemoryStorage();
            _settingsAreIsolated = true;
            _progressStorage = new MemoryStorage();
            _loader = new FailOnceMenuLoader();
            var rules = CreateTestRewardRules();
            SceneManager.sceneLoaded += InspectLoadedScene;

            // 실제 Bootstrap은 그대로 로드한다. 이 추가 설치는 Configure 뒤, 컨테이너 생성 전에
            // 적용된다. 디스크 저장소의 생성자는 I/O가 없고, 이후 모든 읽기·쓰기는 이 저장소로 간다.
            using (LifetimeScope.Enqueue(builder =>
                   {
                       builder.RegisterInstance(_progressStorage).As<ISaveDataStorage>();
                       builder.RegisterInstance(_loader).As<ISceneLoader>();
                       builder.RegisterInstance<IUnitProgressionRules>(rules);
                       var sceneScope = builder.ApplicationOrigin as TacticsLifetimeScope;
                       Assert.That(sceneScope, Is.Not.Null);
                       _scope = sceneScope;
                       var serializedScope = new SerializedObject(sceneScope);
                       serializedScope.FindProperty("progressionRules").objectReferenceValue = rules;
                       serializedScope.ApplyModifiedPropertiesWithoutUndo();
                   }))
            {
                var bootstrap = SceneManager.LoadSceneAsync(BootstrapPath, LoadSceneMode.Single);
                Assert.That(bootstrap, Is.Not.Null);
                yield return WaitFor(() => bootstrap.isDone, "실제 Bootstrap 로드");
            }

            yield return WaitFor(() =>
            {
                _scope = Object.FindFirstObjectByType<TacticsLifetimeScope>();
                return _scope != null && _scope.Container != null
                    && SceneManager.GetActiveScene().path == MainMenuPath
                    && !_scope.Container.Resolve<ISceneTransitionService>().IsLoading;
            }, "Bootstrap 초기화 후 실제 MainMenu 도착");
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(1f / 30f).Within(0.0000001f),
                "실제 게임의 논리 프레임은 초당 30회여야 한다.");
            var resolver = _scope.Container;
            Assert.That(resolver.Resolve<ISaveDataStorage>(), Is.SameAs(_progressStorage));
            Assert.That(_progressStorage.ReadCount, Is.GreaterThan(0), "최초 저장 복원도 메모리 저장소에서 읽어야 한다.");
            Assert.That(Object.FindFirstObjectByType<TacticsMainMenuView>(), Is.Not.Null);
            InspectRoot(_scope.gameObject);

            var outcomeCount = 0;
            var saveCount = 0;
            _subscriptions.Add(resolver.Resolve<ISubscriber<BattleOutcomeDecidedEvent>>().Subscribe(_ => outcomeCount++));
            _subscriptions.Add(resolver.Resolve<ISubscriber<SaveAllCompletedEvent>>().Subscribe(_ => saveCount++));
            var hero = AssetDatabase.LoadAssetAtPath<UnitDefinition>("Assets/_HS/Tactics/Definitions/Rifleman.asset");
            Assert.That(hero, Is.Not.Null);
            var party = resolver.Resolve<PartySelectionService>();
            party.Commit(new[] { new PartySelectionEntry(4, hero) });
            var levels = resolver.Resolve<UnitProgressionService>().Levels;
            Assert.That(levels.GetExperience(hero), Is.Zero);
            var initialRevision = levels.Revision;
            var gameFlow = resolver.Resolve<IGameFlowService>();
            var enterBattle = gameFlow.LoadGameplayLevelAsync(1).AsTask();
            yield return WaitFor(() => enterBattle.IsCompleted, "실제 게임 흐름의 Level1 전환");
            enterBattle.GetAwaiter().GetResult();

            UnitPlacementController placement = null;
            BattleOutcomeService battle = null;
            yield return WaitFor(() =>
            {
                placement = Object.FindFirstObjectByType<UnitPlacementController>();
                battle = Object.FindFirstObjectByType<BattleOutcomeService>();
                return SceneManager.GetActiveScene().path == LevelPath && placement != null && placement.IsBattlePhase
                    && battle != null && battle.HasBattleBegun && battle.HostileAliveCount > 0;
            }, "선택 재생과 실제 전투 개시");
            Assert.That(party.HasSelection, Is.False);
            Assert.That(placement.Plan.Entries, Has.Count.EqualTo(1));
            Assert.That(placement.Plan.Entries[0].Definition, Is.SameAs(hero));
            Assert.That(placement.TryGetSpawnedUnit(placement.Plan.Entries[0].Id, out var ally), Is.True);
            var flowController = Object.FindFirstObjectByType<StageFlowController>();
            Assert.That(flowController.DeployedDefinitionIds, Is.EqualTo(new[] { hero.Id }));

            // 명중 난수나 사격 시간이 아닌 실제 Health API로 끝낸다. 승패 이벤트는 테스트가 발행하지 않는다.
            var hostiles = new List<TacticalUnit>();
            foreach (var unit in Object.FindObjectsByType<TacticalUnit>(FindObjectsSortMode.None))
            {
                if (unit.Team != null && unit.Team.IsHostileTo(ally.Team))
                {
                    hostiles.Add(unit);
                }
            }

            Assert.That(hostiles, Has.Count.EqualTo(battle.HostileAliveCount));
            if (failSave)
            {
                // 스테이지 기록을 저장한 뒤 두 번째 참여자에서 실패시켜 부분 성공을 검사한다.
                _progressStorage.FailingWriteKey = UnitLevelSaveable.DefaultSaveKey;
                LogAssert.Expect(LogType.Exception, new Regex(MemoryStorage.FailureMessage));
            }
            foreach (var hostile in hostiles)
            {
                Assert.That(hostile.Health.IsBound, Is.True);
                hostile.Health.ApplyDamage(hostile.Health.CurrentHealth, ally.gameObject);
                Assert.That(hostile.Health.IsDead, Is.True);
            }

            BattleResultWindow result = null;
            yield return WaitFor(() =>
            {
                result = Object.FindFirstObjectByType<BattleResultWindow>(FindObjectsInactive.Include);
                return result != null && result.IsOpen && result.gameObject.activeInHierarchy;
            }, "실제 승리 결과 창 표시");
            Assert.That(battle.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(result.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(Object.FindFirstObjectByType<UiWindowManager>().TopWindow, Is.SameAs(result));
            var progression = resolver.Resolve<StageProgressionService>().Progression;
            Assert.That(progression.IsCleared(1), Is.True);
            Assert.That(levels.GetExperience(hero), Is.EqualTo(TestExperience));
            Assert.That(levels.Revision, Is.EqualTo(initialRevision + 1));
            Assert.That(outcomeCount, Is.EqualTo(1));
            Assert.That(saveCount, Is.EqualTo(failSave ? 0 : 1));

            var button = new SerializedObject(result).FindProperty("proceedButton").objectReferenceValue as Button;
            Assert.That(button, Is.Not.Null);
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
            if (failSave)
            {
                Assert.That(flowController.HasPendingSave, Is.True);
                Assert.That(_progressStorage.WriteFailureCount, Is.EqualTo(1));
                Assert.That(_progressStorage.WritesFor(StageProgressSaveable.DefaultSaveKey), Is.EqualTo(1));
                Assert.That(_progressStorage.WritesFor(UnitLevelSaveable.DefaultSaveKey), Is.Zero);
                var menuLoadCount = _loader.MainMenuLoadCount;

                LogAssert.Expect(LogType.Exception, new Regex(MemoryStorage.FailureMessage));
                button.onClick.Invoke();
                yield return WaitFor(() => _progressStorage.WriteFailureCount == 2 && result.IsOpen
                    && !flowController.IsTransitionRequested, "저장 재시도 실패 후 결과 창 유지");
                Assert.That(flowController.HasPendingSave, Is.True);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelPath));
                Assert.That(_loader.MainMenuLoadCount, Is.EqualTo(menuLoadCount), "저장이 끝나기 전에 씬을 떠나면 안 된다.");
                Assert.That(saveCount, Is.Zero);
                Assert.That(levels.GetExperience(hero), Is.EqualTo(TestExperience));
                Assert.That(levels.Revision, Is.EqualTo(initialRevision + 1));
                Assert.That(_progressStorage.WritesFor(StageProgressSaveable.DefaultSaveKey), Is.EqualTo(1));
                _progressStorage.FailingWriteKey = null;
            }

            _loader.FailNextMainMenu = true;
            LogAssert.Expect(LogType.Exception, new Regex(FailOnceMenuLoader.FailureMessage));
            button.onClick.Invoke();
            yield return WaitFor(() => _loader.InjectedFailureCount == 1 && result != null && result.IsOpen
                && !flowController.IsTransitionRequested, "실패 후 결과 창의 재시도 복구");
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelPath));
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True);
            Assert.That(flowController.HasPendingSave, Is.False);
            Assert.That(saveCount, Is.EqualTo(1));

            button.onClick.Invoke();
            yield return WaitFor(() => SceneManager.GetActiveScene().path == MainMenuPath
                && !resolver.Resolve<ISceneTransitionService>().IsLoading, "결과 창 실제 버튼으로 MainMenu 재시도 완료");
            Assert.That(Object.FindFirstObjectByType<TacticsMainMenuView>(), Is.Not.Null);
            Assert.That(levels.GetExperience(hero), Is.EqualTo(TestExperience));
            Assert.That(levels.Revision, Is.EqualTo(initialRevision + 1));
            Assert.That(outcomeCount, Is.EqualTo(1));
            Assert.That(saveCount, Is.EqualTo(1));
            Assert.That(_progressStorage.WritesFor(StageProgressSaveable.DefaultSaveKey), Is.EqualTo(1));
            Assert.That(_progressStorage.WritesFor(UnitLevelSaveable.DefaultSaveKey), Is.EqualTo(1));
            Assert.That(_missingScripts, Is.Empty, string.Join("\n", _missingScripts));
        }

        private TacticsProgressionRules CreateTestRewardRules()
        {
            var table = ScriptableObject.CreateInstance<StageExperienceTable>();
            var curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            _testObjects.Add(table);
            _testObjects.Add(curve);
            var serializedTable = new SerializedObject(table);
            var entries = serializedTable.FindProperty("entries");
            entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("stageId").intValue = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("experience").intValue = TestExperience;
            serializedTable.ApplyModifiedPropertiesWithoutUndo();
            var rulesObject = new GameObject("TestOnlyRewardRules");
            Object.DontDestroyOnLoad(rulesObject);
            _testObjects.Add(rulesObject);
            var rules = rulesObject.AddComponent<TacticsProgressionRules>();
            var serializedRules = new SerializedObject(rules);
            serializedRules.FindProperty("stageExperienceTable").objectReferenceValue = table;
            serializedRules.FindProperty("unitLevelCurve").objectReferenceValue = curve;
            serializedRules.ApplyModifiedPropertiesWithoutUndo();
            return rules;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 시작 조건에서 거절된 검사는 기존 게임의 창이나 설정을 정리할 권한이 없다.
            if (!_settingsAreIsolated)
            {
                yield break;
            }

            SceneManager.sceneLoaded -= InspectLoadedScene;
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }
            _subscriptions.Clear();
            // 실패한 검사에서도 열린 결과 창 파괴가 새 씬 전환을 시작하지 않게 정리한다.
            foreach (var result in Object.FindObjectsByType<BattleResultWindow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                typeof(BattleResultWindow).GetField("_onProceed", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(result, null);
                result.Close();
            }
            if (_scope != null)
            {
                Object.Destroy(_scope.gameObject);
            }
            yield return null;
            if (_settingsAreIsolated)
            {
                InputSettingsService.Current.SetInputActionAsset(null, false, false);
                var cleanup = SceneManager.CreateScene("TacticsActualSceneFlowCleanup");
                SceneManager.SetActiveScene(cleanup);
                for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
                {
                    var scene = SceneManager.GetSceneAt(index);
                    if (scene.path.StartsWith("Assets/_HS/Tactics/Scenes/", StringComparison.Ordinal))
                    {
                        yield return SceneManager.UnloadSceneAsync(scene);
                    }
                }
                SettingsStorage.Current = _previousSettingsStorage;
                Time.timeScale = _previousTimeScale;
                _settingsAreIsolated = false;
            }
            foreach (var item in _testObjects)
            {
                if (item != null) Object.Destroy(item);
            }
            _testObjects.Clear();
            yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> predicate, string operation)
        {
            var deadline = Time.realtimeSinceStartup + 30f;
            while (!predicate())
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), operation + " 시간 초과");
                yield return null;
            }
        }

        private void InspectLoadedScene(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            foreach (var root in scene.GetRootGameObjects()) InspectRoot(root);
        }

        private void InspectRoot(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    _missingScripts.Add(child.gameObject.scene.path + ": " + child.name);
            }
        }

        private sealed class FailOnceMenuLoader : ISceneLoader
        {
            public const string FailureMessage = "E2E deliberate MainMenu load failure";
            private readonly SceneLoader _inner = new();
            public bool FailNextMainMenu { get; set; }
            public int InjectedFailureCount { get; private set; }
            public int MainMenuLoadCount { get; private set; }
            public bool IsLoading => _inner.IsLoading;

            public UniTask LoadSceneAsync(SceneReference scene, Action<float> progress = null)
            {
                if (scene.ScenePath == MainMenuPath)
                {
                    MainMenuLoadCount++;
                }
                if (FailNextMainMenu && scene.ScenePath == MainMenuPath)
                {
                    FailNextMainMenu = false;
                    InjectedFailureCount++;
                    throw new InvalidOperationException(FailureMessage);
                }
                return _inner.LoadSceneAsync(scene, progress);
            }
        }

        private sealed class MemoryStorage : ISaveDataStorage
        {
            public const string FailureMessage = "E2E deliberate unit progression write failure";
            private readonly Dictionary<string, string> _values = new();
            private readonly Dictionary<string, int> _writes = new();
            public int ReadCount { get; private set; }
            public string FailingWriteKey { get; set; }
            public int WriteFailureCount { get; private set; }
            public bool Exists(string key) => _values.ContainsKey(key);
            public bool TryRead(string key, out string value)
            {
                ReadCount++;
                return _values.TryGetValue(key, out value);
            }
            public void Write(string key, string value)
            {
                if (key == FailingWriteKey)
                {
                    WriteFailureCount++;
                    throw new IOException(FailureMessage);
                }
                _values[key] = value;
                _writes[key] = WritesFor(key) + 1;
            }
            public void Delete(string key) => _values.Remove(key);
            public int WritesFor(string key) => _writes.TryGetValue(key, out var count) ? count : 0;
        }
    }
}
#endif
