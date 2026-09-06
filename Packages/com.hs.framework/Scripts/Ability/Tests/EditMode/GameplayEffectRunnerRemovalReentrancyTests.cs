using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>
    /// 골라서 걷는 두 자리(태그로 걷기·원인으로 걷기)가 걷힘 알림 안에서 구독자가 다른 효과를 빼도 예외 없이,
    /// 빠뜨림 없이 끝나는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// 걷을 것을 먼저 모두 골라 목록에서 뺀 뒤 되돌리는 것이 계약이다. 살아 있는 목록을 역순 인덱스로 돌며 되돌리면,
    /// 알림 안에서 구독자가 다른 효과를 빼는 순간 목록이 짧아져 다음 인덱스가 범위 밖이 된다. 모두 걷기와 같은 꼴이어야 한다.
    /// </remarks>
    public sealed class GameplayEffectRunnerRemovalReentrancyTests
    {
        private const string DebuffName = "Effect.Debuff";
        private const string CleansedName = "State.Cleansed";

        private static readonly GameplayTag Debuff = GameplayTag.Parse(DebuffName);
        private static readonly GameplayTag Cleansed = GameplayTag.Parse(CleansedName);

        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeSet _attributes;
        private GameplayTagContainer _tags;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _attributes = new AttributeSet();
            _tags = new GameplayTagContainer();
            _runner = new GameplayEffectRunner(_attributes, _tags);
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
        public void RemovingByTagLetsARemovedSubscriberRemoveTheOtherMatchingEffect()
        {
            var first = _runner.Apply(TaggedEffect(DebuffName));
            var second = _runner.Apply(TaggedEffect(DebuffName));
            var removed = new List<ActiveGameplayEffect>();
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind != GameplayEffectChangeKind.Removed)
                {
                    return;
                }

                removed.Add(change.Effect);
                _runner.Remove(ReferenceEquals(change.Effect, first) ? second : first);
            });

            var removedCount = 0;
            Assert.DoesNotThrow(() => removedCount = _runner.RemoveAllWithTags(new[] { Debuff }));

            Assert.That(removedCount, Is.EqualTo(2), "맞는 것 둘을 다 걷어야 한다.");
            Assert.That(removed, Is.EquivalentTo(new[] { first, second }), "각각 정확히 한 번 걷힌다. 빠뜨리지도 두 번 걷지도 않는다.");
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(0));
        }

        [Test]
        public void RemovingBySourceLetsARemovedSubscriberRemoveAnUnrelatedEffect()
        {
            var source = new object();
            var mine = _runner.Apply(TaggedEffect("Effect.Mine"), source);
            var other = _runner.Apply(TaggedEffect("Effect.Other"));
            var mineToo = _runner.Apply(TaggedEffect("Effect.MineToo"), source);
            var removed = new List<ActiveGameplayEffect>();
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind != GameplayEffectChangeKind.Removed)
                {
                    return;
                }

                removed.Add(change.Effect);
                _runner.Remove(other);
            });

            var removedCount = 0;
            Assert.DoesNotThrow(() => removedCount = _runner.RemoveAllFrom(source));

            Assert.That(removedCount, Is.EqualTo(2), "그 원인이 적용한 둘만 센다. 구독자가 뺀 것은 이 걷기의 것이 아니다.");
            Assert.That(removed, Is.EquivalentTo(new[] { mine, mineToo, other }), "셋 다 정확히 한 번 걷힌다.");
            Assert.That(mine.IsActive, Is.False);
            Assert.That(mineToo.IsActive, Is.False);
            Assert.That(other.IsActive, Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(0));
        }

        /// <remarks>정의의 「걷을 효과 태그」로 걷는 것이 실제 게임이 지나는 길이다.</remarks>
        [Test]
        public void ApplyingAnEffectThatRemovesOthersByTagSurvivesARemovedSubscriberRemovingAnother()
        {
            var first = _runner.Apply(TaggedEffect(DebuffName));
            var second = _runner.Apply(TaggedEffect(DebuffName));
            var cleanser = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                grantedTags: new[] { CleansedName },
                removeEffectsWithTags: new[] { DebuffName }));
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed)
                {
                    _runner.Remove(ReferenceEquals(change.Effect, first) ? second : first);
                }
            });

            ActiveGameplayEffect cleansed = null;
            Assert.DoesNotThrow(() => cleansed = _runner.Apply(cleanser));

            Assert.That(cleansed, Is.Not.Null.And.Property(nameof(ActiveGameplayEffect.IsActive)).True, "걷는 쪽 효과는 그대로 적용된다.");
            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.False);
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(_tags.HasTag(Cleansed), Is.True);
        }

        /// <summary>걷을 때까지 유지되는, 효과 태그 하나를 가진 효과 정의를 만든다.</summary>
        /// <param name="assetTagName">이 효과를 설명하는 태그 이름이며 태그로 걷을 때 견주는 값이다.</param>
        /// <returns>만든 정의이다.</returns>
        private GameplayEffectDefinition TaggedEffect(string assetTagName)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                assetTags: new[] { assetTagName }));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
