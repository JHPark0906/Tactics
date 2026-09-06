using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>정해진 결과, 돌아보기, 곧장 걷기, 소리 틀기 잎들을 고정한다.</summary>
    public sealed class LeafTaskBehaviourTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void FinishWithResultReturnsWhatItWasGiven()
        {
            var context = new BehaviourContext();

            Assert.That(new FinishWithResultBehaviour(BehaviourStatus.Success).Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(new FinishWithResultBehaviour(BehaviourStatus.Failure).Tick(context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(new FinishWithResultBehaviour(BehaviourStatus.Running).Tick(context), Is.EqualTo(BehaviourStatus.Running));
        }

        [Test]
        public void RotateToFaceTurnsAtItsSpeedUntilItFaces()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            context.SetValue("target", new Vector3(5f, 0f, 0f));
            var rotate = new RotateToFaceBehaviour(self, "target", precisionDegrees: 5f, degreesPerSecond: 90f, deltaTimeProvider: () => 0.5f);

            Assert.That(rotate.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(Vector3.Angle(self.forward, Vector3.right), Is.EqualTo(45f).Within(0.01f), "한 실행에 속도만큼만 돈다.");

            Assert.That(rotate.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(Vector3.Angle(self.forward, Vector3.right), Is.LessThanOrEqualTo(0.01f));
        }

        [Test]
        public void RotateToFaceTurnsAtOnceWhenTheSpeedIsZeroAndIgnoresHeight()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            context.SetValue("target", new Vector3(0f, 10f, -5f));
            var rotate = new RotateToFaceBehaviour(self, "target", degreesPerSecond: 0f);

            Assert.That(rotate.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(Vector3.Angle(self.forward, Vector3.back), Is.LessThanOrEqualTo(0.01f), "바닥 위의 유닛은 수평으로만 돈다.");
        }

        [Test]
        public void RotateToFaceFailsWithoutATargetOrWhenAlreadyOnIt()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            var rotate = new RotateToFaceBehaviour(self, "target");

            Assert.That(rotate.Tick(context), Is.EqualTo(BehaviourStatus.Failure));

            context.SetValue("target", self.position);
            Assert.That(rotate.Tick(context), Is.EqualTo(BehaviourStatus.Failure), "같은 자리에는 돌아볼 쪽이 없다.");
        }

        [Test]
        public void MoveDirectlyTowardStepsStraightAtTheTargetAndArrives()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            context.SetValue("target", new Vector3(0f, 0f, 10f));
            var move = new MoveDirectlyTowardBehaviour(self, "target", speed: 4f, acceptableRadius: 0.1f, deltaTimeProvider: () => 1f);

            Assert.That(move.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(self.position, Is.EqualTo(new Vector3(0f, 0f, 4f)));
            Assert.That(move.Tick(context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(move.Tick(context), Is.EqualTo(BehaviourStatus.Success), "남은 거리보다 한 걸음이 크면 목표에 놓고 끝낸다.");
            Assert.That(self.position, Is.EqualTo(new Vector3(0f, 0f, 10f)));
        }

        [Test]
        public void MoveDirectlyTowardSucceedsWithoutMovingWhenAlreadyClose()
        {
            var self = Track(new GameObject("Self")).transform;
            var context = new BehaviourContext();
            context.SetValue("target", new Vector3(0f, 0f, 0.3f));
            var move = new MoveDirectlyTowardBehaviour(self, "target", speed: 4f, acceptableRadius: 0.5f, deltaTimeProvider: () => 1f);

            Assert.That(move.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(self.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void MoveDirectlyTowardFailsWithoutATarget()
        {
            var self = Track(new GameObject("Self")).transform;

            Assert.That(new MoveDirectlyTowardBehaviour(self, "target", 4f).Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void TheDefinitionsNeedTheirKeysAndTheOwner()
        {
            var noOwner = new BehaviourBuildContext(null, new BehaviourContext());
            var ownerObject = Track(new GameObject("Owner"));
            var owner = new BehaviourBuildContext(ownerObject, new BehaviourContext());

            Assert.That(new FinishWithResultDefinition().CreateBehaviour(noOwner), Is.TypeOf<FinishWithResultBehaviour>());

            var rotate = Configure(new RotateToFaceDefinition(), ("targetKey", "t"));
            Assert.That(new RotateToFaceDefinition().CreateBehaviour(owner), Is.Null);
            Assert.That(rotate.CreateBehaviour(noOwner), Is.Null);
            Assert.That(rotate.CreateBehaviour(owner), Is.TypeOf<RotateToFaceBehaviour>());

            var move = Configure(new MoveDirectlyTowardDefinition(), ("targetKey", "t"));
            Assert.That(move.CreateBehaviour(noOwner), Is.Null);
            Assert.That(move.CreateBehaviour(owner), Is.TypeOf<MoveDirectlyTowardBehaviour>());

            Assert.That(new PlaySoundDefinition().CreateBehaviour(owner), Is.Null, "틀 소리가 없으면 만들지 않는다.");

            foreach (var definition in new BehaviourNodeDefinition[]
                     {
                         new FinishWithResultDefinition(), new RotateToFaceDefinition(), new MoveDirectlyTowardDefinition(),
                         new PlaySoundDefinition()
                     })
            {
                Assert.That(definition.DisplayName, Is.Not.Empty, $"{definition.GetType().Name}의 이름이 비어 있다.");
            }
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static TDefinition Configure<TDefinition>(TDefinition definition, params (string Field, object Value)[] values)
            where TDefinition : BehaviourNodeDefinition
        {
            foreach (var (field, value) in values)
            {
                var info = typeof(TDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(info, Is.Not.Null, $"{typeof(TDefinition).Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
                info.SetValue(definition, value);
            }

            return definition;
        }
    }
}
