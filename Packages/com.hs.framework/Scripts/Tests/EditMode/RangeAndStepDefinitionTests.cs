using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>거리 표식 서비스, 거리 안 멈춤, 방향 걸음의 설명이 자리를 만들어 내는지, 못 만들 때 비우는지 고정한다.</summary>
    public sealed class RangeAndStepDefinitionTests
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
        public void TargetInRangeServiceNeedsBothKeysAndTheOwner()
        {
            var noOwner = new BehaviourBuildContext(null, new BehaviourContext());
            var owner = new BehaviourBuildContext(Track(new GameObject("Owner")), new BehaviourContext());
            var definition = Configure(new TargetInRangeServiceDefinition(), ("targetKey", "t"), ("inRangeKey", "r"));

            Assert.That(definition.CreateBehaviour(owner), Is.TypeOf<TargetInRangeService>());
            Assert.That(definition.CreateBehaviour(noOwner), Is.Null, "거리를 잴 기준이 없다.");
            Assert.That(Configure(new TargetInRangeServiceDefinition(), ("targetKey", "t")).CreateBehaviour(owner), Is.Null);
            Assert.That(new TargetInRangeServiceDefinition().CreateBehaviour(owner), Is.Null);
        }

        [Test]
        public void HoldWithinRangeNeedsAKeyAndAMoverOnTheOwner()
        {
            var noOwner = new BehaviourBuildContext(null, new BehaviourContext());
            var bare = new BehaviourBuildContext(Track(new GameObject("Bare")), new BehaviourContext());
            var walker = Track(new GameObject("Walker"));
            walker.AddComponent<FakeMover>();
            var moving = new BehaviourBuildContext(walker, new BehaviourContext());
            var definition = Configure(new HoldWithinRangeDefinition(), ("targetKey", "t"));

            Assert.That(definition.CreateBehaviour(moving), Is.TypeOf<HoldWithinRangeBehaviour>());
            Assert.That(definition.CreateBehaviour(bare), Is.Null, "멈추게 할 이동 수단이 없으면 자리를 만들 수 없다.");
            Assert.That(definition.CreateBehaviour(noOwner), Is.Null);
            Assert.That(new HoldWithinRangeDefinition().CreateBehaviour(moving), Is.Null);
        }

        [Test]
        public void StepAlongDirectionNeedsBothKeysAndTheOwner()
        {
            var noOwner = new BehaviourBuildContext(null, new BehaviourContext());
            var owner = new BehaviourBuildContext(Track(new GameObject("Owner")), new BehaviourContext());
            var definition = Configure(new StepAlongDirectionDefinition(), ("destinationKey", "d"), ("directionKey", "a"));

            Assert.That(definition.CreateBehaviour(owner), Is.TypeOf<StepAlongDirectionBehaviour>());
            Assert.That(definition.CreateBehaviour(noOwner), Is.Null);
            Assert.That(Configure(new StepAlongDirectionDefinition(), ("destinationKey", "d")).CreateBehaviour(owner), Is.Null);
        }

        [Test]
        public void EveryDefinitionHasADisplayName()
        {
            foreach (var definition in new BehaviourNodeDefinition[]
                     {
                         new TargetInRangeServiceDefinition(), new HoldWithinRangeDefinition(), new StepAlongDirectionDefinition()
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

        private sealed class FakeMover : MonoBehaviour, ICharacterMover
        {
            public bool HasReachedDestination => true;

            public bool MoveTo(Vector3 destination) => true;

            public void Stop()
            {
            }
        }
    }
}
