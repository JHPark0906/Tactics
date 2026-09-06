using HS.Framework.Tests.Support;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Gameplay.Health;
using MessagePipe;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Tests.EditMode
{
    /// <summary>
    /// 사망 알림을 받은 쪽이 그 자리에서 오브젝트를 파괴해도 체력 문이 무너지지 않는지 검증한다.
    /// </summary>
    /// <remarks>
    /// 부서지는 엄폐물은 사망 알림을 받는 순간 자기 오브젝트를 파괴한다. 알림을 발행한 뒤에 컴포넌트를 다시 만지면
    /// 파괴된 오브젝트에 닿아 예외가 난다. 그래서 어빌리티 시스템에는 발행 전에 보내고, 발행은 마지막에 한다.
    /// </remarks>
    public sealed class HealthAttributeComponentDeathSubscriberTests
    {
        private readonly List<Object> _createdObjects = new();
        private AttributeDefinition _maxHealth;
        private AttributeDefinition _health;

        [SetUp]
        public void SetUp()
        {
            _maxHealth = Track(AttributeDefinition.CreateRuntime("MaxHealth", 100f, 1f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f, 0f, _maxHealth));
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
        public void ASubscriberThatDestroysTheObjectOnDeathDoesNotBreakTheAnnouncement()
        {
            var target = Track(new GameObject("Breakable"));
            var component = target.AddComponent<HealthAttributeComponent>();
            var attributes = target.GetComponent<AttributeSetComponent>().Attributes;
            attributes.AddAttribute(_maxHealth);
            attributes.AddAttribute(_health);
            component.ConfigureAttributes(_health, _maxHealth);
            component.InjectMessagePipePublishers(new TestPublisher<DamageAppliedEvent>(), new DestroyingPublisher());
            var abilitySystem = target.AddComponent<GameplayAbilitySystemComponent>();
            var received = new List<GameplayEventData>();
            using var subscription = abilitySystem.System.Events.Subscribe(received.Add);

            Assert.DoesNotThrow(() => component.ApplyDamage(100, null), "알림을 발행한 뒤에 파괴된 컴포넌트를 다시 만지면 안 된다.");

            Assert.That(target == null, Is.True, "알림을 받은 쪽이 오브젝트를 파괴했다.");
            Assert.That(received, Has.Count.EqualTo(1), "어빌리티 시스템에는 발행 전에 보내야 사망 이벤트가 닿는다.");
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>사망 알림을 받는 순간 대상 오브젝트를 파괴하는 테스트용 발행자이다. 부서지는 엄폐물의 구독자와 같은 행동이다.</summary>
        private sealed class DestroyingPublisher : IPublisher<DeathEvent>
        {
            public void Publish(DeathEvent message)
            {
                Object.DestroyImmediate(message.Target);
            }
        }
    }
}
