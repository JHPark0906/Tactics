using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HS.Tactics.Character.Movement;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.PlayMode
{
    /// <summary>
    /// MainMenu에서 커밋된 배치가 게임플레이 무대에서 Unity의 실제 수명주기를 타고 재생되는지 검증한다.
    /// 켜질 때 배치 단계로 들어가고, Start에서 재생기가 항목을 세우며, 세운 유닛이 커밋한 칸에 같은 종류로 서고, 전투가 시작된다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EditMode 검사는 Start를 검사가 직접 부른다. 여기서는 오브젝트를 켜고 한 프레임을 기다려 Unity가 OnEnable → Start를
    /// 실제 순서로 부르게 한다 — 컨트롤러의 OnEnable(배치 단계 진입)이 재생기의 Start보다 앞선다는 것이 이 흐름의 전제이고,
    /// 그 전제는 실제 수명주기에서만 확인된다.
    /// </para>
    /// <para>
    /// 무대는 씬을 읽지 않고 세운다. 배치 구역·컨트롤러·재생기를 한 오브젝트에 두고 서비스는 직접 넣는다. 그래서 여기서 확인되는
    /// 것은 「커밋 → 재생 → 스폰 → 전투 시작」의 무대 안 흐름이고, 실제 Level1.unity를 씬 전환으로 열어 스코프 주입을 받는 것까지는
    /// 이 검사의 범위 밖이다.
    /// </para>
    /// <para>
    /// 유닛 프리팹은 <see cref="TacticalUnit"/>을 붙인 비활성 오브젝트로 대신한다. 컨테이너 없이 세우므로 스포너가 그 사실을 한 번
    /// 경고하고, 정의에 어빌리티 집합과 행동 트리가 없어 조립이 경고를 남기지만 오류는 아니다.
    /// </para>
    /// </remarks>
    public sealed class PartySelectionReplayStageTests
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

        [UnityTest]
        public IEnumerator MovingAPlacedUnitKeepsItsVisualAtTheNewCellAcrossIdleFrames()
        {
            var template = Track(new GameObject("InterpolatedHeroTemplate"));
            template.SetActive(false);
            template.AddComponent<TacticalUnit>();
            var visualTemplate = new GameObject("Visual").transform;
            visualTemplate.SetParent(template.transform, false);
            var visualOffset = new Vector3(0.1f, 1.2f, 0.3f);
            visualTemplate.localPosition = visualOffset;
            var interpolator = template.AddComponent<FixedStepMotionInterpolator>();
            SetPrivateField(interpolator, "visualRoot", visualTemplate);
            var hero = Track(UnitDefinition.CreateRuntime("RelocatedHero", 100, unitPrefab: template));

            var stage = Track(new GameObject("RelocationStage"));
            stage.SetActive(false);
            var zone = stage.AddComponent<UnitPlacementZone>();
            var controller = stage.AddComponent<UnitPlacementController>();
            SetPrivateField(controller, "placementZone", zone);
            stage.SetActive(true);
            Assert.That(controller.Plan.Grid.TryGetCellCenter(0, out var start), Is.True);
            Assert.That(controller.Plan.Grid.TryGetCellCenter(4, out var destination), Is.True);
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이 세웠다"));
            Assert.That(controller.TryPlaceUnit(hero, start, out var unit), Is.EqualTo(PlacementResult.Success));
            Track(unit.gameObject);
            var mover = unit.GetComponent<PlanarCharacterMover>();
            var visual = unit.transform.Find("Visual");
            Assert.That(mover, Is.Not.Null);
            Assert.That(visual, Is.Not.Null);
            yield return null;

            Assert.That(controller.TryMoveUnit(controller.Plan.Entries[0].Id, destination),
                Is.EqualTo(PlacementResult.Success));
            Assert.That(mover.PreviousLogicPosition, Is.EqualTo(destination));
            Assert.That(mover.CurrentLogicPosition, Is.EqualTo(destination));

            // 실제 고정 틱과 LateUpdate가 지난 뒤에도 이전 배치 자리로 되돌아가면 안 된다.
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;
            Assert.That(unit.transform.position, Is.EqualTo(destination));
            Assert.That(mover.CurrentLogicPosition, Is.EqualTo(destination));
            Assert.That(mover.PreviousLogicPosition, Is.EqualTo(destination));
            var expectedVisual = destination + unit.transform.rotation * visualOffset;
            Assert.That(Vector3.Distance(visual.position, expectedVisual), Is.LessThan(0.0001f));
            Assert.That(mover.HasReachedDestination, Is.True);
        }

        [UnityTest]
        public IEnumerator ACommittedSelectionIsReplayedIntoTheStageAtTheSameCellsAndTheBattleStarts()
        {
            var template = Track(new GameObject("HeroTemplate"));
            template.SetActive(false);
            template.AddComponent<TacticalUnit>();
            var hero = Track(UnitDefinition.CreateRuntime("Hero", 100, unitPrefab: template));

            var service = new PartySelectionService();
            var committedCells = new[] { 0, 4 };
            service.Commit(new[] { new PartySelectionEntry(committedCells[0], hero), new PartySelectionEntry(committedCells[1], hero) });

            var stage = Track(new GameObject("Stage"));
            stage.SetActive(false);
            var zone = stage.AddComponent<UnitPlacementZone>();
            var controller = stage.AddComponent<UnitPlacementController>();
            SetPrivateField(controller, "placementZone", zone);
            var replay = stage.AddComponent<PartySelectionReplay>();
            SetPrivateField(replay, "placementController", controller);
            replay.InjectPartySelectionService(service);

            var spawned = new List<TacticalUnit>();
            controller.UnitPlaced += unit =>
            {
                spawned.Add(unit);
                Track(unit.gameObject);
            };

            // 컨테이너 없이 세우는 첫 스폰에서 스포너가 한 번 경고한다. 이 경고가 나지 않으면 스폰 자체가 일어나지 않은 것이다.
            LogAssert.Expect(LogType.Warning, new Regex("컨테이너 없이 세웠다"));

            stage.SetActive(true);

            Assert.That(controller.Phase, Is.EqualTo(PlacementPhase.Placing), "무대 확인: 켜질 때 배치 단계로 들어가야 Start의 재생이 받아들여진다.");
            Assert.That(controller.PlacedUnitCount, Is.Zero, "무대 확인: Start 전에는 아무것도 서 있지 않아야 한다.");

            yield return null;

            Assert.That(controller.Phase, Is.EqualTo(PlacementPhase.Battle), "재생을 마치면 곧바로 전투가 시작되어야 한다.");
            Assert.That(controller.PlacedUnitCount, Is.EqualTo(committedCells.Length), "커밋한 항목 수만큼 서야 한다.");
            Assert.That(spawned, Has.Count.EqualTo(committedCells.Length), "세워진 유닛 수가 커밋한 항목 수와 같아야 한다.");
            Assert.That(service.HasSelection, Is.False, "재생한 선택은 서비스에서 비워져야 한다.");

            var grid = controller.Plan.Grid;
            foreach (var cellIndex in committedCells)
            {
                Assert.That(grid.TryGetCellCenter(cellIndex, out var cellCenter), Is.True, $"무대 확인: {cellIndex}번 칸이 격자에 있어야 한다.");
                var unitAtCell = spawned.Find(unit => (unit.transform.position - cellCenter).sqrMagnitude < 0.0001f);
                Assert.That(unitAtCell, Is.Not.Null, $"커밋한 {cellIndex}번 칸 {cellCenter}에 유닛이 서 있어야 한다. 배치 패널에서 고른 자리와 인게임 자리가 같아야 한다.");
                Assert.That(unitAtCell.Definition, Is.SameAs(hero), $"{cellIndex}번 칸의 유닛은 커밋한 종류여야 한다.");
            }
        }

        /// <summary>인스펙터로만 채우는 직렬화 필드를 채운다. PlayMode 검사 어셈블리는 에디터 API를 쓰지 않으므로 리플렉션으로 넣는다.</summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"무대 확인: {target.GetType().Name}에 {fieldName}이라는 비공개 인스턴스 필드가 없다.");
            field.SetValue(target, value);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
