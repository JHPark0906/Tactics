using System.Collections.Generic;
using HS.Framework.Persistence;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.UI;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 전투가 끝나면 이기든 지든 스테이지 선택으로 돌아가고, 떠나기 전에 보상과 기록이 끝나 있으며,
    /// 돌아온 목록에 방금 깬 스테이지가 클리어로 보이는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 전투 종료 규칙: 승리든 실패든 전투가 끝나면 스테이지 선택 화면으로 넘어간다. 출구는 하나이므로
    /// 승패에 따라 다른 씬으로 가는 길이 남아 있으면 안 되고, 스테이지 씬을 다시 여는 길도 없어야 한다.
    /// </para>
    /// <para>
    /// 스테이지 선택은 메인 메뉴 안의 목록이다. 메뉴는 열릴 때마다 진행 상태를 다시 읽으므로, 전투가
    /// 남긴 클리어 기록과 메뉴가 읽는 진행 상태가 같은 것이면 방금 깬 스테이지가 클리어로 보인다.
    /// 여기서는 그 둘이 같은 진행 상태를 볼 때 목록이 어떻게 나오는지를 본다.
    /// </para>
    /// </remarks>
    public sealed class BattleEndGoesToStageSelectTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void VictoryReturnsToStageSelect()
        {
            var flow = CreateFlow(out var gameFlow, out var outcomeChannel, out _);

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));

            Assert.That(gameFlow.ReturnToMainMenuCount, Is.EqualTo(1), "이기면 스테이지 선택(메인 메뉴)으로 돌아간다.");
            Assert.That(gameFlow.LoadedLevelIds, Is.Empty, "다음 스테이지로 바로 이어 가는 길은 없다.");
            Assert.That(flow.IsTransitionRequested, Is.False);
        }

        [Test]
        public void DefeatReturnsToTheSamePlace()
        {
            CreateFlow(out var gameFlow, out var outcomeChannel, out _);

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 2));

            Assert.That(gameFlow.ReturnToMainMenuCount, Is.EqualTo(1), "져도 같은 곳으로 돌아간다.");
            Assert.That(gameFlow.LoadedLevelIds, Is.Empty, "같은 스테이지를 다시 여는 재도전 길은 없다.");
        }

        [Test]
        public void RewardAndRecordAreFinishedBeforeLeaving()
        {
            var levels = new UnitLevelProgress();
            var storage = new InMemorySaveDataStorage();
            var flow = CreateFlow(out var gameFlow, out var outcomeChannel, out var progressionService, levels, storage);
            StartBattleWith(flow, "unit.rifleman");

            var clearedWhenLeaving = false;
            var experienceWhenLeaving = -1;
            var savedWhenLeaving = string.Empty;
            gameFlow.BeforeReturnToMainMenu = () =>
            {
                clearedWhenLeaving = progressionService.Progression.IsCleared(1);
                experienceWhenLeaving = levels.GetExperience("unit.rifleman");
                storage.TryRead(StageProgressSaveable.DefaultSaveKey, out savedWhenLeaving);
            };

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));

            Assert.That(gameFlow.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(clearedWhenLeaving, Is.True, "떠나는 순간 클리어 기록이 이미 올라 있어야 한다. 씬이 바뀌면 이 컴포넌트는 사라진다.");
            Assert.That(experienceWhenLeaving, Is.EqualTo(100), "떠나는 순간 경험치가 이미 들어가 있어야 한다.");
            Assert.That(savedWhenLeaving, Does.Contain("\"highestClearedStageId\":1"), "떠나는 순간 진행도가 이미 저장되어 있어야 한다.");
        }

        [Test]
        public void ADefeatLeavesWithoutExperience()
        {
            var levels = new UnitLevelProgress();
            var flow = CreateFlow(out var gameFlow, out var outcomeChannel, out _, levels, new InMemorySaveDataStorage());
            StartBattleWith(flow, "unit.rifleman");

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 1));

            Assert.That(gameFlow.ReturnToMainMenuCount, Is.EqualTo(1));
            Assert.That(levels.RaisedCount, Is.Zero, "승패로 달라지는 것은 경험치 하나뿐이다. 지면 받지 않는다.");
        }

        [Test]
        public void AfterVictoryTheStageSelectShowsThatStageCleared()
        {
            CreateFlow(out var gameFlow, out var outcomeChannel, out var progressionService);
            progressionService.Progression.SetCurrentStage(2);

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));

            // 메뉴는 열릴 때마다 같은 진행 서비스의 진행 상태를 읽어 목록을 다시 만든다.
            var menu = new TacticsMainMenuViewModel(
                gameFlow, CreateCatalog(), progressionService.Progression, new PartySelectionService());
            menu.Load();

            Assert.That(menu.Stages.Count, Is.EqualTo(3));
            Assert.That(menu.Stages[1].StageId, Is.EqualTo(2));
            Assert.That(menu.Stages[1].IsCleared, Is.True, "방금 깬 스테이지가 클리어로 보여야 한다.");
            Assert.That(menu.Stages[0].IsCleared, Is.True, "최고 클리어 번호 이하는 전부 클리어로 본다.");
            Assert.That(menu.Stages[2].IsCleared, Is.False);
            Assert.That(menu.ClearedStageCount, Is.EqualTo(2));
        }

        [Test]
        public void AfterDefeatTheStageSelectShowsNothingNew()
        {
            CreateFlow(out var gameFlow, out var outcomeChannel, out var progressionService);

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 1));

            var menu = new TacticsMainMenuViewModel(
                gameFlow, CreateCatalog(), progressionService.Progression, new PartySelectionService());
            menu.Load();

            Assert.That(menu.ClearedStageCount, Is.Zero);
        }

        /// <summary>세 스테이지를 가진 카탈로그를 만든다.</summary>
        private static FakeSceneCatalog CreateCatalog()
        {
            return new FakeSceneCatalog(
                1, FakeSceneCatalog.Gameplay(1), FakeSceneCatalog.Gameplay(2), FakeSceneCatalog.Gameplay(3));
        }

        /// <summary>흐름 컨트롤러를 세우고 공개 주입 진입점으로 협력자를 연결한다.</summary>
        /// <param name="gameFlow">전환 요청을 기록하는 흐름 서비스이다.</param>
        /// <param name="outcomeChannel">결과를 발행할 통로이다.</param>
        /// <param name="progressionService">컨트롤러와 메뉴가 함께 보는 진행 서비스이다.</param>
        /// <param name="levels">경험치가 들어갈 육성 상태이며 null이면 보상하지 않는다.</param>
        /// <param name="storage">진행도를 기록할 저장소이며 null이면 저장하지 않는다.</param>
        /// <returns>세운 흐름 컨트롤러이다.</returns>
        private StageFlowController CreateFlow(
            out FakeGameFlowService gameFlow,
            out TestMessageChannel<BattleOutcomeDecidedEvent> outcomeChannel,
            out StageProgressionService progressionService,
            UnitLevelProgress levels = null,
            InMemorySaveDataStorage storage = null)
        {
            var catalog = CreateCatalog();
            gameFlow = new FakeGameFlowService();
            outcomeChannel = new TestMessageChannel<BattleOutcomeDecidedEvent>();
            progressionService = new StageProgressionService(catalog);

            SaveOrchestrator saveOrchestrator = null;
            if (storage != null)
            {
                saveOrchestrator = new SaveOrchestrator(
                    storage,
                    new TestPublisher<SaveAllCompletedEvent>(),
                    new TestPublisher<LoadAllCompletedEvent>());
                saveOrchestrator.Register(progressionService.Saveable);
            }

            var controller = CreateObject("StageFlowController").AddComponent<StageFlowController>();
            if (levels != null)
            {
                var placement = CreateObject("UnitPlacementController").AddComponent<UnitPlacementController>();
                placement.BeginPlacement();
                placement.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("placementController").objectReferenceValue = placement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            controller.InjectRuntimeDependencies(
                outcomeChannel,
                gameFlow,
                catalog,
                progressionService,
                saveOrchestrator,
                levels == null ? null : new StageRewardService(levels, CreateTable(100), CreateCurve()));
            return controller;
        }

        /// <summary>지정한 종류를 배치하고 전투를 시작해 명단이 잡히게 한다.</summary>
        private void StartBattleWith(StageFlowController controller, params string[] definitionIds)
        {
            var placement = Object.FindFirstObjectByType<UnitPlacementController>(FindObjectsInactive.Include);
            Assert.That(placement, Is.Not.Null, "검사가 기대는 준비 단계가 실패했다. 배치 컨트롤러가 없다.");
            for (var cellIndex = 0; cellIndex < definitionIds.Length; cellIndex++)
            {
                var definition = UnitDefinition.CreateRuntime("RewardTester", 100, id: definitionIds[cellIndex]);
                _createdObjects.Add(definition);
                Assert.That(
                    placement.Plan.TryPlaceAtCell(definition, cellIndex, out _),
                    Is.EqualTo(PlacementResult.Success),
                    $"검사가 기대는 준비 단계가 실패했다. {cellIndex}번 칸에 배치 항목을 넣지 못했다.");
            }

            Assert.That(placement.TryStartBattle(), Is.True, "전투가 시작되지 않으면 명단이 잡히지 않는다.");
            Assert.That(controller.DeployedDefinitionIds.Count, Is.EqualTo(definitionIds.Length));
        }

        /// <summary>1번 스테이지만 적힌 경험치 표를 만든다.</summary>
        private StageExperienceTable CreateTable(int stageExperience)
        {
            var table = ScriptableObject.CreateInstance<StageExperienceTable>();
            JsonUtility.FromJsonOverwrite($"{{\"entries\":[{{\"stageId\":1,\"experience\":{stageExperience}}}]}}", table);
            _createdObjects.Add(table);
            return table;
        }

        /// <summary>검사 중 레벨이 오르지 않을 만큼 비싼 곡선을 만든다. 재려는 것은 들어간 경험치이기 때문이다.</summary>
        private UnitLevelCurve CreateCurve()
        {
            var curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            JsonUtility.FromJsonOverwrite("{\"maxLevel\":5,\"xpToNext\":[1000,2000,3000,4000]}", curve);
            _createdObjects.Add(curve);
            return curve;
        }

        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>메모리 사전에 기록하는 저장소이다. 떠나는 순간 무엇이 적혀 있는지를 읽는 데 쓴다.</summary>
        private sealed class InMemorySaveDataStorage : ISaveDataStorage
        {
            private readonly Dictionary<string, string> _values = new();

            public bool Exists(string key) => _values.ContainsKey(key);

            public bool TryRead(string key, out string value) => _values.TryGetValue(key, out value);

            public void Write(string key, string value) => _values[key] = value;

            public void Delete(string key) => _values.Remove(key);
        }
    }
}
