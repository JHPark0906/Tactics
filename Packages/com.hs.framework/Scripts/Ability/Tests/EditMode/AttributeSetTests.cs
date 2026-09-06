using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 어트리뷰트 집합의 수정자 적용 순서, 출처별 제거, 상한 연동을 검증한다.
    /// </summary>
    /// <remarks>
    /// 이 세 가지가 이 층의 계약이다. 적용 순서가 바뀌면 같은 수정자 조합에서 다른 값이 나오고,
    /// 출처별 제거가 어긋나면 만료된 효과가 값에 남으며, 상한 연동이 끊기면 최대치 버프가 무의미해진다.
    /// </remarks>
    public sealed class AttributeSetTests
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
        public void CurrentValueStartsFromTheBaseValue()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);

            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(10f).Within(0.001f));
            Assert.That(_attributes.GetBaseValue(power), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void ModifiersApplyInAddThenMultiplyThenOverrideOrder()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);

            _attributes.AddModifier(power, AttributeModifierOperation.Multiply, 2f);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 10f);

            Assert.That(
                _attributes.GetCurrentValue(power),
                Is.EqualTo(40f).Within(0.001f),
                "더하기를 먼저 적용해 (10 + 10) × 2 = 40이어야 한다. 곱하기가 먼저면 30이 된다.");
        }

        [Test]
        public void ModifierOrderDoesNotDependOnTheOrderTheyWereAdded()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);

            _attributes.AddModifier(power, AttributeModifierOperation.Add, 10f);
            _attributes.AddModifier(power, AttributeModifierOperation.Multiply, 2f);

            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(40f).Within(0.001f));
        }

        [Test]
        public void MultipleAdditiveModifiersAreSummedBeforeMultiplying()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 0f);
            _attributes.AddAttribute(power);

            _attributes.AddModifier(power, AttributeModifierOperation.Add, 3f);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 7f);
            _attributes.AddModifier(power, AttributeModifierOperation.Multiply, 3f);

            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void OverrideReplacesEverythingComputedBefore()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 100f);
            _attributes.AddModifier(power, AttributeModifierOperation.Multiply, 5f);

            _attributes.AddModifier(power, AttributeModifierOperation.Override, 7f);

            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void TheLastOverrideWins()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);

            _attributes.AddModifier(power, AttributeModifierOperation.Override, 3f);
            _attributes.AddModifier(power, AttributeModifierOperation.Override, 9f);

            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(9f).Within(0.001f));
        }

        [Test]
        public void ReleasingAHandleRemovesOnlyThatModifier()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 0f);
            _attributes.AddAttribute(power);
            var firstHandle = _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f);
            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(10f).Within(0.001f));

            firstHandle.Dispose();

            Assert.That(
                _attributes.GetCurrentValue(power),
                Is.EqualTo(5f).Within(0.001f),
                "크기가 같은 수정자가 둘 있어도 손잡이가 가리키는 하나만 사라져야 한다.");
            Assert.That(_attributes.GetModifierCount(power), Is.EqualTo(1));
        }

        [Test]
        public void ReleasingTheSameHandleTwiceRemovesOneModifier()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 0f);
            _attributes.AddAttribute(power);
            var handle = _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f);

            handle.Dispose();
            handle.Dispose();

            Assert.That(_attributes.GetModifierCount(power), Is.EqualTo(1));
            Assert.That(_attributes.GetCurrentValue(power), Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void RemovingBySourceTakesAwayEveryModifierFromThatSource()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            var speed = CreateDefinition("Speed", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            _attributes.AddAttribute(speed);
            var buff = new object();
            var otherBuff = new object();
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f, buff);
            _attributes.AddModifier(speed, AttributeModifierOperation.Add, 5f, buff);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 100f, otherBuff);

            var removedCount = _attributes.RemoveModifiersFrom(buff);

            Assert.That(removedCount, Is.EqualTo(2));
            Assert.That(
                _attributes.GetCurrentValue(power),
                Is.EqualTo(110f).Within(0.001f),
                "다른 출처가 얹은 수정자는 남아야 한다.");
            Assert.That(_attributes.GetCurrentValue(speed), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void CurrentValueFollowsTheCapAttribute()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition("Health", defaultBaseValue: 100f, capAttribute: maxHealth);
            _attributes.AddAttribute(maxHealth);
            _attributes.AddAttribute(health);

            _attributes.AddModifier(maxHealth, AttributeModifierOperation.Add, 50f);

            Assert.That(
                _attributes.GetCurrentValue(maxHealth),
                Is.EqualTo(150f).Within(0.001f));
            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(100f).Within(0.001f),
                "상한이 올라간다고 현재값이 저절로 차오르면 안 된다.");

            _attributes.SetBaseValue(health, 150f);
            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(150f).Within(0.001f),
                "상한이 올라간 만큼은 채울 수 있어야 한다.");
        }

        [Test]
        public void ValueIsNeverAllowedAboveTheCap()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition("Health", defaultBaseValue: 100f, capAttribute: maxHealth);
            _attributes.AddAttribute(maxHealth);
            _attributes.AddAttribute(health);

            _attributes.SetBaseValue(health, 500f);

            Assert.That(_attributes.GetCurrentValue(health), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void ClampPolicyCutsTheBaseValueWhenTheCapShrinks()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition("Health", defaultBaseValue: 100f, capAttribute: maxHealth);
            _attributes.AddAttribute(maxHealth);
            _attributes.AddAttribute(health);
            var buffHandle = _attributes.AddModifier(maxHealth, AttributeModifierOperation.Add, 50f);
            _attributes.SetBaseValue(health, 150f);

            buffHandle.Dispose();

            Assert.That(_attributes.GetCurrentValue(maxHealth), Is.EqualTo(100f).Within(0.001f));
            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(100f).Within(0.001f),
                "최대치 버프가 끝나면 넘친 몫은 사라져야 한다.");
            Assert.That(
                _attributes.GetBaseValue(health),
                Is.EqualTo(100f).Within(0.001f),
                "잘라낸 몫이 기본값에 남아 있으면 버프가 다시 걸릴 때 되살아난다.");
        }

        [Test]
        public void PreserveRatioPolicyKeepsTheFilledProportion()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition(
                "Health",
                defaultBaseValue: 50f,
                capAttribute: maxHealth,
                capPolicy: AttributeCapPolicy.PreserveRatio);
            _attributes.AddAttribute(maxHealth);
            _attributes.AddAttribute(health);

            _attributes.AddModifier(maxHealth, AttributeModifierOperation.Multiply, 2f);

            Assert.That(_attributes.GetCurrentValue(maxHealth), Is.EqualTo(200f).Within(0.001f));
            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(100f).Within(0.001f),
                "절반이 차 있었으면 최대치가 두 배가 된 뒤에도 절반이어야 한다.");
        }

        [Test]
        public void CapAppliesEvenWhenTheCapAttributeIsAddedLater()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition("Health", defaultBaseValue: 500f, capAttribute: maxHealth);
            _attributes.AddAttribute(health);
            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(500f).Within(0.001f),
                "상한 어트리뷰트가 없으면 상한이 없는 것으로 다룬다.");

            _attributes.AddAttribute(maxHealth);

            Assert.That(
                _attributes.GetCurrentValue(health),
                Is.EqualTo(100f).Within(0.001f),
                "나열 순서가 결과를 바꾸면 안 된다.");
        }

        [Test]
        public void FixedMaxValueCapsTheAttributeWhenNoCapAttributeIsSet()
        {
            var stamina = CreateDefinitionWithMaxValue("Stamina", defaultBaseValue: 10f, maxValue: 20f);
            _attributes.AddAttribute(stamina);

            _attributes.AddModifier(stamina, AttributeModifierOperation.Add, 100f);

            Assert.That(_attributes.GetCurrentValue(stamina), Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void ValueNeverFallsBelowTheMinimum()
        {
            var health = CreateDefinition("Health", defaultBaseValue: 10f);
            _attributes.AddAttribute(health);

            _attributes.SetBaseValue(health, -50f);

            Assert.That(_attributes.GetCurrentValue(health), Is.Zero);
        }

        [Test]
        public void CircularCapDefinitionsAreRejected()
        {
            var first = CreateDefinition("First", defaultBaseValue: 1f);
            var second = CreateDefinition("Second", defaultBaseValue: 1f, capAttribute: first);
            SetCapAttribute(first, second);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex("순환"));

            Assert.That(_attributes.AddAttribute(first), Is.False);
        }

        [Test]
        public void ChangesAreObservableThroughTheSharedStream()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            var observed = new List<AttributeChangedEvent>();
            using var subscription = _attributes.Changed.Subscribe(observed.Add);

            _attributes.AddModifier(power, AttributeModifierOperation.Add, 5f);

            Assert.That(observed, Has.Count.EqualTo(1));
            Assert.That(observed[0].Definition, Is.SameAs(power));
            Assert.That(observed[0].PreviousValue, Is.EqualTo(10f).Within(0.001f));
            Assert.That(observed[0].CurrentValue, Is.EqualTo(15f).Within(0.001f));
            Assert.That(observed[0].Delta, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void ChangingTheCapNotifiesTheDependentAttributeToo()
        {
            var maxHealth = CreateDefinition("MaxHealth", defaultBaseValue: 100f);
            var health = CreateDefinition("Health", defaultBaseValue: 100f, capAttribute: maxHealth);
            _attributes.AddAttribute(maxHealth);
            _attributes.AddAttribute(health);
            var observed = new List<AttributeChangedEvent>();
            using var subscription = _attributes.Changed.Subscribe(observed.Add);

            _attributes.AddModifier(maxHealth, AttributeModifierOperation.Add, -40f);

            Assert.That(observed, Has.Count.EqualTo(2), "상한과 그 상한을 따르는 값이 모두 통지되어야 한다.");
            Assert.That(observed[1].Definition, Is.SameAs(health));
            Assert.That(observed[1].CurrentValue, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void UnchangedValuesDoNotNotify()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            var notifiedCount = 0;
            using var subscription = _attributes.Changed.Subscribe(_ => notifiedCount++);

            _attributes.SetBaseValue(power, 10f);

            Assert.That(notifiedCount, Is.Zero);
        }

        [Test]
        public void IntegerConversionRoundsAwayFromZeroAtTheMidpoint()
        {
            Assert.That(AttributeValue.ToInt(90.4f), Is.EqualTo(90));
            Assert.That(AttributeValue.ToInt(90.5f), Is.EqualTo(91));
            Assert.That(AttributeValue.ToInt(90.6f), Is.EqualTo(91));
            Assert.That(AttributeValue.ToInt(-90.5f), Is.EqualTo(-91));
        }

        [Test]
        public void SnapshotCapturesBaseValuesAndNotModifiers()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            _attributes.SetBaseValue(power, 42f);
            _attributes.AddModifier(power, AttributeModifierOperation.Add, 100f);

            var snapshot = _attributes.CaptureSnapshot();

            Assert.That(snapshot.entries, Has.Count.EqualTo(1));
            Assert.That(snapshot.entries[0].id, Is.EqualTo("Power"));
            Assert.That(
                snapshot.entries[0].baseValue,
                Is.EqualTo(42f).Within(0.001f),
                "수정자는 원인이 다시 얹어 주므로 저장 대상이 아니다.");
        }

        [Test]
        public void SnapshotRestoresBaseValuesById()
        {
            var power = CreateDefinition("Power", defaultBaseValue: 10f);
            _attributes.AddAttribute(power);
            _attributes.SetBaseValue(power, 42f);
            var snapshot = _attributes.CaptureSnapshot();
            _attributes.SetBaseValue(power, 1f);

            var restoredCount = _attributes.RestoreSnapshot(snapshot);

            Assert.That(restoredCount, Is.EqualTo(1));
            Assert.That(_attributes.GetBaseValue(power), Is.EqualTo(42f).Within(0.001f));
        }

        /// <summary>테스트용 어트리뷰트 정의를 만들고 정리 목록에 등록한다.</summary>
        /// <param name="id">어트리뷰트 식별자이다.</param>
        /// <param name="defaultBaseValue">기본값이다.</param>
        /// <param name="capAttribute">상한 역할을 할 어트리뷰트이다.</param>
        /// <param name="capPolicy">상한 변화 시 기본값 처리 방식이다.</param>
        /// <returns>만든 정의이다.</returns>
        private AttributeDefinition CreateDefinition(
            string id,
            float defaultBaseValue = 0f,
            AttributeDefinition capAttribute = null,
            AttributeCapPolicy capPolicy = AttributeCapPolicy.ClampBaseValue)
        {
            var definition = AttributeDefinition.CreateRuntime(
                id, defaultBaseValue, 0f, capAttribute, capPolicy);
            _createdDefinitions.Add(definition);
            return definition;
        }

        /// <summary>고정 상한을 가진 테스트용 어트리뷰트 정의를 만든다.</summary>
        /// <param name="id">어트리뷰트 식별자이다.</param>
        /// <param name="defaultBaseValue">기본값이다.</param>
        /// <param name="maxValue">고정 상한이다.</param>
        /// <returns>만든 정의이다.</returns>
        private AttributeDefinition CreateDefinitionWithMaxValue(string id, float defaultBaseValue, float maxValue)
        {
            var definition = AttributeDefinition.CreateRuntimeWithMaxValue(id, defaultBaseValue, 0f, maxValue);
            _createdDefinitions.Add(definition);
            return definition;
        }

        /// <summary>순환 상한을 만들려고 이미 생성된 정의의 상한을 바꾼다.</summary>
        /// <param name="definition">바꿀 정의이다.</param>
        /// <param name="capAttribute">상한으로 지정할 어트리뷰트이다.</param>
        private static void SetCapAttribute(AttributeDefinition definition, AttributeDefinition capAttribute)
        {
            var field = typeof(AttributeDefinition).GetField(
                "capAttribute",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "AttributeDefinition.capAttribute 필드를 찾지 못했다.");
            field.SetValue(definition, capAttribute);
        }
    }
}
