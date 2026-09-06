using HS.Framework.Tests.Support;
using R3;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 어트리뷰트 위에 세운 체력 문이 어느 길로 들어온 변화에도 같은 가로채기와 알림을 내는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 피해와 사망은 어트리뷰트 변화에서 판정한다. 그래서 효과가 체력을 직접 깎아도 사망 알림이 나가고,
    /// 발행자가 없어도 체력이 0이면 죽은 것으로 다뤄진다. 알림을 <c>ApplyDamage</c> 안에서만 내면 둘 다 성립하지
    /// 않으므로 그 둘을 여기서 고정한다.
    /// </para>
    /// <para>
    /// 사망은 한 번만 알린다. 죽은 뒤 회복이 잘못 들어와 0 위로 올렸다가 다시 떨어지는 경로에서 두 번 알리면
    /// 집계가 두 번 세기 때문이다.
    /// </para>
    /// </remarks>
    public sealed class HealthAttributeComponentTests
    {
        /// <summary>테스트가 만든 에셋과 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _maxHealth;
        private AttributeDefinition _health;
        private GameObject _gameObject;
        private HealthAttributeComponent _component;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestPublisher<DeathEvent> _deathPublisher;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();
            _gameObject = Track(new GameObject("HealthAttributeComponentTests"));
            _component = CreateComponent(_gameObject, injectPublishers: true);
        }

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
        public void HealthStartsAtTheAttributeValues()
        {
            Assert.That(_component.MaxHealth, Is.EqualTo(100));
            Assert.That(_component.CurrentHealth, Is.EqualTo(100));
            Assert.That(_component.IsDead, Is.False);
        }

        [Test]
        public void ApplyDamageLowersTheAttributeAndAnnouncesTheDamage()
        {
            var instigator = Track(new GameObject("Attacker"));

            _component.ApplyDamage(30, instigator);

            Assert.That(_component.CurrentHealth, Is.EqualTo(70));
            Assert.That(Attributes.GetBaseValue(_health), Is.EqualTo(70f).Within(0.001f));
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(_damagePublisher.Last.Amount, Is.EqualTo(30));
            Assert.That(_damagePublisher.Last.RemainingHealth, Is.EqualTo(70));
            Assert.That(_damagePublisher.Last.Instigator, Is.SameAs(instigator));
        }

        [Test]
        public void AnEffectThatLowersHealthDirectlyAnnouncesDamageAndDeathToo()
        {
            var instigator = Track(new GameObject("Attacker"));
            var runner = new GameplayEffectRunner(Attributes);
            var lethal = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -100f) }));

            runner.Apply(lethal, null, new GameplayEffectContext(instigator));

            Assert.That(_component.IsDead, Is.True);
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1), "효과가 깎은 체력도 피해로 알려야 한다.");
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1), "효과로 0에 닿아도 사망을 알려야 한다. 판정이 어트리뷰트 변화에 있기 때문이다.");
            Assert.That(_deathPublisher.Last.Instigator, Is.SameAs(instigator), "누가 쓰러뜨렸는지가 효과의 사정에서 알림까지 따라와야 한다.");
        }

        [Test]
        public void DeathIsAnnouncedOnceWhenHealthReachesZero()
        {
            _component.ApplyDamage(60, null);
            _component.ApplyDamage(60, null);

            Assert.That(_component.IsDead, Is.True);
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
            Assert.That(_deathPublisher.Last.Target, Is.SameAs(_gameObject));
        }

        [Test]
        public void DamageAfterDeathIsIgnored()
        {
            _component.ApplyDamage(100, null);
            var publishedAfterDeath = _damagePublisher.Published.Count;

            _component.ApplyDamage(10, null);

            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(publishedAfterDeath));
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeathIsNotAnnouncedAgainWhileTheDeadStateTagIsHeld()
        {
            _gameObject.AddComponent<GameplayEffectComponent>();
            var tags = _gameObject.GetComponent<GameplayEffectComponent>().Runner.Tags;
            _component.ApplyDamage(100, null);
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
            tags.AddTag(GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName));

            // 죽은 뒤 회복이 잘못 들어와 0 위로 올렸다가 다시 떨어지는 경로를 흉내 낸다.
            Attributes.SetBaseValue(_health, 20f);
            Attributes.SetBaseValue(_health, 0f);

            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1), "죽은 상태 태그가 있으면 사망을 다시 세지 않는다.");
        }

        [Test]
        public void DeathIsSentToTheAbilitySystemAsAGameplayEvent()
        {
            var abilitySystem = _gameObject.AddComponent<GameplayAbilitySystemComponent>();
            var received = new List<GameplayEventData>();
            using var subscription = abilitySystem.System.Events.Subscribe(received.Add);
            var instigator = Track(new GameObject("Attacker"));

            _component.ApplyDamage(100, instigator);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].EventTag, Is.EqualTo(GameplayTag.Parse(HealthAttributeComponent.DefaultDeathEventTagName)));
            Assert.That(received[0].Payload, Is.InstanceOf<DeathEvent>());
            Assert.That(((DeathEvent)received[0].Payload).Instigator, Is.SameAs(instigator));
        }

        [Test]
        public void WithoutPublishersTheUnitStillDiesAndAnErrorIsLoggedOnce()
        {
            var silentObject = Track(new GameObject("Silent"));
            var silent = CreateComponent(silentObject, injectPublishers: false);
            var abilitySystem = silentObject.AddComponent<GameplayAbilitySystemComponent>();
            var received = new List<GameplayEventData>();
            using var subscription = abilitySystem.System.Events.Subscribe(received.Add);
            LogAssert.Expect(LogType.Error, new Regex("HealthAttributeComponent"));

            silent.ApplyDamage(100, null);
            silent.ResetHealth();
            silent.ApplyDamage(100, null);

            Assert.That(silent.IsDead, Is.True);
            Assert.That(received, Has.Count.EqualTo(2), "발행자가 없어도 사망은 어빌리티 시스템에 닿아 유닛이 물러날 수 있어야 한다.");
        }

        [Test]
        public void AnInterceptorTakesItsShareBeforeHealthChanges()
        {
            AddInterceptor(_gameObject, takes: 10);

            _component.ApplyDamage(30, null);

            Assert.That(_component.CurrentHealth, Is.EqualTo(80));
            Assert.That(_damagePublisher.Last.Amount, Is.EqualTo(20), "알리는 피해는 실제로 체력에 닿은 몫이어야 한다.");
        }

        [Test]
        public void FullyInterceptedDamageLeavesNoTraceAndAnnouncesNothing()
        {
            AddInterceptor(_gameObject, takes: 100);

            _component.ApplyDamage(30, null);

            Assert.That(_component.CurrentHealth, Is.EqualTo(100));
            Assert.That(_damagePublisher.Published, Is.Empty, "받지 않은 피해를 알리면 맞지도 않은 피해를 세게 된다.");
        }

        [Test]
        public void AnEffectArrivingBeforeAnyAccessStillPassesTheDoor()
        {
            var freshObject = Track(new GameObject("Fresh"));
            var fresh = CreateComponent(freshObject, injectPublishers: true);
            AddInterceptor(freshObject, takes: 10);
            var runner = new GameplayEffectRunner(freshObject.GetComponent<AttributeSetComponent>().Attributes);
            var damage = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -30f) }));

            // 체력 문을 한 번도 읽지 않은 채 효과가 먼저 들어온다.
            runner.Apply(damage);

            Assert.That(fresh.CurrentHealth, Is.EqualTo(80), "정의를 지정한 순간 문이 서 있어야 한다. 첫 접근까지 미루면 그 사이의 피해가 문을 건너뛴다.");
        }

        [Test]
        public void AnEffectIsInterceptedTheSameWayAsApplyDamage()
        {
            AddInterceptor(_gameObject, takes: 10);
            var runner = new GameplayEffectRunner(Attributes);
            var damage = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -30f) }));

            runner.Apply(damage);

            Assert.That(_component.CurrentHealth, Is.EqualTo(80), "어느 길로 들어온 피해든 같은 문을 지나야 한다.");
        }

        [Test]
        public void HealingIsNotIntercepted()
        {
            _component.ApplyDamage(50, null);
            AddInterceptor(_gameObject, takes: 100);

            _component.Heal(30);

            Assert.That(_component.CurrentHealth, Is.EqualTo(80), "가로채기는 줄어드는 변화에만 걸린다.");
            _component.ApplyDamage(10, null);
            Assert.That(_component.CurrentHealth, Is.EqualTo(80), "같은 구성에서 피해는 전부 가로채인다.");
        }

        [Test]
        public void HealingCannotExceedMaxHealth()
        {
            _component.ApplyDamage(30, null);

            _component.Heal(500);

            Assert.That(_component.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void SetMaxHealthRestoresOrKeepsHealthAsRequested()
        {
            _component.SetMaxHealth(250);
            Assert.That(_component.MaxHealth, Is.EqualTo(250));
            Assert.That(_component.CurrentHealth, Is.EqualTo(250));

            _component.ApplyDamage(200, null);
            _component.SetMaxHealth(40, restoreToFull: false);

            Assert.That(_component.MaxHealth, Is.EqualTo(40));
            Assert.That(_component.CurrentHealth, Is.EqualTo(40), "새 최대치를 넘는 몫은 상한이 잘라야 한다.");
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1), "최대치가 줄어 잘린 몫은 맞은 것이 아니므로 피해로 알리지 않는다.");
            Assert.That(_deathPublisher.Published, Is.Empty);
        }

        [Test]
        public void RestoringASnapshotIsNeitherDamageNorDeath()
        {
            var full = Attributes.CaptureSnapshot();
            _component.ApplyDamage(60, null);
            var wounded = Attributes.CaptureSnapshot();
            AddInterceptor(_gameObject, takes: 100);

            Attributes.RestoreSnapshot(full);
            Assert.That(_component.CurrentHealth, Is.EqualTo(100));

            Attributes.RestoreSnapshot(wounded);

            Assert.That(_component.CurrentHealth, Is.EqualTo(40), "복원은 가로채이지 않는다.");
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1), "복원은 피해가 아니므로 알리지 않는다.");
        }

        [Test]
        public void MissingDefinitionsAreReportedAndTheUnitStaysAlive()
        {
            var bareObject = Track(new GameObject("Bare"));
            var bare = bareObject.AddComponent<HealthAttributeComponent>();
            LogAssert.Expect(LogType.Error, new Regex("HealthAttributeComponent"));

            Assert.That(bare.IsDead, Is.False, "정의가 없다고 조용히 사망 처리되면 원인을 찾기 어렵다.");
            Assert.That(bare.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void MissingAttributesAreAddedWithAWarning()
        {
            var lateObject = Track(new GameObject("Late"));
            var late = lateObject.AddComponent<HealthAttributeComponent>();
            LogAssert.Expect(LogType.Warning, new Regex("HealthAttributeComponent"));
            LogAssert.Expect(LogType.Warning, new Regex("HealthAttributeComponent"));

            late.ConfigureAttributes(_health, _maxHealth);

            Assert.That(late.CurrentHealth, Is.EqualTo(100));
            Assert.That(lateObject.GetComponent<AttributeSetComponent>().Attributes.Contains(_health), Is.True);
        }

        /// <summary>대상 오브젝트의 어트리뷰트 집합이다.</summary>
        private AttributeSet Attributes => _gameObject.GetComponent<AttributeSetComponent>().Attributes;

        /// <summary>어트리뷰트 묶음을 갖춘 오브젝트에 체력 컴포넌트를 붙인다.</summary>
        /// <param name="target">붙일 오브젝트이다.</param>
        /// <param name="injectPublishers">알림 발행자를 주입할지 여부이다.</param>
        private HealthAttributeComponent CreateComponent(GameObject target, bool injectPublishers)
        {
            var component = target.AddComponent<HealthAttributeComponent>();
            var attributes = target.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_maxHealth);
            attributes.AddAttribute(_health);
            component.ConfigureAttributes(_health, _maxHealth);
            if (injectPublishers)
            {
                component.InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
            }

            return component;
        }

        /// <summary>지정한 양만큼 가로채는 것을 붙인다.</summary>
        private static void AddInterceptor(GameObject target, int takes)
        {
            target.AddComponent<FixedShareInterceptor>().Takes = takes;
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
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
