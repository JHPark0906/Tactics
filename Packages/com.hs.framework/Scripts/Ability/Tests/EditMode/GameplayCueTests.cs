using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>규칙 계층이 큐 태그로 연출 계층에 신호를 보내고, 디스패처가 태그가 맞는 핸들러를 부르는지 검증한다.</summary>
    /// <remarks>
    /// 효과와 어빌리티는 자기가 어떤 소리와 그림을 내야 하는지 알지 못하며 정의에 적힌 큐 태그로 순간만 알린다.
    /// 무엇을 보여 줄지는 그 태그를 받는 핸들러가 정한다.
    /// </remarks>
    public sealed class GameplayCueTests
    {
        private static readonly GameplayTag Grounded = GameplayTag.Parse("State.Grounded");

        /// <summary>테스트가 만든 에셋과 오브젝트이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _health;
        private AttributeSet _attributes;
        private GameplayEffectRunner _runner;
        private GameplayCueDispatcher _dispatcher;
        private RecordingHandler _handler;

        [SetUp]
        public void SetUp()
        {
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_health);
            _dispatcher = new GameplayCueDispatcher();
            _handler = new RecordingHandler();
            _runner = new GameplayEffectRunner(_attributes, new GameplayTagContainer()) { CueDispatcher = _dispatcher };
        }

        [TearDown]
        public void TearDown()
        {
            _runner?.Dispose();
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
        public void HandlersReceiveCuesForTheirTagAndItsChildren()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Damage"), _handler);

            _dispatcher.Dispatch(new GameplayCueEvent(GameplayTag.Parse("Cue.Damage.Fire"), GameplayCueEventKind.Executed, null));
            _dispatcher.Dispatch(new GameplayCueEvent(GameplayTag.Parse("Cue.Heal"), GameplayCueEventKind.Executed, null));

            Assert.That(_handler.Received, Has.Count.EqualTo(1), "계열 하나를 등록하면 그 하위 큐만 받아야 한다.");
            Assert.That(_handler.Received[0].CueTag, Is.EqualTo(GameplayTag.Parse("Cue.Damage.Fire")));
        }

        [Test]
        public void DisposingTheRegistrationStopsDelivery()
        {
            var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Damage"), _handler);
            registration.Dispose();
            registration.Dispose();

            _dispatcher.Dispatch(new GameplayCueEvent(GameplayTag.Parse("Cue.Damage"), GameplayCueEventKind.Executed, null));

            Assert.That(_handler.Received, Is.Empty);
            Assert.That(_dispatcher.HandlerCount, Is.Zero);
        }

        [Test]
        public void AnInstantEffectSendsAnExecutedCueWithTargetAndContext()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue"), _handler);
            var target = Track(new GameObject("Target"));
            _runner.CueTarget = target;
            var instigator = Track(new GameObject("Attacker"));
            var damage = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f) },
                cueTags: new[] { "Cue.Damage.Fire" }));

            _runner.Apply(damage, null, new GameplayEffectContext(instigator));

            Assert.That(_handler.Received, Has.Count.EqualTo(1));
            Assert.That(_handler.Received[0].Kind, Is.EqualTo(GameplayCueEventKind.Executed));
            Assert.That(_handler.Received[0].Target, Is.SameAs(target));
            Assert.That(_handler.Received[0].Context.Instigator, Is.SameAs(instigator));
            Assert.That(_handler.Received[0].Effect, Is.Null);
        }

        [Test]
        public void ALingeringEffectSendsAppliedThenExecutedThenRemoved()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Burn"), _handler);
            var burn = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -5f) },
                duration: 2f,
                period: 1f,
                cueTags: new[] { "Cue.Burn" }));

            var effect = _runner.Apply(burn);
            _runner.Tick(1f);
            _runner.Tick(1f);

            var kinds = _handler.Received.ConvertAll(cue => cue.Kind);
            Assert.That(kinds[0], Is.EqualTo(GameplayCueEventKind.Applied));
            Assert.That(kinds, Does.Contain(GameplayCueEventKind.Executed));
            Assert.That(kinds[kinds.Count - 1], Is.EqualTo(GameplayCueEventKind.Removed));
            Assert.That(_handler.Received[0].Effect, Is.SameAs(effect));
        }

        [Test]
        public void InhibitionSendsRemovedAndReenablingSendsAppliedAgain()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Aura"), _handler);
            _runner.Tags.AddTag(Grounded);
            var aura = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                ongoingRequiredTags: new[] { "State.Grounded" },
                cueTags: new[] { "Cue.Aura" }));
            _runner.Apply(aura);

            _runner.Tags.RemoveTag(Grounded);
            _runner.Tags.AddTag(Grounded);
            _runner.RemoveAll();

            var kinds = _handler.Received.ConvertAll(cue => cue.Kind);
            Assert.That(kinds, Is.EqualTo(new[]
            {
                GameplayCueEventKind.Applied,
                GameplayCueEventKind.Removed,
                GameplayCueEventKind.Applied,
                GameplayCueEventKind.Removed
            }), "억제는 걷힘으로, 억제 해제는 다시 작용으로 보이며, 억제된 채 걷히면 걷힘 큐를 두 번 보내지 않는다.");
        }

        [Test]
        public void AnEffectRemovedWhileInhibitedDoesNotSendRemovedTwice()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Aura"), _handler);
            _runner.Tags.AddTag(Grounded);
            var aura = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                ongoingRequiredTags: new[] { "State.Grounded" },
                cueTags: new[] { "Cue.Aura" }));
            _runner.Apply(aura);
            _runner.Tags.RemoveTag(Grounded);

            _runner.RemoveAll();

            Assert.That(_handler.Received.FindAll(cue => cue.Kind == GameplayCueEventKind.Removed), Has.Count.EqualTo(1));
        }

        [Test]
        public void AnAbilitySendsAppliedOnActivationAndRemovedOnEnd()
        {
            using var registration = _dispatcher.Register(GameplayTag.Parse("Cue.Ability"), _handler);
            var owner = Track(new GameObject("Owner"));
            using var system = new GameplayAbilitySystem(_runner, owner);
            var dash = Track(RunningAbilityDefinition.CreateRuntime("Ability.Dash", cueTags: new[] { "Cue.Ability.Dash" }));
            system.GrantAbility(dash);

            system.TryActivate(dash.AbilityTag);
            system.CancelAbility(dash.AbilityTag);

            Assert.That(_handler.Received, Has.Count.EqualTo(2));
            Assert.That(_handler.Received[0].Kind, Is.EqualTo(GameplayCueEventKind.Applied));
            Assert.That(_handler.Received[0].Target, Is.SameAs(owner));
            Assert.That(_handler.Received[1].Kind, Is.EqualTo(GameplayCueEventKind.Removed));
        }

        [Test]
        public void WithoutADispatcherNothingHappens()
        {
            _runner.CueDispatcher = null;
            var damage = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f) },
                cueTags: new[] { "Cue.Damage" }));

            Assert.DoesNotThrow(() => _runner.Apply(damage));
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f).Within(0.001f), "큐는 연출일 뿐이라 없어도 규칙은 그대로 돈다.");
        }

        [Test]
        public void TheDispatchedStreamCarriesEveryCue()
        {
            var seen = new List<GameplayCueEvent>();
            using var subscription = _dispatcher.Dispatched.Subscribe(seen.Add);

            _dispatcher.Dispatch(new GameplayCueEvent(GameplayTag.Parse("Cue.Anything"), GameplayCueEventKind.Applied, null));

            Assert.That(seen, Has.Count.EqualTo(1), "핸들러가 없어도 지켜보는 쪽은 큐를 받아야 한다.");
        }

        [Test]
        public void TheComponentGivesTheRunnerItsGameObjectAsTarget()
        {
            var actor = Track(new GameObject("Actor"));
            var component = actor.AddComponent<GameplayEffectComponent>();
            component.ConfigureCueDispatcher(_dispatcher);

            Assert.That(component.Runner.CueTarget, Is.SameAs(actor));
            Assert.That(component.Runner.CueDispatcher, Is.SameAs(_dispatcher));
        }

        [Test]
        public void ValidationRejectsAMalformedCueTag()
        {
            var effect = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, cueTags: new[] { "Cue..Broken" }));
            var ability = Track(RunningAbilityDefinition.CreateRuntime("Ability.Dash", cueTags: new[] { "Cue..Broken" }));

            Assert.That(effect.TryValidate(out var effectError), Is.False);
            Assert.That(effectError, Does.Contain("큐 태그"));
            Assert.That(ability.TryValidate(out var abilityError), Is.False);
            Assert.That(abilityError, Does.Contain("큐 태그"));
        }

        /// <summary>만든 객체를 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 객체이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>받은 큐를 기록하는 테스트용 핸들러이다.</summary>
        private sealed class RecordingHandler : IGameplayCueHandler
        {
            /// <summary>받은 큐 목록이다.</summary>
            public List<GameplayCueEvent> Received { get; } = new();

            /// <inheritdoc />
            public void Handle(in GameplayCueEvent cue)
            {
                Received.Add(cue);
            }
        }
    }
}
