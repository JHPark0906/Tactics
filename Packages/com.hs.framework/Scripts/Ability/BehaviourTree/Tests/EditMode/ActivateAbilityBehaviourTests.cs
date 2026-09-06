using System.Collections.Generic;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Ability.BehaviourTree.Tests.EditMode
{
    /// <summary>행동 트리와 어빌리티를 잇는 노드가 상태를 올바르게 옮기는지 검증한다.</summary>
    public sealed class ActivateAbilityBehaviourTests
    {
        /// <summary>테스트에서 사용하는 어빌리티 식별 태그 이름이다.</summary>
        private const string AbilityTagName = "Ability.TakeCover";

        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        /// <summary>대상의 어트리뷰트 집합이다.</summary>
        private AttributeSet _attributes;

        /// <summary>검증 대상 어빌리티 시스템이다.</summary>
        private GameplayAbilitySystem _abilitySystem;

        /// <summary>노드가 사용할 행동 트리 컨텍스트이다.</summary>
        private IBehaviourContext _context;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _abilitySystem = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes));
            _context = new BehaviourContext();
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
        public void AnUngrantedAbilityFailsImmediatelySoTheBranchIsSkipped()
        {
            var node = new ActivateAbilityBehaviour(_abilitySystem, AbilityTagName);

            Assert.That(
                node.Tick(_context),
                Is.EqualTo(BehaviourStatus.Failure),
                "부여되지 않은 어빌리티는 기다릴 것이 없으므로 즉시 실패해야 다음 우선순위로 넘어간다.");
            Assert.That(node.IsWatchingActivation, Is.False);
        }

        [Test]
        public void AnAbilityThatFinishesAtOnceReportsSuccess()
        {
            var node = GrantAndCreateNode(new TestAbility());

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
            Assert.That(node.IsWatchingActivation, Is.False);
        }

        [Test]
        public void AnOngoingAbilityReportsRunningUntilItFinishes()
        {
            var ability = new TestAbility { RemainingTicks = 2 };
            var node = GrantAndCreateNode(ability);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running));

            _abilitySystem.Tick(1f);
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running));

            _abilitySystem.Tick(1f);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success));
        }

        [Test]
        public void CancellingFromOutsideEndsTheWaitWithFailure()
        {
            var ability = new TestAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked(ability);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);
            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running));

            _abilitySystem.CancelAbility(definition.AbilityTag);

            Assert.That(
                node.Tick(_context),
                Is.EqualTo(BehaviourStatus.Failure),
                "바깥에서 취소되었는데 계속 진행 중을 돌려주면 액터가 굳는다.");
        }

        [Test]
        public void RevokingWhileRunningEndsTheWaitWithFailure()
        {
            var ability = new TestAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked(ability);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);
            node.Tick(_context);

            _abilitySystem.RevokeAbility(definition.AbilityTag);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Failure));
            Assert.That(node.IsWatchingActivation, Is.False);
        }

        [Test]
        public void ResetCancelsTheActivationThisNodeStarted()
        {
            var ability = new TestAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked(ability);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);
            node.Tick(_context);

            Assert.That(ability.HasReservation, Is.True);

            node.Reset();

            Assert.That(
                _abilitySystem.IsActive(definition.AbilityTag),
                Is.False,
                "상위 분기가 가로챘는데 어빌리티가 계속 돌면 두 행동이 겹친다.");
            Assert.That(ability.HasReservation, Is.False, "취소 경로에서 잡고 있던 자원이 풀려야 한다.");
            Assert.That(node.IsWatchingActivation, Is.False);
        }

        [Test]
        public void ResetDoesNotTouchAnActivationStartedElsewhere()
        {
            var ability = new TestAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked(ability);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);

            // 노드가 시작하지 않은 활성화이다.
            _abilitySystem.TryActivate(definition.AbilityTag);
            node.Reset();

            Assert.That(_abilitySystem.IsActive(definition.AbilityTag), Is.True);
        }

        [Test]
        public void AfterResetTheNodeCanStartTheAbilityAgain()
        {
            var ability = new TestAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked(ability);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);
            node.Tick(_context);
            node.Reset();

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Running));
            Assert.That(ability.ActivateCount, Is.EqualTo(2));
        }

        [Test]
        public void ABlockedAbilityFailsWithoutWaiting()
        {
            var cooldownEffect = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration, null, 5f, grantedTags: new[] { "Cooldown.TakeCover" }));
            var definition = GrantTracked(new TestAbility(), cooldown: cooldownEffect);
            var node = new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);

            Assert.That(node.Tick(_context), Is.EqualTo(BehaviourStatus.Success));

            Assert.That(
                node.Tick(_context),
                Is.EqualTo(BehaviourStatus.Failure),
                "쿨다운이 도는 동안에는 기다리지 않고 실패해야 한다.");
        }

        [Test]
        public void AnInvalidAbilityTagIsRejectedAtConstruction()
        {
            Assert.Throws<System.ArgumentException>(
                () => new ActivateAbilityBehaviour(_abilitySystem, "Ability..TakeCover"));
            Assert.Throws<System.ArgumentNullException>(
                () => new ActivateAbilityBehaviour(null, AbilityTagName));
        }

        /// <summary>어빌리티를 부여하고 그것을 활성화하는 노드를 만든다.</summary>
        /// <param name="ability">부여할 어빌리티 인스턴스이다.</param>
        private ActivateAbilityBehaviour GrantAndCreateNode(TestAbility ability)
        {
            var definition = GrantTracked(ability);
            return new ActivateAbilityBehaviour(_abilitySystem, definition.AbilityTag);
        }

        /// <summary>테스트용 어빌리티를 부여하고 그 정의를 돌려준다.</summary>
        /// <param name="ability">부여할 어빌리티 인스턴스이다.</param>
        /// <param name="cooldown">쿨다운 효과이며 없으면 null이다.</param>
        private TestAbilityDefinition GrantTracked(TestAbility ability, GameplayEffectDefinition cooldown = null)
        {
            var definition = Track(TestAbilityDefinition.CreateRuntime(AbilityTagName, ability, cooldown));
            _abilitySystem.GrantAbility(definition);
            return definition;
        }

        /// <summary>만든 에셋을 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 에셋이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>진행 길이와 자원 보유를 흉내 내는 테스트용 어빌리티이다.</summary>
        private sealed class TestAbility : GameplayAbility
        {
            /// <summary>남은 진행 틱 수이며 0이면 곧바로 끝난다.</summary>
            public int RemainingTicks { get; set; }

            /// <summary>활성화된 횟수이다.</summary>
            public int ActivateCount { get; private set; }

            /// <summary>바깥 자원을 잡고 있는지 흉내 내는 표식이다.</summary>
            public bool HasReservation { get; private set; }

            /// <inheritdoc />
            protected override void OnActivate()
            {
                ActivateCount++;
                HasReservation = true;
            }

            /// <inheritdoc />
            protected override GameplayAbilityTickResult OnTick(float deltaTime)
            {
                if (RemainingTicks <= 0)
                {
                    return GameplayAbilityTickResult.Finished;
                }

                RemainingTicks--;
                return GameplayAbilityTickResult.Running;
            }

            /// <inheritdoc />
            protected override void OnEnd(GameplayAbilityEndReason endReason)
            {
                HasReservation = false;
            }
        }

        /// <summary>미리 만들어 둔 어빌리티 인스턴스를 돌려주는 테스트용 정의이다.</summary>
        private sealed class TestAbilityDefinition : GameplayAbilityDefinition
        {
            /// <summary>부여할 때 돌려줄 어빌리티 인스턴스이다.</summary>
            private GameplayAbility _instance;

            /// <inheritdoc />
            public override GameplayAbility CreateAbility()
            {
                return _instance;
            }

            /// <summary>테스트용 어빌리티 정의를 만든다.</summary>
            /// <param name="abilityTagName">어빌리티를 식별하는 태그 이름이다.</param>
            /// <param name="instance">부여할 어빌리티 인스턴스이다.</param>
            /// <param name="cooldown">쿨다운 효과이며 없으면 null이다.</param>
            public static TestAbilityDefinition CreateRuntime(
                string abilityTagName,
                GameplayAbility instance,
                GameplayEffectDefinition cooldown)
            {
                var definition = CreateInstance<TestAbilityDefinition>();
                definition._instance = instance;
                definition.ConfigureRuntime(abilityTagName, cooldown: cooldown);
                return definition;
            }
        }
    }
}
