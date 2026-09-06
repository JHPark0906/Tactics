using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 프로젝트의 정의 에셋(어트리뷰트·묶음·어빌리티·집합·효과·태그 카탈로그)이 코드와 같은 이름을 보고 있고
    /// 검증을 통과하는지 확인한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 에셋의 참조와 이름을 실제 로드 결과로 확인한다. 참조가 끊기거나 이름이 코드와 어긋나면 에디터에서는 조용히 빈 칸으로 보이고
    /// 실행 중에는 어빌리티가 안 도는 것으로만 드러난다. 그래서 에셋을 실제로 읽어 코드의 상수와 대조한다.
    /// </para>
    /// <para>
    /// 태그 카탈로그의 문서가 말하듯 코드의 상수와 카탈로그는 서로를 보완한다. 상수가 오타를 막고,
    /// 카탈로그가 데이터와 코드가 같은 목록을 보고 있는지 여기서 확인해 준다.
    /// </para>
    /// </remarks>
    public sealed class TacticsDefinitionAssetTests
    {
        [Test]
        public void ProjectSettingsPreserveTacticsInputWhenPackageDefaultsChange()
        {
            var project = HS.Framework.ProjectManagement.FrameworkProjectConfiguration.Load();
            Assert.That(project, Is.Not.Null);
            var clientSettings = new SerializedObject(project).FindProperty("clientSettings").objectReferenceValue;
            Assert.That(clientSettings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(clientSettings),
                Is.EqualTo("Assets/_HS/ProjectSettings/TacticsClientSettingsConfiguration.asset"));
            var inputActions = new SerializedObject(clientSettings).FindProperty("inputActionAsset").objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(inputActions),
                Is.EqualTo("Assets/InputSystem_Actions.inputactions"));
        }

        private const string Root = "Assets/_HS/Tactics/Definitions/";

        /// <summary>테스트가 만든 객체이며 정리 대상이다. 에셋은 파괴하지 않는다.</summary>
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
        public void TheTagCatalogIsValidAndCoversEveryNameTheCodeUses()
        {
            var catalog = Load<GameplayTagCatalog>("TacticsTagCatalog");

            Assert.That(catalog.TryValidate(out var errorMessage), Is.True, errorMessage);
            foreach (var tagName in new[]
                     {
                         UnitAbilityTags.Family,
                         UnitAbilityTags.Attack,
                         UnitAbilityTags.TakeCover,
                         UnitAbilityTags.MaintainCover,
                         UnitAbilityTags.Death,
                         AttackAbility.IntervalTagName,
                         HealthAttributeComponent.DefaultDeathEventTagName,
                         HealthAttributeComponent.DefaultDeadStateTagName,
                         DamageEffectDefinition.DamageTagName
                     })
            {
                Assert.That(catalog.Contains(GameplayTag.Parse(tagName)), Is.True, $"카탈로그에 코드가 쓰는 '{tagName}'이 없다.");
            }
        }

        [Test]
        public void TheAttributeAssetIdsMatchTheCodeConstants()
        {
            foreach (var id in new[]
                     {
                         UnitAttributeIds.Health,
                         UnitAttributeIds.MaxHealth,
                         UnitAttributeIds.AttackPower,
                         UnitAttributeIds.Level,
                         UnitAttributeIds.Xp
                     })
            {
                Assert.That(Load<AttributeDefinition>($"Attributes/{id}").Id, Is.EqualTo(id), "조립 코드가 이름으로 찾는 정의이므로 에셋의 식별자가 상수와 같아야 한다.");
            }
        }

        [Test]
        public void TheCoverAttributeSetCarriesHealthAndMaxHealth()
        {
            var cover = Load<AttributeSetDefinition>("Attributes/CoverAttributes");

            Assert.That(cover.TryValidate(out var errorMessage), Is.True, errorMessage);
            Assert.That(cover.Contains(Load<AttributeDefinition>("Attributes/Health")), Is.True);
            Assert.That(cover.Contains(Load<AttributeDefinition>("Attributes/MaxHealth")), Is.True);
        }

        [Test]
        public void HealthIsCappedByMaxHealth()
        {
            var health = Load<AttributeDefinition>("Attributes/Health");
            var maxHealth = Load<AttributeDefinition>("Attributes/MaxHealth");

            Assert.That(health.CapAttribute, Is.SameAs(maxHealth), "체력의 상한이 최대 체력이 아니면 회복이 넘치고 최대치 변화가 반영되지 않는다.");
            Assert.That(health.MinValue, Is.Zero);
            Assert.That(maxHealth.MinValue, Is.EqualTo(1f));
        }

        [Test]
        public void TheHeroAndEnemyAttributeSetsDifferOnlyInExperience()
        {
            var hero = Load<AttributeSetDefinition>("Attributes/HeroAttributes");
            var enemy = Load<AttributeSetDefinition>("Attributes/EnemyAttributes");
            var experience = Load<AttributeDefinition>("Attributes/Xp");

            Assert.That(hero.TryValidate(out var heroError), Is.True, heroError);
            Assert.That(enemy.TryValidate(out var enemyError), Is.True, enemyError);
            foreach (var shared in new[] { "Health", "MaxHealth", "AttackPower", "Level" })
            {
                var definition = Load<AttributeDefinition>($"Attributes/{shared}");
                Assert.That(hero.Contains(definition), Is.True, $"Hero 묶음에 {shared}가 없다.");
                Assert.That(enemy.Contains(definition), Is.True, $"Enemy 묶음에 {shared}가 없다.");
            }

            Assert.That(hero.Contains(experience), Is.True, "경험치는 Hero 묶음에만 있다.");
            Assert.That(enemy.Contains(experience), Is.False, "적은 경험치를 쌓지 않는다.");
        }

        [Test]
        public void TheAbilitySetsAreValidAndGrantEveryUnitAbility()
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

                foreach (var tagName in new[] { UnitAbilityTags.Attack, UnitAbilityTags.TakeCover, UnitAbilityTags.MaintainCover, UnitAbilityTags.Death })
                {
                    Assert.That(system.IsGranted(GameplayTag.Parse(tagName)), Is.True, $"{setName}이 {tagName}를 부여하지 않는다.");
                }

                Assert.That(attributes.Contains(Load<AttributeDefinition>("Attributes/Health")), Is.True, $"{setName}이 체력 어트리뷰트를 갖추지 않는다.");
            }
        }

        [Test]
        public void TheDeathAbilityAssetCarriesTheRulesTheCodeRelisOn()
        {
            var death = Load<DeathAbilityDefinition>("Abilities/DeathAbility");

            Assert.That(death.AbilityTag, Is.EqualTo(GameplayTag.Parse(UnitAbilityTags.Death)));
            Assert.That(death.TriggerEventTags, Does.Contain(GameplayTag.Parse(HealthAttributeComponent.DefaultDeathEventTagName)));
            Assert.That(death.CancelAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
            Assert.That(death.BlockAbilitiesWithTags, Does.Contain(GameplayTag.Parse(UnitAbilityTags.Family)));
        }

        [Test]
        public void TheDamageAndHealEffectsTargetTheHealthAttribute()
        {
            var health = Load<AttributeDefinition>("Attributes/Health");
            var damage = Load<DamageEffectDefinition>("Effects/DamageEffect");
            var heal = Load<HealEffectDefinition>("Effects/HealEffect");

            Assert.That(damage.TryValidate(out var damageError), Is.True, damageError);
            Assert.That(heal.TryValidate(out var healError), Is.True, healError);
            Assert.That(damage.Modifiers[0].Attribute, Is.SameAs(health));
            Assert.That(damage.Modifiers[0].SetByCallerTag, Is.EqualTo(GameplayTag.Parse(DamageEffectDefinition.DamageTagName)));
            Assert.That(heal.Modifiers[0].Attribute, Is.SameAs(health));
        }

        [Test]
        public void TheAttackAbilityAssetWiresTheDamageEffectAndAttackPower()
        {
            var attack = Load<AttackAbilityDefinition>("Abilities/AttackAbility");

            Assert.That(attack.DamageEffect, Is.SameAs(Load<DamageEffectDefinition>("Effects/DamageEffect")));
            Assert.That(attack.AttackPowerAttribute, Is.SameAs(Load<AttributeDefinition>("Attributes/AttackPower")));
            Assert.That(attack.BlockedTags, Does.Contain(GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName)));
        }

        /// <summary>정의 폴더의 에셋을 읽는다. 없으면 검사가 그 자리에서 실패한다.</summary>
        /// <typeparam name="T">에셋의 형식이다.</typeparam>
        /// <param name="relativePath">정의 폴더 아래의 경로이며 확장자는 붙이지 않는다.</param>
        private static T Load<T>(string relativePath) where T : Object
        {
            var path = $"{Root}{relativePath}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, $"{path}를 {typeof(T).Name}으로 읽지 못했다.");
            return asset;
        }
    }
}
