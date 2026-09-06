using System.Collections.Generic;
using System.Reflection;
using HS.Framework.AI.Behaviour;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Ability.BehaviourTree.Tests.EditMode
{
    /// <summary>어빌리티 활성화 설명이 자리를 만들어 내는지, 만들 수 없을 때 자리를 비우는지 고정한다.</summary>
    public sealed class ActivateAbilityDefinitionTests
    {
        private const string AbilityTagName = "Ability.TakeCover";

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
        public void AnOwnerWithAnAbilitySystemGetsTheBehaviour()
        {
            var definition = Configure(new ActivateAbilityDefinition(), AbilityTagName);
            var owner = OwnerWith<GameplayAbilitySystemComponent>();

            var behaviour = CreateChild(definition, owner);

            Assert.That(behaviour, Is.TypeOf<ActivateAbilityBehaviour>());
            Assert.That(
                ((ActivateAbilityBehaviour)behaviour).AbilityTag,
                Is.EqualTo(GameplayTag.Parse(AbilityTagName)),
                "에셋에 적힌 태그가 그대로 자리에 실려야 한다.");
        }

        [Test]
        public void AnOwnerWithoutAnAbilitySystemLeavesTheSlotEmpty()
        {
            var definition = Configure(new ActivateAbilityDefinition(), AbilityTagName);

            Assert.That(CreateChild(definition, OwnerWith()), Is.Null);
            Assert.That(CreateChild(definition, null), Is.Null);
        }

        [Test]
        public void AMalformedTagNameLeavesTheSlotEmptyInsteadOfThrowing()
        {
            // 실행 자리는 잘못된 태그에 예외를 던진다. 에셋의 오타 하나가 트리 전체를 만들다 말게 하면
            // 어느 자리가 문제인지 알 수 없으므로, 설명은 그 자리만 비운다.
            var definition = Configure(new ActivateAbilityDefinition(), ".not.a.tag.");
            var owner = OwnerWith<GameplayAbilitySystemComponent>();

            Assert.That(() => CreateChild(definition, owner), Throws.Nothing);
            Assert.That(CreateChild(definition, owner), Is.Null);
        }

        [Test]
        public void AnEmptyTagNameLeavesTheSlotEmpty()
        {
            var owner = OwnerWith<GameplayAbilitySystemComponent>();

            Assert.That(CreateChild(new ActivateAbilityDefinition(), owner), Is.Null);
        }

        private static ActivateAbilityDefinition Configure(ActivateAbilityDefinition definition, string tagName)
        {
            var info = typeof(ActivateAbilityDefinition).GetField(
                "abilityTagName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "태그 이름 필드의 이름이 바뀌었다면 이 검사도 함께 고쳐야 한다.");
            info.SetValue(definition, tagName);
            return definition;
        }

        private IBehaviour CreateChild(BehaviourNodeDefinition definition, GameObject owner)
        {
            var asset = ScriptableObject.CreateInstance<BehaviourTreeAsset>();
            _created.Add(asset);
            SetPrivate(asset, "nodes", new List<BehaviourNodeDefinition> { new SequenceDefinition(), definition });
            SetPrivate(asset, "parents", new List<int> { BehaviourTreeAsset.NoParent, 0 });

            var tree = asset.CreateRuntimeTree(new BehaviourBuildContext(owner, new BehaviourContext()));
            return tree.Root.Children.Count == 0 ? null : tree.Root.Children[0].Value;
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
    }
}
