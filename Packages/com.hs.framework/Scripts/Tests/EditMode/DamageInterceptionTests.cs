using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 피해가 체력에 닿기 전에 가로채이는 규칙을 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>가로채는 것이 붙어 있지 않으면 피해는 그대로 체력에 닿는다.</b> 이 계약은 선택 사항이므로
    /// 쓰지 않는 대상은 무엇도 달라지지 않아야 한다. 그 성질을 첫 검사가 지킨다.
    /// </para>
    /// <para>
    /// <b>전부 가로채이면 피해 알림도 나가지 않는다.</b> 대상이 실제로 받은 피해가 없으므로 알릴 것도 없다.
    /// 알림이 나가면 그것을 듣는 쪽(피격 연출, 위협 판단, 집계)이 맞지도 않은 피해를 세게 된다.
    /// </para>
    /// </remarks>
    public sealed class DamageInterceptionTests
    {
        private GameObject _gameObject;
        private AttributeDefinition _maxHealth;
        private AttributeDefinition _healthDefinition;
        private HealthAttributeComponent _health;
        private TestPublisher<DamageAppliedEvent> _publisher;
        private TestPublisher<DeathEvent> _deathPublisher;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f);
            _healthDefinition = AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth);
            _gameObject = new GameObject("DamageInterceptionTests");
            _health = _gameObject.AddComponent<HealthAttributeComponent>();
            var attributes = _gameObject.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_maxHealth);
            attributes.AddAttribute(_healthDefinition);
            _health.ConfigureAttributes(_healthDefinition, _maxHealth);
            _publisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();
            _health.InjectMessagePipePublishers(_publisher, _deathPublisher);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            Object.DestroyImmediate(_healthDefinition);
            Object.DestroyImmediate(_maxHealth);
        }

        [Test]
        public void DamageIsUnchangedWhenNothingIntercepts()
        {
            var before = _health.CurrentHealth;

            _health.ApplyDamage(30, null);

            Assert.That(_health.CurrentHealth, Is.EqualTo(before - 30));
            Assert.That(_publisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void AnInterceptorTakesItsShareAndTheRestReachesHealth()
        {
            var before = _health.CurrentHealth;
            AddInterceptor(takes: 10);

            _health.ApplyDamage(30, null);

            Assert.That(_health.CurrentHealth, Is.EqualTo(before - 20), "가져간 만큼만 덜어져야 한다.");
        }

        [Test]
        public void FullyInterceptedDamageNeitherReachesHealthNorIsAnnounced()
        {
            var before = _health.CurrentHealth;
            AddInterceptor(takes: 30);

            _health.ApplyDamage(30, null);

            Assert.That(_health.CurrentHealth, Is.EqualTo(before), "전부 가로채였으면 체력이 줄지 않는다.");
            Assert.That(
                _publisher.Published.Count,
                Is.Zero,
                "받지 않은 피해를 알리면 듣는 쪽이 맞지도 않은 피해를 센다.");
        }

        [Test]
        public void InterceptorsAreAskedInOrderAndEachSeesWhatIsLeft()
        {
            var first = AddInterceptor(takes: 10);
            var second = AddInterceptor(takes: 100);
            var before = _health.CurrentHealth;

            _health.ApplyDamage(30, null);

            Assert.That(first.LastSeenAmount, Is.EqualTo(30), "먼저 붙은 쪽이 전체를 본다.");
            Assert.That(second.LastSeenAmount, Is.EqualTo(20), "뒤에 붙은 쪽은 남은 것만 본다.");
            Assert.That(_health.CurrentHealth, Is.EqualTo(before), "둘이 합쳐 전부 가져갔다.");
        }

        [Test]
        public void AnInterceptorCannotTakeMoreThanTheDamage()
        {
            var greedy = AddInterceptor(takes: 999);
            var before = _health.CurrentHealth;

            _health.ApplyDamage(10, null);

            Assert.That(greedy.LastSeenAmount, Is.EqualTo(10));
            Assert.That(_health.CurrentHealth, Is.EqualTo(before), "넘치게 답해도 그 피해만큼만 가져간다.");
        }

        [Test]
        public void ANegativeShareDoesNotIncreaseTheDamage()
        {
            AddInterceptor(takes: -50);
            var before = _health.CurrentHealth;

            _health.ApplyDamage(10, null);

            Assert.That(
                _health.CurrentHealth,
                Is.EqualTo(before - 10),
                "음수를 답해도 피해가 늘어나지 않는다. 가로채지 않은 것과 같게 다룬다.");
        }

        [Test]
        public void TheInstigatorIsHandedToTheInterceptor()
        {
            var attacker = new GameObject("Attacker");
            var interceptor = AddInterceptor(takes: 0);

            try
            {
                _health.ApplyDamage(10, attacker);

                Assert.That(
                    interceptor.LastInstigator,
                    Is.SameAs(attacker),
                    "누가 때렸는지 모르면 방향에 따라 갈리는 가로채기를 판정할 수 없다.");
            }
            finally
            {
                Object.DestroyImmediate(attacker);
            }
        }

        [Test]
        public void AnInterceptorAddedLaterIsAskedToo()
        {
            _health.ApplyDamage(10, null);
            var before = _health.CurrentHealth;

            AddInterceptor(takes: 10);
            _health.ApplyDamage(10, null);

            Assert.That(
                _health.CurrentHealth,
                Is.EqualTo(before),
                "실행 중에 붙은 것도 물어야 한다. 처음 한 번만 찾아 두면 나중에 붙은 것이 잊힌다.");
        }

        /// <summary>정해진 양을 가져가는 가로채는 것을 붙인다.</summary>
        /// <param name="takes">가져가겠다고 답할 양이다.</param>
        /// <returns>붙인 구성요소이다.</returns>
        private StubInterceptor AddInterceptor(int takes)
        {
            var interceptor = _gameObject.AddComponent<StubInterceptor>();
            interceptor.Takes = takes;
            return interceptor;
        }

        /// <summary>가져갈 양을 지정할 수 있는 테스트용 가로채는 구성요소이다.</summary>
        private sealed class StubInterceptor : MonoBehaviour, IDamageInterceptor
        {
            /// <summary>가져가겠다고 답할 양이다.</summary>
            internal int Takes { get; set; }

            /// <summary>마지막으로 물어본 피해량이다.</summary>
            internal int LastSeenAmount { get; private set; }

            /// <summary>마지막으로 전달받은 공격자이다.</summary>
            internal GameObject LastInstigator { get; private set; }

            /// <inheritdoc />
            public int InterceptDamage(int amount, GameObject instigator)
            {
                LastSeenAmount = amount;
                LastInstigator = instigator;
                return Takes;
            }
        }
    }
}
