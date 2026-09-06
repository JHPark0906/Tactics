using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>효과 정의의 정책 해석과 검증 규칙을 확인한다.</summary>
    public sealed class GameplayEffectDefinitionTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

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
        public void InstantEffectDoesNotUseLingeringModifiers()
        {
            var definition = Create(GameplayEffectDurationPolicy.Instant);

            Assert.That(definition.IsInstant, Is.True);
            Assert.That(definition.UsesLingeringModifiers, Is.False);
        }

        [Test]
        public void DurationEffectWithoutPeriodUsesLingeringModifiers()
        {
            var definition = Create(GameplayEffectDurationPolicy.Duration, duration: 2f);

            Assert.That(definition.UsesLingeringModifiers, Is.True);
            Assert.That(definition.HasPeriod, Is.False);
            Assert.That(definition.Duration, Is.EqualTo(2f));
        }

        [Test]
        public void PeriodicEffectDoesNotUseLingeringModifiers()
        {
            var definition = Create(GameplayEffectDurationPolicy.Duration, duration: 2f, period: 0.5f);

            Assert.That(definition.HasPeriod, Is.True);
            Assert.That(
                definition.UsesLingeringModifiers,
                Is.False,
                "주기 효과는 기본값을 실행하므로 수정자를 얹으면 값이 두 번 반영된다.");
        }

        [Test]
        public void TagNamesAreResolvedAndMalformedOnesAreSkipped()
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                grantedTags: new[] { "State.Slowed", "State..Broken" },
                blockedTags: new[] { "Cooldown" });
            Track(definition);

            Assert.That(definition.GrantedTags.Count, Is.EqualTo(1));
            Assert.That(definition.GrantedTags[0].Name, Is.EqualTo("State.Slowed"));
            Assert.That(definition.BlockedTags.Count, Is.EqualTo(1));
            Assert.That(definition.RequiredTags, Is.Empty);
        }

        [Test]
        public void ValidationPassesForAWellFormedEffect()
        {
            var attribute = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[] { GameplayEffectModifier.CreateRuntime(attribute, AttributeModifierOperation.Add, -10f) },
                duration: 1f,
                grantedTags: new[] { "State.Slowed" });
            Track(definition);

            Assert.That(definition.TryValidate(out var errorMessage), Is.True);
            Assert.That(errorMessage, Is.Null);
        }

        [Test]
        public void ValidationRejectsAModifierWithoutAnAttribute()
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(null, AttributeModifierOperation.Add, -10f) });
            Track(definition);

            Assert.That(definition.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        [Test]
        public void ValidationRejectsAnInstantEffectThatGrantsTags()
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                grantedTags: new[] { "State.Slowed" });
            Track(definition);

            Assert.That(definition.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("회수"));
        }

        [Test]
        public void ValidationRejectsAnInstantEffectWithAPeriod()
        {
            var definition = Create(GameplayEffectDurationPolicy.Instant, period: 0.5f);

            Assert.That(definition.TryValidate(out _), Is.False);
        }

        [Test]
        public void ValidationRejectsAZeroLengthDurationEffect()
        {
            var definition = Create(GameplayEffectDurationPolicy.Duration, duration: 0f);

            Assert.That(definition.TryValidate(out _), Is.False);
        }

        [Test]
        public void ValidationRejectsAMalformedTagName()
        {
            var definition = GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                blockedTags: new[] { ".Cooldown" });
            Track(definition);

            Assert.That(definition.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Is.Not.Null);
        }

        /// <summary>수정자 없이 정책만 지정한 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition Create(
            GameplayEffectDurationPolicy durationPolicy,
            float duration = 0f,
            float period = 0f)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(durationPolicy, null, duration, period));
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
