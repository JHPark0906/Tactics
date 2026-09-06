using System.Collections.Generic;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 시야·청취 문이 유닛에서 감지 구성요소를 찾지 않고 스스로 판정하는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <see cref="FieldOfViewSensor"/>의 각도·거리·가림 계산 자체는
    /// <c>FieldOfViewOcclusionCharacterizationTests</c>가 지킨다. 여기서는 <see cref="CanSeeTargetBehaviour"/>와
    /// <see cref="HeardSoundBehaviour"/>가 문맥 키를 읽고 아래를 여닫는 규칙만 본다.
    /// </remarks>
    public sealed class PerceptionGateBehaviourTests
    {
        private const string TargetKey = "target";
        private const string StimulusKey = "stimulus";

        private readonly List<GameObject> _createdObjects = new();

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
        public void CanSeeTargetPassesWhenTheTargetIsWithinTheSensorsView()
        {
            var observer = CreateTransform(Vector3.zero, Vector3.forward);
            var target = CreateTransform(new Vector3(0f, 0f, 5f), Vector3.back);
            var context = new BehaviourContext();
            context.SetValue(TargetKey, target);
            var sensor = new FieldOfViewSensor(90f, 10f, 0);
            var tree = BuildGate(new CanSeeTargetBehaviour(sensor, observer, TargetKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void CanSeeTargetBlocksWhenTheTargetIsBeyondTheSensorsRange()
        {
            var observer = CreateTransform(Vector3.zero, Vector3.forward);
            var target = CreateTransform(new Vector3(0f, 0f, 50f), Vector3.back);
            var context = new BehaviourContext();
            context.SetValue(TargetKey, target);
            var sensor = new FieldOfViewSensor(90f, 10f, 0);
            var tree = BuildGate(new CanSeeTargetBehaviour(sensor, observer, TargetKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void CanSeeTargetBlocksWhenTheContextHasNoTarget()
        {
            var observer = CreateTransform(Vector3.zero, Vector3.forward);
            var sensor = new FieldOfViewSensor(90f, 10f, 0);
            var tree = BuildGate(new CanSeeTargetBehaviour(sensor, observer, TargetKey));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure),
                "아무도 유닛에서 시야를 찾아 담아 준 적이 없어도, 대상이 없으면 그대로 막는다.");
        }

        [Test]
        public void HeardSoundPassesAndLeavesTheStimulusWhenWithinRangeAndNotConsumed()
        {
            var self = CreateTransform(Vector3.zero, Vector3.forward);
            var context = new BehaviourContext();
            context.SetValue(StimulusKey, new SoundStimulus(new Vector3(0f, 0f, 8f), 15f));
            var tree = BuildGate(new HeardSoundBehaviour(self, 10f, StimulusKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(context.TryGetValue<SoundStimulus>(StimulusKey, out _), Is.True,
                "지우라고 하지 않았으면 아래가 계속 그 자극을 쓸 수 있어야 한다.");
        }

        [Test]
        public void HeardSoundClearsTheStimulusWhenConsumeOnSuccessIsSet()
        {
            var self = CreateTransform(Vector3.zero, Vector3.forward);
            var context = new BehaviourContext();
            context.SetValue(StimulusKey, new SoundStimulus(new Vector3(0f, 0f, 8f), 15f));
            var tree = BuildGate(new HeardSoundBehaviour(self, 10f, StimulusKey, consumeOnSuccess: true));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(context.TryGetValue<SoundStimulus>(StimulusKey, out _), Is.False,
                "지우라고 했으면 같은 소리로 두 번 통과하지 않아야 한다.");
        }

        [Test]
        public void HeardSoundBlocksWhenTheDistanceExceedsTheEffectiveRange()
        {
            // 유닛의 청취 반경(10)과 소리 자체의 반경(4) 가운데 더 좁은 쪽이 실제로 들리는 한계다.
            var self = CreateTransform(Vector3.zero, Vector3.forward);
            var context = new BehaviourContext();
            context.SetValue(StimulusKey, new SoundStimulus(new Vector3(0f, 0f, 6f), 4f));
            var tree = BuildGate(new HeardSoundBehaviour(self, 10f, StimulusKey));

            Assert.That(tree.Tick(context), Is.EqualTo(BehaviourStatus.Failure));
        }

        [Test]
        public void HeardSoundBlocksWhenTheContextHasNoStimulus()
        {
            var self = CreateTransform(Vector3.zero, Vector3.forward);
            var tree = BuildGate(new HeardSoundBehaviour(self, 10f, StimulusKey));

            Assert.That(tree.Tick(new BehaviourContext()), Is.EqualTo(BehaviourStatus.Failure),
                "아무도 소리를 알린 적이 없어도, 자극이 없으면 그대로 막는다.");
        }

        /// <summary>그 문 아래에 자식 하나를 붙인 트리를 만든다. 자식이 없으면 문이 언제나 실패한다.</summary>
        private static BehaviourTreeInstance BuildGate(IBehaviour gate)
        {
            var tree = new BehaviourTreeInstance();
            var root = tree.SetRoot(gate);
            tree.AddChild(root, new ActionBehaviour(_ => BehaviourStatus.Success));
            return tree;
        }

        private Transform CreateTransform(Vector3 position, Vector3 forward)
        {
            var created = new GameObject("Transform");
            _createdObjects.Add(created);
            created.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
            return created.transform;
        }
    }
}
