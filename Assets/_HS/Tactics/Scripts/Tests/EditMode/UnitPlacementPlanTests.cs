using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>배치 계획의 상한, 구역 판정, 이동과 회수 규칙을 검증한다.</summary>
    public sealed class UnitPlacementPlanTests
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
        public void PlacingInsideTheZoneAddsAnEntry()
        {
            var plan = CreatePlan(capacity: 3);
            var definition = CreateDefinition();

            var result = plan.TryPlace(definition, new Vector3(1f, 0f, 1f), out var entry);

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(entry.IsValid, Is.True);
            Assert.That(entry.Definition, Is.SameAs(definition));
            Assert.That(plan.Count, Is.EqualTo(1));
            Assert.That(plan.RemainingCapacity, Is.EqualTo(2));
        }

        [Test]
        public void PlacingOutsideTheZoneIsRejected()
        {
            var plan = CreatePlan();

            var result = plan.TryPlace(CreateDefinition(), new Vector3(100f, 0f, 0f), out _);

            Assert.That(result, Is.EqualTo(PlacementResult.OutsidePlacementZone));
            Assert.That(plan.Count, Is.Zero);
        }

        [Test]
        public void PlacingWithoutDefinitionIsRejected()
        {
            var plan = CreatePlan();

            Assert.That(plan.TryPlace(null, Vector3.zero, out _), Is.EqualTo(PlacementResult.MissingDefinition));
        }

        [Test]
        public void PlacingWithoutZoneIsRejected()
        {
            var plan = new UnitPlacementPlan(PlacementArea.None);

            Assert.That(
                plan.TryPlace(CreateDefinition(), Vector3.zero, out _),
                Is.EqualTo(PlacementResult.NoPlacementZone));
        }

        [Test]
        public void PlacingBeyondTheFixedCapacityIsRejected()
        {
            var plan = CreatePlan(capacity: 2);
            var definition = CreateDefinition();

            // 한 칸에는 한 기만 서므로 상한을 시험하려면 서로 다른 칸에 놓아야 한다.
            Assert.That(
                plan.TryPlace(definition, new Vector3(-3f, 0f, -3f), out _),
                Is.EqualTo(PlacementResult.Success));
            Assert.That(
                plan.TryPlace(definition, Vector3.zero, out _),
                Is.EqualTo(PlacementResult.Success));
            Assert.That(plan.IsFull, Is.True);

            Assert.That(
                plan.TryPlace(definition, new Vector3(3f, 0f, 3f), out _),
                Is.EqualTo(PlacementResult.CapacityReached));
            Assert.That(plan.Count, Is.EqualTo(2));
        }

        [Test]
        public void RecallingFreesCapacityForAnotherUnit()
        {
            var plan = CreatePlan(capacity: 1);
            var definition = CreateDefinition();
            plan.TryPlace(definition, Vector3.zero, out var entry);

            Assert.That(plan.TryRecall(entry.Id, out var removed), Is.EqualTo(PlacementResult.Success));
            Assert.That(removed.Id, Is.EqualTo(entry.Id));
            Assert.That(plan.Count, Is.Zero);
            Assert.That(
                plan.TryPlace(definition, Vector3.zero, out _),
                Is.EqualTo(PlacementResult.Success),
                "회수한 자리는 즉시 다시 채울 수 있어야 한다.");
        }

        [Test]
        public void MovingWithinTheZoneUpdatesTheCellWithoutSpendingCapacity()
        {
            var plan = CreatePlan(capacity: 1);
            plan.TryPlace(CreateDefinition(), Vector3.zero, out var entry);
            var destination = new Vector3(-3f, 0f, -3f);

            var result = plan.TryMove(entry.Id, destination, out var movedEntry);

            Assert.That(result, Is.EqualTo(PlacementResult.Success));
            Assert.That(movedEntry.Id, Is.EqualTo(entry.Id));
            Assert.That(
                movedEntry.CellIndex,
                Is.Not.EqualTo(entry.CellIndex),
                "다른 칸을 가리켰는데 칸이 그대로다.");

            // 격자에서는 유닛이 칸의 중심에 서므로, 가리킨 지점 그대로가 아니라 그 칸의 중심에 놓인다.
            Assert.That(plan.Grid.TryGetCellCenter(movedEntry.CellIndex, out var cellCenter), Is.True);
            Assert.That(movedEntry.Position, Is.EqualTo(cellCenter));
            Assert.That(plan.Count, Is.EqualTo(1), "이동은 배치 수를 소비하지 않는다.");
            Assert.That(plan.TryGetEntry(entry.Id, out var stored), Is.True);
            Assert.That(stored.Position, Is.EqualTo(cellCenter));
        }

        [Test]
        public void MovingOutsideTheZoneKeepsTheOriginalPosition()
        {
            var plan = CreatePlan();
            plan.TryPlace(CreateDefinition(), new Vector3(1f, 0f, 1f), out var entry);

            var result = plan.TryMove(entry.Id, new Vector3(100f, 0f, 0f), out _);

            Assert.That(result, Is.EqualTo(PlacementResult.OutsidePlacementZone));
            Assert.That(plan.TryGetEntry(entry.Id, out var stored), Is.True);
            Assert.That(stored.Position, Is.EqualTo(entry.Position));
            Assert.That(stored.CellIndex, Is.EqualTo(entry.CellIndex));
        }

        [Test]
        public void MovingOrRecallingAnUnknownEntryIsRejected()
        {
            var plan = CreatePlan();

            Assert.That(plan.TryMove(999, Vector3.zero, out _), Is.EqualTo(PlacementResult.UnknownEntry));
            Assert.That(plan.TryRecall(999, out _), Is.EqualTo(PlacementResult.UnknownEntry));
        }

        [Test]
        public void EntryIdentifiersStayUniqueAfterRecall()
        {
            var plan = CreatePlan(capacity: 2);
            var definition = CreateDefinition();
            plan.TryPlace(definition, Vector3.zero, out var first);
            plan.TryRecall(first.Id, out _);
            plan.TryPlace(definition, Vector3.zero, out var second);

            Assert.That(second.Id, Is.Not.EqualTo(first.Id), "회수한 식별자가 재사용되면 오래된 참조가 새 유닛을 가리킨다.");
            Assert.That(plan.TryGetEntry(first.Id, out _), Is.False);
        }

        [Test]
        public void CapacityIsAlwaysAtLeastOne()
        {
            var plan = CreatePlan(capacity: 0);

            Assert.That(plan.Capacity, Is.EqualTo(1));

            plan.SetCapacity(-5);
            Assert.That(plan.Capacity, Is.EqualTo(1));
        }

        [Test]
        public void UpdatingTheZoneAffectsLaterPlacementsOnly()
        {
            var plan = CreatePlan(capacity: 2);
            var definition = CreateDefinition();
            var farPosition = new Vector3(20f, 0f, 0f);
            Assert.That(plan.TryPlace(definition, farPosition, out _), Is.EqualTo(PlacementResult.OutsidePlacementZone));

            plan.SetArea(new PlacementArea(new Vector3(20f, 0f, 0f), new Vector3(4f, 2f, 4f)));

            Assert.That(plan.TryPlace(definition, farPosition, out _), Is.EqualTo(PlacementResult.Success));
        }

        private UnitPlacementPlan CreatePlan(int capacity = UnitPlacementPlan.DefaultCapacity)
        {
            return new UnitPlacementPlan(new PlacementArea(Vector3.zero, new Vector3(8f, 4f, 8f)), capacity);
        }

        private UnitDefinition CreateDefinition()
        {
            var definition = UnitDefinition.CreateRuntime("TestUnit", 100);
            _createdDefinitions.Add(definition);
            return definition;
        }
    }
}
