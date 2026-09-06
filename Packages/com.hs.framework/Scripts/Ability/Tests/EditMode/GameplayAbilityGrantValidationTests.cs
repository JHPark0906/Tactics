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
    /// <summary>
    /// 잘못 만들어진 어빌리티 정의가 부여되는 자리에서 거부되는지 검증한다.
    /// </summary>
    /// <remarks>
    /// 지속 효과를 코스트로 쓰거나 태그 없는 효과를 쿨다운으로 쓰는 정의는 부여 시 거부한다.
    /// 대상에 없는 어트리뷰트를 소모하는 코스트는 활성화 판정에서 거부하는지 검증한다.
    /// </remarks>
    public sealed class GameplayAbilityGrantValidationTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        /// <summary>코스트로 소모할 자원 어트리뷰트이다.</summary>
        private AttributeDefinition _stamina;

        /// <summary>대상의 어트리뷰트 집합이다.</summary>
        private AttributeSet _attributes;

        /// <summary>검증 대상 어빌리티 시스템이다.</summary>
        private GameplayAbilitySystem _system;

        [SetUp]
        public void SetUp()
        {
            _stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_stamina);
            _system = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes));
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
        public void ADurationCostIsRejectedAtGrant()
        {
            var lingeringCost = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[] { GameplayEffectModifier.CreateRuntime(_stamina, AttributeModifierOperation.Add, -30f) },
                duration: 1f));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Sprint", cost: lingeringCost));
            ExpectGrantError();

            Assert.That(_system.GrantAbility(definition), Is.Null);

            Assert.That(
                _system.IsGranted(definition.AbilityTag),
                Is.False,
                "지속 효과를 코스트로 들이면 만료 때 자원이 되돌아와 어빌리티가 공짜가 된다.");
        }

        [Test]
        public void ATaglessCooldownIsRejectedAtGrant()
        {
            var silentCooldown = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration, duration: 2f));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Attack", cooldown: silentCooldown));
            ExpectGrantError();

            Assert.That(_system.GrantAbility(definition), Is.Null);

            Assert.That(
                _system.IsGranted(definition.AbilityTag),
                Is.False,
                "태그 없는 쿨다운은 한 번도 막지 않으므로 들이면 안 된다.");
        }

        [Test]
        public void ACostEffectWithABrokenModifierIsRejectedAtGrant()
        {
            var brokenCost = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(null, AttributeModifierOperation.Add, -30f) }));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Sprint", cost: brokenCost));
            ExpectGrantError();

            Assert.That(_system.GrantAbility(definition), Is.Null);
        }

        [Test]
        public void AnInvalidAbilityTagIsRejectedAtGrant()
        {
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability..Broken"));
            ExpectGrantError();

            Assert.That(_system.GrantAbility(definition), Is.Null);
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
        }

        [Test]
        public void AWellFormedAbilityIsStillGranted()
        {
            var cost = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_stamina, AttributeModifierOperation.Add, -30f) }));
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration, duration: 2f, grantedTags: new[] { "Cooldown.Sprint" }));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Sprint", cost: cost, cooldown: cooldown));

            Assert.That(_system.GrantAbility(definition), Is.Not.Null);
            Assert.That(_system.IsGranted(definition.AbilityTag), Is.True);
        }

        [Test]
        public void ACostOnAnAttributeTheTargetLacksCannotBeAfforded()
        {
            var mana = Track(AttributeDefinition.CreateRuntime("Mana", 100f));
            var manaCost = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(mana, AttributeModifierOperation.Add, -10f) }));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Cast", cost: manaCost));
            _system.GrantAbility(definition);

            Assert.That(
                _system.TryActivate(definition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.CostNotAffordable),
                "없는 자원은 모자란 자원이다. 통과시키면 코스트가 아무것도 깎지 않은 채 어빌리티가 돈다.");

            _attributes.AddAttribute(mana);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_attributes.GetBaseValue(mana), Is.EqualTo(90f));
        }

        [Test]
        public void AnInvalidSetGrantsNothingAtAll()
        {
            var mana = Track(AttributeDefinition.CreateRuntime("Mana", 0f));
            var healthy = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Attack"));
            var broken = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability..Broken"));
            var set = Track(GameplayAbilitySet.CreateRuntime(
                new GameplayAbilityDefinition[] { healthy, broken },
                new[] { new GameplayAbilitySet.StartingAttribute(mana, 40f) },
                new[] { "Unit.Infantry" }));
            LogAssert.Expect(LogType.Error, new Regex("GameplayAbilitySet"));

            Assert.That(set.GrantTo(_system), Is.Zero);

            Assert.That(_system.IsGranted(healthy.AbilityTag), Is.False, "집합은 한 벌로 작성된 것이므로 한 벌로 거부한다.");
            Assert.That(_attributes.Contains(mana), Is.False, "어트리뷰트도 태그도 반쯤 들어가 있으면 안 된다.");
            Assert.That(_system.Tags.HasTag(GameplayTag.Parse("Unit.Infantry")), Is.False);
        }

        /// <summary>부여 거부가 남기는 오류 로그를 기대한다.</summary>
        private static void ExpectGrantError()
        {
            LogAssert.Expect(LogType.Error, new Regex("GameplayAbilitySystem"));
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
