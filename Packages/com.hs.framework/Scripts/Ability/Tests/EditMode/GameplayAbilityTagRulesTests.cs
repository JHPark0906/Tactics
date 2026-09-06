using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>어빌리티 태그로 다른 어빌리티를 취소하고 차단하는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 대상의 상태 태그로 표현하는 배타(활성 태그와 차단 태그)와 달리, 이쪽은 어빌리티 자체의 태그를 견준다.
    /// "이동 계열을 전부 끊는다"처럼 상대가 어떤 상태 태그를 쓰는지 몰라도 계열 이름 하나로 표현할 수 있어야 한다.
    /// </remarks>
    public sealed class GameplayAbilityTagRulesTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayAbilitySystem _system;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _system = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes));
        }

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
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
        public void TheIdentityTagIsAlwaysTheFirstAbilityTag()
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime(
                "Ability.Walk", assetTags: new[] { "Ability.Movement" }));

            Assert.That(definition.AssetTags.Count, Is.EqualTo(2));
            Assert.That(definition.AssetTags[0], Is.EqualTo(GameplayTag.Parse("Ability.Walk")));
            Assert.That(definition.AssetTags[1], Is.EqualTo(GameplayTag.Parse("Ability.Movement")));
        }

        [Test]
        public void ActivatingCancelsAbilitiesWhoseTagsMatch()
        {
            var walk = new RunningAbility();
            var walkDefinition = Grant("Ability.Walk", walk, assetTags: new[] { "Ability.Movement" });
            var stun = new RunningAbility();
            var stunDefinition = Grant("Ability.Stun", stun, cancelAbilitiesWithTags: new[] { "Ability.Movement" });
            _system.TryActivate(walkDefinition.AbilityTag);

            Assert.That(_system.TryActivate(stunDefinition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(walk.IsActive, Is.False, "취소 태그에 맞는 어빌리티는 활성화하는 순간 밀려나야 한다.");
            Assert.That(walk.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Cancelled));
            Assert.That(stun.IsActive, Is.True);
        }

        [Test]
        public void CancelPatternsFollowTheTagHierarchy()
        {
            var walk = new RunningAbility();
            var walkDefinition = Grant("Ability.Movement.Walk", walk);
            var lookalike = new RunningAbility();
            var lookalikeDefinition = Grant("AbilityMovement.Walk", lookalike);
            var stunDefinition = Grant("Ability.Stun", cancelAbilitiesWithTags: new[] { "Ability.Movement" });
            _system.TryActivate(walkDefinition.AbilityTag);
            _system.TryActivate(lookalikeDefinition.AbilityTag);

            _system.TryActivate(stunDefinition.AbilityTag);

            Assert.That(walk.IsActive, Is.False, "상위 이름 하나로 계열 전체를 가리켜야 한다.");
            Assert.That(lookalike.IsActive, Is.True, "이름이 접두사로만 겹치는 어빌리티는 계열이 아니다.");
        }

        [Test]
        public void AnActiveAbilityBlocksAbilitiesWhoseTagsMatch()
        {
            var deathDefinition = Grant("Ability.Death", blockAbilitiesWithTags: new[] { "Ability" });
            var attackDefinition = Grant("Ability.Attack");
            _system.TryActivate(deathDefinition.AbilityTag);

            Assert.That(
                _system.TryActivate(attackDefinition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.BlockedByActiveAbility),
                "차단 태그에 맞는 어빌리티는 차단하는 쪽이 살아 있는 동안 활성화되지 않아야 한다.");
            Assert.That(_system.IsBlockedByActiveAbility(attackDefinition), Is.True);

            _system.CancelAbility(deathDefinition.AbilityTag);

            Assert.That(_system.IsBlockedByActiveAbility(attackDefinition), Is.False);
            Assert.That(_system.TryActivate(attackDefinition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void BlockingIsCountedAcrossTwoBlockers()
        {
            var first = Grant("Ability.Freeze", blockAbilitiesWithTags: new[] { "Ability.Movement" });
            var second = Grant("Ability.Root", blockAbilitiesWithTags: new[] { "Ability.Movement" });
            var walk = Grant("Ability.Movement.Walk");
            _system.TryActivate(first.AbilityTag);
            _system.TryActivate(second.AbilityTag);

            _system.CancelAbility(first.AbilityTag);
            Assert.That(
                _system.TryActivate(walk.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.BlockedByActiveAbility),
                "차단하는 어빌리티 둘 중 하나가 끝났다고 다른 하나의 차단까지 풀리면 안 된다.");

            _system.CancelAbility(second.AbilityTag);

            Assert.That(_system.TryActivate(walk.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void ABlockerCanBeReactivatedAfterItEndsEvenIfItsPatternCoversItself()
        {
            var death = new RunningAbility();
            var deathDefinition = Grant("Ability.Death", death, blockAbilitiesWithTags: new[] { "Ability" });
            _system.TryActivate(deathDefinition.AbilityTag);

            Assert.That(_system.TryActivate(deathDefinition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.AlreadyActive));

            _system.CancelAbility(deathDefinition.AbilityTag);

            Assert.That(
                _system.TryActivate(deathDefinition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.Success),
                "차단은 활성 중에만 걸리므로 끝난 뒤에는 자기 계열을 덮는 패턴이라도 스스로를 막지 않는다.");
        }

        [Test]
        public void CallersCanCancelByAbilityTagDirectly()
        {
            var walk = new RunningAbility();
            var walkDefinition = Grant("Ability.Movement.Walk", walk);
            var attack = new RunningAbility();
            var attackDefinition = Grant("Ability.Attack", attack);
            _system.TryActivate(walkDefinition.AbilityTag);
            _system.TryActivate(attackDefinition.AbilityTag);

            var cancelledCount = _system.CancelAbilitiesWithTags(new[] { GameplayTag.Parse("Ability.Movement") });

            Assert.That(cancelledCount, Is.EqualTo(1));
            Assert.That(walk.IsActive, Is.False);
            Assert.That(attack.IsActive, Is.True);
        }

        [Test]
        public void AMalformedRuleTagIsRejectedAtGrant()
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime(
                "Ability.Stun", cancelAbilitiesWithTags: new[] { "Ability..Movement" }));
            LogAssert.Expect(LogType.Error, new Regex("GameplayAbilitySystem"));

            Assert.That(_system.GrantAbility(definition), Is.Null);
        }

        /// <summary>테스트용 어빌리티를 부여하고 그 정의를 돌려준다.</summary>
        private RunningAbilityDefinition Grant(
            string tagName,
            RunningAbility ability = null,
            IEnumerable<string> assetTags = null,
            IEnumerable<string> cancelAbilitiesWithTags = null,
            IEnumerable<string> blockAbilitiesWithTags = null)
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime(
                tagName,
                ability,
                assetTags: assetTags,
                cancelAbilitiesWithTags: cancelAbilitiesWithTags,
                blockAbilitiesWithTags: blockAbilitiesWithTags));
            Assert.That(_system.GrantAbility(definition), Is.Not.Null, $"{tagName} 부여에 실패했다.");
            return definition;
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
