using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>어빌리티 활성화 판정과 진행, 종료 경로를 검증한다.</summary>
    public sealed class GameplayAbilitySystemTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        /// <summary>코스트로 소모할 자원 어트리뷰트이다.</summary>
        private AttributeDefinition _stamina;

        /// <summary>효과가 값을 바꾸는 대상 어트리뷰트이다.</summary>
        private AttributeDefinition _health;

        /// <summary>대상의 어트리뷰트 집합이다.</summary>
        private AttributeSet _attributes;

        /// <summary>검증 대상 어빌리티 시스템이다.</summary>
        private GameplayAbilitySystem _system;

        [SetUp]
        public void SetUp()
        {
            _stamina = Track(AttributeDefinition.CreateRuntime("Stamina", 100f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_stamina);
            _attributes.AddAttribute(_health);
            _system = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes));
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
        public void ActivatingAnUngrantedAbilityIsReportedAsNotGranted()
        {
            var missingTag = GameplayTag.Parse("Ability.TakeCover");

            Assert.That(_system.IsGranted(missingTag), Is.False);
            Assert.That(_system.CanActivate(missingTag), Is.EqualTo(GameplayAbilityActivationResult.NotGranted));
            Assert.That(
                _system.TryActivate(missingTag),
                Is.EqualTo(GameplayAbilityActivationResult.NotGranted),
                "없는 어빌리티는 기다려도 생기지 않으므로 다른 실패와 구분되어야 한다.");
        }

        [Test]
        public void AGrantedAbilityActivatesAndFinishesImmediatelyByDefault()
        {
            var ability = new TrackedAbility();
            var definition = GrantTracked("Ability.Attack", ability);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(ability.ActivateCount, Is.EqualTo(1));
            Assert.That(ability.EndCount, Is.EqualTo(1), "종료 조건을 재정의하지 않은 어빌리티는 곧바로 끝난다.");
            Assert.That(ability.IsActive, Is.False);
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void AnOngoingAbilityStaysActiveUntilItsOwnConditionEndsIt()
        {
            var ability = new TrackedAbility { RemainingTicks = 2 };
            var definition = GrantTracked("Ability.TakeCover", ability);

            _system.TryActivate(definition.AbilityTag);
            Assert.That(ability.IsActive, Is.True);
            Assert.That(_system.IsActive(definition.AbilityTag), Is.True);

            _system.Tick(1f);
            Assert.That(ability.IsActive, Is.True);

            _system.Tick(1f);

            Assert.That(ability.IsActive, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(ability.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Completed));
        }

        [Test]
        public void CancellingReleasesWhatTheAbilityWasHolding()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);

            Assert.That(ability.HasReservation, Is.True);

            Assert.That(_system.CancelAbility(definition.AbilityTag), Is.True);

            Assert.That(ability.HasReservation, Is.False, "취소 경로에서도 잡고 있던 것을 놓아야 한다.");
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(ability.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Cancelled));
        }

        [Test]
        public void RevokingAnActiveAbilityReleasesWhatItWasHolding()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);

            Assert.That(_system.RevokeAbility(definition.AbilityTag), Is.True);

            Assert.That(ability.HasReservation, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(_system.IsGranted(definition.AbilityTag), Is.False);
        }

        [Test]
        public void ClearingTheSystemReleasesWhatEveryAbilityWasHolding()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);

            _system.Clear();

            Assert.That(ability.HasReservation, Is.False);
            Assert.That(ability.EndCount, Is.EqualTo(1));
            Assert.That(_system.GrantedAbilityCount, Is.Zero);
        }

        [Test]
        public void EndIsAnnouncedExactlyOnceEvenWhenCancelledTwice()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);

            Assert.That(_system.CancelAbility(definition.AbilityTag), Is.True);
            Assert.That(_system.CancelAbility(definition.AbilityTag), Is.False);

            Assert.That(ability.EndCount, Is.EqualTo(1));
        }

        [Test]
        public void AnAbilityThatNeverEndsIsForceEndedByTheSafetyNet()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.Stuck", ability, maxDuration: 2f);
            _system.TryActivate(definition.AbilityTag);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("최대 활성 시간"));

            _system.Tick(1f);
            Assert.That(ability.IsActive, Is.True);

            _system.Tick(1f);

            Assert.That(ability.IsActive, Is.False, "종료 조건이 어긋나도 그물이 걷어야 한다.");
            Assert.That(ability.HasReservation, Is.False);
        }

        [Test]
        public void TheSameAbilityIsNotActivatedTwiceAtOnce()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);

            Assert.That(
                _system.TryActivate(definition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.AlreadyActive));
            Assert.That(ability.ActivateCount, Is.EqualTo(1));
        }

        [Test]
        public void DifferentAbilitiesRunAtTheSameTimeUnlessTagsForbidIt()
        {
            var moveAbility = new TrackedAbility { RemainingTicks = int.MaxValue };
            var aimAbility = new TrackedAbility { RemainingTicks = int.MaxValue };
            var move = GrantTracked("Ability.Move", moveAbility);
            var aim = GrantTracked("Ability.Aim", aimAbility);

            _system.TryActivate(move.AbilityTag);
            Assert.That(_system.TryActivate(aim.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(_system.ActiveAbilities.Count, Is.EqualTo(2));
        }

        [Test]
        public void AnActiveTagCanMakeTwoAbilitiesExclusive()
        {
            var coverAbility = new TrackedAbility { RemainingTicks = int.MaxValue };
            var dashAbility = new TrackedAbility { RemainingTicks = int.MaxValue };
            var cover = GrantTracked("Ability.TakeCover", coverAbility, activeTags: new[] { "State.InCover" });
            var dash = GrantTracked("Ability.Dash", dashAbility, blockedTags: new[] { "State.InCover" });

            _system.TryActivate(cover.AbilityTag);

            Assert.That(_system.Tags.HasTag(GameplayTag.Parse("State.InCover")), Is.True);
            Assert.That(_system.TryActivate(dash.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.BlockedByTags));

            _system.CancelAbility(cover.AbilityTag);

            Assert.That(
                _system.Tags.HasTag(GameplayTag.Parse("State.InCover")),
                Is.False,
                "활성 태그는 어빌리티가 끝날 때 회수되어야 한다.");
            Assert.That(_system.TryActivate(dash.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void ARequiredTagMustBePresentBeforeActivation()
        {
            var definition = GrantTracked("Ability.Execute", new TrackedAbility(), requiredTags: new[] { "State.Marked" });

            Assert.That(
                _system.TryActivate(definition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.MissingRequiredTags));

            _system.Tags.AddTag(GameplayTag.Parse("State.Marked"));

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void CostIsCheckedBeforeItIsPaid()
        {
            var costEffect = CreateEffect(GameplayEffectDurationPolicy.Instant, _stamina, -30f);
            var definition = GrantTracked("Ability.Sprint", new TrackedAbility(), cost: costEffect);
            _attributes.SetBaseValue(_stamina, 20f);

            Assert.That(
                _system.TryActivate(definition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.CostNotAffordable));
            Assert.That(
                _attributes.GetBaseValue(_stamina),
                Is.EqualTo(20f),
                "낼 수 없으면 자원을 건드리지 않아야 한다.");

            _attributes.SetBaseValue(_stamina, 50f);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_attributes.GetBaseValue(_stamina), Is.EqualTo(20f));
        }

        [Test]
        public void CooldownIsExpressedAsADurationEffectThatGrantsATag()
        {
            var cooldownEffect = CreateTagEffect("Cooldown.Attack", duration: 2f);
            var definition = GrantTracked("Ability.Attack", new TrackedAbility(), cooldown: cooldownEffect);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(_system.IsOnCooldown(definition), Is.True);
            Assert.That(
                _system.TryActivate(definition.AbilityTag),
                Is.EqualTo(GameplayAbilityActivationResult.OnCooldown),
                "쿨다운은 차단 태그를 따로 적지 않아도 막아야 한다.");

            _system.Effects.Tick(2f);

            Assert.That(_system.IsOnCooldown(definition), Is.False);
            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
        }

        [Test]
        public void AnAbilityCanRejectActivationWithItsOwnCondition()
        {
            var ability = new TrackedAbility { CanActivateResult = false };
            var definition = GrantTracked("Ability.Attack", ability);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Rejected));
            Assert.That(ability.ActivateCount, Is.Zero);
        }

        [Test]
        public void GrantingTheSameAbilityTagTwiceIsIgnored()
        {
            var definition = GrantTracked("Ability.Attack", new TrackedAbility());

            Assert.That(_system.GrantAbility(definition), Is.Null);
            Assert.That(_system.GrantedAbilityCount, Is.EqualTo(1));
        }

        [Test]
        public void ChangesAreAnnouncedForGrantActivationAndEnd()
        {
            var changes = new List<GameplayAbilityChange>();
            using var subscription = _system.Changed.Subscribe(changes.Add);
            var definition = GrantTracked("Ability.Attack", new TrackedAbility());

            _system.TryActivate(definition.AbilityTag);

            Assert.That(changes.Count, Is.EqualTo(3));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayAbilityChangeKind.Granted));
            Assert.That(changes[1].ChangeKind, Is.EqualTo(GameplayAbilityChangeKind.Activated));
            Assert.That(changes[2].ChangeKind, Is.EqualTo(GameplayAbilityChangeKind.Ended));
            Assert.That(changes[2].EndReason, Is.EqualTo(GameplayAbilityEndReason.Completed));
        }

        [Test]
        public void ApplyEffectsAbilityAppliesItsEffectsAndEnds()
        {
            var damageEffect = CreateEffect(GameplayEffectDurationPolicy.Instant, _health, -25f);
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime(
                "Ability.Attack", new[] { damageEffect }));
            _system.GrantAbility(definition);

            Assert.That(_system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(75f));
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void AnAbilitySetGrantsAttributesTagsAndAbilities()
        {
            var mana = Track(AttributeDefinition.CreateRuntime("Mana", 0f));
            var definition = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Attack"));
            var set = Track(GameplayAbilitySet.CreateRuntime(
                new GameplayAbilityDefinition[] { definition },
                new[] { new GameplayAbilitySet.StartingAttribute(mana, 40f) },
                new[] { "Unit.Infantry" }));

            Assert.That(set.GrantTo(_system), Is.EqualTo(1));

            Assert.That(_attributes.GetBaseValue(mana), Is.EqualTo(40f));
            Assert.That(_system.Tags.HasTag(GameplayTag.Parse("Unit")), Is.True);
            Assert.That(_system.IsGranted(definition.AbilityTag), Is.True);
        }

        [Test]
        public void AnAbilitySetRejectsDuplicateAbilityTags()
        {
            var first = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Attack"));
            var second = Track(ApplyEffectsAbilityDefinition.CreateRuntime("Ability.Attack"));
            var set = Track(GameplayAbilitySet.CreateRuntime(new GameplayAbilityDefinition[] { first, second }));

            Assert.That(set.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("Ability.Attack"));
        }

        [Test]
        public void TickIgnoresNonPositiveTime()
        {
            var ability = new TrackedAbility { RemainingTicks = int.MaxValue };
            var definition = GrantTracked("Ability.TakeCover", ability);
            _system.TryActivate(definition.AbilityTag);
            var tickCountAfterActivation = ability.TickCount;

            _system.Tick(0f);
            _system.Tick(-1f);

            Assert.That(ability.TickCount, Is.EqualTo(tickCountAfterActivation));
        }

        /// <summary>추적용 어빌리티를 부여하고 그 정의를 돌려준다.</summary>
        private TrackedAbilityDefinition GrantTracked(
            string abilityTagName,
            TrackedAbility ability,
            GameplayEffectDefinition cost = null,
            GameplayEffectDefinition cooldown = null,
            IEnumerable<string> activeTags = null,
            IEnumerable<string> requiredTags = null,
            IEnumerable<string> blockedTags = null,
            float maxDuration = 0f)
        {
            var definition = Track(TrackedAbilityDefinition.CreateRuntime(
                abilityTagName, ability, cost, cooldown, activeTags, requiredTags, blockedTags, maxDuration));
            _system.GrantAbility(definition);
            return definition;
        }

        /// <summary>어트리뷰트를 바꾸는 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition CreateEffect(
            GameplayEffectDurationPolicy policy,
            AttributeDefinition attribute,
            float magnitude)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                policy,
                new[] { GameplayEffectModifier.CreateRuntime(attribute, AttributeModifierOperation.Add, magnitude) }));
        }

        /// <summary>태그만 부여하는 지속 효과 정의를 만든다.</summary>
        private GameplayEffectDefinition CreateTagEffect(string tagName, float duration)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration, null, duration, grantedTags: new[] { tagName }));
        }

        /// <summary>만든 에셋을 정리 목록에 등록한다.</summary>
        /// <param name="createdObject">등록할 에셋이다.</param>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>호출 횟수와 자원 보유를 기록하는 테스트용 어빌리티이다.</summary>
        private sealed class TrackedAbility : GameplayAbility
        {
            /// <summary>남은 진행 틱 수이며 0이면 곧바로 끝난다.</summary>
            public int RemainingTicks { get; set; }

            /// <summary>자기 조건이 활성화를 허용할지 여부이다.</summary>
            public bool CanActivateResult { get; set; } = true;

            /// <summary>활성화된 횟수이다.</summary>
            public int ActivateCount { get; private set; }

            /// <summary>종료된 횟수이다.</summary>
            public int EndCount { get; private set; }

            /// <summary>틱이 호출된 횟수이다.</summary>
            public int TickCount { get; private set; }

            /// <summary>바깥 자원을 잡고 있는지 흉내 내는 표식이다.</summary>
            public bool HasReservation { get; private set; }

            /// <inheritdoc />
            public override bool CanActivate()
            {
                return CanActivateResult;
            }

            /// <inheritdoc />
            protected override void OnActivate()
            {
                ActivateCount++;
                HasReservation = true;
            }

            /// <inheritdoc />
            protected override GameplayAbilityTickResult OnTick(float deltaTime)
            {
                TickCount++;
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
                EndCount++;
                HasReservation = false;
            }
        }

        /// <summary>미리 만들어 둔 어빌리티 인스턴스를 돌려주는 테스트용 정의이다.</summary>
        private sealed class TrackedAbilityDefinition : GameplayAbilityDefinition
        {
            /// <summary>부여할 때 돌려줄 어빌리티 인스턴스이다.</summary>
            private GameplayAbility _instance;

            /// <inheritdoc />
            public override GameplayAbility CreateAbility()
            {
                return _instance;
            }

            /// <summary>테스트용 어빌리티 정의를 만든다.</summary>
            public static TrackedAbilityDefinition CreateRuntime(
                string abilityTagName,
                GameplayAbility instance,
                GameplayEffectDefinition cost,
                GameplayEffectDefinition cooldown,
                IEnumerable<string> activeTags,
                IEnumerable<string> requiredTags,
                IEnumerable<string> blockedTags,
                float maxDuration)
            {
                var definition = CreateInstance<TrackedAbilityDefinition>();
                definition._instance = instance;
                definition.ConfigureRuntime(
                    abilityTagName, cost, cooldown, activeTags, requiredTags, blockedTags, maxDuration);
                return definition;
            }
        }
    }
}
