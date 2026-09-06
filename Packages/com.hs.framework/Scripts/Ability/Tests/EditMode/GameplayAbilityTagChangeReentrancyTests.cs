using System;
using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 태그 변화 핸들러가 도는 도중에 어빌리티가 부여되거나 회수되어도 순회가 깨지지 않고 결과가 옳은 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 태그 변화에 반응하는 어빌리티가 자신이나 다른 어빌리티를 부여·회수해도
    /// 부여 목록의 스냅샷 순회가 유지되는지 검증한다.
    /// </para>
    /// <para>
    /// 순회 순서에 기대지 않는 것만 단언한다. 부여 목록은 사전이라 순서를 약속하지 않으므로, 「먼저 돈 쪽이 뒤엣것을
    /// 걷었을 때」와 「뒤엣것이 먼저 돌았을 때」 어느 쪽이든 같은 결과가 나와야 한다.
    /// </para>
    /// </remarks>
    public sealed class GameplayAbilityTagChangeReentrancyTests
    {
        private const string StunnedName = "State.Stunned";
        private const string BurningName = "State.Burning";

        private static readonly GameplayTag Stunned = GameplayTag.Parse(StunnedName);
        private static readonly GameplayTag Burning = GameplayTag.Parse(BurningName);

        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<UnityEngine.Object> _createdObjects = new();

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
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void GrantingAnAbilityWhileATagGainedTriggerRunsDoesNotBreakTheWalk()
        {
            var follower = new TagReactingAbility();
            var followerDefinition = Track(TagReactingAbilityDefinition.CreateRuntime(
                "Ability.Follower", follower, triggerOnTagGained: new[] { StunnedName }));
            var leader = new TagReactingAbility { WhenActivated = _ => _system.GrantAbility(followerDefinition) };
            Grant("Ability.Leader", leader, triggerOnTagGained: new[] { StunnedName });

            Assert.DoesNotThrow(() => _system.Tags.AddTag(Stunned));

            Assert.That(leader.IsActive, Is.True);
            Assert.That(_system.IsGranted(followerDefinition.AbilityTag), Is.True, "핸들러 안에서 부여한 어빌리티는 부여된 채로 남는다.");
            Assert.That(follower.ActivateCount, Is.EqualTo(0), "이번 변화가 시작될 때 없던 어빌리티는 이번 변화로 켜지지 않는다.");
            Assert.That(_system.GrantedAbilityCount, Is.EqualTo(2));

            _system.Tags.RemoveTag(Stunned);
            _system.Tags.AddTag(Stunned);

            Assert.That(follower.ActivateCount, Is.EqualTo(1), "다음 변화부터는 보통의 어빌리티로 반응한다.");
        }

        [Test]
        public void RevokingAnotherAbilityWhileATagGainedTriggerRunsDoesNotBreakTheWalk()
        {
            var victim = new TagReactingAbility();
            var victimDefinition = Grant("Ability.Victim", victim, triggerOnTagGained: new[] { StunnedName });
            var revoker = new TagReactingAbility { WhenActivated = _ => _system.RevokeAbility(victimDefinition.AbilityTag) };
            Grant("Ability.Revoker", revoker, triggerOnTagGained: new[] { StunnedName });

            Assert.DoesNotThrow(() => _system.Tags.AddTag(Stunned));

            Assert.That(revoker.IsActive, Is.True);
            Assert.That(_system.IsGranted(victimDefinition.AbilityTag), Is.False, "핸들러 안에서 걷힌 어빌리티는 걷힌 채로 남는다.");
            Assert.That(victim.IsActive, Is.False);
            Assert.That(victim.EndCount, Is.EqualTo(victim.ActivateCount), "먼저 켜졌다면 걷히며 끝났고, 켜지지 않았다면 끝날 것도 없다.");
            Assert.That(_system.GrantedAbilityCount, Is.EqualTo(1));
        }

        [Test]
        public void AnAbilityMayRevokeItselfWhileATagGainedTriggerActivatesIt()
        {
            var self = new TagReactingAbility();
            self.WhenActivated = ability => _system.RevokeAbility(ability.Definition.AbilityTag);
            var definition = Grant("Ability.Self", self, triggerOnTagGained: new[] { StunnedName });

            Assert.DoesNotThrow(() => _system.Tags.AddTag(Stunned));

            Assert.That(_system.IsGranted(definition.AbilityTag), Is.False, "자기를 걷은 어빌리티는 부여 목록에 남지 않는다.");
            Assert.That(self.IsActive, Is.False);
            Assert.That(self.ActivateCount, Is.EqualTo(1));
            Assert.That(self.EndCount, Is.EqualTo(1), "켜지던 중에 걷혔으므로 정확히 한 번 끝난다.");
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void GrantingAnAbilityWhileATagLossCancelsAnotherDoesNotBreakTheWalk()
        {
            var newcomer = new TagReactingAbility();
            var newcomerDefinition = Track(TagReactingAbilityDefinition.CreateRuntime(
                "Ability.Newcomer", newcomer, triggerWhileTagPresent: new[] { BurningName }));
            var burning = new TagReactingAbility { WhenEnded = (_, _) => _system.GrantAbility(newcomerDefinition) };
            Grant("Ability.Burning", burning, triggerWhileTagPresent: new[] { BurningName });
            _system.Tags.AddTag(Burning);
            Assert.That(burning.IsActive, Is.True);

            Assert.DoesNotThrow(() => _system.Tags.RemoveTag(Burning));

            Assert.That(burning.IsActive, Is.False);
            Assert.That(burning.EndCount, Is.EqualTo(1));
            Assert.That(_system.IsGranted(newcomerDefinition.AbilityTag), Is.True, "취소되며 부여한 어빌리티는 부여된 채로 남는다.");
            Assert.That(newcomer.IsActive, Is.False, "유지 조건 태그가 이미 없으므로 부여만 되고 켜지지 않는다.");
            Assert.That(_system.GrantedAbilityCount, Is.EqualTo(2));
        }

        [Test]
        public void RevokingAnotherAbilityWhileATagLossCancelsOneDoesNotBreakTheWalk()
        {
            var other = new TagReactingAbility();
            var otherDefinition = Grant("Ability.Other", other, triggerWhileTagPresent: new[] { BurningName });
            var revoker = new TagReactingAbility { WhenEnded = (_, _) => _system.RevokeAbility(otherDefinition.AbilityTag) };
            Grant("Ability.Revoker", revoker, triggerWhileTagPresent: new[] { BurningName });
            _system.Tags.AddTag(Burning);
            Assert.That(other.IsActive, Is.True);
            Assert.That(revoker.IsActive, Is.True);

            Assert.DoesNotThrow(() => _system.Tags.RemoveTag(Burning));

            Assert.That(_system.IsGranted(otherDefinition.AbilityTag), Is.False);
            Assert.That(other.IsActive, Is.False);
            Assert.That(other.EndCount, Is.EqualTo(1), "핸들러가 취소했든 걷히며 취소됐든 끝나는 것은 한 번뿐이다.");
            Assert.That(revoker.EndCount, Is.EqualTo(1));
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        [Test]
        public void AnAbilityMayRevokeItselfWhileATagLossCancelsIt()
        {
            var self = new TagReactingAbility();
            self.WhenEnded = (ability, _) => _system.RevokeAbility(ability.Definition.AbilityTag);
            var definition = Grant("Ability.Self", self, triggerWhileTagPresent: new[] { BurningName });
            _system.Tags.AddTag(Burning);
            Assert.That(self.IsActive, Is.True);

            Assert.DoesNotThrow(() => _system.Tags.RemoveTag(Burning));

            Assert.That(_system.IsGranted(definition.AbilityTag), Is.False, "끝나며 자기를 걷은 어빌리티는 부여 목록에 남지 않는다.");
            Assert.That(self.IsActive, Is.False);
            Assert.That(self.EndCount, Is.EqualTo(1), "끝나는 도중에 걷혀도 두 번 끝나지 않는다.");
            Assert.That(_system.ActiveAbilities, Is.Empty);
        }

        /// <summary>반응하는 어빌리티를 부여하고 그 정의를 돌려준다.</summary>
        private TagReactingAbilityDefinition Grant(
            string tagName,
            TagReactingAbility ability,
            IEnumerable<string> triggerOnTagGained = null,
            IEnumerable<string> triggerWhileTagPresent = null)
        {
            var definition = Track(TagReactingAbilityDefinition.CreateRuntime(
                tagName, ability, triggerOnTagGained, triggerWhileTagPresent));
            Assert.That(_system.GrantAbility(definition), Is.Not.Null, $"{tagName} 부여에 실패했다.");
            return definition;
        }

        private T Track<T>(T createdObject) where T : UnityEngine.Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }

    /// <summary>
    /// 켜질 때와 끝날 때 검사가 넣어 둔 일을 하는 어빌리티이다. 그 일이 어빌리티 시스템을 다시 만지는 경우를 재현하는 데 쓴다.
    /// </summary>
    internal sealed class TagReactingAbility : GameplayAbility
    {
        /// <summary>켜지는 순간 할 일이며 없으면 아무것도 하지 않는다.</summary>
        public Action<TagReactingAbility> WhenActivated { get; set; }

        /// <summary>끝나는 순간 할 일이며 없으면 아무것도 하지 않는다.</summary>
        public Action<TagReactingAbility, GameplayAbilityEndReason> WhenEnded { get; set; }

        /// <summary>켜진 횟수이다.</summary>
        public int ActivateCount { get; private set; }

        /// <summary>끝난 횟수이다.</summary>
        public int EndCount { get; private set; }

        /// <inheritdoc />
        protected override void OnActivate()
        {
            ActivateCount++;
            WhenActivated?.Invoke(this);
        }

        /// <inheritdoc />
        protected override GameplayAbilityTickResult OnTick(float deltaTime)
        {
            return GameplayAbilityTickResult.Running;
        }

        /// <inheritdoc />
        protected override void OnEnd(GameplayAbilityEndReason endReason)
        {
            EndCount++;
            WhenEnded?.Invoke(this, endReason);
        }
    }

    /// <summary>미리 만들어 둔 <see cref="TagReactingAbility"/>를 돌려주며 태그 트리거를 지정할 수 있는 테스트용 정의이다.</summary>
    internal sealed class TagReactingAbilityDefinition : GameplayAbilityDefinition
    {
        private TagReactingAbility _ability;

        /// <inheritdoc />
        public override GameplayAbility CreateAbility()
        {
            return _ability;
        }

        /// <summary>지정한 어빌리티를 돌려주는 정의를 만든다.</summary>
        /// <param name="tagName">어빌리티를 식별하는 태그 이름이다.</param>
        /// <param name="ability">부여될 때 돌려줄 어빌리티이다.</param>
        /// <param name="triggerOnTagGained">대상이 얻는 순간 활성화를 시도할 태그 이름이다.</param>
        /// <param name="triggerWhileTagPresent">대상이 가진 동안 활성 상태를 유지할 태그 이름이다.</param>
        /// <returns>만든 정의이다.</returns>
        public static TagReactingAbilityDefinition CreateRuntime(
            string tagName,
            TagReactingAbility ability,
            IEnumerable<string> triggerOnTagGained = null,
            IEnumerable<string> triggerWhileTagPresent = null)
        {
            var definition = CreateInstance<TagReactingAbilityDefinition>();
            definition._ability = ability;
            definition.ConfigureRuntime(
                tagName,
                triggerOnTagGained: triggerOnTagGained,
                triggerWhileTagPresent: triggerWhileTagPresent);
            return definition;
        }
    }
}
