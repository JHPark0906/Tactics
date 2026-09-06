using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using HS.Framework.ProjectManagement;
using HS.Framework.Runtime;
using HS.Framework.Scene;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.Progress;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 승리 시 전투 시작에 배치했던 종류에 경험치를 나누고 패배 시에는 지급하지 않는지 검증한다.
    /// 결과 시점에 유닛이 없어도 배치 명단으로 몫을 계산하며, 보상은 방금 치른 스테이지의 표를 사용한다.
    /// </summary>
    public sealed class StageRewardTests
    {
        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
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
        public void EveryDeployedKindGetsItsShareOnVictory()
        {
            var levels = new UnitLevelProgress();
            var controller = CreateFlow(levels, 300, null, out var outcomeChannel, out var placement);
            StartBattleWith(controller, placement, "unit.rifleman", "unit.sniper", "unit.medic");

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 3, 0));

            Assert.That(levels.GetExperience("unit.rifleman"), Is.EqualTo(100));
            Assert.That(levels.GetExperience("unit.sniper"), Is.EqualTo(100));
            Assert.That(levels.GetExperience("unit.medic"), Is.EqualTo(100));
        }

        [Test]
        public void ADefeatAwardsNothing()
        {
            var levels = new UnitLevelProgress();
            var controller = CreateFlow(levels, 300, null, out var outcomeChannel, out var placement);
            StartBattleWith(controller, placement, "unit.rifleman", "unit.sniper", "unit.medic");

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Defeat, 0, 2));

            Assert.That(
                levels.RaisedCount,
                Is.Zero,
                "진 판에서도 자란다면 이기는 것과 지는 것의 값이 같아진다.");
        }

        [Test]
        public void UnitsLostDuringTheBattleStillCountInTheSplit()
        {
            var levels = new UnitLevelProgress();
            var controller = CreateFlow(levels, 300, null, out var outcomeChannel, out var placement);
            StartBattleWith(controller, placement, "unit.rifleman", "unit.sniper", "unit.medic");

            placement.Plan.Clear();
            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 2, 0));

            Assert.That(
                levels.GetExperience("unit.rifleman"),
                Is.EqualTo(100),
                "결과가 날 때 세면 죽은 유닛이 빠져 남은 인원의 몫이 커진다.");
        }

        [Test]
        public void TheRewardBelongsToTheStageThatWasJustCleared()
        {
            var levels = new UnitLevelProgress();
            var table = CreateTable(
                "{\"entries\":[{\"stageId\":1,\"experience\":100},{\"stageId\":2,\"experience\":500}]}");
            var progression = new StageProgressionService(new FakeSceneCatalog(3));
            progression.Progression.SetCurrentStage(2);
            var controller = CreateFlow(levels, table, progression, out var outcomeChannel, out var placement);
            StartBattleWith(controller, placement, "unit.rifleman");

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));

            Assert.That(
                levels.GetExperience("unit.rifleman"),
                Is.EqualTo(500),
                "진행 상태를 먼저 옮기면 아직 안 가 본 판의 경험치가 들어간다.");
        }

        [Test]
        public void AStageMissingFromTheTableAwardsNothing()
        {
            var levels = new UnitLevelProgress();
            var controller = CreateFlow(
                levels,
                CreateTable("{\"entries\":[]}"),
                null,
                out var outcomeChannel,
                out var placement);
            StartBattleWith(controller, placement, "unit.rifleman");
            LogAssert.Expect(LogType.Warning, new Regex("StageExperienceTable"));

            outcomeChannel.Publish(new BattleOutcomeDecidedEvent(BattleOutcome.Victory, 1, 0));

            Assert.That(levels.RaisedCount, Is.Zero);
        }

        [Test]
        public void AnEmptyPartyIsNotDividedBy()
        {
            var levels = new UnitLevelProgress();
            var service = CreateRewardService(levels, 300);

            var result = service.Award(1, Array.Empty<string>());

            Assert.That(result.SharePerUnit, Is.Zero);
            Assert.That(result.AwardedCount, Is.Zero);
            Assert.That(levels.RaisedCount, Is.Zero);
        }

        [Test]
        public void AMissingIdKeepsItsPlaceInTheSplit()
        {
            var levels = new UnitLevelProgress();
            var service = CreateRewardService(levels, 300);

            var result = service.Award(1, new[] { "unit.rifleman", string.Empty, "   " });

            Assert.That(result.SharePerUnit, Is.EqualTo(100), "자리를 채운 것은 사실이므로 나누는 수에는 든다.");
            Assert.That(result.AwardedCount, Is.EqualTo(1));
            Assert.That(levels.GetExperience("unit.rifleman"), Is.EqualTo(100));
        }

        [Test]
        public void TwoOfAKindGiveThatKindTwoShares()
        {
            var levels = new UnitLevelProgress();
            var service = CreateRewardService(levels, 300);

            service.Award(1, new[] { "unit.rifleman", "unit.rifleman", "unit.sniper" });

            Assert.That(
                levels.GetExperience("unit.rifleman"),
                Is.EqualTo(200),
                "몫은 자리마다 셈하고 레벨은 종류마다 붙는다.");
            Assert.That(levels.GetExperience("unit.sniper"), Is.EqualTo(100));
        }

        [Test]
        public void WithoutATableNothingIsAwarded()
        {
            var levels = new UnitLevelProgress();
            var service = new StageRewardService(levels, null, CreateCurve());

            service.Award(1, new[] { "unit.rifleman" });

            Assert.That(
                levels.RaisedCount,
                Is.Zero,
                "기본값으로 대신 올리면 그 수가 어디서 왔는지 모르는 채로 레벨이 오른다.");
        }

        /// <summary>1번 스테이지만 적힌 표로 보상 서비스를 만든다.</summary>
        /// <param name="levels">경험치가 들어갈 육성 상태이다.</param>
        /// <param name="stageExperience">1번 스테이지가 주는 경험치이다.</param>
        /// <returns>만든 보상 서비스이다.</returns>
        private StageRewardService CreateRewardService(UnitLevelProgress levels, int stageExperience)
        {
            var table = CreateTable($"{{\"entries\":[{{\"stageId\":1,\"experience\":{stageExperience}}}]}}");
            return new StageRewardService(levels, table, CreateCurve());
        }

        /// <summary>1번 스테이지만 적힌 표로 흐름 컨트롤러를 세운다.</summary>
        /// <param name="levels">경험치가 들어갈 육성 상태이다.</param>
        /// <param name="stageExperience">1번 스테이지가 주는 경험치이다.</param>
        /// <param name="progression">진행 서비스이며 null이면 컨트롤러가 임시로 만든다.</param>
        /// <param name="outcomeChannel">결과를 발행할 통로이다.</param>
        /// <param name="placement">명단을 잡아 올 배치 컨트롤러이다.</param>
        /// <returns>세운 흐름 컨트롤러이다.</returns>
        private StageFlowController CreateFlow(
            UnitLevelProgress levels,
            int stageExperience,
            StageProgressionService progression,
            out TestMessageChannel<BattleOutcomeDecidedEvent> outcomeChannel,
            out UnitPlacementController placement)
        {
            var table = CreateTable($"{{\"entries\":[{{\"stageId\":1,\"experience\":{stageExperience}}}]}}");
            return CreateFlow(levels, table, progression, out outcomeChannel, out placement);
        }

        /// <summary>배치 컨트롤러를 이어 붙인 흐름 컨트롤러를 세우고 의존성을 주입한다.</summary>
        /// <remarks>
        /// EditMode에서는 수명주기 콜백이 돌지 않으므로 공개 주입 진입점으로 실제 실행 경로를 그대로 태운다.
        /// </remarks>
        /// <param name="levels">경험치가 들어갈 육성 상태이다.</param>
        /// <param name="table">스테이지 경험치 표이다.</param>
        /// <param name="progression">진행 서비스이며 null이면 컨트롤러가 임시로 만든다.</param>
        /// <param name="outcomeChannel">결과를 발행할 통로이다.</param>
        /// <param name="placement">명단을 잡아 올 배치 컨트롤러이다.</param>
        /// <returns>세운 흐름 컨트롤러이다.</returns>
        private StageFlowController CreateFlow(
            UnitLevelProgress levels,
            StageExperienceTable table,
            StageProgressionService progression,
            out TestMessageChannel<BattleOutcomeDecidedEvent> outcomeChannel,
            out UnitPlacementController placement)
        {
            placement = CreateObject("UnitPlacementController").AddComponent<UnitPlacementController>();
            placement.BeginPlacement();
            placement.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));

            var controller = CreateObject("StageFlowController").AddComponent<StageFlowController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("placementController").objectReferenceValue = placement;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            outcomeChannel = new TestMessageChannel<BattleOutcomeDecidedEvent>();
            controller.InjectRuntimeDependencies(
                outcomeChannel,
                new FakeGameFlowService(),
                new FakeSceneCatalog(3),
                progression,
                null,
                new StageRewardService(levels, table, CreateCurve()));
            return controller;
        }

        /// <summary>지정한 종류들을 배치하고 전투를 시작해 명단이 잡히게 한다.</summary>
        /// <remarks>
        /// 컨트롤러의 배치 경로는 프리팹을 스폰하므로 EditMode에서 태우기 어렵다. 재려는 것은 스폰이 아니라
        /// 무엇을 배치했는지가 명단에 어떻게 남는지이므로, 규칙만 판정하는 계획에 직접 넣는다.
        /// 배치는 격자 칸 위에서 일어나고 한 칸에는 하나만 서므로, 항목마다 다른 칸(0번부터 차례로)에 세운다.
        /// </remarks>
        /// <param name="controller">명단을 잡을 흐름 컨트롤러이다.</param>
        /// <param name="placement">유닛을 넣을 배치 컨트롤러이다.</param>
        /// <param name="definitionIds">배치할 유닛 종류의 식별자들이다.</param>
        private void StartBattleWith(
            StageFlowController controller,
            UnitPlacementController placement,
            params string[] definitionIds)
        {
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
            Assert.That(
                controller.DeployedDefinitionIds.Count,
                Is.EqualTo(definitionIds.Length),
                "전투 시작 시점에 배치한 수만큼 명단에 남아야 한다.");
        }

        /// <summary>적힌 값을 채운 스테이지 경험치 표를 만든다.</summary>
        /// <param name="json">채울 값이 담긴 JSON이다.</param>
        /// <returns>만든 표이다.</returns>
        private StageExperienceTable CreateTable(string json)
        {
            var table = ScriptableObject.CreateInstance<StageExperienceTable>();
            JsonUtility.FromJsonOverwrite(json, table);
            _createdObjects.Add(table);
            return table;
        }

        /// <summary>레벨 하나를 올리는 데 1000이 드는 곡선을 만든다.</summary>
        /// <remarks>
        /// 검사 중 레벨이 오르지 않을 만큼 비싸게 잡는다. 재려는 것은 <b>몫이 얼마나 들어갔는가</b>인데,
        /// 레벨이 오르면 들어간 경험치가 그만큼 줄어 남은 값으로는 몫을 되짚을 수 없다.
        /// </remarks>
        /// <returns>만든 곡선이다.</returns>
        private UnitLevelCurve CreateCurve()
        {
            var curve = ScriptableObject.CreateInstance<UnitLevelCurve>();
            JsonUtility.FromJsonOverwrite("{\"maxLevel\":5,\"xpToNext\":[1000,2000,3000,4000]}", curve);
            _createdObjects.Add(curve);
            return curve;
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 오브젝트이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>씬 전환 요청을 삼키는 테스트용 게임 흐름 서비스이다.</summary>
        private sealed class FakeGameFlowService : IGameFlowService
        {
            public UniTask StartNewGameAsync() => UniTask.CompletedTask;

            public UniTask LoadGameplayLevelAsync(int levelId) => UniTask.CompletedTask;

            public UniTask ReturnToMainMenuAsync() => UniTask.CompletedTask;
        }

        /// <summary>1부터 지정한 개수까지의 Gameplay 레벨만 가진 테스트용 씬 카탈로그이다.</summary>
        private sealed class FakeSceneCatalog : IProjectSceneCatalog
        {
            private readonly int _stageCount;

            /// <summary>지정한 개수의 스테이지를 가진 카탈로그를 만든다.</summary>
            /// <param name="stageCount">가진 스테이지 수이다.</param>
            public FakeSceneCatalog(int stageCount)
            {
                _stageCount = stageCount;
            }

            /// <inheritdoc />
            public IReadOnlyList<ProjectSceneDefinition> Scenes { get; } = Array.Empty<ProjectSceneDefinition>();

            /// <inheritdoc />
            public int DefaultGameplayLevelId => 1;

            /// <inheritdoc />
            public SceneReference BootstrapScene { get; } = SceneReference.Create("Assets/Scenes/Bootstrap.unity");

            /// <inheritdoc />
            public SceneReference LoadingScene { get; } = SceneReference.Create("Assets/Scenes/Loading.unity");

            /// <inheritdoc />
            public SceneReference MainMenuScene { get; } = SceneReference.Create("Assets/Scenes/MainMenu.unity");

            /// <inheritdoc />
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
            /// <param name="scenePath">확인할 씬 경로이다.</param>
            /// <param name="levelId">찾은 레벨 식별자이며 항상 기본값이다.</param>
            /// <returns>항상 false이다.</returns>
            public bool TryGetGameplayLevelId(string scenePath, out int levelId)
            {
                levelId = default;
                return false;
            }
        }
    }
}
