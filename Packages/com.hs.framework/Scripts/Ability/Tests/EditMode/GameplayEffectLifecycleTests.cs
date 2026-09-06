using System;
using System.Collections.Generic;
using System.Reflection;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>효과의 적용·제거 콜백 경계에서도 실제 획득한 자원만 남거나 되돌아가는지 검증한다.</summary>
    public sealed class GameplayEffectLifecycleTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private AttributeSet _attributes;
        private AttributeDefinition _health;
        private AttributeDefinition _armor;
        private GameplayTagContainer _tags;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _armor = Track(AttributeDefinition.CreateRuntime("Armor", 10f));
            _attributes.AddAttribute(_health);
            _attributes.AddAttribute(_armor);
            _tags = new GameplayTagContainer();
            _runner = new GameplayEffectRunner(_attributes, _tags);
        }

        [TearDown]
        public void TearDown()
        {
            _runner.Dispose();
            _attributes.Dispose();
            foreach (var value in _objects)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
            _objects.Clear();
        }

        [Test]
        public void TryApplyDistinguishesAnInstantExecutionFromARejectedApplication()
        {
            var instant = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { Modifier(_health, -10f) }));
            var blocked = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { Modifier(_health, -10f) }, requiredTags: new[] { "State.Ready" }));

            Assert.That(_runner.TryApply(instant, out var executed), Is.EqualTo(GameplayEffectApplicationResult.Executed));
            Assert.That(_runner.TryApply(blocked, out var rejected), Is.EqualTo(GameplayEffectApplicationResult.Rejected));
            Assert.That(executed, Is.Null);
            Assert.That(rejected, Is.Null);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(90f));
        }

        [Test]
        public void RemovingFromAppliedDoesNotSendLateCuesOrExecuteThePeriod()
        {
            var periodic = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                new[] { Modifier(_health, -10f) }, duration: 5f, period: 1f, cueTags: new[] { "Cue.Damage" }));
            var cues = new List<GameplayCueEventKind>();
            _runner.CueDispatcher = new GameplayCueDispatcher();
            using var cueSubscription = _runner.CueDispatcher.Dispatched.Subscribe(cue => cues.Add(cue.Kind));
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Applied)
                {
                    _runner.Remove(change.Effect);
                }
            });

            Assert.That(_runner.TryApply(periodic, out var effect), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));

            Assert.That(effect.IsActive, Is.False);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f));
            Assert.That(cues, Is.Empty, "아직 Applied 큐를 보내지 않았으므로 걷을 연출도 없다.");
        }

        [Test]
        public void RemovingDuringTheFirstGrantedTagDoesNotStealAnotherOwnersTag()
        {
            var first = GameplayTag.Parse("State.First");
            var shared = GameplayTag.Parse("State.Shared");
            _tags.AddTag(shared);
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_armor, 5f) }, grantedTags: new[] { first.Name, shared.Name }));
            using var subscription = _tags.Changed.Subscribe(change =>
            {
                if (change.Tag == first && change.ChangeKind == GameplayTagChangeKind.Gained)
                {
                    _runner.RemoveAll();
                }
            });

            Assert.That(_runner.TryApply(definition, out _), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));

            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(10f));
            Assert.That(_tags.GetCount(first), Is.Zero);
            Assert.That(_tags.GetCount(shared), Is.EqualTo(1));
        }

        [Test]
        public void RemovingDuringTheFirstModifierDoesNotLeaveLaterModifiersOrTags()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_health, 20f), Modifier(_armor, 5f) }, grantedTags: new[] { "State.Buffed" }));
            using var subscription = _attributes.Changed.Subscribe(change =>
            {
                if (change.Definition == _health && _runner.ActiveEffectCount > 0)
                {
                    _runner.RemoveAll();
                }
            });

            Assert.That(_runner.TryApply(definition, out _), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));

            Assert.That(_attributes.GetModifierCount(_health), Is.Zero);
            Assert.That(_attributes.GetModifierCount(_armor), Is.Zero);
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(100f));
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(10f));
            Assert.That(_tags.DistinctTagCount, Is.Zero);
        }

        [Test]
        public void DisposeRejectsReapplicationFromRemovalAndCannotBeReentered()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_armor, 5f) }, grantedTags: new[] { "State.Buffed" }));
            _runner.Apply(definition);
            var result = GameplayEffectApplicationResult.Applied;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed)
                {
                    _runner.Dispose();
                    result = _runner.TryApply(definition, out _);
                }
            });

            _runner.Dispose();

            Assert.That(result, Is.EqualTo(GameplayEffectApplicationResult.Rejected));
            Assert.That(_runner.CanApply(definition), Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(10f));
            Assert.That(_tags.DistinctTagCount, Is.Zero);
        }

        [Test]
        public void DisposingDuringModifierApplicationLeavesNoResourcesOrLateNotifications()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_health, 20f), Modifier(_armor, 5f) }, grantedTags: new[] { "State.Buffed" }));
            using var subscription = _attributes.Changed.Subscribe(_ => _runner.Dispose());

            Assert.That(_runner.TryApply(definition, out _), Is.EqualTo(GameplayEffectApplicationResult.Cancelled));
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetModifierCount(_health), Is.Zero);
            Assert.That(_attributes.GetModifierCount(_armor), Is.Zero);
            Assert.That(_tags.DistinctTagCount, Is.Zero);
        }

        [Test]
        public void RemovalCueFailureDoesNotPreventDisposingTheRemainingEffects()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                new[] { Modifier(_armor, 5f) }, grantedTags: new[] { "State.Buffed" }, cueTags: new[] { "Cue.Buff" }));
            _runner.CueDispatcher = new GameplayCueDispatcher();
            using var registration = _runner.CueDispatcher.Register(GameplayTag.Parse("Cue.Buff"), new ThrowOnRemoved());
            var first = _runner.Apply(definition);
            var second = _runner.Apply(definition);

            var failure = Assert.Throws<AggregateException>(() => _runner.Dispose());

            Assert.That(failure.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(first.IsActive || second.IsActive, Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetModifierCount(_armor), Is.Zero);
            Assert.That(_tags.DistinctTagCount, Is.Zero);
            Assert.DoesNotThrow(() => _runner.Dispose());
        }

        [Test]
        public void APeriodCallbackCannotAdvanceTheRunnerAgain()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                new[] { Modifier(_health, -10f) }, duration: 5f, period: 1f));
            var effect = _runner.Apply(definition);
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Executed)
                {
                    _runner.Tick(1f);
                }
            });

            Assert.DoesNotThrow(() => _runner.Tick(1f));

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f));
            Assert.That(effect.RemainingTime, Is.EqualTo(4f));
        }

        [Test]
        public void RemovingDuringAPeriodStopsTheRemainingModifiers()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                new[] { Modifier(_health, -10f), Modifier(_armor, -1f) }, duration: 5f, period: 1f));
            var effect = _runner.Apply(definition);
            using var subscription = _attributes.Changed.Subscribe(change =>
            {
                if (change.Definition == _health)
                {
                    _runner.Remove(effect);
                }
            });

            _runner.Tick(1f);

            Assert.That(effect.IsActive, Is.False);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(80f));
            Assert.That(_attributes.GetBaseValue(_armor), Is.EqualTo(9f));
        }

        [Test]
        public void CleanupCallbacksAreRecheckedBeforeExecutingAnIncomingEffect()
        {
            var removed = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Infinite,
                assetTags: new[] { "Effect.Old" }));
            var incoming = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { Modifier(_health, -10f) }, blockedTags: new[] { "State.Blocked" },
                removeEffectsWithTags: new[] { "Effect.Old" }));
            _runner.Apply(removed);
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed)
                {
                    _tags.AddTag(GameplayTag.Parse("State.Blocked"));
                }
            });

            Assert.That(_runner.TryApply(incoming, out _), Is.EqualTo(GameplayEffectApplicationResult.Rejected));
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(100f));
        }

        [Test]
        public void AFailingStackHandleStillReleasesTheWholeLayerAndRefreshesTheRemainingLifetime()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                new[] { Modifier(_health, 10f), Modifier(_armor, 5f) }, duration: 1f,
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget,
                    expirationPolicy: GameplayEffectStackExpirationPolicy.RemoveSingleStackAndRefreshDuration)));
            var effect = _runner.Apply(definition);
            _runner.Apply(definition);
            // 실제 수정자 손잡이의 해제 뒤 실패를 주입해 다음 손잡이와 만료 상태의 정리를 함께 검증한다.
            var layers = (List<List<IDisposable>>)typeof(ActiveGameplayEffect)
                .GetField("_stackModifierHandles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effect);
            layers[1][0] = new ThrowAfterDispose(layers[1][0]);
            var stackChanges = 0;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.StackChanged)
                {
                    stackChanges++;
                }
            });

            Assert.Throws<AggregateException>(() => _runner.Tick(1f));

            Assert.That(effect.StackCount, Is.EqualTo(1));
            Assert.That(effect.RemainingTime, Is.EqualTo(1f));
            Assert.That(stackChanges, Is.EqualTo(1));
            Assert.That(_attributes.GetCurrentValue(_health), Is.EqualTo(110f));
            Assert.That(_attributes.GetCurrentValue(_armor), Is.EqualTo(15f));
            _runner.Tick(0.5f);
            Assert.That(effect.IsActive, Is.True);
            Assert.That(effect.RemainingTime, Is.EqualTo(0.5f));
            _runner.Tick(0.5f);
            Assert.That(effect.IsActive, Is.False);
            Assert.That(_attributes.GetModifierCount(_health), Is.Zero);
            Assert.That(_attributes.GetModifierCount(_armor), Is.Zero);
        }

        private GameplayEffectModifier Modifier(AttributeDefinition attribute, float magnitude)
            => GameplayEffectModifier.CreateRuntime(attribute, AttributeModifierOperation.Add, magnitude);

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }

        private sealed class ThrowOnRemoved : IGameplayCueHandler
        {
            public void Handle(in GameplayCueEvent cue)
            {
                if (cue.Kind == GameplayCueEventKind.Removed)
                {
                    throw new InvalidOperationException("removal cue failed");
                }
            }
        }

        private sealed class ThrowAfterDispose : IDisposable
        {
            private readonly IDisposable _inner;
            public ThrowAfterDispose(IDisposable inner) => _inner = inner;

            public void Dispose()
            {
                _inner.Dispose();
                throw new InvalidOperationException("stack handle failed");
            }
        }
    }
}
