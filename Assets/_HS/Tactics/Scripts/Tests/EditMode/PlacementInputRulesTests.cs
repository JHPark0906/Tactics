using System.Collections.Generic;
using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>포인터 좌표를 배치 요청으로 바꾸는 순수 규칙을 검증한다.</summary>
    public sealed class PlacementInputRulesTests
    {
        [Test]
        public void DownwardRayProjectsOntoTheGroundPlane()
        {
            var ray = new Ray(new Vector3(2f, 10f, -3f), Vector3.down);

            Assert.That(PlacementInputRules.TryProjectRayOntoPlane(ray, 0f, out var point), Is.True);
            Assert.That(point.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(point.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(point.z, Is.EqualTo(-3f).Within(0.0001f));
        }

        [Test]
        public void SlantedRayProjectsOntoTheRaisedGroundPlane()
        {
            var ray = new Ray(new Vector3(0f, 4f, 0f), new Vector3(1f, -1f, 0f).normalized);

            Assert.That(PlacementInputRules.TryProjectRayOntoPlane(ray, 2f, out var point), Is.True);
            Assert.That(point.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(point.y, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void RayParallelToThePlaneFails()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.forward);

            Assert.That(PlacementInputRules.TryProjectRayOntoPlane(ray, 0f, out _), Is.False);
        }

        [Test]
        public void RayPointingAwayFromThePlaneFails()
        {
            var ray = new Ray(new Vector3(0f, 5f, 0f), Vector3.up);

            Assert.That(PlacementInputRules.TryProjectRayOntoPlane(ray, 0f, out _), Is.False);
        }

        [Test]
        public void PickFindsTheNearestEntryWithinTheRadius()
        {
            // 여기서 재는 것은 반경 안에서 가장 가까운 것을 고르는 계산이지 격자가 아니다.
            // 항목마다 칸 번호를 달리 준 것은 서로 다른 유닛이라는 것을 지키기 위해서일 뿐,
            // 좌표는 반경을 시험하려고 고른 값이라 그 칸의 중심이 아니다.
            var entries = new List<PlacementEntry>
            {
                new(1, null, cellIndex: 0, new Vector3(0f, 0f, 0f)),
                new(2, null, cellIndex: 1, new Vector3(1.5f, 0f, 0f))
            };

            Assert.That(PlacementInputRules.TryPickEntry(entries, new Vector3(1.2f, 0f, 0f), 1f, out var entryId), Is.True);
            Assert.That(entryId, Is.EqualTo(2));
        }

        [Test]
        public void PickIgnoresHeightDifference()
        {
            var entries = new List<PlacementEntry> { new(7, null, cellIndex: 0, new Vector3(0f, 0f, 0f)) };

            Assert.That(PlacementInputRules.TryPickEntry(entries, new Vector3(0f, 12f, 0f), 1f, out var entryId), Is.True);
            Assert.That(entryId, Is.EqualTo(7));
        }

        [Test]
        public void PickFailsOutsideTheRadius()
        {
            var entries = new List<PlacementEntry> { new(1, null, cellIndex: 0, Vector3.zero) };

            Assert.That(PlacementInputRules.TryPickEntry(entries, new Vector3(5f, 0f, 0f), 1f, out var entryId), Is.False);
            Assert.That(entryId, Is.Zero);
        }

        [Test]
        public void PickFailsOnEmptyOrInvalidInput()
        {
            Assert.That(PlacementInputRules.TryPickEntry(null, Vector3.zero, 1f, out _), Is.False);
            Assert.That(PlacementInputRules.TryPickEntry(new List<PlacementEntry>(), Vector3.zero, 1f, out _), Is.False);
            Assert.That(
                PlacementInputRules.TryPickEntry(new List<PlacementEntry> { new(1, null, cellIndex: 0, Vector3.zero) }, Vector3.zero, 0f, out _),
                Is.False);
        }

        [Test]
        public void PrimaryMovesTheHeldEntryFirst()
        {
            var command = PlacementInputRules.ResolvePrimaryCommand(heldEntryId: 3, pickedEntryId: 5, hasSelectedDefinition: true);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.Move));
            Assert.That(command.EntryId, Is.EqualTo(3));
        }

        [Test]
        public void PrimaryPicksUpTheClickedEntry()
        {
            var command = PlacementInputRules.ResolvePrimaryCommand(heldEntryId: 0, pickedEntryId: 5, hasSelectedDefinition: true);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.BeginMove));
            Assert.That(command.EntryId, Is.EqualTo(5));
        }

        [Test]
        public void PrimaryPlacesOnEmptyGroundWhenAUnitIsSelected()
        {
            var command = PlacementInputRules.ResolvePrimaryCommand(heldEntryId: 0, pickedEntryId: 0, hasSelectedDefinition: true);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.Place));
            Assert.That(command.EntryId, Is.Zero);
        }

        [Test]
        public void PrimaryDoesNothingOnEmptyGroundWithoutASelection()
        {
            var command = PlacementInputRules.ResolvePrimaryCommand(heldEntryId: 0, pickedEntryId: 0, hasSelectedDefinition: false);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.None));
        }

        [Test]
        public void SecondaryCancelsTheHeldMoveFirst()
        {
            var command = PlacementInputRules.ResolveSecondaryCommand(heldEntryId: 3, pickedEntryId: 5);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.CancelMove));
            Assert.That(command.EntryId, Is.EqualTo(3));
        }

        [Test]
        public void SecondaryRecallsTheClickedEntry()
        {
            var command = PlacementInputRules.ResolveSecondaryCommand(heldEntryId: 0, pickedEntryId: 5);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.Recall));
            Assert.That(command.EntryId, Is.EqualTo(5));
        }

        [Test]
        public void SecondaryDoesNothingOnEmptyGround()
        {
            var command = PlacementInputRules.ResolveSecondaryCommand(heldEntryId: 0, pickedEntryId: 0);

            Assert.That(command.Kind, Is.EqualTo(PlacementInputActionKind.None));
        }
    }
}
