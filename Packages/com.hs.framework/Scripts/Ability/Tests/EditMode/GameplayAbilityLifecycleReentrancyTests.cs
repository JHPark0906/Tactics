using System;
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
    /// <summary>동기 콜백 사이에도 활성화 하나의 소유권과 정리가 유지되는지 검증한다.</summary>
    public sealed class GameplayAbilityLifecycleReentrancyTests
    {
        private readonly List<UnityEngine.Object> _objects = new();
        private AttributeSet _attributes;
        private GameplayEffectRunner _effects;
        private GameplayAbilitySystem _system;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _effects = new GameplayEffectRunner(_attributes);
            _system = new GameplayAbilitySystem(_effects);
        }

        [TearDown]
        public void TearDown()
        {
            _system.Dispose();
            _effects.Dispose();
            _attributes.Dispose();
            foreach (var value in _objects)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }

            _objects.Clear();
        }

        [Test]
        public void AnActiveTagThatTriggersItsOwnAbilityDoesNotCreateASecondActivation()
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime(
                "Ability.Self", activeTags: new[] { "State.Self" },
                triggerOnTagGained: new[] { "State.Self" }));
            var ability = _system.GrantAbility(definition);
            var stateTag = GameplayTag.Parse("State.Self");

            _system.TryActivate(definition.AbilityTag);

            Assert.That(ability.ActivationCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.EqualTo(new[] { ability }));
            Assert.That(_system.Tags.GetCount(stateTag), Is.EqualTo(1));

            _system.CancelAbility(definition.AbilityTag);

            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.Tags.GetCount(stateTag), Is.Zero);
            Assert.That(((RunningAbility)ability).EndCount, Is.EqualTo(1));
        }

        [Test]
        public void PayingTheCostCannotReenterTheSameActivation()
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes.AddAttribute(stamina);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f) }));
            var ability = new LifecycleAbility();
            var definition = Grant(ability, cost: cost);
            var reentrantResult = GameplayAbilityActivationResult.Success;
            using var subscription = _attributes.Changed.Subscribe(_ =>
                reentrantResult = _system.TryActivate(definition.AbilityTag));

            _system.TryActivate(definition.AbilityTag);

            Assert.That(reentrantResult, Is.EqualTo(GameplayAbilityActivationResult.AlreadyActive));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(90f));
            Assert.That(ability.StartCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities.Count, Is.EqualTo(1));
        }

        [Test]
        public void CancellationDuringTheFirstTagDoesNotRemoveTagsThisActivationNeverAdded()
        {
            var first = GameplayTag.Parse("State.First");
            var shared = GameplayTag.Parse("State.Shared");
            _system.Tags.AddTag(shared);
            var ability = new LifecycleAbility();
            var definition = Grant(ability, activeTags: new[] { first.Name, shared.Name },
                blockAbilitiesWithTags: new[] { "Ability.Other" });
            using var subscription = _system.Tags.Changed.Subscribe(change =>
            {
                if (change.Tag == first && change.ChangeKind == GameplayTagChangeKind.Gained)
                {
                    _system.CancelAbility(definition.AbilityTag);
                }
            });

            _system.TryActivate(definition.AbilityTag);

            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.BlockedAbilityTags, Is.Empty);
            Assert.That(_system.Tags.GetCount(first), Is.Zero);
            Assert.That(_system.Tags.GetCount(shared), Is.EqualTo(1), "다른 부여자의 태그는 그대로 남아야 한다.");
            Assert.That(ability.StartCount, Is.Zero, "준비 중 취소되면 사용자 행동을 시작하지 않는다.");
            Assert.That(ability.EndCount, Is.Zero);
        }

        [Test]
        public void CancellationInsideOnActivateCleansUpAfterTheCallbackReturns()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            ability.WhenStarted = () =>
            {
                _system.CancelAbility(definition.AbilityTag);
                ability.HoldsResource = true;
            };
            var changes = new List<GameplayAbilityChangeKind>();
            using var subscription = _system.Changed.Subscribe(change => changes.Add(change.ChangeKind));

            _system.TryActivate(definition.AbilityTag);

            Assert.That(ability.HoldsResource, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(changes, Is.EqualTo(new[] { GameplayAbilityChangeKind.Activated, GameplayAbilityChangeKind.Ended }));
        }

        [Test]
        public void RevokingDoesNotDeleteAReplacementGrantedByTheEndCallback()
        {
            var original = new LifecycleAbility();
            var definition = Grant(original);
            var replacement = new LifecycleAbility();
            var replacementDefinition = Track(LifecycleDefinition.Create(replacement, activateOnGrant: true));
            original.WhenEnded = () => _system.GrantAbility(replacementDefinition);
            _system.TryActivate(definition.AbilityTag);

            _system.RevokeAbility(definition.AbilityTag);

            Assert.That(_system.TryGetAbility(definition.AbilityTag, out var granted), Is.True);
            Assert.That(granted, Is.SameAs(replacement));
            Assert.That(replacement.IsActive, Is.True);
            Assert.That(original.EndCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.EqualTo(new[] { replacement }));
        }

        [Test]
        public void ATagLossCallbackCannotRestartTheInstanceBeforeItsCleanupFinishes()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability, activeTags: new[] { "State.Active" });
            _system.TryActivate(definition.AbilityTag);
            var result = GameplayAbilityActivationResult.Success;
            using (var subscription = _system.Tags.Changed.Subscribe(change =>
                   {
                       if (change.ChangeKind == GameplayTagChangeKind.Lost)
                       {
                           result = _system.TryActivate(definition.AbilityTag);
                       }
                   }))
            {
                _system.CancelAbility(definition.AbilityTag);
            }

            Assert.That(result, Is.EqualTo(GameplayAbilityActivationResult.AlreadyActive));
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(ability.HoldsResource, Is.True);
        }

        [Test]
        public void RevokingFromTheGrantedNotificationPreventsAutomaticActivation()
        {
            var ability = new LifecycleAbility();
            var definition = Track(LifecycleDefinition.Create(ability, activateOnGrant: true));
            using var subscription = _system.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayAbilityChangeKind.Granted)
                {
                    _system.RevokeAbility(change.Definition.AbilityTag);
                }
            });

            _system.GrantAbility(definition);

            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void DisposingFromOnActivateDoesNotPublishToDisposedStreamsOrLeakResources()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            ability.WhenStarted = () =>
            {
                _system.Dispose();
                ability.HoldsResource = true;
            };

            Assert.DoesNotThrow(() => _system.TryActivate(definition.AbilityTag));

            Assert.That(ability.HoldsResource, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.GrantAbility(definition), Is.Null);
        }

        [Test]
        public void AnotherSystemCannotEndAnAbilityItDoesNotOwn()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            _system.TryActivate(definition.AbilityTag);
            using var other = new GameplayAbilitySystem(_effects);

            Assert.That(other.EndAbility(ability, GameplayAbilityEndReason.Cancelled), Is.False);
            Assert.That(ability.IsActive, Is.True);
            Assert.That(ability.EndCount, Is.Zero);
        }

        [Test]
        public void DisposeFinishesEveryAbilityEvenWhenTheFirstEndCallbackThrows()
        {
            var first = new LifecycleAbility();
            var firstDefinition = Grant(first, activeTags: new[] { "State.Shared" },
                blockAbilitiesWithTags: new[] { "Ability.Blocked" });
            var second = new LifecycleAbility();
            var secondDefinition = Track(LifecycleDefinition.Create(second,
                activeTags: new[] { "State.Shared" }, blockAbilitiesWithTags: new[] { "Ability.Blocked" },
                tagName: "Ability.Second"));
            _system.GrantAbility(secondDefinition);
            _system.TryActivate(firstDefinition.AbilityTag);
            _system.TryActivate(secondDefinition.AbilityTag);
            first.WhenEnded = () => throw new InvalidOperationException("first cleanup failed");

            var failure = Assert.Throws<AggregateException>(() => _system.Dispose());

            Assert.That(failure.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(failure.InnerExceptions[0].Message, Is.EqualTo("first cleanup failed"));
            Assert.That(first.EndCount, Is.EqualTo(1));
            Assert.That(second.EndCount, Is.EqualTo(1), "앞선 종료의 예외가 뒤 어빌리티 정리를 막으면 안 된다.");
            Assert.That(first.HoldsResource || second.HoldsResource, Is.False);
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.BlockedAbilityTags, Is.Empty);
            Assert.That(_system.Tags.GetCount(GameplayTag.Parse("State.Shared")), Is.Zero);
            Assert.DoesNotThrow(() => _system.Dispose());
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DeferredEndPreservesClearPolicyWithoutBlockingOrdinaryCancellationTriggers(bool clear)
        {
            var first = new LifecycleAbility();
            var firstDefinition = Grant(first);
            var second = new LifecycleAbility();
            var secondDefinition = Track(LifecycleDefinition.Create(second, activateOnGrant: true,
                tagName: "Ability.Second"));
            GameplayAbility grantedFromEnd = null;
            first.WhenStarted = () =>
            {
                if (clear)
                {
                    _system.Clear();
                }
                else
                {
                    _system.CancelAbility(firstDefinition.AbilityTag);
                }
            };
            first.WhenEnded = () => grantedFromEnd = _system.GrantAbility(secondDefinition);

            _system.TryActivate(firstDefinition.AbilityTag);

            Assert.That(first.EndCount, Is.EqualTo(1));
            if (clear)
            {
                Assert.That(grantedFromEnd, Is.Null, "Clear의 지연 정리 콜백도 새 부여를 만들면 안 된다.");
                Assert.That(_system.GrantedAbilityCount, Is.Zero);
                Assert.That(_system.ActiveAbilities, Is.Empty);
                Assert.That(_system.GrantAbility(secondDefinition), Is.SameAs(second), "정리가 끝나면 다시 사용할 수 있다.");
                Assert.That(second.IsActive, Is.True);
            }
            else
            {
                Assert.That(grantedFromEnd, Is.SameAs(second), "보통 취소의 다른 어빌리티 트리거는 계속 허용한다.");
                Assert.That(second.IsActive, Is.True);
            }
        }

        [Test]
        public void RevocationInsideTheCustomConditionDoesNotActivateAnUngrantedInstance()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            var nestedResult = GameplayAbilityActivationResult.Success;
            ability.WhenChecked = () =>
            {
                nestedResult = _system.TryActivate(definition.AbilityTag);
                _system.RevokeAbility(definition.AbilityTag);
                return true;
            };

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.NotGranted));
            Assert.That(nestedResult, Is.EqualTo(GameplayAbilityActivationResult.AlreadyActive));
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);

            ability.WhenChecked = null;
            Assert.That(_system.GrantAbility(definition), Is.SameAs(ability), "끝난 판정의 예약도 남지 않아야 한다.");
        }

        [Test]
        public void AThrowingActivationReleasesItsTagsAndReservationBeforeRethrowing()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability, activeTags: new[] { "State.Active" });
            ability.WhenStarted = () => throw new InvalidOperationException("activation failed");

            Assert.Throws<InvalidOperationException>(() => _system.TryActivate(definition.AbilityTag));

            Assert.That(ability.HoldsResource, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.Tags.HasTag(GameplayTag.Parse("State.Active")), Is.False);

            ability.WhenStarted = null;
            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void AFinishingTickDoesNotEndANewActivationStartedInsideThatTick()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            _system.TryActivate(definition.AbilityTag);
            ability.WhenTicked = deltaTime =>
            {
                if (deltaTime > 0f)
                {
                    _system.CancelAbility(definition.AbilityTag);
                    _system.TryActivate(definition.AbilityTag);
                    return GameplayAbilityTickResult.Finished;
                }

                return GameplayAbilityTickResult.Running;
            };

            _system.Tick(1f);

            Assert.That(ability.IsActive, Is.True);
            Assert.That(ability.ActivationCount, Is.EqualTo(2));
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(ability.ActiveTime, Is.Zero);
        }

        [Test]
        public void NestedTicksDoNotAdvanceTheSameAbilityTwice()
        {
            var ability = new LifecycleAbility();
            var definition = Grant(ability);
            _system.TryActivate(definition.AbilityTag);
            ability.WhenTicked = _ =>
            {
                _system.Tick(1f);
                return GameplayAbilityTickResult.Running;
            };

            Assert.DoesNotThrow(() => _system.Tick(1f));
            Assert.That(ability.ActiveTime, Is.EqualTo(1f));
        }

        [TestCase(50f, GameplayAbilityActivationResult.CostNotAffordable, 50f)]
        [TestCase(60f, GameplayAbilityActivationResult.Success, 0f)]
        [TestCase(100f, GameplayAbilityActivationResult.Success, 40f)]
        public void RepeatedCostsOnTheSameResourceMustBeAffordableTogether(
            float available, GameplayAbilityActivationResult expected, float remaining)
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", available));
            _attributes.AddAttribute(stamina);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant, new[]
            {
                GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -30f),
                GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -30f)
            }));
            var definition = Grant(new LifecycleAbility(), cost: cost);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(expected));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(remaining));
        }

        private LifecycleDefinition Grant(LifecycleAbility ability, GameplayEffectDefinition cost = null,
            IEnumerable<string> activeTags = null, IEnumerable<string> blockAbilitiesWithTags = null)
        {
            var definition = Track(LifecycleDefinition.Create(ability, cost, activeTags, blockAbilitiesWithTags));
            _system.GrantAbility(definition);
            return definition;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EffectRequirementsRejectCostOrCooldownBeforeAnyCostIsPaid(bool rejectCooldown)
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes.AddAttribute(stamina);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f) },
                requiredTags: rejectCooldown ? null : new[] { "State.Ready" }));
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                duration: 2f, grantedTags: new[] { "Cooldown.Test" },
                requiredTags: rejectCooldown ? new[] { "State.Ready" } : null));
            var ability = new LifecycleAbility();
            var definition = Track(LifecycleDefinition.Create(ability, cost: cost, cooldown: cooldown));
            _system.GrantAbility(definition);
            var expected = rejectCooldown ? GameplayAbilityActivationResult.CooldownApplicationFailed
                : GameplayAbilityActivationResult.CostApplicationFailed;

            Assert.That(_system.CanActivate(definition.AbilityTag), Is.EqualTo(expected));
            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(expected));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(100f));
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_effects.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void ACostCallbackThatBlocksTheCooldownDoesNotStartTheAbility()
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes.AddAttribute(stamina);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f) }));
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                duration: 2f, grantedTags: new[] { "Cooldown.Test" }, blockedTags: new[] { "State.Blocked" }));
            var ability = new LifecycleAbility();
            var definition = Track(LifecycleDefinition.Create(ability, cost: cost, cooldown: cooldown));
            _system.GrantAbility(definition);
            using var subscription = _attributes.Changed.Subscribe(_ =>
                _system.Tags.AddTag(GameplayTag.Parse("State.Blocked")));

            Assert.That(_system.CanActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.CooldownApplicationFailed));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(90f), "이미 실행한 즉시 코스트는 되돌리지 않는다.");
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_effects.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void ACooldownRemovedFromAppliedDoesNotCountAsAStartedAbility()
        {
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                duration: 2f, grantedTags: new[] { "Cooldown.Test" }));
            var ability = new LifecycleAbility();
            var definition = Track(LifecycleDefinition.Create(ability, cooldown: cooldown));
            _system.GrantAbility(definition);
            using var subscription = _effects.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Applied)
                {
                    _effects.Remove(change.Effect);
                }
            });

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.CooldownApplicationFailed));
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_system.IsOnCooldown(definition), Is.False);
        }

        [Test]
        public void CancellationDuringCostPaymentStopsTheRemainingCostModifiers()
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes.AddAttribute(stamina);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant, new[]
            {
                GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f),
                GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f)
            }));
            var ability = new LifecycleAbility();
            var definition = Grant(ability, cost);
            using var subscription = _attributes.Changed.Subscribe(_ => _system.CancelAbility(definition.AbilityTag));

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Cancelled));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(90f));
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void PreparationCancellationRemovesTheCooldownItJustApplied()
        {
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Duration,
                duration: 2f, grantedTags: new[] { "Cooldown.Test" }));
            var ability = new LifecycleAbility();
            var definition = Track(LifecycleDefinition.Create(ability, cooldown: cooldown,
                activeTags: new[] { "State.Starting" }));
            _system.GrantAbility(definition);
            using var subscription = _system.Tags.Changed.Subscribe(change =>
            {
                if (change.Tag == GameplayTag.Parse("State.Starting") && change.ChangeKind == GameplayTagChangeKind.Gained)
                {
                    _system.CancelAbility(definition.AbilityTag);
                }
            });

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Cancelled));
            Assert.That(ability.StartCount, Is.Zero);
            Assert.That(_system.ActiveAbilities, Is.Empty);
            Assert.That(_effects.ActiveEffectCount, Is.Zero);
            Assert.That(_system.Tags.DistinctTagCount, Is.Zero);
        }

        [Test]
        public void CustomActivationChecksCannotInvalidateTheCostThenStartForFree()
        {
            var stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _attributes.AddAttribute(stamina);
            var ready = GameplayTag.Parse("State.Ready");
            _system.Tags.AddTag(ready);
            var cost = Track(GameplayEffectDefinition.CreateRuntime(GameplayEffectDurationPolicy.Instant,
                new[] { GameplayEffectModifier.CreateRuntime(stamina, AttributeModifierOperation.Add, -10f) },
                requiredTags: new[] { ready.Name }));
            var ability = new LifecycleAbility();
            var definition = Grant(ability, cost);
            ability.WhenChecked = () =>
            {
                _system.Tags.RemoveTag(ready);
                return true;
            };

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.CostApplicationFailed));
            Assert.That(_attributes.GetBaseValue(stamina), Is.EqualTo(100f));
            Assert.That(ability.StartCount, Is.Zero);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }

        private sealed class LifecycleAbility : GameplayAbility
        {
            public Action WhenStarted;
            public Action WhenEnded;
            public Func<bool> WhenChecked;
            public Func<float, GameplayAbilityTickResult> WhenTicked;
            public bool HoldsResource;
            public int StartCount;
            public int EndCount;

            public override bool CanActivate() => WhenChecked?.Invoke() ?? true;

            protected override void OnActivate()
            {
                StartCount++;
                HoldsResource = true;
                WhenStarted?.Invoke();
            }

            protected override void OnEnd(GameplayAbilityEndReason endReason)
            {
                EndCount++;
                HoldsResource = false;
                WhenEnded?.Invoke();
            }

            protected override GameplayAbilityTickResult OnTick(float deltaTime)
                => WhenTicked?.Invoke(deltaTime) ?? GameplayAbilityTickResult.Running;
        }

        private sealed class LifecycleDefinition : GameplayAbilityDefinition
        {
            private GameplayAbility _ability;
            public override GameplayAbility CreateAbility() => _ability;

            public static LifecycleDefinition Create(GameplayAbility ability, GameplayEffectDefinition cost = null,
                IEnumerable<string> activeTags = null, IEnumerable<string> blockAbilitiesWithTags = null,
                bool activateOnGrant = false, string tagName = "Ability.Test", GameplayEffectDefinition cooldown = null)
            {
                var definition = CreateInstance<LifecycleDefinition>();
                definition._ability = ability;
                definition.ConfigureRuntime(tagName, cost: cost, cooldown: cooldown, activeTags: activeTags,
                    blockAbilitiesWithTags: blockAbilitiesWithTags, activateOnGranted: activateOnGrant);
                return definition;
            }
        }
    }
}
