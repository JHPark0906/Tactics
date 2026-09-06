using System.Collections.Generic;
using System.Reflection;
using HS.Tactics.Character.Movement;
using HS.Tactics.Foundation.Geometry;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    public sealed class PlanarCharacterMoverCollisionProgressTests
    {
        private GameObject _unit;
        private PlanarCharacterMover _mover;

        [SetUp]
        public void SetUp()
        {
            _unit = new GameObject("Collision progress unit");
            _mover = _unit.AddComponent<PlanarCharacterMover>();
            _mover.enabled = false;
            _mover.Initialize(null);
            SetField("acceleration", 0f);
            SetField("stoppingDistance", 0.01f);
            _mover.SetObstacles(null, 0.5f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_unit);

        [TestCase(0.02f)]
        [TestCase(1f)]
        public void ABlockedFinalStepDoesNotArriveAndResumesWhenTheBlockerLeaves(float step)
        {
            _mover.SetSpeed(2f / step);
            _mover.SetPathPlanner(StraightLine);
            _mover.SetNearbyUnitsQuery((_, results) =>
            {
                results.Clear();
                results.Add(new PlanarCircle(new PlanarPosition(0f, 0.75f), 0.1f));
            });
            var destination = new Vector3(0f, 0f, 1f);
            Assert.That(_mover.MoveTo(destination), Is.True);

            _mover.Tick(step);

            Assert.That(_mover.CurrentLogicPosition.z, Is.LessThan(0.2f));
            Assert.That(_mover.HasReachedDestination, Is.False);
            _mover.SetNearbyUnitsQuery(null);
            Assert.That(_mover.MoveTo(destination), Is.True);
            _mover.Tick(step);
            Assert.That(_mover.HasReachedDestination, Is.True);
            Assert.That(_mover.CurrentLogicPosition, Is.EqualTo(destination));
        }

        [Test]
        public void ABlockedIntermediateCornerIsStillVisitedAfterTheBlockerLeaves()
        {
            _mover.SetSpeed(1f);
            var corner = new Vector3(0f, 0f, 1f);
            var destination = new Vector3(2f, 0f, 1f);
            _mover.SetPathPlanner((from, to, corners) =>
            {
                corners.Add(from);
                corners.Add(corner);
                corners.Add(to);
                return true;
            });
            _mover.SetNearbyUnitsQuery((_, results) =>
            {
                results.Clear();
                results.Add(new PlanarCircle(new PlanarPosition(0f, 0.75f), 0.1f));
            });
            Assert.That(_mover.MoveTo(destination), Is.True);
            _mover.Tick(1f);

            _mover.SetNearbyUnitsQuery(null);
            _mover.Tick(0.5f);
            Assert.That(_mover.CurrentLogicPosition.x, Is.Zero, "다음 모서리로 대각선 이동하면 안 된다.");
            Assert.That(_mover.CurrentLogicPosition.z, Is.GreaterThan(0.5f).And.LessThan(1f));
            _mover.Tick(3f);
            Assert.That(_mover.CurrentLogicPosition, Is.EqualTo(destination));
        }

        [Test]
        public void AStuckUnitReplansFromItsActualPosition()
        {
            var plannedStarts = new List<Vector3>();
            _mover.SetSpeed(2f);
            _mover.SetPathPlanner((from, to, corners) =>
            {
                plannedStarts.Add(from);
                return StraightLine(from, to, corners);
            });
            _mover.SetNearbyUnitsQuery((_, results) =>
            {
                results.Clear();
                results.Add(new PlanarCircle(new PlanarPosition(0f, 0.75f), 0.1f));
            });
            Assert.That(_mover.MoveTo(Vector3.forward), Is.True);
            for (var step = 0; step < 4; step++)
            {
                _mover.Tick(1f);
            }

            Assert.That(plannedStarts, Has.Count.EqualTo(2));
            Assert.That(plannedStarts[1].z, Is.GreaterThan(0f).And.LessThan(0.2f));
            Assert.That(plannedStarts[1], Is.EqualTo(_mover.CurrentLogicPosition));
            Assert.That(_mover.HasReachedDestination, Is.False);
        }

        [Test]
        public void ASingleStepChecksEachSegmentOfAnObstacleDetour()
        {
            _unit.transform.position = new Vector3(-2f, 0f, 0f);
            _mover.Initialize(null);
            _mover.SetSpeed(20f);
            _mover.SetObstacles(new[]
            {
                new PlanarRectangle(PlanarPosition.Zero, new PlanarPosition(0.5f, 0.5f), new PlanarPosition(0f, 1f))
            }, 0.5f);
            _mover.SetPathPlanner((from, to, corners) =>
            {
                corners.Add(from);
                corners.Add(new Vector3(-1.1f, 0f, 1.1f));
                corners.Add(new Vector3(1.1f, 0f, 1.1f));
                corners.Add(to);
                return true;
            });
            var destination = new Vector3(2f, 0f, 0f);
            Assert.That(_mover.MoveTo(destination), Is.True);

            _mover.Tick(1f);

            Assert.That(_mover.HasReachedDestination, Is.True);
            Assert.That(Vector3.Distance(_mover.CurrentLogicPosition, destination), Is.LessThan(0.00001f));
        }

        private static bool StraightLine(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            corners.Add(from);
            corners.Add(to);
            return true;
        }

        private void SetField(string name, float value) => typeof(PlanarCharacterMover)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_mover, value);
    }
}
