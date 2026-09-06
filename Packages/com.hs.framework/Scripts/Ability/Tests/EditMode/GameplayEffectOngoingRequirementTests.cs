using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>유지 중 효과의 진행 조건이 깨지면 걷지 않고 억제되며, 되돌아오면 다시 작용하는 규칙을 검증한다.</summary>
    /// <remarks>
    /// 적용 조건은 적용하는 순간 한 번 보지만 진행 조건은 유지되는 동안 계속 본다. 억제된 동안 수정자와 부여 태그는
    /// 걷혀 있고 주기 실행은 쉬지만 지속 시간은 그대로 흘러야 한다. 땅에 있는 동안만 작용하는 버프가 이 형태이다.
    /// </remarks>
    public sealed class GameplayEffectOngoingRequirementTests
    {
        private static readonly GameplayTag Grounded = GameplayTag.Parse("State.Grounded");
        private static readonly GameplayTag Silenced = GameplayTag.Parse("State.Silenced");
        private static readonly GameplayTag Hasted = GameplayTag.Parse("State.Hasted");

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
        public void LosingARequiredTagInhibitsTheEffectWithoutRemovingIt()
        {
            _tags.AddTag(Grounded);
            var haste = CreateGroundedHaste();
            var effect = _runner.Apply(haste);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(15f).Within(0.001f));
            Assert.That(_tags.HasTag(Hasted), Is.True);

            _tags.RemoveTag(Grounded);

            Assert.That(effect.IsActive, Is.True, "조건이 깨져도 효과는 걷히지 않는다.");
            Assert.That(effect.IsInhibited, Is.True);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f), "억제된 동안 수정자는 걷혀 있어야 한다.");
            Assert.That(_tags.HasTag(Hasted), Is.False, "억제된 동안 부여 태그도 걷혀 있어야 한다.");
        }

        [Test]
        public void RegainingTheTagReappliesTheEffect()
        {
            _tags.AddTag(Grounded);
            var effect = _runner.Apply(CreateGroundedHaste());
            _tags.RemoveTag(Grounded);

            _tags.AddTag(Grounded);

            Assert.That(effect.IsInhibited, Is.False);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(15f).Within(0.001f), "조건이 되돌아오면 수정자를 다시 얹어야 한다.");
            Assert.That(_tags.HasTag(Hasted), Is.True);
        }

        [Test]
        public void GainingABlockedTagInhibitsTheEffect()
        {
            var aura = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, 5f) },
                ongoingBlockedTags: new[] { "State.Silenced" }));
            var effect = _runner.Apply(aura);

            _tags.AddTag(Silenced);
            Assert.That(effect.IsInhibited, Is.True);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f));

            _tags.RemoveTag(Silenced);

            Assert.That(effect.IsInhibited, Is.False);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void AnEffectAppliedWhileTheRequirementIsUnmetStartsInhibited()
        {
            var effect = _runner.Apply(CreateGroundedHaste());

            Assert.That(effect, Is.Not.Null, "적용 자체는 된다. 진행 조건은 적용 조건이 아니다.");
            Assert.That(effect.IsInhibited, Is.True);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f));
            Assert.That(_tags.HasTag(Hasted), Is.False);
        }

        [Test]
        public void TheDurationKeepsRunningWhileInhibited()
        {
            _tags.AddTag(Grounded);
            var effect = _runner.Apply(CreateGroundedHaste(duration: 2f));
            _tags.RemoveTag(Grounded);

            _runner.Tick(2f);

            Assert.That(effect.IsActive, Is.False, "억제된 동안에도 지속 시간은 흐른다.");
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f));
            Assert.That(_tags.HasTag(Hasted), Is.False);
        }

        [Test]
        public void PeriodicExecutionPausesWhileInhibited()
        {
            _tags.AddTag(Grounded);
            var regen = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, 5f) },
                period: 1f,
                ongoingRequiredTags: new[] { "State.Grounded" }));
            _attributes.SetBaseValue(_health, 50f);
            _runner.Apply(regen);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(55f).Within(0.001f));
            _tags.RemoveTag(Grounded);

            _runner.Tick(1f);
            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(55f).Within(0.001f), "억제된 동안 주기 실행은 쉰다.");

            _tags.AddTag(Grounded);
            _runner.Tick(1f);

            Assert.That(_attributes.GetBaseValue(_health), Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void RemovingAnInhibitedEffectDoesNotStealAnotherEffectsTag()
        {
            _tags.AddTag(Grounded);
            var haste = CreateGroundedHaste();
            var otherHaste = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite, grantedTags: new[] { "State.Hasted" }));
            var effect = _runner.Apply(haste);
            _runner.Apply(otherHaste);
            _tags.RemoveTag(Grounded);
            Assert.That(_tags.GetCount(Hasted), Is.EqualTo(1));

            _runner.Remove(effect);

            Assert.That(_tags.GetCount(Hasted), Is.EqualTo(1), "억제된 효과는 이미 걷혀 있으므로 제거할 때 태그를 다시 걷으면 안 된다.");
        }

        [Test]
        public void ReenablingAStackedEffectRestoresEveryLayer()
        {
            _tags.AddTag(Grounded);
            var stackingHaste = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, 5f) },
                stacking: new GameplayEffectStackingSettings(GameplayEffectStackingPolicy.AggregateByTarget),
                ongoingRequiredTags: new[] { "State.Grounded" }));
            _runner.Apply(stackingHaste);
            _runner.Apply(stackingHaste);
            _tags.RemoveTag(Grounded);
            _runner.Apply(stackingHaste);
            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(10f).Within(0.001f), "억제된 동안 더한 층도 얹히지 않는다.");

            _tags.AddTag(Grounded);

            Assert.That(_attributes.GetCurrentValue(_speed), Is.EqualTo(25f).Within(0.001f), "억제가 풀리면 층수만큼 다시 얹어야 한다.");
        }

        [Test]
        public void InhibitionChangesAreAnnounced()
        {
            _tags.AddTag(Grounded);
            var effect = _runner.Apply(CreateGroundedHaste());
            var changes = new List<GameplayEffectChange>();
            using var subscription = _runner.Changed.Subscribe(changes.Add);

            _tags.RemoveTag(Grounded);

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].ChangeKind, Is.EqualTo(GameplayEffectChangeKind.InhibitionChanged));
            Assert.That(changes[0].Effect, Is.SameAs(effect));
            Assert.That(changes[0].Effect.IsInhibited, Is.True);
        }

        [Test]
        public void ValidationRejectsOngoingRequirementsOnAnInstantEffect()
        {
            var instant = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Instant, ongoingRequiredTags: new[] { "State.Grounded" }));

            Assert.That(instant.TryValidate(out var errorMessage), Is.False);
            Assert.That(errorMessage, Does.Contain("진행 조건"));
        }

        [Test]
        public void ADisposedRunnerStopsWatchingTags()
        {
            _tags.AddTag(Grounded);
            var effect = _runner.Apply(CreateGroundedHaste());

            _runner.Dispose();

            Assert.That(effect.IsActive, Is.False, "놓을 때 유지 중인 효과는 되돌아간다.");
            Assert.DoesNotThrow(() => _tags.RemoveTag(Grounded));
            Assert.That(_runner.Apply(CreateGroundedHaste()), Is.Null, "놓은 뒤에는 아무것도 적용하지 않는다.");
        }

        /// <summary>땅에 있는 동안만 작용하는 가속 효과를 만든다.</summary>
        /// <param name="duration">지속 시간이며 0이면 무한 효과이다.</param>
        private GameplayEffectDefinition CreateGroundedHaste(float duration = 0f)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                duration > 0f ? GameplayEffectDurationPolicy.Duration : GameplayEffectDurationPolicy.Infinite,
                new[] { GameplayEffectModifier.CreateRuntime(_speed, AttributeModifierOperation.Add, 5f) },
                duration: duration,
                grantedTags: new[] { "State.Hasted" },
                ongoingRequiredTags: new[] { "State.Grounded" }));
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
