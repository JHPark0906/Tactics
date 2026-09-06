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
    /// <summary>유지 중인 효과가 어빌리티를 부여하고, 효과가 걷히거나 억제되면 거두는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 장비를 끼거나 변신하는 동안만 쓸 수 있는 어빌리티가 이 형태이다. 부여하는 것이 소유권이므로,
    /// 다른 데서 이미 부여되어 있던 어빌리티는 효과가 걷혀도 남아야 한다.
    /// </remarks>
    public sealed class GameplayAbilityGrantedByEffectTests
    {
        private static readonly GameplayTag Grounded = GameplayTag.Parse("State.Grounded");

        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayEffectRunner _runner;
        private GameplayAbilitySystem _system;
        private RunningAbilityDefinition _dash;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _runner = new GameplayEffectRunner(_attributes, new GameplayTagContainer());
            _system = new GameplayAbilitySystem(_runner);
            _dash = Track(RunningAbilityDefinition.CreateRuntime("Ability.Dash"));
        }

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
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
        public void ApplyingTheEffectGrantsTheAbilityAndRemovingItRevokes()
        {
            var boots = CreateGrantingEffect(GameplayEffectDurationPolicy.Infinite);

            var effect = _runner.Apply(boots);
            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.True, "효과가 작용하기 시작하면 어빌리티가 부여되어야 한다.");

            _runner.Remove(effect);

            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.False, "효과가 걷히면 부여도 거두어야 한다.");
        }

        [Test]
        public void ExpiryRevokesAndCancelsTheAbilityIfItIsRunning()
        {
            var boots = CreateGrantingEffect(GameplayEffectDurationPolicy.Duration, duration: 1f);
            _runner.Apply(boots);
            Assert.That(_system.TryActivate(_dash.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            _system.TryGetAbility(_dash.AbilityTag, out var dash);

            _runner.Tick(1f);

            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.False);
            Assert.That(dash.IsActive, Is.False, "거둘 때 활성 중이면 취소되어 잡고 있던 것을 놓아야 한다.");
            Assert.That(dash.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Cancelled));
        }

        [Test]
        public void InhibitionRevokesAndReenablingGrantsAgain()
        {
            _system.Tags.AddTag(Grounded);
            var boots = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                ongoingRequiredTags: new[] { "State.Grounded" },
                abilities: new[] { _dash }));
            _runner.Apply(boots);
            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.True);

            _system.Tags.RemoveTag(Grounded);
            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.False, "억제된 효과의 어빌리티는 거두어야 한다.");

            _system.Tags.AddTag(Grounded);

            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.True, "억제가 풀리면 다시 부여해야 한다.");
        }

        [Test]
        public void AnEffectAppliedWhileInhibitedGrantsNothingUntilEnabled()
        {
            var boots = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                ongoingRequiredTags: new[] { "State.Grounded" },
                abilities: new[] { _dash }));

            _runner.Apply(boots);
            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.False);

            _system.Tags.AddTag(Grounded);

            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.True);
        }

        [Test]
        public void AnAbilityGrantedElsewhereSurvivesTheEffect()
        {
            _system.GrantAbility(_dash);
            var boots = CreateGrantingEffect(GameplayEffectDurationPolicy.Infinite);

            var effect = _runner.Apply(boots);
            _runner.Remove(effect);

            Assert.That(_system.IsGranted(_dash.AbilityTag), Is.True, "부여하는 것이 소유권이다. 남이 부여한 것을 거두면 안 된다.");
        }

        [Test]
        public void AnAbilityThatActivatesOnGrantStartsWhenTheEffectGrantsIt()
        {
            var passive = new RunningAbility();
            var passiveDefinition = Track(RunningAbilityDefinition.CreateRuntime("Ability.Passive", passive, activateOnGrant: true));
            var aura = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, abilities: new[] { passiveDefinition }));

            _runner.Apply(aura);

            Assert.That(passive.IsActive, Is.True);
        }

        [Test]
        public void ValidationRejectsGrantedAbilitiesOnAnInstantEffectAndEmptyEntries()
        {
            var instant = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant, abilities: new[] { _dash }));
            var withEmpty = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, abilities: new GameplayAbilityDefinition[] { null }));

            Assert.That(instant.TryValidate(out var instantError), Is.False);
            Assert.That(instantError, Does.Contain("어빌리티"));
            Assert.That(withEmpty.TryValidate(out var emptyError), Is.False);
            Assert.That(emptyError, Is.Not.Null);
        }

        [Test]
        public void DisposingTheSystemStopsGrantingFromEffects()
        {
            _system.Dispose();
            var boots = CreateGrantingEffect(GameplayEffectDurationPolicy.Infinite);

            _runner.Apply(boots);

            Assert.That(_system.GrantedAbilityCount, Is.Zero);
        }

        /// <summary>대시 어빌리티를 부여하는 효과를 만든다.</summary>
        private GameplayEffectDefinition CreateGrantingEffect(GameplayEffectDurationPolicy policy, float duration = 0f)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(policy, duration: duration, abilities: new[] { _dash }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingAnEffectDoesNotRevokeAReplacementGrant(bool reuseInstance)
        {
            var effect = _runner.Apply(CreateGrantingEffect(GameplayEffectDurationPolicy.Infinite));
            _system.TryGetAbility(_dash.AbilityTag, out var original);
            _system.RevokeAbility(_dash.AbilityTag);
            var replacementDefinition = reuseInstance ? _dash
                : Track(RunningAbilityDefinition.CreateRuntime(_dash.AbilityTag.Name));
            var replacement = _system.GrantAbility(replacementDefinition);

            _runner.Remove(effect);

            Assert.That(_system.TryGetAbility(_dash.AbilityTag, out var remaining), Is.True);
            Assert.That(remaining, Is.SameAs(replacement));
            if (reuseInstance)
            {
                Assert.That(remaining, Is.SameAs(original), "같은 인스턴스라도 새 부여는 이전 효과의 소유가 아니다.");
            }
        }

        [Test]
        public void RemovingTheEffectFromGrantedRevokesTheCurrentGrantAndSkipsTheRest()
        {
            var second = Track(RunningAbilityDefinition.CreateRuntime("Ability.Second"));
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                abilities: new[] { _dash, second }));
            using var subscription = _system.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayAbilityChangeKind.Granted)
                {
                    _runner.RemoveAll();
                }
            });

            Assert.That(_runner.TryApply(definition, out _), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void ClearDuringTheGrantedNotificationDoesNotRecreateEffectOwnership()
        {
            var second = Track(RunningAbilityDefinition.CreateRuntime("Ability.Second"));
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                abilities: new[] { _dash, second }));
            using (var subscription = _system.Changed.Subscribe(change =>
                   {
                       if (change.ChangeKind == GameplayAbilityChangeKind.Granted)
                       {
                           _system.Clear();
                       }
                   }))
            {
                Assert.DoesNotThrow(() => _runner.Apply(definition));
            }

            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            var manuallyGranted = _system.GrantAbility(_dash);
            _runner.RemoveAll();
            Assert.That(_system.TryGetAbility(_dash.AbilityTag, out var remaining), Is.True);
            Assert.That(remaining, Is.SameAs(manuallyGranted));
        }

        [Test]
        public void DisposeInsideABulkRemovalStillRevokesAbilitiesOwnedByLaterEffects()
        {
            var first = _runner.Apply(Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite)));
            var second = _runner.Apply(CreateGrantingEffect(GameplayEffectDurationPolicy.Infinite));
            var removed = new List<ActiveGameplayEffect>();
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed)
                {
                    removed.Add(change.Effect);
                    if (ReferenceEquals(change.Effect, first))
                    {
                        _runner.Dispose();
                    }
                }
            });

            _runner.RemoveAll();

            Assert.That(removed, Is.EqualTo(new[] { first, second }));
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void RemovingTheGrantingEffectInsideTheAbilityFactoryLeavesNoGrant()
        {
            var factory = Track(CallbackAbilityDefinition.Create(() => _runner.RemoveAll()));
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                abilities: new[] { factory }));

            Assert.That(_runner.TryApply(definition, out _), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            Assert.That(factory.Ability.StartCount, Is.Zero);
        }

        [Test]
        public void AnOldGrantDoesNotAutoActivateTheSameInstanceAfterItHasBeenRegranted()
        {
            var definition = Track(CallbackAbilityDefinition.Create(null, activateOnGrant: true));
            var replaced = false;
            using var subscription = _system.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayAbilityChangeKind.Granted && !replaced)
                {
                    replaced = true;
                    _system.RevokeAbility(definition.AbilityTag);
                    _system.GrantAbility(definition);
                }
            });

            _system.GrantAbility(definition);

            Assert.That(definition.Ability.StartCount, Is.EqualTo(1));
            Assert.That(_system.GrantedAbilityCount, Is.EqualTo(1));
        }

        private sealed class CallbackAbility : GameplayAbility
        {
            public int StartCount;
            protected override void OnActivate() => StartCount++;
        }

        private sealed class CallbackAbilityDefinition : GameplayAbilityDefinition
        {
            private System.Action _beforeCreate;
            public CallbackAbility Ability { get; } = new();

            public override GameplayAbility CreateAbility()
            {
                _beforeCreate?.Invoke();
                return Ability;
            }

            public static CallbackAbilityDefinition Create(System.Action beforeCreate, bool activateOnGrant = false)
            {
                var definition = CreateInstance<CallbackAbilityDefinition>();
                definition._beforeCreate = beforeCreate;
                definition.ConfigureRuntime("Ability.Callback", activateOnGranted: activateOnGrant);
                return definition;
            }
        }

        /// <summary>만든 에셋을 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 에셋이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
