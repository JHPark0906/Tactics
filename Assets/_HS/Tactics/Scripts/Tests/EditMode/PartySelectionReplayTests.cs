using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// MainMenu에서 커밋된 배치를 게임플레이 씬의 재생기가 배치 컨트롤러에 그대로 넘기고 전투를 시작하는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 커밋이 있으면 항목마다 <see cref="UnitPlacementController.TryPlaceUnitAtCell"/>로 가고 끝에 <see cref="UnitPlacementController.TryStartBattle"/>가
    /// 불린다. 커밋이 없으면 아무것도 하지 않는다 — 그래서 커밋 없이 게임플레이 씬에 들어오면 손 배치 창으로 떨어진다.
    /// 그 두 갈래를 여기서 고정한다.
    /// </para>
    /// <para>
    /// 스폰까지 실제로 태운다. 프리팹은 <see cref="TacticalUnit"/>을 붙인 비활성 오브젝트로 대신하고, 컨테이너 없이 세우므로
    /// 스포너가 그 사실을 한 번 경고한다. 재생기의 Start는 에디터가 부르지 않으므로 검사가 직접 부른다.
    /// </para>
    /// </remarks>
    public sealed class PartySelectionReplayTests
    {
        private const string PlacementControllerPropertyName = "placementController";

        private readonly List<Object> _createdObjects = new();
        private UnitPlacementController _controller;
        private PartySelectionReplay _replay;
        private PartySelectionService _service;
        private UnitDefinition _hero;

        [SetUp]
        public void SetUp()
        {
            var controllerObject = Track(new GameObject("UnitPlacementController"));
            _controller = controllerObject.AddComponent<UnitPlacementController>();
            _controller.UnitPlaced += unit => Track(unit.gameObject);
            _controller.BeginPlacement();
            // 씬에 구역 오브젝트를 두지 않으므로 계획에 직접 넉넉한 구역을 지정한다.
            _controller.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));

            var template = Track(new GameObject("HeroTemplate"));
            template.SetActive(false);
            template.AddComponent<TacticalUnit>();
            _hero = Track(UnitDefinition.CreateRuntime("Hero", 100, unitPrefab: template));

            _service = new PartySelectionService();
            _replay = Track(new GameObject("PartySelectionReplay")).AddComponent<PartySelectionReplay>();
            var serializedReplay = new SerializedObject(_replay);
            serializedReplay.FindProperty(PlacementControllerPropertyName).objectReferenceValue = _controller;
            serializedReplay.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            // 에디터는 플레이 모드가 아닐 때 OnDestroy를 부르지 않아 컨트롤러가 든 스포너가 정리되지 않는다.
            // 직접 불러 스포너의 스테이징 오브젝트가 검사 씬에 남지 않게 한다.
            if (_controller != null)
            {
                MonoBehaviourLifecycle.InvokeOnDestroy(_controller);
            }

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
        public void CommittedEntriesAreReplayedAndTheBattleStarts()
        {
            _replay.InjectPartySelectionService(_service);
            _service.Commit(new[] { new PartySelectionEntry(0, _hero), new PartySelectionEntry(4, _hero) });
            ExpectSpawnerWarningAboutMissingContainer();

            MonoBehaviourLifecycle.InvokeStart(_replay);

            Assert.That(_controller.PlacedUnitCount, Is.EqualTo(2), "커밋된 항목 수만큼 세워야 한다.");
            Assert.That(_controller.Phase, Is.EqualTo(PlacementPhase.Battle), "재생을 마치면 곧바로 전투를 시작한다.");
            Assert.That(_service.HasSelection, Is.False, "재생한 선택은 서비스에서 비운다. 남겨 두면 다음 스테이지에 또 세운다.");
        }

        [Test]
        public void WithoutACommitNothingIsPlacedAndPlacingContinues()
        {
            _replay.InjectPartySelectionService(_service);

            MonoBehaviourLifecycle.InvokeStart(_replay);

            Assert.That(_controller.PlacedUnitCount, Is.Zero);
            Assert.That(_controller.Phase, Is.EqualTo(PlacementPhase.Placing), "커밋이 없으면 손 배치 단계가 그대로 남는다. 이것이 커밋 없이 들어온 씬의 모습이다.");
        }

        [Test]
        public void WithoutAServiceNothingIsPlacedAndPlacingContinues()
        {
            MonoBehaviourLifecycle.InvokeStart(_replay);

            Assert.That(_controller.PlacedUnitCount, Is.Zero);
            Assert.That(_controller.Phase, Is.EqualTo(PlacementPhase.Placing));
        }

        [Test]
        public void AnEntryThatCannotBePlacedIsReportedAndTheRestStillStartTheBattle()
        {
            _replay.InjectPartySelectionService(_service);
            _service.Commit(new[] { new PartySelectionEntry(0, _hero), new PartySelectionEntry(0, _hero) });
            ExpectSpawnerWarningAboutMissingContainer();
            LogAssert.Expect(LogType.Warning, new Regex("다시 세우지 못했다"));

            MonoBehaviourLifecycle.InvokeStart(_replay);

            Assert.That(_controller.PlacedUnitCount, Is.EqualTo(1), "같은 칸의 둘째 항목은 거부되고 첫째만 선다.");
            Assert.That(_controller.Phase, Is.EqualTo(PlacementPhase.Battle), "하나라도 섰으면 전투는 시작한다.");
        }

        [Test]
        public void WhenNothingCouldBePlacedTheBattleDoesNotStart()
        {
            _replay.InjectPartySelectionService(_service);
            var withoutPrefab = Track(UnitDefinition.CreateRuntime("Ghost", 100));
            _service.Commit(new[] { new PartySelectionEntry(0, withoutPrefab) });
            LogAssert.Expect(LogType.Warning, new Regex("다시 세우지 못했다"));
            LogAssert.Expect(LogType.Warning, new Regex("전투를 시작하지 못했다"));

            MonoBehaviourLifecycle.InvokeStart(_replay);

            Assert.That(_controller.PlacedUnitCount, Is.Zero);
            Assert.That(_controller.Phase, Is.EqualTo(PlacementPhase.Placing), "세운 유닛이 없으면 전투를 시작하지 않는다.");
        }

        /// <summary>컨테이너 없이 세우는 첫 스폰에서 스포너가 한 번 남기는 경고를 미리 받아 둔다.</summary>
        private static void ExpectSpawnerWarningAboutMissingContainer()
        {
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이 세웠다"));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
