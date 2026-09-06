using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>효과의 즉시·지속·무한 처리와 되돌리기, 주기 실행, 태그 조건을 검증한다.</summary>
    public sealed class GameplayEffectRunnerTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        /// <summary>테스트에서 값을 바꿀 대상 어트리뷰트이다.</summary>
        private AttributeDefinition _health;

        /// <summary>여러 어트리뷰트를 한 효과가 건드리는 경우를 위한 두 번째 어트리뷰트이다.</summary>
        private AttributeDefinition _armor;

        /// <summary>대상의 어트리뷰트 집합이다.</summary>
        private AttributeSet _attributes;

        /// <summary>대상의 태그 컨테이너이다.</summary>
        private GameplayTagContainer _tags;

        /// <summary>검증 대상 실행기이다.</summary>
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _health = CreateAttribute("Health", 100f);
            _armor = CreateAttribute("Armor", 10f);
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_health);
            _attributes.AddAttribute(_armor);
            _tags = new GameplayTagContainer();
            _runner = new GameplayEffectRunner(_attributes, _tags);
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
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
        public void InstantEffectChangesTheBaseValueAndLeavesNothingBehind()
        {
            var definition = CreateEffect(GameplayEffectDurationPolicy.Instant, Modifier(_health, -10f));

            var applied = _runner.Apply(definition);

            Assert.That(applied, Is.Null, "즉시 효과는 유지되지 않으므로 기록을 남기지 않는다.");
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f));
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f));
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetModifierCount(_health), Is.Zero, "즉시 효과는 수정자를 남기면 안 된다.");
        }

        [Test]
        public void InstantOverrideAndMultiplyActOnTheBaseValue()
        {
            _runner.Apply(CreateEffect(
                GameplayEffectDurationPolicy.Instant,
                Modifier(_health, 0.5f, AttributeModifierOperation.Multiply)));

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(50f));

            _runner.Apply(CreateEffect(
                GameplayEffectDurationPolicy.Instant,
                Modifier(_health, 30f, AttributeModifierOperation.Override)));

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(30f));
        }

        [Test]
        public void DurationEffectLeavesTheBaseValueAloneAndRevertsWhenItExpires()
        {
            var definition = CreateEffect(GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 2f);

            var effect = _runner.Apply(definition);

            Assert.That(effect, Is.Not.Null);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f), "지속 효과는 기본값을 건드리지 않는다.");
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f));

            _runner.Tick(1f);
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f), "아직 만료 전이면 유지되어야 한다.");

            _runner.Tick(1f);

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(100f), "만료되면 되돌아가야 한다.");
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(effect.IsActive, Is.False);
        }

        [Test]
        public void InfiniteEffectStaysUntilItIsRemoved()
        {
            var definition = CreateEffect(GameplayEffectDurationPolicy.Infinite, Modifier(_health, -10f));

            var effect = _runner.Apply(definition);
            _runner.Tick(100f);

            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1), "무한 효과는 시간이 지나도 사라지지 않는다.");
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f));

            Assert.That(_runner.Remove(effect), Is.True);
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(100f));
            Assert.That(_runner.Remove(effect), Is.False, "이미 제거된 효과는 다시 제거되지 않는다.");
        }

        [Test]
        public void ExpiryRevertsEveryAttributeTheEffectTouched()
        {
            var definition = CreateEffectWithModifiers(
                GameplayEffectDurationPolicy.Duration,
                new[] { Modifier(_health, -10f), Modifier(_armor, -5f) },
                duration: 1f);

            _runner.Apply(definition);
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f));
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(5f));

            _runner.Tick(1f);

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(100f));
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(10f));
            Assert.That(_attributes.GetModifierCount(_health), Is.Zero);
            Assert.That(_attributes.GetModifierCount(_armor), Is.Zero);
        }

        [Test]
        public void OneExpiringEffectDoesNotRemoveAnotherEffectsModifier()
        {
            var shortEffect = CreateEffect(GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 1f);
            var longEffect = CreateEffect(GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 5f);
            _runner.Apply(shortEffect);
            _runner.Apply(longEffect);

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(80f));

            _runner.Tick(1f);

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(90f), "만료된 효과의 몫만 걷혀야 한다.");
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void GrantedTagSurvivesWhileAnotherEffectStillGrantsIt()
        {
            var cooldownTag = GameplayTag.Parse("Cooldown.Attack");
            var shortEffect = CreateEffect(
                GameplayEffectDurationPolicy.Duration, null, duration: 1f, grantedTags: new[] { "Cooldown.Attack" });
            var longEffect = CreateEffect(
                GameplayEffectDurationPolicy.Duration, null, duration: 5f, grantedTags: new[] { "Cooldown.Attack" });
            _runner.Apply(shortEffect);
            _runner.Apply(longEffect);

            Assert.That(_tags.GetCount(cooldownTag), Is.EqualTo(2));

            _runner.Tick(1f);

            Assert.That(
                _tags.HasTag(cooldownTag),
                Is.True,
                "같은 태그를 부여한 효과가 남아 있으면 태그가 유지되어야 한다.");
            Assert.That(_tags.GetCount(cooldownTag), Is.EqualTo(1));

            _runner.Tick(4f);

            Assert.That(_tags.HasTag(cooldownTag), Is.False);
        }

        [Test]
        public void ABlockedTagStopsTheEffectFromBeingApplied()
        {
            var cooldownEffect = CreateEffect(
                GameplayEffectDurationPolicy.Duration, null, duration: 2f, grantedTags: new[] { "Cooldown.Attack" });
            var attackEffect = CreateEffect(
                GameplayEffectDurationPolicy.Instant, Modifier(_health, -10f), blockedTags: new[] { "Cooldown" });

            _runner.Apply(cooldownEffect);

            Assert.That(_runner.CanApply(attackEffect), Is.False, "쿨다운 태그가 붙어 있는 동안에는 막혀야 한다.");
            Assert.That(_runner.Apply(attackEffect), Is.Null);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f));

            _runner.Tick(2f);

            Assert.That(_runner.CanApply(attackEffect), Is.True, "쿨다운이 끝나면 다시 쓸 수 있어야 한다.");
            _runner.Apply(attackEffect);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f));
        }

        [Test]
        public void ARequiredTagMustBePresentBeforeTheEffectApplies()
        {
            var definition = CreateEffect(
                GameplayEffectDurationPolicy.Instant, Modifier(_health, -10f), requiredTags: new[] { "State.Marked" });

            Assert.That(_runner.Apply(definition), Is.Null);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f));

            _tags.AddTag(GameplayTag.Parse("State.Marked"));
            _runner.Apply(definition);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f));
        }

        [Test]
        public void PeriodicEffectAccumulatesOnTheBaseValueAndDoesNotRevert()
        {
            var definition = CreateEffect(
                GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 1f, period: 0.5f);

            _runner.Apply(definition);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f), "적용 순간에 한 번 실행한다.");
            Assert.That(_attributes.GetModifierCount(_health), Is.Zero, "주기 효과는 수정자를 얹지 않는다.");

            _runner.Tick(0.5f);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f));

            _runner.Tick(0.5f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(70f));
            Assert.That(_runner.ActiveEffectCount, Is.Zero, "지속 시간이 다하면 만료된다.");
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(70f), "주기로 깎은 값은 되돌아가지 않는다.");
        }

        [Test]
        public void PeriodicEffectCanSkipTheExecutionOnApplication()
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_health, -10f) },
                period: 1f);
            SetExecuteOnApplication(definition, false);
            Track(definition);

            _runner.Apply(definition);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f));

            _runner.Tick(1f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f));
        }

        [Test]
        public void ALargeTickRunsEveryPeriodThatFitsInIt()
        {
            var definition = CreateEffect(
                GameplayEffectDurationPolicy.Infinite, Modifier(_health, -10f), period: 1f);
            _runner.Apply(definition);

            _runner.Tick(3f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(60f), "적용 시 1회와 3초 동안 3회를 실행한다.");
        }

        [Test]
        public void EffectsFromOneSourceAreRemovedTogether()
        {
            var source = new object();
            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Infinite, Modifier(_health, -10f)), source);
            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Infinite, Modifier(_armor, -5f)), source);
            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Infinite, Modifier(_health, -20f)), new object());

            Assert.That(_runner.RemoveAllFrom(source), Is.EqualTo(2));

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(80f), "다른 원인의 효과는 남아야 한다.");
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(10f));
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void RemoveAllRevertsEverything()
        {
            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Infinite, Modifier(_health, -10f)));
            _runner.Apply(CreateEffect(
                GameplayEffectDurationPolicy.Infinite, null, grantedTags: new[] { "State.Slowed" }));

            Assert.That(_runner.RemoveAll(), Is.EqualTo(2));

            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(100f));
            Assert.That(_tags.HasTag(GameplayTag.Parse("State.Slowed")), Is.False);
        }

        [Test]
        public void ChangesAreAnnouncedForApplicationExecutionAndRemoval()
        {
            var changes = new List<GameplayEffectChange>();
            using var subscription = _runner.Changed.Subscribe(changes.Add);

            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Instant, Modifier(_health, -10f)));
            _runner.Apply(CreateEffect(GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 1f));
            _runner.Tick(1f);

            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.Executed));
            Assert.That(changes[0].Effect, Is.Null, "즉시 효과의 실행에는 유지되는 기록이 없다.");
            Assert.That(changes[1].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.Applied));
            Assert.That(changes[2].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.Removed));
        }

        [Test]
        public void TickIgnoresNonPositiveTime()
        {
            var definition = CreateEffect(GameplayEffectDurationPolicy.Duration, Modifier(_health, -10f), duration: 1f);
            var effect = _runner.Apply(definition);

            _runner.Tick(0f);
            _runner.Tick(-5f);

            Assert.That(effect.RemainingTime, Is.EqualTo(1f));
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void ApplyingNullDoesNothing()
        {
            Assert.That(_runner.Apply(null), Is.Null);
            Assert.That(_runner.CanApply(null), Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
        }

        /// <summary>지정한 어트리뷰트를 바꾸는 수정자 항목을 만든다.</summary>
        /// <param name="attribute">바꿀 어트리뷰트이다.</param>
        /// <param name="magnitude">수정자의 크기이다.</param>
        /// <param name="operation">값을 바꾸는 방식이다.</param>
        private static GameplayEffectModifier Modifier(
            AttributeDefinition attribute,
            float magnitude,
            AttributeModifierOperation operation = AttributeModifierOperation.Add)
        {
            return GameplayEffectModifier.CreateRuntime(attribute, operation, magnitude);
        }

        /// <summary>수정자 하나를 가진 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition CreateEffect(
            GameplayEffectDurationPolicy durationPolicy,
            GameplayEffectModifier modifier,
            float duration = 0f,
            float period = 0f,
            IEnumerable<string> grantedTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null)
        {
            return CreateEffectWithModifiers(
                durationPolicy,
                modifier == null ? null : new[] { modifier },
                duration,
                period,
                grantedTags,
                requiredTags,
                blockedTags);
        }

        /// <summary>수정자 목록을 가진 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition CreateEffectWithModifiers(
            GameplayEffectDurationPolicy durationPolicy,
            IEnumerable<GameplayEffectModifier> modifiers,
            float duration = 0f,
            float period = 0f,
            IEnumerable<string> grantedTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null)
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                durationPolicy, modifiers, duration, period, grantedTags, requiredTags, blockedTags);
            return Track(definition);
        }

        /// <summary>테스트용 어트리뷰트 정의를 만든다.</summary>
        /// <param name="id">어트리뷰트 식별자이다.</param>
        /// <param name="defaultBaseValue">시작 기본값이다.</param>
        private AttributeDefinition CreateAttribute(string id, float defaultBaseValue)
        {
            return Track(AttributeDefinition.CreateRuntime(id, defaultBaseValue));
        }

        /// <summary>만든 에셋을 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 에셋이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>
        /// 적용 순간 실행 여부를 직렬화 필드에 직접 넣는다.
        /// 인스펙터에서만 지정하는 값이라 조립 메서드에 없으므로 직렬화 경로를 그대로 태운다.
        /// </summary>
        /// <param name="definition">설정할 효과 정의이다.</param>
        /// <param name="value">적용 순간에 실행할지 여부이다.</param>
        private static void SetExecuteOnApplication(GameplayEffectDefinition definition, bool value)
        {
            var serializedDefinition = new UnityEditor.SerializedObject(definition);
            serializedDefinition.FindProperty("executeOnApplication").boolValue = value;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
