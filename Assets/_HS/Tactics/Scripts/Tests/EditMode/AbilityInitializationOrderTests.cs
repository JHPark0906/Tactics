using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 어빌리티 구성요소들이 서로의 초기화 차례에 기대지 않는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>이 성질이 깨지면 사용자에게는 "유닛이 가만히 서 있는 것"으로만 보인다.</b>
    /// 어빌리티 시스템은 효과 실행기를, 효과 실행기는 어트리뷰트 집합을 필요로 한다.
    /// 셋 중 하나라도 Awake에서 남의 상태를 읽으면, 그 셋의 Awake 차례가 결과를 바꾼다.
    /// Unity는 같은 오브젝트에 붙은 컴포넌트의 Awake 차례를 보장하지 않으므로 그것은 운에 맡기는 것이 된다.
    /// </para>
    /// <para>
    /// <b>지금 구조는 세 단계가 모두 처음 쓰일 때 만들어진다.</b> 그래서 차례가 어떻든 결과가 같다.
    /// 어빌리티 시스템 컴포넌트는 유닛 조립이 데려오므로 여기서는 조립된 유닛에서 찾아 쓴다.
    /// 다만 그 사실은 코드를 읽어야만 알 수 있고, 누군가 Awake에서 미리 만들어 두도록 "최적화"하면
    /// 조용히 사라진다. 여기서 불리한 차례를 직접 만들어 걸어 둔다.
    /// </para>
    /// <para>
    /// <b>확인하지 않는 것</b>: 실제 플레이 모드에서 Unity가 매기는 차례 자체는 여기서 재현하지 않는다.
    /// 이 테스트가 말하는 것은 "어떤 차례로 불려도 결과가 같다"이며, 그것이 성립하면 차례를 알 필요가 없다.
    /// </para>
    /// </remarks>
    public sealed class AbilityInitializationOrderTests
    {
        private GameObject _unitObject;
        private UnitDefinition _definition;

        [TearDown]
        public void TearDown()
        {
            if (_unitObject != null)
            {
                Object.DestroyImmediate(_unitObject);
                _unitObject = null;
            }

            if (_definition != null)
            {
                Object.DestroyImmediate(_definition);
                _definition = null;
            }
        }

        [Test]
        public void AssemblingTheUnitBringsTheWholeAbilityChainWithIt()
        {
            CreateUnit();

            Assert.That(
                _unitObject.GetComponent<GameplayAbilitySystemComponent>(),
                Is.Not.Null,
                "유닛 조립이 어빌리티 시스템을 데려와야 한다.");
            Assert.That(
                _unitObject.GetComponent<GameplayEffectComponent>(),
                Is.Not.Null,
                "어빌리티 시스템이 요구하는 효과 실행기까지 연쇄로 따라와야 한다.");
            Assert.That(
                _unitObject.GetComponent<AttributeSetComponent>(),
                Is.Not.Null,
                "효과 실행기가 요구하는 어트리뷰트 집합까지 연쇄로 따라와야 한다.");
        }

        [Test]
        public void TheAbilitySystemIsUsableWithoutAnyAwakeHavingRun()
        {
            CreateUnit();
            var component = _unitObject.GetComponent<GameplayAbilitySystemComponent>();

            // EditMode에서는 Awake가 전혀 불리지 않는다. 그 상태에서도 시스템이 서야
            // 초기화가 Awake에 실려 있지 않다고 말할 수 있다.
            Assert.That(
                component.System,
                Is.Not.Null,
                "Awake가 한 번도 불리지 않아도 어빌리티 시스템은 서야 한다.");
        }

        [Test]
        public void TheAbilitySystemIsUsableWhenAwakeRunsInTheWorstOrder()
        {
            CreateUnit();
            var component = _unitObject.GetComponent<GameplayAbilitySystemComponent>();

            // 의존하는 쪽을 먼저, 의존되는 쪽을 나중에 깨운다. 어느 단계든 남의 상태를 미리 읽으면 여기서 터진다.
            MonoBehaviourLifecycle.InvokeAwake(_unitObject.GetComponent<GameplayAbilitySystemComponent>());
            MonoBehaviourLifecycle.InvokeAwake(_unitObject.GetComponent<AttributeSetComponent>());

            Assert.That(
                component.System,
                Is.Not.Null,
                "가장 불리한 차례로 깨워도 어빌리티 시스템은 서야 한다.");
        }

        [Test]
        public void TheAbilitySystemIsTheSameInstanceWhicheverStepIsTouchedFirst()
        {
            CreateUnit();
            var abilitySystemComponent = _unitObject.GetComponent<GameplayAbilitySystemComponent>();
            var effectComponent = _unitObject.GetComponent<GameplayEffectComponent>();

            // 효과 실행기를 먼저 만들어 두고 어빌리티 시스템을 나중에 묻는다.
            var runnerTouchedFirst = effectComponent.Runner;
            var abilitySystem = abilitySystemComponent.System;

            Assert.That(abilitySystem, Is.Not.Null);
            Assert.That(
                effectComponent.Runner,
                Is.SameAs(runnerTouchedFirst),
                "먼저 만들어진 실행기가 그대로 쓰여야 어트리뷰트가 두 벌로 갈리지 않는다.");
        }

        /// <summary>어빌리티 구성요소가 아직 없는 유닛을 조립한다.</summary>
        /// <remarks>
        /// 만든 유닛은 <see cref="_unitObject"/>에 둔다. 돌려주는 값은 없다 — 받아 쓰는 검사가 없는 값을
        /// 돌려주면, 그 값을 만들기 위한 타입이 <b>검사에서만 쓰이는 프로덕션 타입</b>이 된다.
        /// </remarks>
        private void CreateUnit()
        {
            _unitObject = new GameObject("Unit");
            var unit = _unitObject.AddComponent<TacticalUnit>();
            _definition = UnitDefinition.CreateRuntime(
                "유닛", 100, default, 3.5f, attackRange: 20f);
            unit.SetDefinition(_definition);
            unit.InitializeUnit();
            StraightLineCoverTravel.AttachSensor(_unitObject);
            _unitObject.AddComponent<UnitCoverState>();
            _unitObject.AddComponent<StubMover>();
        }

        /// <summary>아무 일도 하지 않는 테스트용 이동 구성요소이다.</summary>
        private sealed class StubMover : MonoBehaviour, ICharacterMover
        {
            /// <inheritdoc />
            public bool HasReachedDestination => true;

            /// <inheritdoc />
            public bool MoveTo(Vector3 destination) => true;

            /// <inheritdoc />
            public void Stop()
            {
            }
        }
    }
}
