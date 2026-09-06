using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 어트리뷰트 집합이 나열 순서와 알림 도중의 변경에 흔들리지 않는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 상한 어트리뷰트와 대상 어트리뷰트의 나열 순서가 달라도 같은 기본값을 저장하는지 비교한다.
    /// </para>
    /// <para>
    /// 출처별로 수정자를 걷는 동안 값 변화 알림이 나가고, 그 알림을 받은 쪽이 어트리뷰트를 더할 수 있다.
    /// 그때 걷는 쪽이 사전을 그대로 돌고 있으면 열거 중 수정으로 터진다. 알림 안에서 더하는 구독자를 실제로 붙여 본다.
    /// </para>
    /// </remarks>
    public sealed class AttributeSetOrderAndReentrancyTests
    {
        private readonly List<AttributeDefinition> _createdDefinitions = new();
        private AttributeSet _attributes;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
            _attributes = null;

            foreach (var definition in _createdDefinitions)
            {
                if (definition != null)
                {
                    Object.DestroyImmediate(definition);
                }
            }

            _createdDefinitions.Clear();
        }

        [Test]
        public void TheBaseValueDoesNotDependOnWhichAttributeIsListedFirst()
        {
            var maxHealth = CreateDefinition("MaxHealth", 100f);
            var health = CreateDefinition("Health", 0f, capAttribute: maxHealth);
            var capFirst = new AttributeSet();
            var dependentFirst = new AttributeSet();

            capFirst.AddAttribute(maxHealth);
            capFirst.AddAttribute(health, 150f);
            dependentFirst.AddAttribute(health, 150f);
            dependentFirst.AddAttribute(maxHealth);

            Assert.That(
                capFirst.GetBaseValue(health),
                Is.EqualTo(dependentFirst.GetBaseValue(health)).Within(0.001f),
                "같은 데이터인데 나열 순서에 따라 저장되는 기본값이 달라진다.");
            Assert.That(capFirst.GetBaseValue(health), Is.EqualTo(100f).Within(0.001f));
            capFirst.Dispose();
            dependentFirst.Dispose();
        }

        [Test]
        public void TheStartingBaseValueIsClampedToTheFixedBounds()
        {
            var ratio = CreateDefinitionWithMax("Ratio", 0f, minValue: 0f, maxValue: 1f);

            _attributes.AddAttribute(ratio, 5f);
            Assert.That(_attributes.GetBaseValue(ratio), Is.EqualTo(1f).Within(0.001f));

            _attributes.RemoveAttribute(ratio);
            _attributes.AddAttribute(ratio, -5f);
            Assert.That(_attributes.GetBaseValue(ratio), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void TheDefaultBaseValueIsClampedTooWhenItLiesOutsideTheBounds()
        {
            var ratio = CreateDefinitionWithMax("Ratio", 3f, minValue: 0f, maxValue: 1f);

            _attributes.AddAttribute(ratio);

            Assert.That(_attributes.GetBaseValue(ratio), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void ASubscriberMayAddAnAttributeWhileModifiersAreRemovedBySource()
        {
            var power = CreateDefinition("Power", 10f);
            var armor = CreateDefinition("Armor", 5f);
            var speed = CreateDefinition("Speed", 1f);
            _attributes.AddAttribute(power);
            _attributes.AddAttribute(armor);
            var source = new object();
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f, source);
            _attributes.AddModifier(armor, AttributeModifierOperation.Add, 5f, source);
            using var subscription = _attributes.Changed.Subscribe(_ =>
            {
                if (!_attributes.Contains(speed))
                {
                    _attributes.AddAttribute(speed);
                }
            });

            var removedCount = 0;
            Assert.DoesNotThrow(
                () => removedCount = _attributes.RemoveModifiersFrom(source),
                "알림 안에서 어트리뷰트를 더하는 구독자가 있으면 열거 중 수정으로 터진다.");

            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(10f).Within(0.001f));
            Assert.That(_attributes.GetCurrentValue(armor), Is.EqualTo(5f).Within(0.001f));
            Assert.That(_attributes.Contains(speed), Is.True);
        }

        [Test]
        public void ASubscriberMayRemoveAnAttributeWhileModifiersAreRemovedBySource()
        {
            var power = CreateDefinition("Power", 10f);
            var armor = CreateDefinition("Armor", 5f);
            _attributes.AddAttribute(power);
            _attributes.AddAttribute(armor);
            var source = new object();
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f, source);
            _attributes.AddModifier(armor, AttributeModifierOperation.Add, 5f, source);
            var announced = new List<AttributeDefinition>();
            using var subscription = _attributes.Changed.Subscribe(change =>
            {
                announced.Add(change.Definition);
                _attributes.RemoveAttribute(power);
                _attributes.RemoveAttribute(armor);
            });

            Assert.DoesNotThrow(() => _attributes.RemoveModifiersFrom(source));

            Assert.That(announced.Count, Is.EqualTo(1), "집합을 이미 떠난 어트리뷰트의 변화를 알리면 안 된다.");
            Assert.That(_attributes.Count, Is.Zero);
        }

        /// <summary>정리 목록에 등록된 어트리뷰트 정의를 만든다.</summary>
        private AttributeDefinition CreateDefinition(
            string id,
            float defaultBaseValue,
            float minValue = 0f,
            AttributeDefinition capAttribute = null)
        {
            var definition = AttributeDefinition.CreateRuntime(id, defaultBaseValue, minValue, capAttribute);
            _createdDefinitions.Add(definition);
            return definition;
        }

        /// <summary>정리 목록에 등록된 고정 상한 어트리뷰트 정의를 만든다.</summary>
        private AttributeDefinition CreateDefinitionWithMax(
            string id,
            float defaultBaseValue,
            float minValue,
            float maxValue)
        {
            var definition = AttributeDefinition.CreateRuntimeWithMaxValue(id, defaultBaseValue, minValue, maxValue);
            _createdDefinitions.Add(definition);
            return definition;
        }
    }
}
