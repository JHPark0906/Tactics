using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>에셋에 적힌 설명이 실제로 도는 자리를 만들어 내는지, 만들 수 없을 때 그 자리를 비우는지 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// 자리마다 두 가지를 본다. 유닛에 필요한 것이 붙어 있으면 그 타입의 자리가 나오고, 없으면
    /// 자리가 트리에서 빠진다. 빠지는 쪽이 조용히 다른 것으로 채워지면 트리가 왜 그렇게 도는지
    /// 알 수 없다.
    /// </para>
    /// <para>
    /// 설명의 값은 직렬화 필드라 코드로 여는 통로가 없다. 검사를 위해 공개 통로를 내지 않고 편집기가
    /// 하는 일을 흉내 내되, <b>필드가 없으면 검사가 붉어진다.</b> 이름이 바뀌었는데 조용히 아무것도 안 하면
    /// 검사가 없는 것과 같다.
    /// </para>
    /// </remarks>
    public sealed class BehaviourNodeDefinitionTests
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
        public void MoveToPositionNeedsAMoverOnTheOwner()
        {
            var definition = Configure(new MoveToPositionDefinition(), ("destinationKey", "destination"));

            AssertCreates<MoveToPositionBehaviour>(definition, OwnerWith<FakeMover>());
            AssertSkips(definition, OwnerWith());
            AssertSkips(definition, null);
        }

        [Test]
        public void MoveToPositionWithoutAKeyIsSkipped()
        {
            AssertSkips(new MoveToPositionDefinition(), OwnerWith<FakeMover>());
        }

        [Test]
        public void ChaseTargetNeedsAMoverOnTheOwner()
        {
            var definition = Configure(new ChaseTargetDefinition(), ("targetKey", "target"));

            AssertCreates<ChaseTargetBehaviour>(definition, OwnerWith<FakeMover>());
            AssertSkips(definition, OwnerWith());
        }

        [Test]
        public void ChaseTargetWithoutAKeyIsSkipped()
        {
            AssertSkips(new ChaseTargetDefinition(), OwnerWith<FakeMover>());
        }

        [Test]
        public void WaitNeedsNothingFromTheOwner()
        {
            AssertCreates<WaitBehaviour>(new WaitDefinition(), null);
        }

        [Test]
        public void DecoratorsNeedNothingFromTheOwner()
        {
            AssertCreates<InverterBehaviour>(new InverterDefinition(), null);
            AssertCreates<SucceederBehaviour>(new SucceederDefinition(), null);
            AssertCreates<RepeaterBehaviour>(new RepeaterDefinition(), null);
            AssertCreates<CooldownBehaviour>(new CooldownDefinition(), null);
        }

        [Test]
        public void ParallelNeedsNothingFromTheOwner()
        {
            AssertCreates<ParallelBehaviour>(new ParallelDefinition(), null);
        }

        /// <remarks>
        /// 시야 판정은 노드가 스스로 계산하므로 유닛에 감지 구성요소가 없어도 선다. 필요한 것은
        /// 유닛 자신(관찰자 자리)과 대상 키뿐이다.
        /// </remarks>
        [Test]
        public void CanSeeTargetNeedsOnlyAnOwner()
        {
            var definition = Configure(new CanSeeTargetDefinition(), ("targetKey", "target"));

            AssertCreates<CanSeeTargetBehaviour>(definition, OwnerWith());
            AssertSkips(definition, null);
        }

        [Test]
        public void CanSeeTargetWithoutAKeyIsSkipped()
        {
            AssertSkips(new CanSeeTargetDefinition(), OwnerWith());
        }

        /// <remarks>
        /// 청취 판정은 노드가 스스로 계산하므로 유닛에 감지 구성요소가 없어도 선다. 필요한 것은
        /// 유닛 자신(듣는 쪽의 자리)과 자극 키뿐이다.
        /// </remarks>
        [Test]
        public void HeardSoundNeedsOnlyAnOwner()
        {
            var definition = Configure(new HeardSoundDefinition(), ("stimulusKey", "stimulus"));

            AssertCreates<HeardSoundBehaviour>(definition, OwnerWith());
            AssertSkips(definition, null);
        }

        [Test]
        public void HeardSoundWithoutAKeyIsSkipped()
        {
            AssertSkips(new HeardSoundDefinition(), OwnerWith());
        }

        [Test]
        public void EveryDefinitionHasADisplayName()
        {
            var definitions = new BehaviourNodeDefinition[]
            {
                new MoveToPositionDefinition(), new ChaseTargetDefinition(), new WaitDefinition(),
                new InverterDefinition(), new SucceederDefinition(), new RepeaterDefinition(),
                new CooldownDefinition(), new ParallelDefinition(),
                new CanSeeTargetDefinition(), new HeardSoundDefinition()
            };

            foreach (var definition in definitions)
            {
                Assert.That(definition.DisplayName, Is.Not.Empty, $"{definition.GetType().Name}의 이름이 비어 있다.");
            }
        }

        /// <summary>편집기가 채워 줄 직렬화 필드를 검사에서 채운다.</summary>
        /// <typeparam name="TDefinition">채울 설명의 타입이다.</typeparam>
        /// <param name="definition">채울 설명이다.</param>
        /// <param name="values">필드 이름과 값의 쌍이다.</param>
        /// <returns>채운 설명 그대로이다.</returns>
        private static TDefinition Configure<TDefinition>(
            TDefinition definition,
            params (string Field, object Value)[] values)
            where TDefinition : BehaviourNodeDefinition
        {
            foreach (var (field, value) in values)
            {
                var info = typeof(TDefinition).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(
                    info,
                    Is.Not.Null,
                    $"{typeof(TDefinition).Name}에 '{field}' 필드가 없다. 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
                info.SetValue(definition, value);
            }

            return definition;
        }

        /// <summary>그 설명을 뿌리 아래 자식으로 넣어 만들었을 때 나온 자리를 돌려준다.</summary>
        /// <param name="definition">만들어 볼 설명이다.</param>
        /// <param name="owner">트리를 쓸 유닛이며 없으면 null이다.</param>
        /// <returns>만들어진 자리이며, 설명이 자리를 비웠으면 null이다.</returns>
        private IBehaviour CreateChild(BehaviourNodeDefinition definition, GameObject owner)
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            _created.Add(asset);
            SetPrivate(asset, "nodes", new List<BehaviourNodeDefinition> { new SequenceDefinition(), definition });
            SetPrivate(asset, "parents", new List<int> { BehaviourTreeAsset.NoParent, 0 });

            var tree = asset.CreateRuntimeTree(new BehaviourBuildContext(owner, new BehaviourContext()));
            return tree.Root.Children.Count == 0 ? null : tree.Root.Children[0].Value;
        }

        private void AssertCreates<TBehaviour>(BehaviourNodeDefinition definition, GameObject owner)
            where TBehaviour : IBehaviour
        {
            Assert.That(
                CreateChild(definition, owner),
                Is.TypeOf<TBehaviour>(),
                $"{definition.DisplayName}에서 {typeof(TBehaviour).Name}이 나와야 한다.");
        }

        private void AssertSkips(BehaviourNodeDefinition definition, GameObject owner)
        {
            Assert.That(
                CreateChild(definition, owner),
                Is.Null,
                $"{definition.DisplayName}은 만들 수 없을 때 자리를 비워야 한다.");
        }

        private GameObject OwnerWith()
        {
            var owner = new GameObject("Owner");
            _created.Add(owner);
            return owner;
        }

        private GameObject OwnerWith<TComponent>() where TComponent : Component
        {
            var owner = OwnerWith();
            owner.AddComponent<TComponent>();
            return owner;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"{target.GetType().Name}에 '{field}' 필드가 없다.");
            info.SetValue(target, value);
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
