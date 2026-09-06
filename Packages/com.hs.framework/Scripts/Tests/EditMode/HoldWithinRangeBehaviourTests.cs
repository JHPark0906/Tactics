using HS.Framework.Tests.Support;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>거리 안에서 멈추는 잎의 멈춤 판정과 되돌리기를 고정한다.</summary>
    public sealed class HoldWithinRangeBehaviourTests
    {
        private const string TargetKey = "target";

        private GameObject _unitObject;
        private GameObject _targetObject;
        private BehaviourContext _context;
        private CountingMover _mover;

        [SetUp]
        public void SetUp()
        {
            _unitObject = new GameObject("Unit");
            _targetObject = new GameObject("Target");
            _unitObject.transform.position = Vector3.zero;
            _targetObject.transform.position = new Vector3(0f, 0f, 20f);
            _context = new BehaviourContext();
            _mover = new CountingMover();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_unitObject);
            Object.DestroyImmediate(_targetObject);
        }

        [Test]
        public void FailsWithoutATarget()
        {
            var node = CreateHoldNode(10f);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(_mover.StopCount, Is.Zero);
        }

        [Test]
        public void FailsWhenTheTargetIsOutOfRange()
        {
            _context.SetValue(TargetKey, _targetObject.transform);
            var node = CreateHoldNode(10f);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure), "20미터 떨어진 대상은 거리 밖이다.");
            Assert.That(_mover.StopCount, Is.Zero);
        }

        [Test]
        public void StopsTheMoverAndSucceedsWhenTheTargetIsInRange()
        {
            _targetObject.transform.position = new Vector3(0f, 0f, 5f);
            _context.SetValue(TargetKey, _targetObject.transform);
            var node = CreateHoldNode(10f);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(_mover.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void StopsOnlyOnceWhileItKeepsHolding()
        {
            _targetObject.transform.position = new Vector3(0f, 0f, 5f);
            _context.SetValue(TargetKey, _targetObject.transform);
            var node = CreateHoldNode(10f);

            node.Tick(_context);
            node.Tick(_context);
            node.Tick(_context);

            Assert.That(_mover.StopCount, Is.EqualTo(1), "멈춰 있는 동안 매 실행마다 멈추라고 지시할 필요가 없다.");
        }

        [Test]
        public void ResetMakesItStopAgainWhenReselected()
        {
            _targetObject.transform.position = new Vector3(0f, 0f, 5f);
            _context.SetValue(TargetKey, _targetObject.transform);
            var node = CreateHoldNode(10f);
            node.Tick(_context);

            node.Reset();
            node.Tick(_context);

            Assert.That(_mover.StopCount, Is.EqualTo(2), "가로채였다가 다시 고르면 멈춤을 다시 지시해야 한다.");
        }

        [Test]
        public void LeavingRangeClearsTheHoldSoReturningStopsAgain()
        {
            _targetObject.transform.position = new Vector3(0f, 0f, 5f);
            _context.SetValue(TargetKey, _targetObject.transform);
            var node = CreateHoldNode(10f);
            node.Tick(_context);

            _targetObject.transform.position = new Vector3(0f, 0f, 30f);
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));

            _targetObject.transform.position = new Vector3(0f, 0f, 5f);
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(_mover.StopCount, Is.EqualTo(2));
        }

        private HoldWithinRangeBehaviour CreateHoldNode(float holdDistance)
            => new(_unitObject.transform, _mover, TargetKey, holdDistance);

        private sealed class CountingMover : ICharacterMover
        {
            public bool HasReachedDestination { get; set; }

            public int StopCount { get; private set; }

            public bool MoveTo(Vector3 destination) => true;

            public void Stop() => StopCount++;
        }
    }
}
