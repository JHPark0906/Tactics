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
    /// 회피 어빌리티가 유닛 체력 어트리뷰트의 기본값 변화에서 피해를 넘기는 규칙을 검증한다.
    /// 굴림의 성패, 엄폐물 쪽 알림의 공격자, 같은 프레임의 잇단 피해, 점유 해제, 면역 엄폐물, 회복이다.
    /// </summary>
    public sealed class CoverEvasionFilterTests
    {
        private static readonly GameplayTag EvasionTag = GameplayTag.Parse(UnitAbilityTags.CoverEvasion);

        private readonly List<Object> _createdObjects = new();

        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestMessageChannel<DeathEvent> _deathChannel;
        private TestUnitAttributes _attributes;
        private GameObject _unitObject;
        private TacticalUnit _unit;
        private HealthAttributeComponent _unitHealth;
        private UnitCoverState _coverState;
        private GameObject _coverObject;
        private CoverPoint _coverPoint;
        private HealthAttributeComponent _coverHealth;
        private GameObject _attacker;

        [SetUp]
        public void SetUp()
        {
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathChannel = new TestMessageChannel<DeathEvent>();
            _attributes = new TestUnitAttributes();
            var damageEffect = Track(DamageEffectDefinition.CreateRuntime(_attributes.Health));

            _unitObject = Track(new GameObject("Unit"));
            _unit = _unitObject.AddComponent<TacticalUnit>();
            _unitHealth = _unitObject.GetComponent<HealthAttributeComponent>();
            _unitHealth.InjectMessagePipePublishers(_damagePublisher, _deathChannel);
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _unit.SetDefinition(_attributes.CreateUnitDefinition(
                "소총병", 100, abilities: Track(CoverEvasionAbilityDefinition.CreateRuntime(damageEffect))));
            _unit.InitializeUnit();

            _coverObject = Track(new GameObject("Cover"));
            _coverObject.transform.position = new Vector3(10f, 0f, 0f);
            _coverPoint = _attributes.AttachCover(_coverObject);
            _coverHealth = _coverPoint.Health;
            _coverHealth.InjectMessagePipePublishers(_damagePublisher, _deathChannel);
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
        public void AnAbsorbedHitHandsTheDamageToTheCoverWithTheOriginalAttacker()
        {
            SetAbsorbChance(1f);

            _unitHealth.ApplyDamage(20, _attacker);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth), "유닛의 체력은 그대로다.");
            Assert.That(_coverHealth.CurrentHealth, Is.EqualTo(_coverHealth.MaxHealth - 20), "엄폐물이 그 피해를 받는다.");
            Assert.That(_damagePublisher.Published, Has.Count.EqualTo(1), "피해 알림은 엄폐물 쪽 하나뿐이다. 유닛은 맞지 않았다.");
            Assert.That(_damagePublisher.Last.Target, Is.SameAs(_coverObject));
            Assert.That(_damagePublisher.Last.Instigator, Is.SameAs(_attacker), "누가 쐈는지는 엄폐물 쪽 알림에도 그대로 실린다.");
        }

        [Test]
        public void ACoverThatNeverAbsorbsLetsTheUnitTakeTheDamage()
        {
            SetAbsorbChance(0f);

            _unitHealth.ApplyDamage(20, _attacker);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth - 20));
            Assert.That(_coverHealth.CurrentHealth, Is.EqualTo(_coverHealth.MaxHealth));
            Assert.That(_damagePublisher.Last.Target, Is.SameAs(_unitObject));
        }

        [Test]
        public void DamageAfterTheCoverDiesInTheSameFrameHitsTheUnitEvenAtFullAbsorbChance()
        {
            _coverPoint.GetComponent<AttributeSetComponent>().Attributes.SetBaseValue(_attributes.Health, 10f);
            SetAbsorbChance(1f);

            _unitHealth.ApplyDamage(20, _attacker);
            Assert.That(_coverHealth.IsDead, Is.True, "엄폐물이 먼저 부서진다.");
            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth), "부서지게 한 그 피해는 회피된 것이다.");

            _unitHealth.ApplyDamage(20, _attacker);

            Assert.That(
                _unitHealth.CurrentHealth,
                Is.EqualTo(_unitHealth.MaxHealth - 20),
                "부서진 엄폐물은 흡수 확률이 1이어도 대신 맞지 않는다. 부서진 뒤의 피해는 유닛이 받는다.");
        }

        [Test]
        public void ReleasingTheCoverDropsTheFilter()
        {
            SetAbsorbChance(1f);

            _coverState.ReleaseCover();
            _unitHealth.ApplyDamage(20, _attacker);

            Assert.That(_unit.AbilitySystem.System.IsActive(EvasionTag), Is.False, "자리를 놓으면 회피가 끝난다.");
            // 필터 수는 언제나 「체력 문 하나 + 회피」다. 체력 문(HealthAttributeComponent)이 조립 때부터 자기 자신을
            // 필터로 걸어 두므로(피해를 가로채는 자리가 그 필터다) 회피가 빠져도 0이 아니라 체력 문 하나가 남는다.
            Assert.That(
                _unit.AbilitySystem.System.Attributes.GetBaseValueFilterCount(_attributes.Health),
                Is.EqualTo(1),
                "회피가 끝났으면 필터는 체력 문 하나만 남아야 한다.");
            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth - 20), "흡수 확률이 1이어도 자리를 놓았으면 유닛이 맞는다.");
        }

        [Test]
        public void AnImmuneCoverStillCountsAsAnEvasion()
        {
            var immunity = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                immunityTags: new[] { DamageEffectDefinition.AssetTagName }));
            _coverPoint.GetComponent<GameplayEffectComponent>().ApplyEffect(immunity, this);
            SetAbsorbChance(1f);

            _unitHealth.ApplyDamage(20, _attacker);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth), "회피는 엄폐물의 사정과 무관하게 성립한다.");
            Assert.That(_coverHealth.CurrentHealth, Is.EqualTo(_coverHealth.MaxHealth), "면역인 엄폐물은 피해를 받지 않는다.");
            Assert.That(_damagePublisher.Published, Is.Empty, "피해는 어디에도 닿지 않았다.");
        }

        [Test]
        public void HealingIsNotEvaded()
        {
            // 엄폐 밖에서 맞아 체력을 깎은 뒤 다시 엄폐한다. 흡수 확률이 1이므로 회복까지 회피의 대상이면 체력이 그대로 남는다.
            SetAbsorbChance(1f);
            _coverState.ReleaseCover();
            _unitHealth.ApplyDamage(20, _attacker);
            _coverState.ClaimCover(_coverPoint);
            _coverState.RefreshInCoverTag();
            Assert.That(_unit.AbilitySystem.System.IsActive(EvasionTag), Is.True, "무대 확인: 회피가 다시 켜져 있어야 한다.");

            _unitHealth.Heal(10);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth - 10), "회복은 회피의 대상이 아니다.");
        }

        /// <summary>엄폐물의 흡수 확률을 둔다. 1이면 언제나 대신 맞고 0이면 결코 대신 맞지 않으므로 굴림과 무관하게 결과가 정해진다.</summary>
        /// <param name="absorbChance">흡수 확률 어트리뷰트에 넣을 값이다.</param>
        private void SetAbsorbChance(float absorbChance)
        {
            _coverPoint.GetComponent<AttributeSetComponent>().Attributes.SetBaseValue(_attributes.CoverAbsorbChance, absorbChance);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
