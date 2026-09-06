using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
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
    /// 엄폐는 명중률을 바꾸지 않고, 회피 어빌리티가 확률로 피해를 엄폐물에 넘기는지 검증한다.
    /// 엄폐 중 피해와 개방된 위치의 피해를 비교해 엄폐의 이득도 확인한다.
    /// 난수 대신 정해진 굴림 값을 사용해 결과를 결정적으로 만든다.
    /// </summary>
    public sealed class CoverEvasionTests
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
        private CoverPoint _coverPoint;
        private HealthAttributeComponent _coverHealth;

        [SetUp]
        public void SetUp()
        {
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathChannel = new TestMessageChannel<DeathEvent>();
            _attributes = new TestUnitAttributes();
            var damageEffect = Track(DamageEffectDefinition.CreateRuntime(_attributes.Health));

            _unitObject = CreateObject("Unit");
            _unit = _unitObject.AddComponent<TacticalUnit>();
            _unitHealth = _unitObject.GetComponent<HealthAttributeComponent>();
            _unitHealth.InjectMessagePipePublishers(_damagePublisher, _deathChannel);
            _coverState = _unitObject.AddComponent<UnitCoverState>();
            _unit.SetDefinition(_attributes.CreateUnitDefinition(
                "소총병", 100, abilities: Track(CoverEvasionAbilityDefinition.CreateRuntime(damageEffect))));
            _unit.InitializeUnit();

            var coverObject = CreateObject("Cover");
            coverObject.transform.position = new Vector3(10f, 0f, 0f);
            _coverPoint = _attributes.AttachCover(coverObject);
            _coverHealth = _coverPoint.Health;
            _coverHealth.InjectMessagePipePublishers(_damagePublisher, _deathChannel);

            TakeCover();
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

        // 집합의 어빌리티 부여와 엄폐 중 태그를 통한 활성화까지 실제 연결을 검증한다.
        [Test]
        public void AUnitInCoverHasTheEvasionAbilityActive()
        {
            Assert.That(_unit.AbilitySystem.System.IsGranted(EvasionTag), Is.True, "회피는 어빌리티 집합이 부여해야 한다.");
            Assert.That(
                _unit.AbilitySystem.System.IsActive(EvasionTag),
                Is.True,
                "엄폐 중 태그가 회피를 켜야 한다. 아무도 켜지 않으면 엄폐가 아무 일도 하지 않는다.");
        }

        [Test]
        public void TheHitChanceNoLongerKnowsAboutCover()
        {
            Assert.That(
                typeof(HitChanceCalculator).GetMethod("CalculateHitChance"),
                Is.Null,
                "엄폐 보정을 반영하던 계산이 남아 있으면 엄폐의 이득이 두 벌이 된다.");
            Assert.That(
                typeof(AttackProfile).GetProperty("CoverHitPenalty"),
                Is.Null,
                "사격 수치에 엄폐 보정이 남아 있으면 언젠가 다시 쓰이게 된다.");
        }

        [Test]
        public void TheCoverTakesTheDamageWhenItAlwaysAbsorbs()
        {
            SetAbsorbChance(1f);

            _unitHealth.ApplyDamage(20, null);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth), "유닛은 맞지 않는다.");
            Assert.That(
                _coverHealth.CurrentHealth,
                Is.EqualTo(_coverHealth.MaxHealth - 20),
                "엄폐물이 그 피해를 통째로 받는다.");
        }

        [Test]
        public void TheUnitTakesTheDamageWhenTheCoverNeverAbsorbs()
        {
            SetAbsorbChance(0f);

            _unitHealth.ApplyDamage(20, null);

            Assert.That(_unitHealth.CurrentHealth, Is.EqualTo(_unitHealth.MaxHealth - 20));
            Assert.That(_coverHealth.CurrentHealth, Is.EqualTo(_coverHealth.MaxHealth));
        }

        [Test]
        public void TheCoverDoesNotEvadeForAUnitThatHasNotArrived()
        {
            SetAbsorbChance(1f);
            _unitObject.transform.position = _coverPoint.Position + new Vector3(0f, 0f, 50f);
            _coverState.RefreshInCoverTag();

            _unitHealth.ApplyDamage(20, null);

            Assert.That(_unit.AbilitySystem.System.IsActive(EvasionTag), Is.False, "자리를 떠나면 엄폐 중 태그와 함께 회피도 꺼진다.");
            Assert.That(
                _unitHealth.CurrentHealth,
                Is.EqualTo(_unitHealth.MaxHealth - 20),
                "자리를 잡아 두기만 하고 아직 가지 않았으면 엄폐물이 대신 맞아 줄 수 없다.");
        }

        [Test]
        public void BeingInCoverIsBetterThanStandingInTheOpen()
        {
            SetAbsorbChance(1f);
            var exposedObject = CreateObject("Exposed");
            var exposedHealth = _attributes.AttachHealth(exposedObject);
            exposedHealth.InjectMessagePipePublishers(_damagePublisher, _deathChannel);

            _unitHealth.ApplyDamage(20, null);
            exposedHealth.ApplyDamage(20, null);

            Assert.That(
                _unitHealth.CurrentHealth,
                Is.GreaterThan(exposedHealth.CurrentHealth),
                "엄폐가 아무 이득도 주지 않는 구간이 생기면 유닛이 엄폐를 찾아가는 것이 헛일이 된다.");
        }

        [Test]
        public void TheCoverIsReleasedBeforeItDisappears()
        {
            SetAbsorbChance(1f);

            _unitHealth.ApplyDamage(_coverHealth.MaxHealth, null);

            Assert.That(_coverPoint.IsDestroyed, Is.True);
            Assert.That(_coverState.HasCoverClaim, Is.False, "부서진 자리를 계속 잡고 있으면 안 된다.");
            Assert.That(_unit.AbilitySystem.System.IsActive(EvasionTag), Is.False, "잡은 자리가 없으면 회피도 끝난다.");
        }

        [Test]
        public void TheCoverStopsEvadingOnceItIsDestroyed()
        {
            SetAbsorbChance(1f);
            _unitHealth.ApplyDamage(_coverHealth.MaxHealth, null);
            var healthAfterCoverDied = _unitHealth.CurrentHealth;

            _unitHealth.ApplyDamage(20, null);

            Assert.That(
                _unitHealth.CurrentHealth,
                Is.EqualTo(healthAfterCoverDied - 20),
                "부서진 엄폐물이 계속 대신 맞아 주면 파괴가 아무 뜻도 없어진다.");
        }

        /// <summary>유닛이 엄폐 지점을 잡고 그 자리에 서서 엄폐 중 태그를 붙이게 한다.</summary>
        private void TakeCover()
        {
            _coverState.ClaimCover(_coverPoint);
            _unitObject.transform.position = _coverPoint.Position;
            _coverState.RefreshInCoverTag();
        }

        /// <summary>엄폐물의 흡수 확률을 둔다. 1이면 언제나 대신 맞고 0이면 결코 대신 맞지 않으므로 굴림과 무관하게 결과가 정해진다.</summary>
        /// <param name="absorbChance">흡수 확률 어트리뷰트에 넣을 값이다.</param>
        private void SetAbsorbChance(float absorbChance)
        {
            _coverPoint.GetComponent<AttributeSetComponent>().Attributes.SetBaseValue(_attributes.CoverAbsorbChance, absorbChance);
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 GameObject이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        /// <returns>등록한 객체를 그대로 돌려준다.</returns>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
