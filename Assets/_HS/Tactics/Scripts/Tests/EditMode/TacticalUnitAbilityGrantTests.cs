using System.Collections.Generic;
using System.Reflection;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛 조립이 정의의 어빌리티 집합을 실제로 부여하고, 부여와 시계를 유닛이 맡는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 유닛 정의가 어빌리티 집합을 들고 있어도 아무도 읽지 않으면 유닛에 어빌리티가 없다.
    /// 행동 트리의 활성화 자리는 부여되지 않은 어빌리티를 실패로 답하고 다음 우선순위로 넘어가므로,
    /// 오류 없이 유닛이 전진만 한다. 그 자리를 여기서 잡는다.
    /// </para>
    /// <para>
    /// 에디터는 플레이 모드가 아닐 때 Awake와 FixedUpdate를 호출하지 않으므로 조립은
    /// <see cref="TacticalUnit.InitializeUnit"/>을 직접 부르고, 고정 스텝은 리플렉션으로 직접 돌린다.
    /// </para>
    /// </remarks>
    public sealed class TacticalUnitAbilityGrantTests
    {
        /// <summary>사격 어빌리티의 식별 태그이다.</summary>
        private static readonly GameplayTag AttackTag = GameplayTag.Parse(UnitAbilityTags.Attack);

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private GameObject _unitObject;
        private TacticalUnit _unit;

        [SetUp]
        public void SetUp()
        {
            _unitObject = Track(new GameObject("Unit"));
            _unit = _unitObject.AddComponent<TacticalUnit>();
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
        public void TheDefinitionAbilitySetIsGrantedDuringAssembly()
        {
            _unit.SetDefinition(CreateDefinition(CreateAttackSet()));

            _unit.InitializeUnit();

            Assert.That(_unit.AbilitySystem, Is.Not.Null, "조립이 어빌리티 시스템 컴포넌트를 데려와야 한다.");
            Assert.That(
                _unit.AbilitySystem.System.IsGranted(AttackTag),
                Is.True,
                "정의의 어빌리티 집합이 부여되지 않으면 유닛은 트리에서 사격 자리를 건너뛰고 전진만 한다.");
        }

        [Test]
        public void ADefinitionWithoutAnAbilitySetStillAssembles()
        {
            _unit.SetDefinition(CreateDefinition(null));

            Assert.DoesNotThrow(() => _unit.InitializeUnit());

            Assert.That(_unit.IsUnitInitialized, Is.True);
            Assert.That(_unit.AbilitySystem, Is.Not.Null);
            Assert.That(_unit.AbilitySystem.System.GrantedAbilityCount, Is.Zero);
        }

        [Test]
        public void AssemblingTwiceDoesNotGrantTwice()
        {
            _unit.SetDefinition(CreateDefinition(CreateAttackSet()));

            _unit.InitializeUnit();
            _unit.InitializeUnit();

            Assert.That(_unit.AbilitySystem.System.GrantedAbilityCount, Is.EqualTo(1));
        }

        [Test]
        public void AnAbilitySystemAlreadyOnThePrefabIsReused()
        {
            var existing = _unitObject.AddComponent<GameplayAbilitySystemComponent>();
            _unit.SetDefinition(CreateDefinition(CreateAttackSet()));

            _unit.InitializeUnit();

            Assert.That(
                _unit.AbilitySystem,
                Is.SameAs(existing),
                "이미 붙어 있는 컴포넌트를 두고 새로 붙이면 시스템이 두 벌이 된다.");
            Assert.That(existing.System.IsGranted(AttackTag), Is.True);
        }

        [Test]
        public void TheUnitTakesOverGrantingAndTheClock()
        {
            _unit.SetDefinition(CreateDefinition(CreateAttackSet()));

            _unit.InitializeUnit();

            var abilitySystem = _unit.AbilitySystem;
            var effectComponent = _unitObject.GetComponent<GameplayEffectComponent>();
            Assert.That(abilitySystem.GrantOnAwake, Is.False, "부여는 조립이 맡으므로 컴포넌트의 자동 부여는 꺼져야 한다.");
            Assert.That(abilitySystem.TickAutomatically, Is.False, "시간은 고정 스텝이 흘리므로 프레임 자동 틱은 꺼져야 한다.");
            Assert.That(
                effectComponent.TickAutomatically,
                Is.False,
                "효과 실행기도 같은 시계를 봐야 하므로 프레임 자동 틱은 꺼져야 한다.");
        }

        [Test]
        public void TheFixedStepAdvancesAbilitiesAndEffectsOnTheSameClock()
        {
            var ability = new TickCountingAbility();
            var definition = Track(TickCountingAbilityDefinition.CreateRuntime("Ability.Count", ability));
            _unit.SetDefinition(CreateDefinition(Track(GameplayAbilitySet.CreateRuntime(new[] { definition }))));
            _unit.InitializeUnit();
            var system = _unit.AbilitySystem.System;
            var lingering = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                duration: Time.fixedDeltaTime * 1.5f,
                grantedTags: new[] { "State.Lingering" }));
            Assert.That(system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            system.Effects.Apply(lingering);
            var ticksAfterActivation = ability.TickCount;

            InvokeFixedUpdate(_unit);
            Assert.That(
                ability.TickCount,
                Is.EqualTo(ticksAfterActivation + 1),
                "고정 스텝 한 번에 어빌리티 틱이 한 번 돌아야 한다.");
            Assert.That(
                system.Tags.HasTag(GameplayTag.Parse("State.Lingering")),
                Is.True,
                "한 스텝 반짜리 효과가 한 스텝 만에 걷히면 시계가 어긋난 것이다.");

            InvokeFixedUpdate(_unit);

            Assert.That(ability.TickCount, Is.EqualTo(ticksAfterActivation + 2));
            Assert.That(
                system.Tags.HasTag(GameplayTag.Parse("State.Lingering")),
                Is.False,
                "효과의 시간도 같은 고정 스텝이 흘려야 한다. 효과만 다른 시계에 남으면 사격 간격이 판단과 어긋난다.");
        }

        /// <summary>사격 어빌리티 하나를 담은 집합을 만든다.</summary>
        private GameplayAbilitySet CreateAttackSet()
        {
            return Track(GameplayAbilitySet.CreateRuntime(
                new GameplayAbilityDefinition[] { Track(AttackAbilityDefinition.CreateRuntime(UnitAbilityTags.Attack)) }));
        }

        /// <summary>지정한 집합을 든 유닛 정의를 만든다.</summary>
        /// <param name="abilitySet">정의가 들 어빌리티 집합이며 없으면 null이다.</param>
        private UnitDefinition CreateDefinition(GameplayAbilitySet abilitySet)
        {
            return Track(UnitDefinition.CreateRuntime("소총병", 100, abilitySet: abilitySet));
        }

        /// <summary>에디터가 부르지 않는 고정 스텝 콜백을 직접 부른다.</summary>
        /// <param name="unit">스텝을 돌릴 유닛이다.</param>
        private static void InvokeFixedUpdate(TacticalUnit unit)
        {
            var method = typeof(TacticalUnit).GetMethod(
                "FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, "TacticalUnit.FixedUpdate 메서드를 찾지 못했다.");
            method.Invoke(unit, null);
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>틱 횟수를 세며 계속 진행하는 테스트용 어빌리티이다.</summary>
        private sealed class TickCountingAbility : GameplayAbility
        {
            /// <summary>흐른 시간이 있는 틱이 호출된 횟수이다.</summary>
            public int TickCount { get; private set; }

            /// <inheritdoc />
            protected override GameplayAbilityTickResult OnTick(float deltaTime)
            {
                if (deltaTime > 0f)
                {
                    TickCount++;
                }

                return GameplayAbilityTickResult.Running;
            }
        }

        /// <summary>미리 만든 어빌리티 인스턴스를 돌려주는 테스트용 정의이다.</summary>
        private sealed class TickCountingAbilityDefinition : GameplayAbilityDefinition
        {
            private TickCountingAbility _ability;

            /// <inheritdoc />
            public override GameplayAbility CreateAbility()
            {
                return _ability;
            }

            /// <summary>지정한 어빌리티를 돌려주는 정의를 만든다.</summary>
            /// <param name="tagName">어빌리티를 식별하는 태그 이름이다.</param>
            /// <param name="ability">부여될 때 돌려줄 어빌리티이다.</param>
            public static TickCountingAbilityDefinition CreateRuntime(string tagName, TickCountingAbility ability)
            {
                var definition = CreateInstance<TickCountingAbilityDefinition>();
                definition._ability = ability;
                definition.ConfigureRuntime(tagName);
                return definition;
            }
        }
    }
}
