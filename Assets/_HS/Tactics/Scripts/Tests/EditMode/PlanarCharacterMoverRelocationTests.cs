using System.Collections.Generic;
using System.Reflection;
using HS.Tactics.Character.Movement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    public sealed class PlanarCharacterMoverRelocationTests
    {
        private const float Tolerance = 0.00001f;
        private GameObject _unit;
        private PlanarCharacterMover _mover;
        private Transform _visual;
        private FixedStepMotionInterpolator _interpolator;
        private readonly Vector3 _visualOffset = new(0f, 0.7f, 0.2f);

        [SetUp]
        public void SetUp()
        {
            _unit = new GameObject("Relocated unit");
            _mover = _unit.AddComponent<PlanarCharacterMover>();
            _mover.enabled = false;
            _mover.SetPathPlanner(StraightLine);
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(_unit.transform, false);
            _visual.localPosition = _visualOffset;
            _interpolator = _unit.AddComponent<FixedStepMotionInterpolator>();
            typeof(FixedStepMotionInterpolator).GetField("visualRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_interpolator, _visual);
            InvokeInterpolator("Awake");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_unit);

        [Test]
        public void InitialPlacementBeforeInitializationSetsTheFirstLogicPosition()
        {
            var position = new Vector3(3f, 2f, -4f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);

            _mover.Relocate(position, rotation);
            _mover.Initialize(null);
            _mover.Tick(0.02f);

            AssertPositionAndHistory(position);
            Assert.That(Quaternion.Angle(_unit.transform.rotation, rotation), Is.LessThan(Tolerance));
            Assert.That(_mover.HasReachedDestination, Is.True);
            AssertVisualPosition(position);
        }

        [Test]
        public void AnIdleRelocationKeepsTheNewPositionAcrossLogicAndVisualTicks()
        {
            _mover.Initialize(null);
            var position = new Vector3(0.5f, 0.2f, 0.3f);

            _mover.Relocate(position, Quaternion.identity);
            for (var tick = 0; tick < 3; tick++)
            {
                _mover.Tick(0.02f);
                AssertPositionAndHistory(position);
                AssertVisualPosition(position);
            }

            Assert.That(_mover.CurrentSpeed, Is.Zero);
            Assert.That(_mover.HasReachedDestination, Is.True);
        }

        [Test]
        public void VisualFramesLeaveMotionUnchangedUntilTheNextFixedUpdate()
        {
            _mover.Initialize(null);
            Assert.That(_mover.MoveTo(new Vector3(10f, 0f, 10f)), Is.True);

            AssertVisualFramesDoNotChangeMotion();
            AssertPositionAndHistory(Vector3.zero);
            Assert.That(_mover.CurrentSpeed, Is.Zero);

            typeof(PlanarCharacterMover).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_mover, null);

            Assert.That(_mover.CurrentLogicPosition.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(_mover.CurrentSpeed, Is.GreaterThan(0f));
            AssertVisualFramesDoNotChangeMotion();
        }

        [Test]
        public void RelocatingDuringMovementReplansToTheSamePlanarGoalAtTheNewHeightAndKeepsSpeed()
        {
            var starts = new List<Vector3>();
            var goals = new List<Vector3>();
            _mover.SetPathPlanner((from, to, corners) =>
            {
                starts.Add(from);
                goals.Add(to);
                return StraightLine(from, to, corners);
            });
            _mover.Initialize(null);
            var destination = new Vector3(0f, 0f, 10f);
            Assert.That(_mover.MoveTo(destination), Is.True);
            _mover.Tick(0.1f);
            var speed = _mover.CurrentSpeed;
            Assert.That(speed, Is.GreaterThan(0f));
            var position = new Vector3(1f, 2f, 1f);

            _mover.Relocate(position, Quaternion.identity);

            AssertPositionAndHistory(position);
            AssertVisualPosition(position);
            Assert.That(_mover.CurrentSpeed, Is.EqualTo(speed));
            Assert.That(starts, Has.Count.EqualTo(2));
            Assert.That(starts[1], Is.EqualTo(position));
            Assert.That(goals[1], Is.EqualTo(new Vector3(destination.x, position.y, destination.z)));
            Assert.That(_mover.HasReachedDestination, Is.False);

            _mover.Tick(0.1f);

            Assert.That(_mover.PreviousLogicPosition, Is.EqualTo(position));
            Assert.That(_mover.CurrentLogicPosition.y, Is.EqualTo(position.y));
            Assert.That(_mover.CurrentLogicPosition.x, Is.LessThan(position.x));
            Assert.That(_mover.CurrentLogicPosition.z, Is.GreaterThan(position.z));
        }

        [Test]
        public void AnUnreachableRelocationDiscardsTheOldRouteAndDeceleratesUntilANewRequest()
        {
            _mover.Initialize(null);
            var destination = Vector3.forward * 10f;
            Assert.That(_mover.MoveTo(destination), Is.True);
            _mover.Tick(0.1f);
            var speed = _mover.CurrentSpeed;
            _mover.SetPathPlanner((_, _, _) => false);
            var position = new Vector3(3f, 4f, 2f);

            _mover.Relocate(position, Quaternion.identity);

            Assert.That(_mover.CurrentSpeed, Is.EqualTo(speed), "위치 변경 자체가 속도를 끊지는 않는다.");
            Assert.That(_mover.HasReachedDestination, Is.True, "새 경로가 없으면 이동 요청을 중단한다.");
            _mover.Tick(0.02f);
            AssertPositionAndHistory(position);
            Assert.That(_mover.CurrentSpeed, Is.LessThan(speed).And.GreaterThan(0f));
            AssertVisualPosition(position);

            _mover.SetPathPlanner(StraightLine);
            Assert.That(_mover.MoveTo(new Vector3(destination.x, position.y, destination.z)), Is.True);
            _mover.Tick(0.1f);
            Assert.That(_mover.CurrentLogicPosition.z, Is.GreaterThan(position.z));
            Assert.That(_mover.CurrentLogicPosition.y, Is.EqualTo(position.y));
        }

        private void AssertPositionAndHistory(Vector3 position)
        {
            Assert.That(_unit.transform.position, Is.EqualTo(position));
            Assert.That(_mover.CurrentLogicPosition, Is.EqualTo(position));
            Assert.That(_mover.PreviousLogicPosition, Is.EqualTo(position));
        }

        private void AssertVisualPosition(Vector3 position)
        {
            InvokeInterpolator("LateUpdate");
            var expected = position + _unit.transform.rotation * _visualOffset;
            Assert.That(Vector3.Distance(_visual.position, expected), Is.LessThan(Tolerance));
        }

        private void AssertVisualFramesDoNotChangeMotion()
        {
            var position = _unit.transform.position;
            var rotation = _unit.transform.rotation;
            var previous = _mover.PreviousLogicPosition;
            var current = _mover.CurrentLogicPosition;
            var speed = _mover.CurrentSpeed;
            for (var frame = 0; frame < 5; frame++)
            {
                InvokeInterpolator("LateUpdate");
                Assert.That(_unit.transform.position, Is.EqualTo(position));
                Assert.That(_unit.transform.rotation, Is.EqualTo(rotation));
                Assert.That(_mover.PreviousLogicPosition, Is.EqualTo(previous));
                Assert.That(_mover.CurrentLogicPosition, Is.EqualTo(current));
                Assert.That(_mover.CurrentSpeed, Is.EqualTo(speed));
            }
        }

        private void InvokeInterpolator(string name) => typeof(FixedStepMotionInterpolator)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_interpolator, null);

        private static bool StraightLine(Vector3 from, Vector3 to, List<Vector3> corners)
        {
            corners.Add(from);
            corners.Add(to);
            return true;
        }
    }
}
