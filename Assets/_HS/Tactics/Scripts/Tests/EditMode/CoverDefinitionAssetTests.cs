using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐물 관련 정의 에셋(흡수 확률 어트리뷰트, 엄폐물 묶음과 집합, 부서짐과 회피 어빌리티, 태그 카탈로그)이
    /// 코드와 같은 이름을 보고 있고 검증을 통과하는지 확인한다.
    /// </summary>
    public sealed class CoverDefinitionAssetTests
    {
        private const string Root = "Assets/_HS/Tactics/Definitions/";

        private readonly List<System.IDisposable> _disposables = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();
        }

        [Test]
        public void TheTagCatalogCoversTheCoverNames()
        {
            var catalog = Load<GameplayTagCatalog>("TacticsTagCatalog");

            Assert.That(catalog.TryValidate(out var errorMessage), Is.True, errorMessage);
            foreach (var tagName in new[]
                     {
                         UnitAbilityTags.CoverDeath,
                         UnitAbilityTags.CoverEvasion,
                         UnitAbilityTags.InCoverState,
                         DamageEffectDefinition.AssetTagName
                     })
            {
                Assert.That(catalog.Contains(GameplayTag.Parse(tagName)), Is.True, $"카탈로그에 코드가 쓰는 '{tagName}'이 없다.");
            }
        }

        [Test]
        public void TheAbsorbChanceAssetMatchesTheCodeConstantAndStaysAProbability()
        {
            var absorbChance = Load<AttributeDefinition>("Attributes/CoverAbsorbChance");

            Assert.That(absorbChance.Id, Is.EqualTo(UnitAttributeIds.CoverAbsorbChance), "엄폐 지점이 이름으로 찾는 정의이므로 식별자가 상수와 같아야 한다.");
            Assert.That(absorbChance.MinValue, Is.Zero);
        }

        [Test]
        public void TheCoverAttributeSetCarriesTheAbsorbChance()
        {
            var cover = Load<AttributeSetDefinition>("Attributes/CoverAttributes");

            Assert.That(cover.TryValidate(out var errorMessage), Is.True, errorMessage);
            Assert.That(cover.Contains(Load<AttributeDefinition>("Attributes/CoverAbsorbChance")), Is.True, "흡수 확률이 묶음에 없으면 엄폐물이 대신 맞아 주지 않는다.");
        }

        [Test]
        public void TheCoverAbilitySetGrantsTheDeathAbilityWithItsAttributes()
        {
            var set = Load<GameplayAbilitySet>("Abilities/CoverAbilities");
            Assert.That(set.TryValidate(out var errorMessage), Is.True, errorMessage);

            var attributes = new AttributeSet();
            var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes));
            _disposables.Add(system);
            _disposables.Add(attributes);

            set.GrantTo(system);

            Assert.That(system.IsGranted(GameplayTag.Parse(UnitAbilityTags.CoverDeath)), Is.True, "엄폐물 집합이 부서짐 어빌리티를 부여하지 않는다.");
            Assert.That(attributes.Contains(Load<AttributeDefinition>("Attributes/Health")), Is.True);
            Assert.That(attributes.Contains(Load<AttributeDefinition>("Attributes/CoverAbsorbChance")), Is.True);
        }

        [Test]
        public void TheCoverDeathAbilityAssetCarriesTheRulesTheCodeReliesOn()
        {
            var death = Load<DeathAbilityDefinition>("Abilities/CoverDeathAbility");

            Assert.That(death.TryValidate(out var errorMessage), Is.True, errorMessage);
            Assert.That(death.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.CoverDeath)));
            Assert.That(death.TriggerEventTags, Does.Contain(GameplayTag.Parse(HealthAttributeComponent.DefaultDeathEventTagName)));
            Assert.That(death.CancelAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
            Assert.That(death.BlockAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
        }

        [Test]
        public void TheCoverEvasionAbilityAssetIsTriggeredByTheInCoverTagAndWiresTheDamageEffect()
        {
            var evasion = Load<CoverEvasionAbilityDefinition>("Abilities/CoverEvasionAbility");

            Assert.That(evasion.TryValidate(out var errorMessage), Is.True, errorMessage);
            Assert.That(evasion.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.CoverEvasion)));
            Assert.That(evasion.TriggerWhileTagPresent, Does.Contain(GameplayTag.Parse(UnitAbilityTags.InCoverState)), "엄폐 중 태그가 회피를 켜고 끈다.");
            Assert.That(evasion.DamageEffect, Is.SameAs(Load<DamageEffectDefinition>("Effects/DamageEffect")), "엄폐물에 넘기는 피해는 사격과 같은 피해 효과여야 한다.");
        }

        [Test]
        public void TheUnitAbilitySetsGrantTheEvasion()
        {
            foreach (var setName in new[] { "HeroAbilities", "EnemyAbilities" })
            {
                var set = Load<GameplayAbilitySet>($"Abilities/{setName}");
                Assert.That(set.TryValidate(out var errorMessage), Is.True, $"{setName}: {errorMessage}");

                var attributes = new AttributeSet();
                var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes));
                _disposables.Add(system);
                _disposables.Add(attributes);

                set.GrantTo(system);

                Assert.That(system.IsGranted(GameplayTag.Parse(UnitAbilityTags.CoverEvasion)), Is.True, $"{setName}이 회피를 부여하지 않는다.");
            }
        }

        [Test]
        public void TheDamageEffectAssetCarriesTheAssetTagImmunityLooksFor()
        {
            var damage = Load<DamageEffectDefinition>("Effects/DamageEffect");

            Assert.That(damage.AssetTags, Does.Contain(GameplayTag.Parse(DamageEffectDefinition.AssetTagName)), "에셋 태그가 없으면 면역과 제거가 피해 효과를 가리킬 수 없다.");
        }

        private static T Load<T>(string relativePath) where T : Object
        {
            var path = $"{Root}{relativePath}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"{path}를 {typeof(T).Name}으로 읽지 못했다.");
            return asset;
        }
    }
}
