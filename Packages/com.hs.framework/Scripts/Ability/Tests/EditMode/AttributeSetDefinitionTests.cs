using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>어트리뷰트 묶음 데이터가 집합·어빌리티 집합·컴포넌트를 통해 갖춰지는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 어떤 어트리뷰트를 갖추는가는 액터의 종류가 정하는 데이터이며, 같은 묶음을 어빌리티 구성이 다른
    /// 여러 집합이 공유한다. 묶음이 덮어쓰기를 지정하지 않은 항목은 있는 값을 존중해야 한다.
    /// </remarks>
    public sealed class AttributeSetDefinitionTests
    {
        /// <summary>테스트가 만든 에셋과 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _maxHealth;
        private AttributeDefinition _health;
        private AttributeDefinition _experience;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
            _experience = Track(AttributeDefinition.CreateRuntime("Experience", 0f));
        }

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
        public void ApplyingAddsMissingAttributesWithTheirStartingValues()
        {
            var definition = CreateSet(
                new AttributeSetDefinition.Entry(_maxHealth, 250f),
                new AttributeSetDefinition.Entry(_health),
                new AttributeSetDefinition.Entry(_experience));
            using var attributes = new AttributeSet();

            var addedCount = definition.ApplyTo(attributes);

            Assert.That(addedCount, Is.EqualTo(3));
            Assert.That(attributes.GetBaseValue(_maxHealth), Is.EqualTo(250f).Within(0.001f), "덮어쓴 항목은 지정한 값으로 시작한다.");
            Assert.That(attributes.GetBaseValue(_health), Is.EqualTo(100f).Within(0.001f), "덮어쓰지 않은 항목은 정의의 기본값으로 시작한다.");
            Assert.That(attributes.GetCurrentValue(_health), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void ApplyingRespectsExistingValuesUnlessTheEntryOverrides()
        {
            var definition = CreateSet(
                new AttributeSetDefinition.Entry(_maxHealth, 250f),
                new AttributeSetDefinition.Entry(_experience));
            using var attributes = new AttributeSet();
            attributes.AddAttribute(_maxHealth, 80f);
            attributes.AddAttribute(_experience, 40f);

            var addedCount = definition.ApplyTo(attributes);

            Assert.That(addedCount, Is.Zero);
            Assert.That(attributes.GetBaseValue(_maxHealth), Is.EqualTo(250f).Within(0.001f));
            Assert.That(attributes.GetBaseValue(_experience), Is.EqualTo(40f).Within(0.001f), "덮어쓰기를 지정하지 않은 항목은 있는 값을 존중한다.");
        }

        [Test]
        public void ValidationRejectsEmptyAndDuplicateEntries()
        {
            var withEmpty = CreateSet(new AttributeSetDefinition.Entry(null));
            var withDuplicate = CreateSet(
                new AttributeSetDefinition.Entry(_health),
                new AttributeSetDefinition.Entry(_health, 10f));

            Assert.That(withEmpty.TryValidate(out var emptyError), Is.False);
            Assert.That(emptyError, Is.Not.Null);
            Assert.That(withDuplicate.TryValidate(out var duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("Health"));
        }

        [Test]
        public void AnAbilitySetAppliesItsAttributeSetsBeforeInlineAttributesAndAbilities()
        {
            var definition = CreateSet(
                new AttributeSetDefinition.Entry(_maxHealth),
                new AttributeSetDefinition.Entry(_health),
                new AttributeSetDefinition.Entry(_experience));
            var abilitySet = Track(GameplayAbilitySet.CreateRuntime(
                attributes: new[] { new GameplayAbilitySet.StartingAttribute(_experience, 15f) },
                attributeSetDefinitions: new[] { definition }));
            using var attributes = new AttributeSet();
            using var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes));

            abilitySet.GrantTo(system);

            Assert.That(attributes.Contains(_health), Is.True);
            Assert.That(attributes.Contains(_maxHealth), Is.True);
            Assert.That(attributes.GetBaseValue(_experience), Is.EqualTo(15f).Within(0.001f), "인라인 항목은 묶음 위에 얹힌다.");
        }

        [Test]
        public void TwoAbilitySetsCanShareOneAttributeSet()
        {
            var definition = CreateSet(new AttributeSetDefinition.Entry(_maxHealth), new AttributeSetDefinition.Entry(_health));
            var first = Track(GameplayAbilitySet.CreateRuntime(attributeSetDefinitions: new[] { definition }));
            var second = Track(GameplayAbilitySet.CreateRuntime(attributeSetDefinitions: new[] { definition }));
            using var firstAttributes = new AttributeSet();
            using var secondAttributes = new AttributeSet();
            using var firstSystem = new GameplayAbilitySystem(new GameplayEffectRunner(firstAttributes));
            using var secondSystem = new GameplayAbilitySystem(new GameplayEffectRunner(secondAttributes));

            first.GrantTo(firstSystem);
            second.GrantTo(secondSystem);

            Assert.That(firstAttributes.Count, Is.EqualTo(2));
            Assert.That(secondAttributes.Count, Is.EqualTo(2));
        }

        [Test]
        public void AnAbilitySetWithABrokenAttributeSetGrantsNothing()
        {
            var broken = CreateSet(new AttributeSetDefinition.Entry(null));
            var abilitySet = Track(GameplayAbilitySet.CreateRuntime(attributeSetDefinitions: new[] { broken }));
            using var attributes = new AttributeSet();
            using var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes));
            LogAssert.Expect(LogType.Error, new Regex("GameplayAbilitySet"));

            Assert.That(abilitySet.GrantTo(system), Is.Zero);
            Assert.That(attributes.Count, Is.Zero);
        }

        [Test]
        public void TheComponentAppliesItsAttributeSetsWhenTheSetIsFirstTouched()
        {
            var definition = CreateSet(new AttributeSetDefinition.Entry(_maxHealth), new AttributeSetDefinition.Entry(_health));
            var actor = Track(new GameObject("Actor"));
            var component = actor.AddComponent<AttributeSetComponent>();
            AttachAttributeSet(component, definition);

            var attributes = component.Attributes;

            Assert.That(attributes.Contains(_health), Is.True, "어빌리티 시스템이 없는 액터도 묶음으로 어트리뷰트를 갖춰야 한다.");
            Assert.That(attributes.GetCurrentValue(_health), Is.EqualTo(100f).Within(0.001f));
        }

        /// <summary>정리 목록에 등록된 어트리뷰트 묶음을 만든다.</summary>
        private AttributeSetDefinition CreateSet(params AttributeSetDefinition.Entry[] entries)
        {
            return Track(AttributeSetDefinition.CreateRuntime(entries));
        }

        /// <summary>
        /// 인스펙터 칸을 코드에서 채운다. 에디터가 아니면 직렬화 필드에 닿을 길이 없으므로 리플렉션으로 넣는다.
        /// </summary>
        private static void AttachAttributeSet(AttributeSetComponent component, AttributeSetDefinition definition)
        {
            var field = typeof(AttributeSetComponent).GetField(
                "attributeSets", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "AttributeSetComponent.attributeSets 필드를 찾지 못했다.");
            field.SetValue(component, new List<AttributeSetDefinition> { definition });
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
