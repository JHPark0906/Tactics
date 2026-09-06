using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>맵 조작이 기존 배치 API로 전달되는 경로를 검증한다.</summary>
    /// <remarks>
    /// EditMode에서는 MonoBehaviour 수명주기 콜백이 호출되지 않으므로 포인터 장치와 카메라를 쓰지 않고,
    /// 공개 진입점인 <see cref="PlacementInputController.HandlePrimaryPress"/>와
    /// <see cref="PlacementInputController.HandleSecondaryPress"/>로 실제 처리 경로를 그대로 태운다.
    /// </remarks>
    public sealed class PlacementInputControllerTests
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
        public void ClicksAreIgnoredOutsideThePlacingPhase()
        {
            var placement = CreatePlacementController(beginPlacement: false);
            var input = CreateInputController(placement);

            var result = input.HandlePrimaryPress(Vector3.zero);

            Assert.That(result, Is.EqualTo(PlacementResult.NotInPlacingPhase));
            Assert.That(placement.PlacedUnitCount, Is.Zero);
        }

        [Test]
        public void EmptyGroundDoesNothingWithoutASelectedUnit()
        {
            var placement = CreatePlacementController();
            var input = CreateInputController(placement);

            var result = input.HandlePrimaryPress(new Vector3(2f, 0f, 2f));

            Assert.That(result, Is.EqualTo(PlacementResult.MissingDefinition));
            Assert.That(placement.PlacedUnitCount, Is.Zero);
            Assert.That(input.HeldEntryId, Is.Zero);
        }

        [Test]
        public void EmptyGroundForwardsTheWindowSelectionToThePlacementController()
        {
            var placement = CreatePlacementController();
            var window = CreatePlacementWindow(placement);
            window.SelectUnitDefinition(CreateDefinition());
            var input = CreateInputController(placement: null, window);

            var result = input.HandlePrimaryPress(new Vector3(2f, 0f, 2f));

            // 프리팹 없는 정의라 스폰까지는 가지 않지만, 선택이 창을 거쳐 배치 컨트롤러에 도달했음을 뜻한다.
            Assert.That(result, Is.EqualTo(PlacementResult.MissingUnitPrefab));
        }

        [Test]
        public void ClickingAPlacedUnitPicksItUp()
        {
            var placement = CreatePlacementController();
            var entry = SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);

            var result = input.HandlePrimaryPress(new Vector3(0.3f, 0f, 0.2f));

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(input.HeldEntryId, Is.EqualTo(entry.Id));
            Assert.That(placement.PlacedUnitCount, Is.EqualTo(1));
        }

        [Test]
        public void TheSecondClickMovesTheHeldUnit()
        {
            var placement = CreatePlacementController();
            var entry = SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);
            input.HandlePrimaryPress(Vector3.zero);

            var result = input.HandlePrimaryPress(new Vector3(4f, 0f, -2f));

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(input.HeldEntryId, Is.Zero, "옮기고 나면 집어 든 상태가 풀려야 한다.");
            Assert.That(placement.Plan.TryGetEntry(entry.Id, out var movedEntry), Is.True);
            // 격자에서는 가리킨 지점이 아니라 그 지점이 든 칸의 중심에 선다.
            Assert.That(
                movedEntry.CellIndex,
                Is.Not.EqualTo(entry.CellIndex),
                "다른 칸을 가리켰는데 칸이 그대로다.");
            Assert.That(placement.Plan.Grid.TryGetCellCenter(movedEntry.CellIndex, out var cellCenter), Is.True);
            Assert.That(movedEntry.Position, Is.EqualTo(cellCenter));
        }

        [Test]
        public void SecondaryPressRecallsThePlacedUnit()
        {
            var placement = CreatePlacementController();
            SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);

            var result = input.HandleSecondaryPress(new Vector3(0.2f, 0f, 0f));

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(placement.PlacedUnitCount, Is.Zero);
        }

        [Test]
        public void SecondaryPressCancelsTheHeldMove()
        {
            var placement = CreatePlacementController();
            var entry = SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);
            input.HandlePrimaryPress(Vector3.zero);

            var result = input.HandleSecondaryPress(new Vector3(6f, 0f, 6f));

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(input.HeldEntryId, Is.Zero);
            Assert.That(placement.Plan.TryGetEntry(entry.Id, out var keptEntry), Is.True);
            Assert.That(keptEntry.Position, Is.EqualTo(entry.Position), "취소는 유닛을 옮기지 않아야 한다.");
            Assert.That(keptEntry.CellIndex, Is.EqualTo(entry.CellIndex), "취소는 칸을 바꾸지 않아야 한다.");
        }

        [Test]
        public void DistantClicksDoNotPickUpAUnit()
        {
            var placement = CreatePlacementController();
            SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);

            var result = input.HandlePrimaryPress(new Vector3(8f, 0f, 8f));

            Assert.That(result, Is.EqualTo(PlacementResult.MissingDefinition));
            Assert.That(input.HeldEntryId, Is.Zero);
        }

        [Test]
        public void StartingBattleStopsAcceptingClicks()
        {
            var placement = CreatePlacementController();
            SeedEntry(placement, Vector3.zero);
            var input = CreateInputController(placement);

            Assert.That(placement.TryStartBattle(), Is.True);

            Assert.That(input.HandlePrimaryPress(Vector3.zero), Is.EqualTo(PlacementResult.NotInPlacingPhase));
            Assert.That(input.HandleSecondaryPress(Vector3.zero), Is.EqualTo(PlacementResult.NotInPlacingPhase));
            Assert.That(placement.PlacedUnitCount, Is.EqualTo(1), "전투 중에는 회수도 받아들이지 않아야 한다.");
        }

        /// <summary>배치 단계를 연 배치 컨트롤러와 넉넉한 구역을 만든다.</summary>
        /// <param name="beginPlacement">배치 단계를 즉시 열지 여부이다.</param>
        private UnitPlacementController CreatePlacementController(bool beginPlacement = true)
        {
            var placement = CreateObject("UnitPlacementController").AddComponent<UnitPlacementController>();
            if (beginPlacement)
            {
                placement.BeginPlacement();
            }

            // 씬에 구역 오브젝트를 두지 않으므로 계획에 직접 구역을 지정한다.
            placement.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
            return placement;
        }

        /// <summary>배치 컨트롤러에 연결한 배치 창을 만든다.</summary>
        /// <param name="placement">연결할 배치 컨트롤러이다.</param>
        private PlacementWindow CreatePlacementWindow(UnitPlacementController placement)
        {
            var window = CreateObject("PlacementWindow").AddComponent<PlacementWindow>();
            window.SetPlacementController(placement);
            return window;
        }

        /// <summary>조작 대상을 연결한 입력 컴포넌트를 만든다.</summary>
        /// <param name="placement">직접 지정할 배치 컨트롤러이며 null이면 창의 컨트롤러를 따른다.</param>
        /// <param name="window">배치 창이며 없으면 연결하지 않는다.</param>
        private PlacementInputController CreateInputController(
            UnitPlacementController placement,
            PlacementWindow window = null)
        {
            var input = CreateObject("PlacementInputController").AddComponent<PlacementInputController>();
            if (window != null)
            {
                input.SetPlacementWindow(window);
            }

            if (placement != null)
            {
                input.SetPlacementController(placement);
            }

            return input;
        }

        /// <summary>스폰 없이 배치 계획에만 항목을 넣어 이미 놓인 유닛을 흉내 낸다.</summary>
        /// <param name="placement">항목을 넣을 배치 컨트롤러이다.</param>
        /// <param name="position">배치할 월드 좌표이다.</param>
        /// <returns>추가된 배치 항목이다.</returns>
        private PlacementEntry SeedEntry(UnitPlacementController placement, Vector3 position)
        {
            var result = placement.Plan.TryPlace(CreateDefinition(), position, out var entry);
            Assert.That(result, Is.EqualTo(PlacementResult.Success), "테스트 준비 단계의 배치는 성공해야 한다.");
            return entry;
        }

        /// <summary>프리팹이 연결되지 않은 임시 유닛 정의를 만든다.</summary>
        private UnitDefinition CreateDefinition()
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100);
            _createdObjects.Add(definition);
            return definition;
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
