using System.Collections.Generic;
using System.Text.RegularExpressions;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 사망이 어빌리티로 가는지, 죽은 상태 태그가 붙는지, 그리고 물러남(소멸)이 사망 알림 안이 아니라
    /// 다음 고정 스텝에 일어나는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>사망과 소멸은 서로 다른 어빌리티다.</b> <see cref="DeathAbility"/>는 활성화되면 죽은 상태 태그만
    /// 붙이고 곧바로 끝난다. <see cref="DisappearanceAbility"/>가 그 끝남을 이벤트로 받아 활성화되고,
    /// 물러남(<c>Owner.SetActive(false)</c>)을 맡는다. 이 파일의 핵심은 그 이어짐이 순서에서 벗어난다는
    /// 것이다 — 오브젝트가 비활성화되면 등록 구성요소가 집계에서 유닛을 빼는데 그 회수 경로는 승패를
    /// 판정하지 않는다. 사망 알림 안에서 곧바로 비활성화하면 집계보다 먼저 빠질 수 있고, 그것은 두 구독자의
    /// 차례에 달린다. 소멸 어빌리티는 물러남을 다음 고정 스텝으로 미루므로, 알림이 도는 동안 유닛은 언제나
    /// 활성이다.
    /// </para>
    /// <para>
    /// 이동을 멈추고 행동 트리를 끄고 엄폐 예약을 놓는 것은 이 파일이 아니라 각 컴포넌트가 죽은 상태 태그를
    /// 스스로 구독해서 하며, 그 반응은 <c>UnitDeathReactionsTests</c>가 따로 검증한다.
    /// </para>
    /// </remarks>
    public sealed class DeathAbilityTests
    {
        private static readonly GameplayTag DeathTag = GameplayTag.Parse(UnitAbilityTags.Death);
        private static readonly GameplayTag DisappearanceTag = GameplayTag.Parse(UnitAbilityTags.Disappearance);
        private static readonly GameplayTag AttackTag = GameplayTag.Parse(UnitAbilityTags.Attack);
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(DeathAbility.DeadStateTagName);

        /// <summary>테스트가 만든 오브젝트와 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private TestMessageChannel<DeathEvent> _deathChannel;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestUnitAttributes _attributes;

        [SetUp]
        public void SetUp()
        {
            _deathChannel = new TestMessageChannel<DeathEvent>();
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _attributes = new TestUnitAttributes();
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
        public void TheDeathEventAppliesTheDeadStateTagAndBlocksTheRest()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true);
            var system = unit.AbilitySystem.System;

            Kill(unit);

            Assert.That(system.Tags.HasTag(DeadStateTag), Is.True, "사망 알림이 죽은 상태 태그를 붙여야 한다.");
            Assert.That(system.IsActive(DisappearanceTag), Is.True, "사망 어빌리티가 끝나며 소멸 어빌리티를 트리거해야 한다.");
            Assert.That(
                system.TryActivate(AttackTag),
                Is.EqualTo(GameplayAbilityActivationResult.BlockedByActiveAbility),
                "죽은 유닛은 다른 어빌리티를 쓰지 못해야 한다.");
            Assert.That(unit.gameObject.activeSelf, Is.True, "물러남은 사망 알림 안에서 일어나지 않는다.");
        }

        [Test]
        public void TheUnitRetiresOnTheNextFixedStep()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true);
            Kill(unit);
            Assert.That(unit.gameObject.activeSelf, Is.True);

            MonoBehaviourLifecycle.InvokeFixedUpdate(unit);

            Assert.That(unit.gameObject.activeSelf, Is.False, "다음 고정 스텝에 물러나야 한다.");
            Assert.That(unit.AbilitySystem.System.IsActive(DisappearanceTag), Is.False);
            Assert.That(unit.AbilitySystem.System.Tags.HasTag(DeadStateTag), Is.True, "죽은 상태 태그는 어빌리티가 끝나도 남아야 한다.");
        }

        [Test]
        public void RetirementWaitsForTheDisappearDelay()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true, disappearDelay: Time.fixedDeltaTime * 2f);
            Kill(unit);

            MonoBehaviourLifecycle.InvokeFixedUpdate(unit);
            Assert.That(unit.gameObject.activeSelf, Is.True, "연출 시간이 차기 전에는 물러나지 않는다.");

            MonoBehaviourLifecycle.InvokeFixedUpdate(unit);

            Assert.That(unit.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void DeathCancelsAbilitiesThatWereRunning()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true);
            var running = new RunningTestAbility();
            unit.AbilitySystem.System.GrantAbility(Track(RunningTestAbilityDefinition.CreateRuntime("Ability.Hold", running)));
            unit.AbilitySystem.System.TryActivate(GameplayTag.Parse("Ability.Hold"));
            Assert.That(running.IsActive, Is.True);

            Kill(unit);

            Assert.That(running.IsActive, Is.False, "돌고 있던 어빌리티는 전부 취소되어야 한다.");
            Assert.That(running.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Cancelled));
        }

        [Test]
        public void VictoryIsDecidedEvenWhenRetirementSubscribedBeforeTheOutcomeService()
        {
            var enemy = CreateUnit(new TeamId(2), withDeathAbility: true);
            var ally = CreateUnit(new TeamId(1), withDeathAbility: true);
            var service = Track(new GameObject("Outcome")).AddComponent<BattleOutcomeService>();
            var outcomePublisher = new TestPublisher<BattleOutcomeDecidedEvent>();
            var placementChannel = new TestMessageChannel<PlacementCompletedEvent>();
            service.InjectMessagePipeDependencies(
                _deathChannel, outcomePublisher, DefaultTeamRelationPolicy.Instance, placementChannel);
            placementChannel.Publish(default);
            enemy.GetComponent<BattleUnitRegistrant>().InjectRegistry(service);
            ally.GetComponent<BattleUnitRegistrant>().InjectRegistry(service);
            var wasActiveDuringDispatch = false;
            using var probe = _deathChannel.Subscribe<DeathEvent>(_ => wasActiveDuringDispatch = enemy.gameObject.activeSelf);

            Kill(enemy);

            Assert.That(wasActiveDuringDispatch, Is.True, "알림이 도는 동안 유닛이 비활성화되면 집계보다 먼저 등록에서 빠질 수 있다.");
            Assert.That(outcomePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(outcomePublisher.Last.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(enemy.GetComponent<BattleUnitRegistrant>().IsRegistered, Is.True, "등록에서 빠지는 것은 물러난 뒤의 일이다.");
        }

        [Test]
        public void AUnitWithoutTheDeathAbilityStillRetiresImmediately()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: false);
            LogAssert.Expect(LogType.Warning, new Regex("DefeatedUnitRetirement"));

            Kill(unit);

            Assert.That(unit.gameObject.activeSelf, Is.False, "사망 어빌리티가 없는 유닛은 즉시 비활성화되어야 한다.");
        }

        [Test]
        public void ACancelledDeathDoesNotRetireTheUnit()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true);
            Kill(unit);

            unit.AbilitySystem.System.Clear();

            Assert.That(unit.gameObject.activeSelf, Is.True, "파괴나 정리로 취소된 것은 물러남이 아니다.");
        }

        [Test]
        public void ASecondDeathEventDoesNotRestartTheDisappearanceAbility()
        {
            var unit = CreateUnit(new TeamId(1), withDeathAbility: true);
            Kill(unit);
            var activationCount = ActivationCountOf(unit, DisappearanceTag);

            _deathChannel.Publish(new DeathEvent(unit.gameObject, null));

            Assert.That(ActivationCountOf(unit, DisappearanceTag), Is.EqualTo(activationCount));
        }

        /// <summary>지정한 어빌리티의 활성화 횟수를 읽는다.</summary>
        private static int ActivationCountOf(TacticalUnit unit, GameplayTag abilityTag)
        {
            unit.AbilitySystem.System.TryGetAbility(abilityTag, out var ability);
            return ability.ActivationCount;
        }

        /// <summary>체력이 0이 되도록 피해를 준다.</summary>
        private static void Kill(TacticalUnit unit)
        {
            unit.Health.ApplyDamage(unit.Health.MaxHealth, null);
        }

        /// <summary>조립된 유닛을 만들고 사망 알림 채널에 연결한다.</summary>
        /// <param name="team">유닛의 진영이다.</param>
        /// <param name="withDeathAbility">어빌리티 집합에 사망·소멸 어빌리티를 넣을지 여부이다.</param>
        /// <param name="disappearDelay">물러나기까지 기다리는 시간(초)이다.</param>
        private TacticalUnit CreateUnit(TeamId team, bool withDeathAbility, float disappearDelay = 0f)
        {
            var unitObject = Track(new GameObject("Unit"));
            var unit = unitObject.AddComponent<TacticalUnit>();
            unit.GetComponent<HealthAttributeComponent>().InjectMessagePipePublishers(_damagePublisher, _deathChannel);
            unit.GetComponent<DefeatedUnitRetirement>().InjectMessagePipeDependencies(_deathChannel);

            var abilities = new List<GameplayAbilityDefinition> { Track(AttackAbilityDefinition.CreateRuntime(UnitAbilityTags.Attack)) };
            if (withDeathAbility)
            {
                abilities.Add(Track(DeathAbilityDefinition.CreateRuntime()));
                abilities.Add(Track(DisappearanceAbilityDefinition.CreateRuntime(disappearDelay)));
            }

            unit.SetDefinition(_attributes.CreateUnitDefinition("소총병", 100, team, abilities: abilities.ToArray()));
            unit.InitializeUnit();
            return unit;
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>끝내라고 할 때까지 활성 상태로 남는 테스트용 어빌리티이다.</summary>
        private sealed class RunningTestAbility : GameplayAbility
        {
            /// <inheritdoc />
            protected override GameplayAbilityTickResult OnTick(float deltaTime) => GameplayAbilityTickResult.Running;
        }

        /// <summary>미리 만든 어빌리티 인스턴스를 돌려주는 테스트용 정의이다.</summary>
        private sealed class RunningTestAbilityDefinition : GameplayAbilityDefinition
        {
            private RunningTestAbility _ability;

            /// <inheritdoc />
            public override GameplayAbility CreateAbility() => _ability;

            /// <summary>지정한 어빌리티를 돌려주는 정의를 만든다.</summary>
            public static RunningTestAbilityDefinition CreateRuntime(string tagName, RunningTestAbility ability)
            {
                var definition = CreateInstance<RunningTestAbilityDefinition>();
                definition._ability = ability;
                definition.ConfigureRuntime(tagName);
                return definition;
            }
        }
    }
}
