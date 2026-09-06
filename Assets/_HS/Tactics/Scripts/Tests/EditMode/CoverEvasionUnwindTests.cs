using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 회피 필터가 도는 도중에 그 필터 자신이 빠지는 자리를 못박는다. 유닛이 맞은 한 번의 피해가 엄폐물을 부수고,
    /// 부서짐이 점유를 풀어 엄폐 중 태그가 떨어지며, 그 태그로 켜져 있던 회피가 취소되어 자기 필터를 뺀다.
    /// 이 모두가 유닛 체력 필터 순회 한 번 안에서 일어난다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 경로: 유닛 체력 필터 → 엄폐물 피해 효과 → 엄폐물 체력 0 → 사망 이벤트 → 엄폐물 부서짐 어빌리티 →
    /// MarkDestroyed → 점유자 ReleaseCover → 엄폐 중 태그 제거 → 회피 취소 → OnEnd가 필터 손잡이를 해제(순회 중) →
    /// 회피 필터는 현재값을 돌려준다. 전부 동기이며 어느 고리에도 큐가 없다.
    /// </para>
    /// <para>
    /// 성립 근거는 어트리뷰트 집합이 필터를 <b>스냅샷으로 순회</b>하는 것이다. 살아 있는 목록을 돌면 순회 중 제거에서
    /// 예외가 나거나 뒤 필터가 건너뛰어진다. 그래서 회피 필터 뒤에 살피는 필터를 하나 더 걸어, 회피가 빠진 뒤에도
    /// 순회가 그 필터까지 이어지고 그 순간 목록에서 회피만 빠져 있음을 본다. 그것이 「순회 중에 빠졌다」의 증거다.
    /// </para>
    /// <para>
    /// <b>필터 수를 셀 때 체력 문을 잊지 않는다.</b> 유닛의 체력 어트리뷰트에는 체력 문(HealthAttributeComponent)이
    /// 조립 때부터 자기 자신을 필터로 걸어 두고 있다(피해를 가로채는 자리가 그 필터다). 그래서 이 파일의 필터 수는
    /// 언제나 「체력 문 하나 + 회피(+ 살피는 필터)」이며, 회피가 빠져도 0이 아니라 체력 문 하나가 남는다.
    /// </para>
    /// </remarks>
    public sealed class CoverEvasionUnwindTests
    {
        private static readonly GameplayTag EvasionTag = GameplayTag.Parse(UnitAbilityTags.CoverEvasion);

        private readonly List<Object> _createdObjects = new();

        private TestUnitAttributes _attributes;
        private GameObject _unitObject;
        private TacticalUnit _unit;
        private HealthAttributeComponent _unitHealth;
        private UnitCoverState _coverState;
        private CoverPoint _coverPoint;
        private HealthAttributeComponent _coverHealth;
        private GameObject _attacker;

        [SetUp]
        public void SetUp()
        {
            var damagePublisher = new TestPublisher<DamageAppliedEvent>();
            var deathChannel = new TestMessageChannel<DeathEvent>();
            _attributes = new TestUnitAttributes();
            var damageEffect = Track(DamageEffectDefinition.CreateRuntime(_attributes.Health));

            _unitObject = Track(new GameObject("Unit"));
            _unit = _unitObject.AddComponent<TacticalUnit>();
            _unitHealth = _unitObject.GetComponent<HealthAttributeComponent>();
            _unitHealth.InjectMessagePipePublishers(damagePublisher, deathChannel);
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _unit.SetDefinition(_attributes.CreateUnitDefinition(
                "소총병", 100, abilities: Track(CoverEvasionAbilityDefinition.CreateRuntime(damageEffect))));
            _unit.InitializeUnit();

            var coverObject = Track(new GameObject("Cover"));
            coverObject.transform.position = new Vector3(10f, 0f, 0f);
            // 흡수 확률 1은 굴림과 무관하게 언제나 대신 맞는다.
            _coverPoint = _attributes.AttachCover(coverObject, absorbChance: 1f);
            _coverHealth = _coverPoint.Health;
            _coverHealth.InjectMessagePipePublishers(damagePublisher, deathChannel);
            _attacker = Track(new GameObject("Attacker"));

            _coverState.ClaimCover(_coverPoint);
            _unitObject.transform.position = _coverPoint.Position;
            _coverState.RefreshInCoverTag();
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
            _attributes.Dispose();
        }

        [Test]
        public void TheEvasionFilterRemovesItselfInsideTheFilterWalkThatKillsTheCover()
        {
            var system = _unit.AbilitySystem.System;
            var attributes = system.Attributes;
            var probe = new ProbeFilter(attributes, _attributes.Health, system);
            using var probeHandle = attributes.AddBaseValueFilter(_attributes.Health, probe);
            Assert.That(
                attributes.GetBaseValueFilterCount(_attributes.Health),
                Is.EqualTo(3),
                "체력 문, 회피, 살피는 필터 셋이다. 체력 문은 조립 때부터 자기를 필터로 걸어 둔다.");
            Assert.That(system.IsActive(EvasionTag), Is.True, "엄폐 중이라 회피가 켜져 있어야 한다.");

            Assert.DoesNotThrow(() => _unitHealth.ApplyDamage(_coverHealth.MaxHealth, _attacker));

            Assert.That(probe.CallCount, Is.EqualTo(1), "회피 필터가 빠진 뒤에도 순회는 뒤 필터까지 이어진다.");
            Assert.That(
                probe.FilterCountWhenCalled,
                Is.EqualTo(2),
                "살피는 필터가 불린 순간 목록에는 체력 문과 자기만 남아 있다. 회피는 그 순회 안에서 빠졌다.");
            Assert.That(probe.EvasionActiveWhenCalled, Is.False, "그 순간 회피는 이미 취소되어 있다.");
            Assert.That(
                probe.ProposedValueWhenCalled,
                Is.EqualTo(_unitHealth.MaxHealth),
                "회피가 현재값을 돌려주었으므로 뒤 필터는 변화 없는 제안을 받는다.");
            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth), "엄폐물을 부순 그 피해는 유닛에 닿지 않는다.");
            Assert.That(_coverPoint.IsDestroyed, Is.True);
            Assert.That(_coverState.HasCoverClaim, Is.False);
        }

        [Test]
        public void TheFilterListMayEmptyOutWhileItIsBeingWalked()
        {
            var system = _unit.AbilitySystem.System;
            var attributes = system.Attributes;
            Assert.That(
                attributes.GetBaseValueFilterCount(_attributes.Health),
                Is.EqualTo(2),
                "체력 문과 회피 둘이다. 체력 문은 조립 때부터 자기를 필터로 걸어 둔다.");

            Assert.DoesNotThrow(() => _unitHealth.ApplyDamage(_coverHealth.MaxHealth, _attacker));

            Assert.That(
                attributes.GetBaseValueFilterCount(_attributes.Health),
                Is.EqualTo(1),
                "회피가 빠지고 체력 문 하나만 남는다.");
            Assert.That(system.IsActive(EvasionTag), Is.False, "잡은 자리가 없으면 회피도 끝난다.");
            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth));
            Assert.That(_coverHealth.IsDead, Is.True);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>회피 필터 뒤에 서서, 자기가 불린 순간의 필터 수와 회피 상태, 받은 제안값을 적어 두는 필터이다.</summary>
        private sealed class ProbeFilter : IAttributeBaseValueFilter
        {
            private readonly AttributeSet _attributes;
            private readonly AttributeDefinition _health;
            private readonly GameplayAbilitySystem _system;

            public ProbeFilter(AttributeSet attributes, AttributeDefinition health, GameplayAbilitySystem system)
            {
                _attributes = attributes;
                _health = health;
                _system = system;
            }

            public int CallCount { get; private set; }

            public int FilterCountWhenCalled { get; private set; } = -1;

            public bool EvasionActiveWhenCalled { get; private set; }

            public float ProposedValueWhenCalled { get; private set; }

            public float FilterBaseValue(
                AttributeDefinition definition,
                float currentBaseValue,
                float proposedBaseValue,
                in AttributeChangeContext context)
            {
                CallCount++;
                FilterCountWhenCalled = _attributes.GetBaseValueFilterCount(_health);
                EvasionActiveWhenCalled = _system.IsActive(EvasionTag);
                ProposedValueWhenCalled = proposedBaseValue;
                return proposedBaseValue;
            }
        }
    }
}
