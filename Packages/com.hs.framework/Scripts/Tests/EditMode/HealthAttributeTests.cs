using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 체력이 어트리뷰트 위에 서 있는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 값은 어트리뷰트 집합 한 곳에만 있고 체력 문은 그것을 읽고 쓴다. 값을 컴포넌트와 어트리뷰트가 나눠 가지면
    /// 버프가 걸린 상태에서 피해가 어긋나므로, 여기서는 어트리뷰트를 직접 건드렸을 때 문이 곧바로 그 값을 보고하는지,
    /// 반대로 문으로 깎았을 때 어트리뷰트가 따라 움직이는지를 함께 본다.
    /// </para>
    /// <para>
    /// 최대 체력은 현재 체력의 상한 어트리뷰트이다. 최대치 버프가 상한을 밀어 올리고, 버프가 끝나면 넘친 몫이 잘려
    /// 나가며, 그 잘린 몫이 되살아나지 않아야 한다.
    /// </para>
    /// </remarks>
    public sealed class HealthAttributeTests
    {
        private GameObject _gameObject;
        private AttributeDefinition _maxHealthDefinition;
        private AttributeDefinition _healthDefinition;
        private HealthAttributeComponent _health;

        [SetUp]
        public void SetUp()
        {
            _maxHealthDefinition = AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f);
            _healthDefinition = AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealthDefinition);
            _gameObject = new GameObject("HealthAttributeTests");
            _health = _gameObject.AddComponent<HealthAttributeComponent>();
            Attributes.AddAttribute(_maxHealthDefinition);
            Attributes.AddAttribute(_healthDefinition);
            _health.ConfigureAttributes(_healthDefinition, _maxHealthDefinition);
            _health.InjectMessagePipePublishers(new TestPublisher<DamageAppliedEvent>(), new TestPublisher<DeathEvent>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }

            Object.DestroyImmediate(_healthDefinition);
            Object.DestroyImmediate(_maxHealthDefinition);
        }

        /// <summary>대상의 어트리뷰트 집합이다.</summary>
        private AttributeSet Attributes => _gameObject.GetComponent<AttributeSetComponent>().Attributes;

        [Test]
        public void AttachingHealthBringsAnAttributeSetAlong()
        {
            Assert.That(
                _gameObject.GetComponent<AttributeSetComponent>(),
                Is.Not.Null,
                "체력이 어트리뷰트 위에 서므로 어트리뷰트 집합이 함께 붙어야 한다.");
        }

        [Test]
        public void TheDoorPointsAtTheDefinitionsItWasGiven()
        {
            Assert.That(_health.HealthAttribute, Is.SameAs(_healthDefinition));
            Assert.That(_health.MaxHealthAttribute, Is.SameAs(_maxHealthDefinition));
            Assert.That(
                _health.HealthAttribute.CapAttribute,
                Is.SameAs(_health.MaxHealthAttribute),
                "최대 체력이 현재 체력의 상한이어야 최대치 버프가 상한을 밀어 올린다.");
        }

        [Test]
        public void DamageMovesTheAttributeValueRatherThanAPrivateField()
        {
            _health.SetMaxHealth(100);

            _health.ApplyDamage(30, null);

            Assert.That(
                Attributes.GetCurrentValueAsInt(_health.HealthAttribute),
                Is.EqualTo(70),
                "피해가 어트리뷰트를 거치지 않으면 값이 두 곳으로 갈라진다.");
            Assert.That(_health.CurrentHealth, Is.EqualTo(70));
        }

        [Test]
        public void ChangingTheAttributeIsImmediatelyVisibleThroughTheHealthContract()
        {
            _health.SetMaxHealth(100);

            Attributes.SetBaseValue(_health.HealthAttribute, 42f);

            Assert.That(
                _health.CurrentHealth,
                Is.EqualTo(42),
                "문이 값을 따로 들고 있으면 어트리뷰트를 바꿔도 반영되지 않는다.");
        }

        [Test]
        public void RaisingTheMaxHealthAttributeRaisesTheCapWithoutHealing()
        {
            _health.SetMaxHealth(100);
            _health.ApplyDamage(40, null);

            using var buff = Attributes.AddModifier(_health.MaxHealthAttribute, AttributeModifierOperation.Add, 50f);

            Assert.That(_health.MaxHealth, Is.EqualTo(150), "최대치 버프가 최대 체력에 반영되어야 한다.");
            Assert.That(_health.CurrentHealth, Is.EqualTo(60), "최대치가 올랐다고 현재 체력이 저절로 차오르면 안 된다.");

            _health.Heal(90);
            Assert.That(_health.CurrentHealth, Is.EqualTo(150), "올라간 상한까지는 회복할 수 있어야 한다.");
        }

        [Test]
        public void LosingAMaxHealthBuffCutsTheOverflowAndDoesNotComeBack()
        {
            _health.SetMaxHealth(100);
            var buff = Attributes.AddModifier(_health.MaxHealthAttribute, AttributeModifierOperation.Add, 50f);
            _health.Heal(50);
            Assert.That(_health.CurrentHealth, Is.EqualTo(150));

            buff.Dispose();

            Assert.That(_health.MaxHealth, Is.EqualTo(100));
            Assert.That(_health.CurrentHealth, Is.EqualTo(100), "버프가 끝나면 넘친 몫은 잘려 나가야 한다.");

            using var secondBuff = Attributes.AddModifier(_health.MaxHealthAttribute, AttributeModifierOperation.Add, 50f);
            Assert.That(
                _health.CurrentHealth,
                Is.EqualTo(100),
                "잘라 낸 몫이 남아 있었다면 버프가 다시 걸릴 때 되살아난다.");
        }

        [Test]
        public void HealthNeverExceedsTheMaxHealthAttribute()
        {
            _health.SetMaxHealth(100);

            Attributes.SetBaseValue(_health.HealthAttribute, 9999f);

            Assert.That(_health.CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void IntegerContractUsesTheSharedRoundingRule()
        {
            _health.SetMaxHealth(100);

            Attributes.SetBaseValue(_health.HealthAttribute, 90.5f);
            Assert.That(_health.CurrentHealth, Is.EqualTo(91), "0.5는 0에서 먼 쪽으로 보낸다.");

            Attributes.SetBaseValue(_health.HealthAttribute, 90.4f);
            Assert.That(_health.CurrentHealth, Is.EqualTo(90), "버림이 아니라 반올림이다.");
        }

        [Test]
        public void SavedSnapshotCarriesTheHealthBaseValue()
        {
            _health.SetMaxHealth(100);
            _health.ApplyDamage(35, null);

            var snapshot = Attributes.CaptureSnapshot();
            _health.ResetHealth();
            Assert.That(_health.CurrentHealth, Is.EqualTo(100));

            Attributes.RestoreSnapshot(snapshot);

            Assert.That(
                _health.CurrentHealth,
                Is.EqualTo(65),
                "체력이 어트리뷰트에 있으므로 저장 경로로 그대로 오간다.");
        }
    }
}
