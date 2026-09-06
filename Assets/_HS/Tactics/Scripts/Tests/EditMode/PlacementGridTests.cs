using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>격자가 처음 서는 자리를 정하는 데까지만 쓰이는지 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>격자는 초기 배치일 뿐이다.</b> 전투가 시작되면 유닛은 칸을 지키지 않고 자유롭게 움직인다.
    /// 그래서 여기서 확인하는 것은 칸↔좌표 계산과 점유 규칙이며, 전투가 시작된 뒤에는
    /// <b>칸을 아무도 묻지 않는다</b>는 것까지가 대상이다.
    /// </para>
    /// <para>
    /// 점유를 따로 세는 표는 없다. 어느 칸이 찼는지는 이미 배치한 항목이 답하므로,
    /// 이 검사들도 그 경로로만 묻는다.
    /// </para>
    /// </remarks>
    public sealed class PlacementGridTests
    {
        /// <summary>검사가 만든 것들이며 정리 대상이다.</summary>
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
        public void EveryCellCentreResolvesBackToItsOwnCell()
        {
            var grid = new PlacementGrid(CreateArea());

            for (var cellIndex = 0; cellIndex < grid.CellCount; cellIndex++)
            {
                Assert.That(grid.TryGetCellCenter(cellIndex, out var center), Is.True, $"{cellIndex}번 칸의 중심을 구하지 못했다.");
                Assert.That(grid.TryGetCellIndex(center, out var roundTripped), Is.True);
                Assert.That(
                    roundTripped,
                    Is.EqualTo(cellIndex),
                    $"{cellIndex}번 칸의 중심이 {roundTripped}번 칸으로 돌아왔다.");
            }
        }

        [Test]
        public void TheCellsCoverTheZoneWithoutOverlapping()
        {
            // 아홉 칸의 중심이 모두 다른 칸이면 칸이 겹치지도 비지도 않은 것이다.
            var grid = new PlacementGrid(CreateArea());
            var seenCells = new HashSet<int>();

            for (var cellIndex = 0; cellIndex < grid.CellCount; cellIndex++)
            {
                grid.TryGetCellCenter(cellIndex, out var center);
                grid.TryGetCellIndex(center, out var resolved);
                Assert.That(seenCells.Add(resolved), Is.True, $"{resolved}번 칸이 두 번 나왔다.");
            }

            Assert.That(seenCells.Count, Is.EqualTo(PartyRules.GridCellCount));
        }

        [Test]
        public void APointOutsideTheZoneBelongsToNoCell()
        {
            // 가장 가까운 칸으로 끌어당기지 않는다. 끌어당기면 겨냥하지 않은 자리에 유닛이 선다.
            var grid = new PlacementGrid(CreateArea());

            Assert.That(
                grid.TryGetCellIndex(new Vector3(100f, 0f, 100f), out var cellIndex),
                Is.False,
                "구역 밖을 가리켰는데 칸을 돌려주었다.");
            Assert.That(cellIndex, Is.EqualTo(-1));
        }

        [Test]
        public void AnEmptyZoneHasNoCells()
        {
            var grid = new PlacementGrid(PlacementArea.None);

            Assert.That(grid.IsEmpty, Is.True);
            Assert.That(grid.TryGetCellCenter(0, out _), Is.False, "구역이 없는데 칸의 중심을 돌려주었다.");
            Assert.That(grid.TryGetCellIndex(Vector3.zero, out _), Is.False);
        }

        [Test]
        public void ASecondUnitCannotShareACell()
        {
            var plan = CreatePlan();
            Assert.That(
                plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out _),
                Is.EqualTo(PlacementResult.Success));

            Assert.That(
                plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out _),
                Is.EqualTo(PlacementResult.CellOccupied),
                "이미 유닛이 선 칸인데 한 기가 더 들어갔다.");
            Assert.That(plan.Count, Is.EqualTo(1));
        }

        [Test]
        public void RecallingFreesTheCell()
        {
            var plan = CreatePlan();
            plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out var entry);
            Assert.That(plan.IsCellOccupied(4), Is.True);

            Assert.That(plan.TryRecall(entry.Id, out _), Is.EqualTo(PlacementResult.Success));

            Assert.That(plan.IsCellOccupied(4), Is.False, "회수했는데 칸이 아직 찬 것으로 남았다.");
            Assert.That(
                plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out _),
                Is.EqualTo(PlacementResult.Success),
                "회수한 칸은 즉시 다시 채울 수 있어야 한다.");
        }

        [Test]
        public void FillingFiveCellsLeavesTheSixthRefused()
        {
            // 칸은 아홉이지만 데려가는 것은 다섯이다. 빈 칸이 넷 남아도 여섯째는 들어가지 못한다.
            var plan = CreatePlan();
            for (var cellIndex = 0; cellIndex < PartyRules.MaxPartySize; cellIndex++)
            {
                Assert.That(
                    plan.TryPlaceAtCell(CreateUnitDefinition(), cellIndex, out _),
                    Is.EqualTo(PlacementResult.Success));
            }

            Assert.That(
                plan.IsCellOccupied(PartyRules.MaxPartySize),
                Is.False,
                "이 검사는 빈 칸이 남아 있어야 뜻을 갖는다.");
            Assert.That(
                plan.TryPlaceAtCell(CreateUnitDefinition(), PartyRules.MaxPartySize, out _),
                Is.EqualTo(PlacementResult.CapacityReached),
                "빈 칸이 남았다는 이유로 파티 인원을 넘겨 배치됐다.");
        }

        [Test]
        public void MovingOntoAnOccupiedCellIsRefused()
        {
            var plan = CreatePlan();
            plan.TryPlaceAtCell(CreateUnitDefinition(), 0, out var first);
            plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out _);

            Assert.That(
                plan.TryMoveToCell(first.Id, 4, out _),
                Is.EqualTo(PlacementResult.CellOccupied),
                "다른 유닛이 선 칸으로 옮겨졌다.");
            Assert.That(plan.TryGetEntry(first.Id, out var kept), Is.True);
            Assert.That(kept.CellIndex, Is.EqualTo(0), "거절된 이동이 칸을 바꿔 놓았다.");
        }

        [Test]
        public void MovingOntoItsOwnCellIsAccepted()
        {
            // 자기 칸을 자기가 막고 있다고 거절하면 방금 집어 든 유닛을 도로 놓지 못한다.
            var plan = CreatePlan();
            plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out var entry);

            Assert.That(plan.TryMoveToCell(entry.Id, 4, out _), Is.EqualTo(PlacementResult.Success));
        }

        [Test]
        public void NobodyAsksAboutCellsOnceTheBattleStarts()
        {
            // 격자는 초기 배치까지다. 전투가 시작되면 칸 상태를 묻지도 보여 주지도 않는다.
            var controller = CreatePlacementController();
            controller.Plan.TryPlaceAtCell(CreateUnitDefinition(), 4, out _);

            var viewModel = new PlacementViewModel(controller);
            try
            {
                Assert.That(
                    viewModel.CellStates[4],
                    Is.EqualTo(PlacementCellState.Occupied),
                    "배치 단계인데 찬 칸이 찬 것으로 보이지 않는다.");

                Assert.That(controller.TryStartBattle(), Is.True);

                for (var cellIndex = 0; cellIndex < viewModel.CellCount; cellIndex++)
                {
                    Assert.That(
                        viewModel.CellStates[cellIndex],
                        Is.EqualTo(PlacementCellState.Unavailable),
                        $"전투가 시작됐는데 {cellIndex}번 칸이 아직 칸 상태를 내보이고 있다.");
                }

                Assert.That(
                    viewModel.TryPlaceSelectedUnitAtCell(0),
                    Is.EqualTo(PlacementResult.NotInPlacingPhase),
                    "전투가 시작된 뒤인데 칸에 배치가 받아들여졌다.");
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        /// <summary>배치를 받아 줄 넉넉한 구역을 만든다.</summary>
        /// <returns>만든 배치 구역이다.</returns>
        private static PlacementArea CreateArea()
        {
            return new PlacementArea(Vector3.zero, new Vector3(18f, 6f, 18f));
        }

        /// <summary>구역을 갖춘 배치 계획을 만든다.</summary>
        /// <returns>만든 배치 계획이다.</returns>
        private static UnitPlacementPlan CreatePlan()
        {
            return new UnitPlacementPlan(CreateArea());
        }

        /// <summary>배치 단계를 열고 구역을 지정한 배치 컨트롤러를 만든다.</summary>
        /// <returns>만든 배치 컨트롤러이다.</returns>
        private UnitPlacementController CreatePlacementController()
        {
            var controllerObject = new GameObject("UnitPlacementController");
            _createdObjects.Add(controllerObject);

            var controller = controllerObject.AddComponent<UnitPlacementController>();
            controller.BeginPlacement();
            controller.Plan.SetArea(CreateArea());
            return controller;
        }

        /// <summary>검사에서 쓸 유닛 정의를 만든다.</summary>
        /// <returns>만든 유닛 정의이다.</returns>
        private UnitDefinition CreateUnitDefinition()
        {
            var definition = UnitDefinition.CreateRuntime("GridTester", 100);
            _createdObjects.Add(definition);
            return definition;
        }
    }
}
