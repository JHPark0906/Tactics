using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>사격 어빌리티의 교전 수명과 사격 간격을 검증한다.</summary>
    /// <remarks>
    /// 에디터는 플레이 모드가 아닐 때 Awake를 호출하지 않으므로 유닛 조립은
    /// <see cref="TacticalUnit.InitializeUnit"/>을 직접 불러 구동하고, 어빌리티 시스템도 직접 만들어 연결한다.
    /// </remarks>
    public sealed class AttackAbilityTests
    {
        /// <summary>사격 어빌리티의 식별 태그 이름이다.</summary>
        private const string AttackTagName = "Ability.Attack";

        /// <summary>유닛 정의가 쓰는 기본 명중률이다. 1이면 굴림과 무관하게 언제나 맞으므로 검사가 결정적이다.</summary>
        private const float BaseHitChance = 1f;

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayEffectRunner _effects;
        private GameplayAbilitySystem _abilitySystem;
        private TacticalUnit _unit;
        private FakeTargetSource _targetSource;
        private FakeDamageable _target;
        private AttackAbility _ability;

        [SetUp]
        public void SetUp()
        {
            BuildAttacker(BaseHitChance);
        }

        /// <summary>
        /// 지정한 기본 명중률의 공격자와 대상을 세운다. 굴림은 Unity 난수이므로 명중률을 1이나 0으로 두어
        /// 결과를 정한다. 조립된 유닛은 정의를 바꿀 수 없으므로 다른 명중률이 필요하면 새 공격자로 갈아탄다.
        /// </summary>
        /// <param name="baseHitChance">유닛 정의에 넣을 기본 명중률이다.</param>
        private void BuildAttacker(float baseHitChance)
        {
            _attributes?.Dispose();

            var unitObject = CreateObject("Attacker");
            _unit = unitObject.AddComponent<TacticalUnit>();
            _unit.SetDefinition(Track(UnitDefinition.CreateRuntime(
                "소총병", 100, default, 3.5f, attackRange: 10f, attackDamage: 25, attackInterval: 1f,
                baseHitChance: baseHitChance)));
            _unit.InitializeUnit();
            _targetSource = unitObject.AddComponent<FakeTargetSource>();

            var targetObject = CreateObject("Target");
            targetObject.transform.position = new Vector3(5f, 0f, 0f);
            _target = targetObject.AddComponent<FakeDamageable>();
            _targetSource.CurrentTarget = targetObject.transform;

            _attributes = new AttributeSet();
            _effects = new GameplayEffectRunner(_attributes);
            _abilitySystem = new GameplayAbilitySystem(_effects, unitObject);
            _abilitySystem.GrantAbility(Track(AttackAbilityDefinition.CreateRuntime(AttackTagName)));
            _abilitySystem.TryGetAbility(GameplayTag.Parse(AttackTagName), out var granted);
            _ability = (AttackAbility)granted;
        }

        [TearDown]
        public void TearDown()
        {
            _attributes?.Dispose();
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
        public void ActivatingFiresOnceAndKeepsTheEngagementRunning()
        {
            Assert.That(Activate(), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(_ability.ShotCount, Is.EqualTo(1), "교전을 시작하는 순간 한 발 쏜다.");
            Assert.That(_target.TotalDamage, Is.EqualTo(25));
            Assert.That(
                _ability.IsActive,
                Is.True,
                "간격을 기다리는 동안에도 활성으로 남아야 엄폐를 유지한 채 사격할 수 있다.");
        }

        [Test]
        public void TheFireIntervalSurvivesAnInterruption()
        {
            Activate();
            Assert.That(_ability.ShotCount, Is.EqualTo(1));

            // 상위 분기가 가로챘다 곧바로 돌아온 상황을 흉내 낸다.
            _abilitySystem.CancelAbility(GameplayTag.Parse(AttackTagName));
            Activate();

            Assert.That(
                _ability.ShotCount,
                Is.Zero,
                "가로채였다 돌아왔다고 간격이 지워지면 사격 속도 제한을 우회할 수 있다.");
            Assert.That(_target.TotalDamage, Is.EqualTo(25));
        }

        [Test]
        public void TheNextShotWaitsForTheInterval()
        {
            Activate();

            Tick(0.5f);
            Assert.That(_ability.ShotCount, Is.EqualTo(1), "간격이 차기 전에는 더 쏘지 않는다.");
            Assert.That(_ability.IsActive, Is.True);

            Tick(0.5f);

            Assert.That(_ability.ShotCount, Is.EqualTo(2));
            Assert.That(_target.TotalDamage, Is.EqualTo(50));
        }

        [Test]
        public void ActivationIsRejectedWithoutATarget()
        {
            _targetSource.CurrentTarget = null;

            Assert.That(Activate(), Is.EqualTo(GameplayAbilityActivationResult.Rejected));
            Assert.That(_ability.ShotCount, Is.Zero);
        }

        [Test]
        public void ActivationIsRejectedWhenTheTargetIsOutOfRange()
        {
            _target.transform.position = new Vector3(50f, 0f, 0f);

            Assert.That(Activate(), Is.EqualTo(GameplayAbilityActivationResult.Rejected));
        }

        [Test]
        public void TheEngagementEndsWhenTheTargetDies()
        {
            Activate();

            _target.IsDead = true;
            Tick(1f);

            Assert.That(_ability.IsActive, Is.False, "대상이 죽으면 그 자리에서 교전을 끝낸다.");
            Assert.That(_ability.ShotCount, Is.EqualTo(1));
        }

        [Test]
        public void TheEngagementEndsWhenTheTargetLeavesRange()
        {
            Activate();

            _target.transform.position = new Vector3(50f, 0f, 0f);
            Tick(1f);

            Assert.That(_ability.IsActive, Is.False);
        }

        [Test]
        public void TheEngagementEndsWhenTheTargetDisappears()
        {
            Activate();

            _targetSource.CurrentTarget = null;
            Tick(1f);

            Assert.That(_ability.IsActive, Is.False);
        }

        [Test]
        public void AMissDealsNoDamage()
        {
            BuildAttacker(0f);

            Activate();

            Assert.That(_ability.ShotCount, Is.EqualTo(1));
            Assert.That(_ability.LastShotHit, Is.False);
            Assert.That(_target.TotalDamage, Is.Zero);
        }

        [Test]
        public void AnOpenTargetIsShotAtTheBaseHitChance()
        {
            Activate();

            Assert.That(_ability.LastHitChance, Is.EqualTo(BaseHitChance).Within(0.0001f));
        }

        // 엄폐는 명중률을 낮추지 않는다. 피해 흡수는 엄폐물이 대신 맞는 별도 규칙이다.
        [Test]
        public void ACoveredTargetIsShotAtTheSameHitChance()
        {
            _target.gameObject.AddComponent<FakeCoverState>().IsCovered = true;

            Activate();

            Assert.That(_ability.ShotCount, Is.EqualTo(1));
            Assert.That(
                _ability.LastHitChance,
                Is.EqualTo(BaseHitChance).Within(0.0001f),
                "엄폐는 명중률을 건드리지 않는다. 이득은 엄폐물이 대신 맞는 것으로만 나타난다.");
        }

        [Test]
        public void TheFireIntervalKeepsRunningWhileTheAbilityIsInactive()
        {
            Activate();
            Assert.That(_ability.ShotCount, Is.EqualTo(1));

            // 상위 분기가 가로챈 동안을 흉내 낸다. 어빌리티는 멈춰 있고 효과 실행기만 돈다.
            _abilitySystem.CancelAbility(GameplayTag.Parse(AttackTagName));
            _effects.Tick(1f);
            Activate();

            Assert.That(
                _ability.ShotCount,
                Is.EqualTo(1),
                "쉬는 동안에도 간격이 흘러야 돌아온 자리에서 바로 쏜다. " +
                "간격을 어빌리티 안에서 세면 여기서 한 발을 더 기다리게 된다.");
            Assert.That(_target.TotalDamage, Is.EqualTo(50));
        }

        // 사격 간격의 시간은 효과 실행기가 담당한다. 어빌리티 시스템만 흘려도 간격이 줄어들면 안 된다.
        [Test]
        public void TheAbilitySystemTickDoesNotAdvanceTheFireInterval()
        {
            Activate();

            // 어빌리티 시스템만 넉넉히 흘린다. 실행기를 함께 흘리게 바뀌었다면 간격이 여기서 걷힌다.
            _abilitySystem.Tick(5f);

            Assert.That(
                _ability.ShotCount,
                Is.EqualTo(1),
                "간격을 흘리는 것은 효과 실행기뿐이어야 한다. 어빌리티 시스템의 틱이 함께 흘리면 사격 속도가 어긋난다.");
        }

        /// <summary>효과 실행기와 어빌리티 시스템에 지정한 시간을 순서대로 공급한다.</summary>
        /// <remarks>사격 간격은 효과 실행기가, 사격 판단은 어빌리티 시스템이 담당한다.
        /// Tactics의 고정 스텝과 같이 효과를 먼저 진행해 갱신된 간격을 같은 스텝의 판단에서 읽는다.</remarks>
        /// <param name="deltaTime">흘릴 시간(초)이다.</param>
        private void Tick(float deltaTime)
        {
            _effects.Tick(deltaTime);
            _abilitySystem.Tick(deltaTime);
        }

        /// <summary>사격 어빌리티 활성화를 시도한다.</summary>
        private GameplayAbilityActivationResult Activate()
        {
            return _abilitySystem.TryActivate(GameplayTag.Parse(AttackTagName));
        }

        /// <summary>정리 목록에 등록된 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateObject(string objectName)
        {
            return Track(new GameObject(objectName));
        }

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
            public bool IsDead { get; set; }

            /// <inheritdoc />
            public void ApplyDamage(int amount, GameObject instigator)
            {
                TotalDamage += amount;
            }
        }

        /// <summary>엄폐 여부를 직접 지정할 수 있는 테스트용 엄폐 상태이다.</summary>
        private sealed class FakeCoverState : MonoBehaviour, IUnitCoverState
        {
            /// <summary>엄폐 중으로 답할지 여부이다.</summary>
            public bool IsCovered { get; set; }

            /// <inheritdoc />
            public bool IsInCover => IsCovered;

            /// <inheritdoc />
            public CoverPoint ClaimedCover => null;

        }
    }
}
