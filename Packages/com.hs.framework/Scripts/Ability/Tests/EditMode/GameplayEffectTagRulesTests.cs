using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>효과 태그로 다른 효과를 걷어내고 막는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 정화가 디버프 계열을 걷어내고 무적이 피해 계열을 막는 것이 이 형태이다. 대상의 상태 태그와 달리
    /// 효과 자체를 가리키므로 상대 효과가 어떤 상태 태그를 쓰는지 몰라도 계열 이름 하나로 표현할 수 있어야 한다.
    /// </remarks>
    public sealed class GameplayEffectTagRulesTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _speed;
        private AttributeDefinition _health;
        private AttributeSet _attributes;
        private GameplayTagContainer _tags;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _speed = Track(AttributeDefinition.CreateRuntime("Speed", 10f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_speed);
            _attributes.AddAttribute(_health);
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
        public void ApplyingAnEffectRemovesEffectsWhoseTagsMatch()
        {
            var slow = CreateDebuff("Effect.Debuff.Slow");
            var poison = CreateDebuff("Effect.Debuff.Poison");
            var buff = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, 5f) },
                assetTags: new[] { "Effect.Buff.Haste" }));
            _runner.Apply(slow);
            _runner.Apply(poison);
            _runner.Apply(buff);
            var cleanse = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant, removeEffectsWithTags: new[] { "Effect.Debuff" }));

            _runner.Apply(cleanse);

            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1), "디버프 계열만 걷혀야 한다.");
            Assert.That(_runner.ActiveEffects[0].Definition, Is.SameAs(buff));
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(15f).Within(0.001f), "걷힌 디버프의 수정자가 되돌아가야 한다.");
        }

        [Test]
        public void RemovedEffectsAreAnnouncedAsRemoved()
        {
            var slow = CreateDebuff("Effect.Debuff.Slow");
            _runner.Apply(slow);
            var changes = new List<GameplayEffectChange>();
            using var subscription = _runner.Changed.Subscribe(changes.Add);

            _runner.RemoveAllWithTags(new[] { GameplayTag.Parse("Effect.Debuff") });

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.Removed));
            Assert.That(changes[0].Definition, Is.SameAs(slow));
        }

        [Test]
        public void RemovalPatternsFollowTheTagHierarchyButNotPrefixes()
        {
            var slow = CreateDebuff("Effect.Debuff.Slow");
            var lookalike = CreateDebuff("Effect.Debuffed");
            _runner.Apply(slow);
            _runner.Apply(lookalike);

            var removedCount = _runner.RemoveAllWithTags(new[] { GameplayTag.Parse("Effect.Debuff") });

            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(_runner.ActiveEffects[0].Definition, Is.SameAs(lookalike), "이름이 접두사로만 겹치는 효과는 계열이 아니다.");
        }

        [Test]
        public void AnActiveEffectGrantsImmunityToEffectsWhoseTagsMatch()
        {
            var invulnerable = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration, duration: 2f, immunityTags: new[] { "Effect.Damage" }));
            var damage = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -30f) },
                assetTags: new[] { "Effect.Damage.Fire" }));
            _runner.Apply(invulnerable);

            Assert.That(_runner.IsImmuneTo(damage), Is.True);
            Assert.That(_runner.CanApply(damage), Is.False);
            Assert.That(_runner.Apply(damage), Is.Null);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f).Within(0.001f), "면역인 동안 피해는 닿지 않아야 한다.");

            _runner.Tick(2f);

            Assert.That(_runner.IsImmuneTo(damage), Is.False);
            _runner.Apply(damage);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(70f).Within(0.001f), "면역이 걷히면 피해가 다시 닿아야 한다.");
        }

        [Test]
        public void ImmunityDoesNotTouchEffectsWithoutMatchingTags()
        {
            var invulnerable = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, immunityTags: new[] { "Effect.Damage" }));
            var untagged = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -30f) }));
            var heal = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, 10f) },
                assetTags: new[] { "Effect.Heal" }));
            _runner.Apply(invulnerable);

            _runner.Apply(untagged);
            _runner.Apply(heal);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f).Within(0.001f), "태그가 맞지 않는 효과는 면역과 무관하다.");
        }

        [Test]
        public void HasActiveEffectWithTagsFollowsTheHierarchy()
        {
            _runner.Apply(CreateDebuff("Effect.Debuff.Slow"));

            Assert.That(_runner.HasActiveEffectWithTags(new[] { GameplayTag.Parse("Effect.Debuff") }), Is.True);
            Assert.That(_runner.HasActiveEffectWithTags(new[] { GameplayTag.Parse("Effect.Buff") }), Is.False);
        }

        [Test]
        public void ValidationRejectsImmunityOnAnInstantEffectAndMalformedRuleTags()
        {
            var instantImmunity = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant, immunityTags: new[] { "Effect.Damage" }));
            var malformed = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, removeEffectsWithTags: new[] { "Effect..Debuff" }));

            Assert.That(instantImmunity.TryValidate(out var immunityError), Is.False);
            Assert.That(immunityError, Does.Contain("면역"));
            Assert.That(malformed.TryValidate(out var malformedError), Is.False);
            Assert.That(malformedError, Is.Not.Null);
        }

        /// <summary>속도를 깎는 무한 디버프 정의를 지정한 효과 태그로 만든다.</summary>
        private GameplayEffectDefinition CreateDebuff(string assetTagName)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, -3f) },
                assetTags: new[] { assetTagName }));
        }

        /// <summary>만든 에셋을 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 에셋이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
