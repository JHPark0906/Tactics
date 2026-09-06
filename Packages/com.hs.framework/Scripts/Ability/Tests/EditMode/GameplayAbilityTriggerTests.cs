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
    /// <summary>게임플레이 이벤트와 태그 변화로 어빌리티가 스스로 시작되고 끝나는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 이벤트는 "이런 일이 일어났다"만 알리고 무엇이 반응할지는 정의의 트리거가 정한다.
    /// 사망처럼 여러 어빌리티가 저마다 반응할 수 있는 일을 바깥 코드가 하나하나 활성화하지 않아도 되게 하는 장치이다.
    /// </remarks>
    public sealed class GameplayAbilityTriggerTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayAbilitySystem _system;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _system = new GameplayAbilitySystem(new GameplayEffectRunner(_attributes));
        }

        [TearDown]
        public void TearDown()
        {
            _system?.Dispose();
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
        public void AGameplayEventActivatesAbilitiesThatListItAsATrigger()
        {
            var death = new RunningAbility();
            Grant("Ability.Death", death, triggerEventTags: new[] { "Event.Death" });
            var payload = new object();

            var activatedCount = _system.SendGameplayEvent(GameplayTag.Parse("Event.Death"), payload, 7f);

            Assert.That(activatedCount, Is.EqualTo(1));
            Assert.That(death.IsActive, Is.True);
            Assert.That(death.TriggeringEvent.EventTag, Is.EqualTo(GameplayTag.Parse("Event.Death")));
            Assert.That(death.TriggeringEvent.Payload, Is.SameAs(payload), "이벤트가 실어 온 것을 어빌리티가 읽을 수 있어야 한다.");
            Assert.That(death.TriggeringEvent.Magnitude, Is.EqualTo(7f));
        }

        [Test]
        public void EventTriggersFollowTheTagHierarchy()
        {
            var death = new RunningAbility();
            Grant("Ability.Death", death, triggerEventTags: new[] { "Event.Death" });

            _system.SendGameplayEvent(GameplayTag.Parse("Event.Death.Explosion"));

            Assert.That(death.IsActive, Is.True, "상위 이름을 트리거로 적으면 그 계열의 이벤트에 모두 반응해야 한다.");
        }

        [Test]
        public void AnEventNobodyListensToActivatesNothingButStillFlows()
        {
            var received = new List<GameplayEventData>();
            using var subscription = _system.Events.Subscribe(received.Add);
            Grant("Ability.Death", triggerEventTags: new[] { "Event.Death" });

            var activatedCount = _system.SendGameplayEvent(GameplayTag.Parse("Event.Stage.Cleared"));

            Assert.That(activatedCount, Is.Zero);
            Assert.That(received.Count, Is.EqualTo(1), "어빌리티가 아닌 구독자도 같은 이벤트를 받아야 한다.");
        }

        [Test]
        public void ATriggeredAbilityStillObeysActivationChecks()
        {
            var death = new RunningAbility();
            Grant("Ability.Death", death, triggerEventTags: new[] { "Event.Death" }, blockedTags: new[] { "State.Immortal" });
            _system.Tags.AddTag(GameplayTag.Parse("State.Immortal"));

            var activatedCount = _system.SendGameplayEvent(GameplayTag.Parse("Event.Death"));

            Assert.That(activatedCount, Is.Zero, "트리거로 시작되더라도 활성화 조건은 같은 검사를 거쳐야 한다.");
            Assert.That(death.IsActive, Is.False);
        }

        [Test]
        public void AManualActivationDoesNotCarryAStaleEvent()
        {
            var death = new RunningAbility();
            var definition = Grant("Ability.Death", death, triggerEventTags: new[] { "Event.Death" });
            _system.SendGameplayEvent(GameplayTag.Parse("Event.Death"), new object());
            _system.CancelAbility(definition.AbilityTag);

            _system.TryActivate(definition.AbilityTag);

            Assert.That(death.TriggeringEvent.IsValid, Is.False, "요청으로 시작한 활성화에 지난 이벤트가 남아 있으면 안 된다.");
        }

        [Test]
        public void GainingATagActivatesTheAbilityListeningForIt()
        {
            var stunned = new RunningAbility();
            Grant("Ability.Stunned", stunned, triggerOnTagGained: new[] { "State.Stunned" });

            _system.Tags.AddTag(GameplayTag.Parse("State.Stunned"));

            Assert.That(stunned.IsActive, Is.True);
            Assert.That(stunned.ActivateCount, Is.EqualTo(1));
        }

        [Test]
        public void ATagGainedTriggerFiresOnlyWhenTheTagIsActuallyGained()
        {
            var stunned = new RunningAbility();
            var definition = Grant("Ability.Stunned", stunned, triggerOnTagGained: new[] { "State.Stunned" });
            var tag = GameplayTag.Parse("State.Stunned");
            _system.Tags.AddTag(tag);
            _system.CancelAbility(definition.AbilityTag);

            _system.Tags.AddTag(tag);

            Assert.That(stunned.ActivateCount, Is.EqualTo(1), "이미 가진 태그의 참조 계수만 오르는 것은 얻은 것이 아니다.");
        }

        [Test]
        public void WhileTagPresentKeepsTheAbilityRunningUntilTheTagIsLost()
        {
            var burning = new RunningAbility();
            Grant("Ability.Burning", burning, triggerWhileTagPresent: new[] { "State.Burning" });
            var tag = GameplayTag.Parse("State.Burning");

            _system.Tags.AddTag(tag);
            Assert.That(burning.IsActive, Is.True);

            _system.Tags.RemoveTag(tag);

            Assert.That(burning.IsActive, Is.False, "유지 조건 태그를 잃으면 어빌리티도 끝나야 한다.");
            Assert.That(burning.LastEndReason, Is.EqualTo(GameplayAbilityEndReason.Cancelled));
        }

        [Test]
        public void WhileTagPresentActivatesOnGrantWhenTheTagIsAlreadyHeld()
        {
            _system.Tags.AddTag(GameplayTag.Parse("State.Burning"));
            var burning = new RunningAbility();

            Grant("Ability.Burning", burning, triggerWhileTagPresent: new[] { "State.Burning" });

            Assert.That(burning.IsActive, Is.True, "부여 시점에 이미 조건이 갖춰져 있으면 곧바로 돌아야 한다.");
        }

        [Test]
        public void LosingOneOfTwoMatchingTagsKeepsTheAbilityRunning()
        {
            var burning = new RunningAbility();
            Grant("Ability.Burning", burning, triggerWhileTagPresent: new[] { "State.Burning" });
            var fire = GameplayTag.Parse("State.Burning.Fire");
            var acid = GameplayTag.Parse("State.Burning.Acid");
            _system.Tags.AddTag(fire);
            _system.Tags.AddTag(acid);

            _system.Tags.RemoveTag(fire);
            Assert.That(burning.IsActive, Is.True, "계열의 다른 태그가 남아 있으면 조건은 아직 성립한다.");

            _system.Tags.RemoveTag(acid);

            Assert.That(burning.IsActive, Is.False);
        }

        [Test]
        public void ActivateOnGrantStartsTheAbilityImmediately()
        {
            var passive = new RunningAbility();

            Grant("Ability.Passive", passive, activateOnGrant: true);

            Assert.That(passive.IsActive, Is.True);
            Assert.That(_system.ActiveAbilities.Count, Is.EqualTo(1));
        }

        [Test]
        public void ADisposedSystemIgnoresEventsAndTagChanges()
        {
            var death = new RunningAbility();
            Grant("Ability.Death", death, triggerEventTags: new[] { "Event.Death" }, triggerOnTagGained: new[] { "State.Dead" });

            _system.Dispose();

            Assert.That(_system.SendGameplayEvent(GameplayTag.Parse("Event.Death")), Is.Zero);
            Assert.DoesNotThrow(() => _system.Tags.AddTag(GameplayTag.Parse("State.Dead")));
            Assert.That(death.IsActive, Is.False);
        }

        /// <summary>테스트용 어빌리티를 부여하고 그 정의를 돌려준다.</summary>
        private RunningAbilityDefinition Grant(
            string tagName,
            RunningAbility ability = null,
            IEnumerable<string> triggerEventTags = null,
            IEnumerable<string> triggerOnTagGained = null,
            IEnumerable<string> triggerWhileTagPresent = null,
            bool activateOnGrant = false,
            IEnumerable<string> blockedTags = null)
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime(
                tagName,
                ability,
                triggerEventTags: triggerEventTags,
                triggerOnTagGained: triggerOnTagGained,
                triggerWhileTagPresent: triggerWhileTagPresent,
                activateOnGrant: activateOnGrant,
                blockedTags: blockedTags));
            Assert.That(_system.GrantAbility(definition), Is.Not.Null, $"{tagName} 부여에 실패했다.");
            return definition;
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
