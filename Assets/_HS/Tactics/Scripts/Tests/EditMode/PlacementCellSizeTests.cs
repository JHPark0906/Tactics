using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>배치가 유닛 지름보다 좁은 칸을 거절하는지 고정한다.</summary>
    /// <remarks>
    /// <see cref="UnitPlacementPlan.TryMoveToCell"/>은 <see cref="UnitPlacementPlan.TryPlaceAtCell"/>과 같은
    /// 부류(칸 번호를 직접 받는 연산)이므로 같은 규칙을 따라야 한다. 두 자리를 각각 확인하고,
    /// 옮기는 쪽은 배치할 때는 넉넉했던 구역이 이후 좁아지는 경우로 검증한다 — 그래야 두 규칙이
    /// 갈릴 수 있는 실제 자리가 된다.
    /// </remarks>
    public sealed class PlacementCellSizeTests
    {
        private readonly List<UnitDefinition> _createdDefinitions = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var definition in _createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _createdDefinitions.Clear();
        }

        [Test]
        public void PlacingIntoACellSmallerThanTheUnitIsRejected()
        {
            var plan = new UnitPlacementPlan(new PlacementArea(Vector3.zero, new Vector3(3f, 1f, 3f)));
            var definition = CreateDefinition(radius: 0.6f);

            var result = plan.TryPlaceAtCell(definition, 0, out var entry);

            Assert.That(result, Is.EqualTo(PlacementResult.CellTooSmall), "칸(1×1)보다 지름(1.2)이 큰 유닛은 들어가면 안 된다.");
            Assert.That(entry.IsValid, Is.False);
            Assert.That(plan.Count, Is.Zero);
        }

        [Test]
        public void PlacingIntoACellExactlyTheUnitsSizeSucceeds()
        {
            var plan = new UnitPlacementPlan(new PlacementArea(Vector3.zero, new Vector3(3f, 1f, 3f)));
            var definition = CreateDefinition(radius: 0.5f);

            var result = plan.TryPlaceAtCell(definition, 0, out var entry);

            Assert.That(result, Is.EqualTo(PlacementResult.Success), "칸(1×1)에 지름(1.0)이 딱 맞는 유닛은 거절되면 안 된다.");
            Assert.That(entry.IsValid, Is.True);
        }

        [Test]
        public void MovingIntoACellTooSmallForTheUnitIsRejected()
        {
            var plan = new UnitPlacementPlan(new PlacementArea(Vector3.zero, new Vector3(9f, 1f, 9f)));
            var definition = CreateDefinition(radius: 0.6f);
            Assert.That(
                plan.TryPlaceAtCell(definition, 0, out var entry),
                Is.EqualTo(PlacementResult.Success),
                "무대 확인: 넉넉한 구역(칸 3×3)에서는 배치가 먼저 성공해야 한다.");

            plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(3f, 1f, 3f)));
            var result = plan.TryMoveToCell(entry.Id, 1, out var movedEntry);

            Assert.That(result, Is.EqualTo(PlacementResult.CellTooSmall), "구역이 좁아져 칸(1×1)이 지름(1.2)보다 작아지면 옮기지 못해야 한다.");
            Assert.That(movedEntry.IsValid, Is.False);
            Assert.That(plan.TryGetEntry(entry.Id, out var stored), Is.True);
            Assert.That(stored.CellIndex, Is.EqualTo(entry.CellIndex), "실패한 이동은 원래 칸을 그대로 둬야 한다.");
        }

        private UnitDefinition CreateDefinition(float radius)
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100, radius: radius);
            _createdDefinitions.Add(definition);
            return definition;
        }
    }
}
