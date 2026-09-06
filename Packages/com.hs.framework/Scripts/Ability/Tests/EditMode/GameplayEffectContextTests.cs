using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 효과를 적용하는 순간의 사정에서 크기를 읽고, 가해자가 어트리뷰트 변화까지 따라가는지 검증한다.
    /// </summary>
    /// <remarks>
    /// 같은 피해 효과라도 쏜 유닛의 공격력이나 명중 판정 뒤 정해진 값에 따라 크기가 달라야 한다.
    /// 정의에 숫자를 박으면 그런 효과를 표현할 수 없으므로 크기의 출처를 여기서 하나씩 고정한다.
    /// </remarks>
    public sealed class GameplayEffectContextTests
    {
        /// <summary>호출자가 정한 피해량의 태그 이름이다.</summary>
        private const string DamageTagName = "Data.Damage";

        /// <summary>테스트가 만든 에셋과 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _health;
        private AttributeDefinition _maxHealth;
        private AttributeDefinition _attackPower;
        private AttributeSet _target;
        private AttributeSet _source;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
            _attackPower = Track(AttributeDefinition.CreateRuntime("AttackPower", 25f));
            _target = new AttributeSet();
            _target.AddAttribute(_maxHealth);
            _target.AddAttribute(_health);
            _source = new AttributeSet();
            _source.AddAttribute(_attackPower);
            _runner = new GameplayEffectRunner(_target, new GameplayTagContainer());
        }

        [TearDown]
        public void TearDown()
        {
            _target?.Dispose();
            _source?.Dispose();
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
        public void ASetByCallerMagnitudeIsReadFromTheContext()
        {
            var damage = CreateInstant(GameplayEffectModifier.CreateSetByCallerRuntime(
                _health, AttributeModifierOperation.Add, DamageTagName, coefficient: -1f));
            var context = new GameplayEffectContext().SetByCaller(DamageTagName, 30f);

            _runner.Apply(damage, null, context);

            Assert.That(
                _target.GetBaseValue(_health),
                Is.EqualTo(70f).Within(0.001f),
                "계수 -1이 양수로 넘긴 피해량을 빼는 더하기로 만들어야 한다.");
        }

        [Test]
        public void AMissingSetByCallerValueSkipsTheModifierWithAWarning()
        {
            var damage = CreateInstant(GameplayEffectModifier.CreateSetByCallerRuntime(
                _health, AttributeModifierOperation.Add, DamageTagName, coefficient: -1f));
            LogAssert.Expect(LogType.Warning, new Regex("GameplayEffectRunner"));

            _runner.Apply(damage, null, new GameplayEffectContext());

            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(100f).Within(0.001f), "읽을 곳이 없는 크기를 0으로 흘리면 안 된다.");
        }

        [Test]
        public void ASourceAttributeMagnitudeReadsTheInstigatorsAttributes()
        {
            var damage = CreateInstant(GameplayEffectModifier.CreateAttributeBasedRuntime(
                _health,
                AttributeModifierOperation.Add,
                GameplayEffectMagnitudeSource.SourceAttribute,
                _attackPower,
                coefficient: -1f));

            _runner.Apply(damage, null, new GameplayEffectContext(sourceAttributes: _source));

            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(75f).Within(0.001f));
        }

        [Test]
        public void ATargetAttributeMagnitudeReadsTheReceiversAttributes()
        {
            _target.SetBaseValue(_health, 20f);
            var heal = CreateInstant(GameplayEffectModifier.CreateAttributeBasedRuntime(
                _health,
                AttributeModifierOperation.Add,
                GameplayEffectMagnitudeSource.TargetAttribute,
                _maxHealth,
                coefficient: 0.5f));

            _runner.Apply(heal, null, new GameplayEffectContext());

            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(70f).Within(0.001f), "최대 체력의 절반을 회복해야 한다.");
        }

        [Test]
        public void TheFormulaAppliesPreAndPostAdds()
        {
            var effect = CreateInstant(GameplayEffectModifier.CreateSetByCallerRuntime(
                _health, AttributeModifierOperation.Add, DamageTagName, coefficient: -2f, preMultiplyAdd: 5f, postMultiplyAdd: 3f));
            var context = new GameplayEffectContext().SetByCaller(DamageTagName, 10f);

            _runner.Apply(effect, null, context);

            // -2 × (10 + 5) + 3 = -27
            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(73f).Within(0.001f));
        }

        [Test]
        public void TheInstigatorAndCauserReachTheAttributeChangeNotification()
        {
            var instigator = Track(new GameObject("Attacker"));
            var causer = new object();
            var damage = CreateInstant(GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f));
            var received = new List<AttributeChangedEvent>();
            using var subscription = _target.Changed.Subscribe(received.Add);

            _runner.Apply(damage, null, new GameplayEffectContext(instigator, causer));

            Assert.That(received.Count, Is.EqualTo(1));
            Assert.That(received[0].Context.Instigator, Is.SameAs(instigator), "누가 쏘았는지가 어트리뷰트 변화까지 따라가야 한다.");
            Assert.That(received[0].Context.Cause, Is.SameAs(causer));
        }

        [Test]
        public void WithoutAContextTheCauseFallsBackToTheSourceThenTheDefinition()
        {
            var damage = CreateInstant(GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f));
            var received = new List<AttributeChangedEvent>();
            using var subscription = _target.Changed.Subscribe(received.Add);
            var source = new object();

            _runner.Apply(damage, source);
            _runner.Apply(damage);

            Assert.That(received[0].Context.Cause, Is.SameAs(source));
            Assert.That(received[1].Context.Cause, Is.SameAs(damage));
        }

        [Test]
        public void APeriodicEffectReusesTheContextOnEveryExecution()
        {
            var burn = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[]
                {
                    GameplayEffectModifier.CreateSetByCallerRuntime(
                        _health, AttributeModifierOperation.Add, DamageTagName, coefficient: -1f)
                },
                duration: 10f,
                period: 1f));
            var context = new GameplayEffectContext().SetByCaller(DamageTagName, 5f);

            var effect = _runner.Apply(burn, null, context);
            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(95f).Within(0.001f));

            _runner.Tick(1f);

            Assert.That(effect.Context, Is.SameAs(context));
            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(90f).Within(0.001f), "주기 실행도 적용할 때의 사정으로 크기를 읽어야 한다.");
        }

        [Test]
        public void ALingeringAttributeBasedMagnitudeIsFixedAtApplication()
        {
            var armor = Track(AttributeDefinition.CreateRuntime("Armor", 10f));
            _target.AddAttribute(armor);
            var buff = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[]
                {
                    GameplayEffectModifier.CreateAttributeBasedRuntime(
                        armor, AttributeModifierOperation.Add, GameplayEffectMagnitudeSource.SourceAttribute, _attackPower)
                }));

            _runner.Apply(buff, null, new GameplayEffectContext(sourceAttributes: _source));
            Assert.That(_target.GetCurrentValue(armor), Is.EqualTo(35f).Within(0.001f));

            _source.SetBaseValue(_attackPower, 100f);

            Assert.That(
                _target.GetCurrentValue(armor),
                Is.EqualTo(35f).Within(0.001f),
                "사정에서 읽은 크기는 적용하는 순간 정해지고 그 뒤 원천을 따라가지 않는다.");
        }

        [Test]
        public void AScalarModifierNeedsNoContext()
        {
            var damage = CreateInstant(GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f));

            _runner.Apply(damage);

            Assert.That(_target.GetBaseValue(_health), Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void ValidationRejectsASetByCallerModifierWithAMalformedTag()
        {
            var definition = CreateInstant(GameplayEffectModifier.CreateSetByCallerRuntime(
                _health, AttributeModifierOperation.Add, "Data..Damage"));

            Assert.That(definition.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("SetByCaller"));
        }

        [Test]
        public void ValidationRejectsAnAttributeBasedModifierWithoutACapturedAttribute()
        {
            var definition = CreateInstant(GameplayEffectModifier.CreateAttributeBasedRuntime(
                _health, AttributeModifierOperation.Add, GameplayEffectMagnitudeSource.SourceAttribute, null));

            Assert.That(definition.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void TheComponentPassesTheContextThrough()
        {
            var targetObject = Track(new GameObject("Target"));
            var effectComponent = targetObject.AddComponent<GameplayEffectComponent>();
            var attributes = targetObject.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_health);
            var damage = CreateInstant(GameplayEffectModifier.CreateSetByCallerRuntime(
                _health, AttributeModifierOperation.Add, DamageTagName, coefficient: -1f));

            effectComponent.ApplyEffect(damage, null, new GameplayEffectContext().SetByCaller(DamageTagName, 40f));

            Assert.That(attributes.GetBaseValue(_health), Is.EqualTo(60f).Within(0.001f));
        }

        /// <summary>수정자 하나를 가진 즉시 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition CreateInstant(GameplayEffectModifier modifier)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant, new[] { modifier }));
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
