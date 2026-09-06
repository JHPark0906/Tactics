using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>같은 효과를 다시 적용했을 때 쌓이는 규칙(방식·한도·지속 갱신·주기 리셋·만료)을 검증한다.</summary>
    /// <remarks>
    /// 쌓이는 효과는 기록 하나가 층수를 세고 층마다 수정자 한 벌씩 얹힌다. 한 층만 걷을 때 그 층의 수정자만 되돌아가고,
    /// 주기 실행은 층수만큼 반복되며, 부여 태그는 한 번만 부여되어야 한다.
    /// </remarks>
    public sealed class GameplayEffectStackingTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _speed;
        private AttributeDefinition _health;
        private AttributeSet _attributes;
        private GameplayTagContainer _tags;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _speed = Track(AttributeDefinition.CreateRuntime("Speed", 10f));
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_speed);
            _attributes.AddAttribute(_health);
            _tags = new GameplayTagContainer();
            _runner = new GameplayEffectRunner(_attributes, _tags);
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
        public void WithoutStackingEachApplicationIsASeparateEffect()
        {
            var slow = CreateSlow(GameplayEffectStackingSettings.None);

            _runner.Apply(slow);
            _runner.Apply(slow);

            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(2));
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void AggregatingByTargetAddsALayerToTheSameRecord()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget));

            var first = _runner.Apply(slow, "archer");
            var second = _runner.Apply(slow, "mage");

            Assert.That(second, Is.SameAs(first), "누가 걸었든 같은 정의는 같은 기록에 쌓인다.");
            Assert.That(first.StackCount, Is.EqualTo(2));
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1));
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(4f).Within(0.001f), "층마다 수정자 한 벌씩 얹혀야 한다.");
        }

        [Test]
        public void AggregatingBySourceKeepsDifferentSourcesApart()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateBySource));

            var fromArcher = _runner.Apply(slow, "archer");
            _runner.Apply(slow, "archer");
            var fromMage = _runner.Apply(slow, "mage");

            Assert.That(fromArcher.StackCount, Is.EqualTo(2));
            Assert.That(fromMage, Is.Not.SameAs(fromArcher));
            Assert.That(fromMage.StackCount, Is.EqualTo(1));
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(2));
        }

        [Test]
        public void TheLimitStopsGrowthButStillRefreshesTheDuration()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget, limit: 2));
            var effect = _runner.Apply(slow);
            _runner.Apply(slow);
            _runner.Tick(1.5f);

            _runner.Apply(slow);

            Assert.That(effect.StackCount, Is.EqualTo(2), "한도에 닿으면 층을 더하지 않는다.");
            Assert.That(effect.RemainingTime, Is.EqualTo(2f).Within(0.001f), "한도에 닿은 재적용도 지속 시간은 갱신한다.");
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void NeverRefreshLeavesTheDurationAlone()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(
                GameplayEffectStackingPolicy.AggregateByTarget,
                durationRefreshPolicy: GameplayEffectStackDurationRefreshPolicy.NeverRefresh));
            var effect = _runner.Apply(slow);
            _runner.Tick(1.5f);

            _runner.Apply(slow);

            Assert.That(effect.StackCount, Is.EqualTo(2));
            Assert.That(effect.RemainingTime, Is.EqualTo(0.5f).Within(0.001f), "지속 시간을 갱신하지 않는 규칙에서는 처음 걸린 시각 기준으로 만료된다.");
        }

        [Test]
        public void ClearingTheEntireStackRemovesEveryLayerAtOnce()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget));
            _runner.Apply(slow);
            _runner.Apply(slow);
            _runner.Apply(slow);

            _runner.Tick(2f);

            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f), "모든 층의 수정자가 되돌아가야 한다.");
        }

        [Test]
        public void RemovingASingleStackPeelsOneLayerPerDuration()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(
                GameplayEffectStackingPolicy.AggregateByTarget,
                expirationPolicy: GameplayEffectStackExpirationPolicy.RemoveSingleStackAndRefreshDuration));
            var effect = _runner.Apply(slow);
            _runner.Apply(slow);
            _runner.Apply(slow);

            _runner.Tick(2f);
            Assert.That(effect.StackCount, Is.EqualTo(2));
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(4f).Within(0.001f), "걷힌 층의 수정자만 되돌아가야 한다.");

            _runner.Tick(2f);
            Assert.That(effect.StackCount, Is.EqualTo(1));
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(7f).Within(0.001f));

            _runner.Tick(2f);
            Assert.That(effect.IsActive, Is.False);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void RefreshingTheDurationKeepsTheStackAlive()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(
                GameplayEffectStackingPolicy.AggregateByTarget,
                expirationPolicy: GameplayEffectStackExpirationPolicy.RefreshDuration));
            var effect = _runner.Apply(slow);
            _runner.Apply(slow);

            _runner.Tick(2f);
            _runner.Tick(2f);

            Assert.That(effect.IsActive, Is.True, "층을 걷지 않고 지속 시간만 다시 세는 규칙이다.");
            Assert.That(effect.StackCount, Is.EqualTo(2));
        }

        [Test]
        public void PeriodicExecutionRepeatsOncePerLayer()
        {
            var burn = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -5f) },
                period: 1f,
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget)));
            _runner.Apply(burn);
            _runner.Apply(burn);
            var afterApplication = _attributes.GetBaseValue(_health);

            _runner.Tick(1f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(afterApplication - 10f).Within(0.001f), "주기 실행은 층수만큼 반복된다.");
        }

        [Test]
        public void AddingALayerResetsThePeriodTimerByDefault()
        {
            var burn = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -5f) },
                period: 1f,
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget)));
            _runner.Apply(burn);
            _runner.Tick(0.9f);
            var beforeSecondLayer = _attributes.GetBaseValue(_health);

            _runner.Apply(burn);
            _runner.Tick(0.2f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(beforeSecondLayer).Within(0.001f), "층이 더해지면 다음 주기까지의 시간을 처음부터 다시 센다.");
        }

        [Test]
        public void GrantedTagsAreNotStacked()
        {
            var slow = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                grantedTags: new[] { "State.Slowed" },
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget)));
            var tag = GameplayTag.Parse("State.Slowed");

            _runner.Apply(slow);
            _runner.Apply(slow);
            Assert.That(_tags.GetCount(tag), Is.EqualTo(1), "부여 태그는 층수와 무관하게 한 번만 부여된다.");

            _runner.RemoveAll();

            Assert.That(_tags.HasTag(tag), Is.False);
        }

        [Test]
        public void StackChangesAreAnnounced()
        {
            var slow = CreateSlow(new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget));
            var changes = new List<GameplayEffectChange>();
            using var subscription = _runner.Changed.Subscribe(changes.Add);

            _runner.Apply(slow);
            _runner.Apply(slow);

            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.Applied));
            Assert.That(changes[1].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.StackChanged));
            Assert.That(changes[1].Effect.StackCount, Is.EqualTo(2));
        }

        [Test]
        public void ValidationRejectsStackingOnAnInstantEffect()
        {
            var instant = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant,
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget)));

            Assert.That(instant.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("쌓을 수 없다"));
        }

        /// <summary>속도를 3 깎는 2초짜리 지속 효과를 지정한 쌓임 규칙으로 만든다.</summary>
        private GameplayEffectDefinition CreateSlow(GameplayEffectStackingSettings stacking)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, -3f) },
                duration: 2f,
                stacking: stacking));
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
