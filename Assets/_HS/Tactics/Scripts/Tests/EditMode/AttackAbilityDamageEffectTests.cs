using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 사격 피해가 피해 효과로 대상의 어트리뷰트에 닿고, 그 길에서도 가로채기와 사망 알림이 그대로인지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 피해가 효과로 가면 쏜 쪽은 대상의 체력을 직접 깎지 않는다. 효과가 체력 어트리뷰트를 바꾸고 체력 문이 그 변화에서
    /// 가로채기와 알림을 처리한다. 여기서는 그 길 전체를 실제 구성요소로 세워 누가 쓰러뜨렸는지까지 확인한다.
    /// </para>
    /// <para>
    /// 효과 실행기가 없는 대상(피해 창구만 있는 소품)에는 효과를 쓸 수 없으므로 피해 창구로 직접 준다.
    /// 그 구분이 지켜지는지도 함께 본다.
    /// </para>
    /// </remarks>
    public sealed class AttackAbilityDamageEffectTests
    {
        private static readonly GameplayTag AttackTag = GameplayTag.Parse(UnitAbilityTags.Attack);

        /// <summary>테스트가 만든 오브젝트와 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _maxHealth;
        private AttributeDefinition _health;
        private AttributeDefinition _attackPower;
        private DamageEffectDefinition _damageEffect;
        private GameObject _attackerObject;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestPublisher<DeathEvent> _deathPublisher;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
            _attackPower = Track(AttributeDefinition.CreateRuntime("AttackPower", 10f));
            _damageEffect = Track(DamageEffectDefinition.CreateRuntime(_health));
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();

            _attackerObject = Track(new GameObject("Attacker"));
            var unit = _attackerObject.AddComponent<TacticalUnit>();
            // 명중률 1은 굴림과 무관하게 언제나 맞는다. 검사는 굴림이 아니라 확률로 결과를 정한다.
            unit.SetDefinition(Track(UnitDefinition.CreateRuntime(
                "소총병", 100, default, 3.5f, attackRange: 10f, attackDamage: 25, attackInterval: 1f,
                baseHitChance: 1f)));
            unit.InitializeUnit();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }

            _disposables.Clear();
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
        public void AHitAppliesTheDamageEffectThroughTheTargetsAttributes()
        {
            var target = CreateAttributeTarget();
            var system = CreateAbilitySystem(target.transform, _damageEffect);

            Assert.That(system.TryActivate(AttackTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(target.CurrentHealth, Is.EqualTo(75), "명중한 피해량이 효과를 지나 체력 어트리뷰트를 깎아야 한다.");
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(_damagePublisher.Last.Instigator, Is.SameAs(_attackerObject), "누가 쏘았는지가 효과의 사정에서 알림까지 따라와야 한다.");
        }

        [Test]
        public void ALethalHitAnnouncesDeathWithTheAttackerAsInstigator()
        {
            var target = CreateAttributeTarget();
            target.ApplyDamage(80, null);
            var system = CreateAbilitySystem(target.transform, _damageEffect);

            system.TryActivate(AttackTag);

            Assert.That(target.IsDead, Is.True);
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1), "효과로 0에 닿은 사망도 알려야 승패가 난다.");
            Assert.That(_deathPublisher.Last.Instigator, Is.SameAs(_attackerObject));
        }

        [Test]
        public void AnInterceptorStillTakesItsShareOfDamageThatArrivesAsAnEffect()
        {
            var target = CreateAttributeTarget();
            target.gameObject.AddComponent<FixedShareInterceptor>().Takes = 10;
            var system = CreateAbilitySystem(target.transform, _damageEffect);

            system.TryActivate(AttackTag);

            Assert.That(target.CurrentHealth, Is.EqualTo(85), "효과로 온 피해도 같은 문에서 가로채여야 한다.");
        }

        [Test]
        public void WithoutADamageEffectTheAbilityUsesTheDamageableDirectly()
        {
            var target = CreateAttributeTarget();
            var system = CreateAbilitySystem(target.transform, damageEffect: null);

            system.TryActivate(AttackTag);

            Assert.That(target.CurrentHealth, Is.EqualTo(75));
            Assert.That(_damagePublisher.Last.Instigator, Is.SameAs(_attackerObject));
        }

        [Test]
        public void ATargetWithoutAnEffectRunnerIsDamagedDirectlyEvenWhenAnEffectIsWired()
        {
            var targetObject = Track(new GameObject("Prop"));
            targetObject.transform.position = new Vector3(5f, 0f, 0f);
            var prop = targetObject.AddComponent<FakeDamageable>();
            var system = CreateAbilitySystem(targetObject.transform, _damageEffect);

            system.TryActivate(AttackTag);

            Assert.That(prop.TotalDamage, Is.EqualTo(25), "효과 실행기가 없는 대상에는 피해 창구로 직접 주어야 한다.");
        }

        [Test]
        public void TheAttackPowerAttributeOverridesTheDefinitionDamage()
        {
            var target = CreateAttributeTarget();
            var system = CreateAbilitySystem(target.transform, _damageEffect, _attackPower);
            system.Attributes.AddAttribute(_attackPower, 40f);

            system.TryActivate(AttackTag);

            Assert.That(target.CurrentHealth, Is.EqualTo(60), "소유자가 공격력 어트리뷰트를 가지면 피해량은 거기서 온다.");
        }

        [Test]
        public void WithoutTheAttackPowerAttributeTheDefinitionDamageIsUsed()
        {
            var target = CreateAttributeTarget();
            var system = CreateAbilitySystem(target.transform, _damageEffect, _attackPower);

            system.TryActivate(AttackTag);

            Assert.That(target.CurrentHealth, Is.EqualTo(75));
        }

        [Test]
        public void DamageAndHealDefinitionsValidateTheirShape()
        {
            Assert.That(_damageEffect.TryValidate(out _), Is.True);
            Assert.That(Track(DamageEffectDefinition.CreateRuntime(null)).TryValidate(out var missingError), Is.False);
            Assert.That(missingError, Is.Not.Null);

            var uncapped = Track(AttributeDefinition.CreateRuntime("Uncapped", 100f));
            Assert.That(Track(HealEffectDefinition.CreateRuntime(uncapped)).TryValidate(out var uncappedError), Is.False);
            Assert.That(uncappedError, Does.Contain("상한"));
            Assert.That(Track(HealEffectDefinition.CreateRuntime(_health)).TryValidate(out _), Is.True);
        }

        [Test]
        public void AHealEffectCannotRaiseHealthAboveTheCap()
        {
            var target = CreateAttributeTarget();
            target.ApplyDamage(30, null);
            var heal = Track(HealEffectDefinition.CreateRuntime(_health, 500f));

            target.GetComponent<GameplayEffectComponent>().ApplyEffect(heal);

            Assert.That(target.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void AnAttackDefinitionWithABrokenDamageEffectIsRejectedAtGrant()
        {
            var broken = Track(DamageEffectDefinition.CreateRuntime(null));
            var definition = Track(AttackAbilityDefinition.CreateRuntime(UnitAbilityTags.Attack, broken));
            using var attributes = new AttributeSet();
            using var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes), _attackerObject);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("GameplayAbilitySystem"));

            Assert.That(system.GrantAbility(definition), Is.Null);
        }

        /// <summary>어트리뷰트 위의 체력 문과 효과 실행기를 갖춘 대상을 사거리 안에 세운다.</summary>
        private HealthAttributeComponent CreateAttributeTarget()
        {
            var targetObject = Track(new GameObject("Target"));
            targetObject.transform.position = new Vector3(5f, 0f, 0f);
            var health = targetObject.AddComponent<HealthAttributeComponent>();
            targetObject.AddComponent<GameplayEffectComponent>();
            var attributes = targetObject.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_maxHealth);
            attributes.AddAttribute(_health);
            health.ConfigureAttributes(_health, _maxHealth);
            health.InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
            return health;
        }

        /// <summary>공격자에게 사격 어빌리티를 부여한 시스템을 만들고 대상을 가리키게 한다.</summary>
        private GameplayAbilitySystem CreateAbilitySystem(
            Transform target,
            DamageEffectDefinition damageEffect,
            AttributeDefinition attackPower = null)
        {
            _attackerObject.AddComponent<FakeTargetSource>().CurrentTarget = target;
            var attributes = new AttributeSet();
            var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes), _attackerObject);
            _disposables.Add(system);
            _disposables.Add(attributes);
            var definition = Track(AttackAbilityDefinition.CreateRuntime(UnitAbilityTags.Attack, damageEffect, attackPower));
            Assert.That(system.GrantAbility(definition), Is.Not.Null);
            return system;
        }

        /// <summary>검사가 만든 시스템과 집합이며 정리 대상이다.</summary>
        private readonly List<System.IDisposable> _disposables = new();

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>대상을 직접 지정할 수 있는 테스트용 대상 공급자이다.</summary>
        private sealed class FakeTargetSource : MonoBehaviour, ICombatTargetSource
        {
            /// <inheritdoc />
            public Transform CurrentTarget { get; set; }
        }

        /// <summary>받은 피해를 세는 테스트용 피해 대상이다.</summary>
        private sealed class FakeDamageable : MonoBehaviour, IDamageable
        {
            /// <summary>지금까지 받은 피해의 합이다.</summary>
            public int TotalDamage { get; private set; }

            /// <inheritdoc />
            public int MaxHealth => 100;

            /// <inheritdoc />
            public int CurrentHealth => Mathf.Max(0, MaxHealth - TotalDamage);

            /// <inheritdoc />
            public bool IsDead => false;

            /// <inheritdoc />
            public void ApplyDamage(int amount, GameObject instigator)
            {
                TotalDamage += amount;
            }
        }

        /// <summary>정해진 양만큼 가로채는 테스트용 가로채기이다.</summary>
        private sealed class FixedShareInterceptor : MonoBehaviour, IDamageInterceptor
        {
            /// <summary>가로챌 양이다.</summary>
            public int Takes { get; set; }

            /// <inheritdoc />
            public int InterceptDamage(int amount, GameObject instigator)
            {
                return Takes;
            }
        }
    }
}
