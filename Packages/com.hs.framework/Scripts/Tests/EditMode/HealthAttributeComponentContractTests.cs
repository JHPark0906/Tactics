using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 체력 문의 <see cref="IDamageable"/> 계약이 지키는 경계값을 검증한다.
    /// 최대치의 하한, 0 이하의 피해, 최대치를 넘는 피해, 죽은 뒤의 회복, 되살림이다.
    /// </summary>
    public sealed class HealthAttributeComponentContractTests
    {
        private GameObject _gameObject;
        private AttributeDefinition _maxHealth;
        private AttributeDefinition _healthDefinition;
        private HealthAttributeComponent _health;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestPublisher<DeathEvent> _deathPublisher;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f);
            _healthDefinition = AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth);
            _gameObject = new GameObject("HealthAttributeComponentContractTests");
            _health = _gameObject.AddComponent<HealthAttributeComponent>();
            var attributes = _gameObject.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_maxHealth);
            attributes.AddAttribute(_healthDefinition);
            _health.ConfigureAttributes(_healthDefinition, _maxHealth);
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();
            _health.InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_healthDefinition);
            Object.DestroyImmediate(_maxHealth);
        }

        [Test]
        public void SetMaxHealthClampsInvalidValuesToOne()
        {
            _health.SetMaxHealth(0);

            Assert.That(_health.MaxHealth, Is.EqualTo(1));
            Assert.That(_health.CurrentHealth, Is.EqualTo(1));
        }

        [Test]
        public void NonPositiveDamageIsIgnored()
        {
            var maxHealth = _health.MaxHealth;

            _health.ApplyDamage(0, null);
            _health.ApplyDamage(-25, null);

            Assert.That(_health.CurrentHealth, Is.EqualTo(maxHealth));
            Assert.That(_damagePublisher.Published, Is.Empty);
        }

        [Test]
        public void DamageBeyondMaxHealthClampsToZeroAndReportsTheAppliedAmount()
        {
            var maxHealth = _health.MaxHealth;

            _health.ApplyDamage(maxHealth + 500, null);

            Assert.That(_health.CurrentHealth, Is.Zero);
            Assert.That(_health.IsDead, Is.True);
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(_damagePublisher.Last.Amount, Is.EqualTo(maxHealth), "알리는 피해는 실제로 깎인 몫이다.");
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void HealIsIgnoredWhileDead()
        {
            _health.ApplyDamage(_health.MaxHealth, null);

            _health.Heal(50);

            Assert.That(_health.CurrentHealth, Is.Zero);
            Assert.That(_health.IsDead, Is.True);
        }

        [Test]
        public void ResetHealthRestoresFullHealthWithoutAnnouncingDamage()
        {
            _health.ApplyDamage(_health.MaxHealth, null);
            var announcedDamage = _damagePublisher.Published.Count;

            _health.ResetHealth();

            Assert.That(_health.CurrentHealth, Is.EqualTo(_health.MaxHealth));
            Assert.That(_health.IsDead, Is.False);
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(announcedDamage), "되살림은 피해가 아니다.");
        }
    }
}
