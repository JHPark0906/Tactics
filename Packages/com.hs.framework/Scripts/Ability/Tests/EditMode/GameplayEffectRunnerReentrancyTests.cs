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
    /// 효과 실행기가 알림을 내는 도중에 구독자가 효과를 더하거나 빼도 순회가 깨지지 않고 결과가 옳은 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 틱, 진행 조건 재평가, 전체 제거는 순회 대상을 먼저 확정한다.
    /// 그 안의 알림에서 구독자가 효과를 추가·제거해도 각 순회가 유지되는지 검증한다.
    /// </para>
    /// <para>
    /// 억제를 다시 보는 자리는 그 안에서 태그가 다시 바뀌면 끝난 뒤 한 번 더 보는데, 그것도 여기서 함께 본다.
    /// </para>
    /// </remarks>
    public sealed class GameplayEffectRunnerReentrancyTests
    {
        private const string GroundedName = "State.Grounded";
        private const string MarkedName = "State.Marked";
        private const string LateName = "State.Late";

        private static readonly GameplayTag Grounded = GameplayTag.Parse(GroundedName);
        private static readonly GameplayTag Marked = GameplayTag.Parse(MarkedName);
        private static readonly GameplayTag Late = GameplayTag.Parse(LateName);

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
        public void ARemovedSubscriberMayApplyAnotherEffectWhileTheTickExpiresOne()
        {
            var brief = _runner.Apply(BriefEffect(MarkedName));
            var lateDefinition = LastingEffect(LateName);
            ActiveGameplayEffect late = null;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed && late == null)
                {
                    late = _runner.Apply(lateDefinition);
                }
            });

            Assert.DoesNotThrow(() => _runner.Tick(2f));

            Assert.That(brief.IsActive, Is.False, "시간이 다한 효과는 걷힌다.");
            Assert.That(late, Is.Not.Null.And.Property(nameof(ActiveGameplayEffect.IsActive)).True, "걷힘 알림 안에서 더한 효과는 유지된다.");
            Assert.That(_tags.HasTag(Marked), Is.False);
            Assert.That(_tags.HasTag(Late), Is.True);
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
        }

        [Test]
        public void ARemovedSubscriberMayRemoveAnotherEffectWhileTheTickExpiresOne()
        {
            var first = _runner.Apply(BriefEffect(MarkedName));
            var second = _runner.Apply(BriefEffect(LateName));
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed && ReferenceEquals(change.Effect, first))
                {
                    _runner.Remove(second);
                }
            });

            Assert.DoesNotThrow(() => _runner.Tick(2f));

            Assert.That(first.IsActive, Is.False);
            Assert.That(second.IsActive, Is.False, "걷힘 알림 안에서 뺀 효과는 뺀 채로 남는다.");
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(0));
            Assert.That(_tags.HasTag(Marked), Is.False);
            Assert.That(_tags.HasTag(Late), Is.False);
        }

        [Test]
        public void AnInhibitionSubscriberMayApplyAnotherEffectWhileInhibitionIsRefreshed()
        {
            _tags.AddTag(Grounded);
            var haste = _runner.Apply(GroundedEffect(MarkedName));
            var lateDefinition = LastingEffect(LateName);
            ActiveGameplayEffect late = null;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.InhibitionChanged && change.Effect.IsInhibited && late == null)
                {
                    late = _runner.Apply(lateDefinition);
                }
            });

            Assert.DoesNotThrow(() => _tags.RemoveTag(Grounded));

            Assert.That(haste.IsInhibited, Is.True);
            Assert.That(late, Is.Not.Null.And.Property(nameof(ActiveGameplayEffect.IsActive)).True, "억제 알림 안에서 더한 효과는 유지된다.");
            Assert.That(_tags.HasTag(Marked), Is.False, "억제된 효과의 부여 태그는 걷혀 있다.");
            Assert.That(_tags.HasTag(Late), Is.True);
        }

        [Test]
        public void AnInhibitionSubscriberMayRemoveAnotherEffectWhileInhibitionIsRefreshed()
        {
            _tags.AddTag(Grounded);
            var first = _runner.Apply(GroundedEffect(MarkedName));
            var second = _runner.Apply(GroundedEffect(LateName));
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.InhibitionChanged && ReferenceEquals(change.Effect, first))
                {
                    _runner.Remove(second);
                }
            });

            Assert.DoesNotThrow(() => _tags.RemoveTag(Grounded));

            Assert.That(first.IsActive, Is.True);
            Assert.That(first.IsInhibited, Is.True);
            Assert.That(second.IsActive, Is.False, "억제 알림 안에서 뺀 효과는 뺀 채로 남는다.");
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(_tags.HasTag(Marked), Is.False);
            Assert.That(_tags.HasTag(Late), Is.False);
        }

        [Test]
        public void AnInhibitionSubscriberMayRestoreTheTagWhileInhibitionIsRefreshed()
        {
            _tags.AddTag(Grounded);
            var haste = _runner.Apply(GroundedEffect(MarkedName));
            var restored = false;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.InhibitionChanged && change.Effect.IsInhibited && !restored)
                {
                    restored = true;
                    _tags.AddTag(Grounded);
                }
            });

            Assert.DoesNotThrow(() => _tags.RemoveTag(Grounded));

            Assert.That(restored, Is.True, "억제되는 순간 구독자가 조건 태그를 되돌려 놓았다.");
            Assert.That(haste.IsInhibited, Is.False, "다시 보는 도중에 바뀐 태그는 끝난 뒤 한 번 더 보아 반영된다.");
            Assert.That(_tags.HasTag(Marked), Is.True, "억제가 풀렸으므로 부여 태그가 다시 있다.");
            Assert.That(_tags.HasTag(Grounded), Is.True);
        }

        [Test]
        public void RemoveAllLetsARemovedSubscriberApplyAnEffectThatSurvives()
        {
            _runner.Apply(LastingEffect(MarkedName));
            var lateDefinition = LastingEffect(LateName);
            ActiveGameplayEffect late = null;
            using var subscription = _runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Removed && late == null)
                {
                    late = _runner.Apply(lateDefinition);
                }
            });

            Assert.DoesNotThrow(() => _runner.RemoveAll());

            Assert.That(late, Is.Not.Null.And.Property(nameof(ActiveGameplayEffect.IsActive)).True, "모두 걷는 도중에 더한 효과는 걷히지 않는다. 걷는 것은 그때 있던 것들이다.");
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(_tags.HasTag(Marked), Is.False);
            Assert.That(_tags.HasTag(Late), Is.True);
        }

        /// <summary>1초 뒤에 시간이 다하는, 태그 하나를 부여하는 효과 정의를 만든다.</summary>
        /// <param name="grantedTagName">유지되는 동안 부여할 태그 이름이다.</param>
        /// <returns>만든 정의이다.</returns>
        private GameplayEffectDefinition BriefEffect(string grantedTagName)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                duration: 1f,
                grantedTags: new[] { grantedTagName }));
        }

        /// <summary>걷을 때까지 유지되는, 태그 하나를 부여하는 효과 정의를 만든다.</summary>
        /// <param name="grantedTagName">유지되는 동안 부여할 태그 이름이다.</param>
        /// <returns>만든 정의이다.</returns>
        private GameplayEffectDefinition LastingEffect(string grantedTagName)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                grantedTags: new[] { grantedTagName }));
        }

        /// <summary>땅에 있는 동안만 작용하는, 태그 하나를 부여하는 무한 효과 정의를 만든다.</summary>
        /// <param name="grantedTagName">작용하는 동안 부여할 태그 이름이다.</param>
        /// <returns>만든 정의이다.</returns>
        private GameplayEffectDefinition GroundedEffect(string grantedTagName)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                grantedTags: new[] { grantedTagName },
                ongoingRequiredTags: new[] { GroundedName }));
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
