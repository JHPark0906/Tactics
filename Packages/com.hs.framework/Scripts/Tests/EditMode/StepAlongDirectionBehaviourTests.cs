using HS.Framework.Tests.Support;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>방향을 따라 한 걸음 나아간 좌표를 적는 잎을 고정한다.</summary>
    public sealed class StepAlongDirectionBehaviourTests
    {
        private const string DestinationKey = "destination";
        private const string DirectionKey = "direction";

        private GameObject _unitObject;
        private BehaviourContext _context;

        [SetUp]
        public void SetUp()
        {
            _unitObject = new GameObject("Unit");
            _unitObject.transform.position = new Vector3(1f, 0f, 1f);
            _context = new BehaviourContext();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_unitObject);
        }

        [Test]
        public void StepsAlongTheDirectionInTheContext()
        {
            _context.SetValue(DirectionKey, Vector3.right);
            var step = new StepAlongDirectionBehaviour(_unitObject.transform, DestinationKey, DirectionKey, 3f);

            Assert.That(step.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(_context.TryGetValue<Vector3>(DestinationKey, out var destination), Is.True);
            Assert.That(destination, Is.EqualTo(new Vector3(4f, 0f, 1f)));
        }

        [Test]
        public void FallsBackToTheUnitsForwardWhenThereIsNoDirection()
        {
            _unitObject.transform.rotation = Quaternion.LookRotation(Vector3.back);
            var step = new StepAlongDirectionBehaviour(_unitObject.transform, DestinationKey, DirectionKey, 2f);

            Assert.That(step.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(_context.TryGetValue<Vector3>(DestinationKey, out var destination), Is.True);
            Assert.That(destination.z, Is.EqualTo(-1f).Within(1e-4f), "방향이 없으면 유닛이 바라보는 쪽으로 간다.");
        }

        [Test]
        public void NormalisesTheDirectionAndDropsItsHeight()
        {
            _context.SetValue(DirectionKey, new Vector3(0f, 10f, 2f));
            var step = new StepAlongDirectionBehaviour(_unitObject.transform, DestinationKey, DirectionKey, 5f);

            step.Tick(_context);

            Assert.That(_context.TryGetValue<Vector3>(DestinationKey, out var destination), Is.True);
            Assert.That(destination, Is.EqualTo(new Vector3(1f, 0f, 6f)), "길이는 걸음이 정하고 높이는 버린다.");
        }

        [Test]
        public void FailsWhenTheDirectionIsZero()
        {
            _context.SetValue(DirectionKey, Vector3.zero);
            var step = new StepAlongDirectionBehaviour(_unitObject.transform, DestinationKey, DirectionKey);

            Assert.That(step.Tick(_context), Is.EqualTo(BehaviourStatus.Failure), "갈 곳이 없으면 실패해야 위가 다른 길을 고른다.");
            Assert.That(_context.TryGetValue<Vector3>(DestinationKey, out _), Is.False);
        }

        [Test]
        public void AVerticalOnlyDirectionCountsAsNoDirection()
        {
            _context.SetValue(DirectionKey, Vector3.up);
            var step = new StepAlongDirectionBehaviour(_unitObject.transform, DestinationKey, DirectionKey);

            Assert.That(step.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));
        }
    }
}
