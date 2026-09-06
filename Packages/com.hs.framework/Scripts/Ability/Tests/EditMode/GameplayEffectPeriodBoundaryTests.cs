using System.Collections.Generic;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using NUnit.Framework;
using UnityEngine;

namespace HS.Framework.Ability.Tests.EditMode
{
    /// <summary>주기 실행이 효과가 살아 있는 동안에만 일어나는지 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>계약.</b> 지속 D초·주기 P초인 효과는 적용 뒤 <c>floor(D / P)</c>번 실행하며, 주기가 만료 시각과
    /// 정확히 겹치면 그 실행은 나간다. 적용 순간의 실행은 이 셈과 별개로 한 번 더해진다.
    /// <b>이 횟수는 스텝을 어떻게 쪼개도 같아야 한다.</b>
    /// </para>
    /// <para>
    /// <b>왜 경계를 실행하는 쪽으로 정했나.</b> 3초 지속·1초 주기처럼 딱 떨어지는 설정이 가장 흔한데,
    /// 반대로 정하면 그런 설정에서 마지막 한 번이 사라져 설정한 사람이 센 횟수와 어긋난다.
    /// 만료 시각에 걸친 주기를 뺄 이유는 「살지 않은 시간」인데, 그 시각까지는 살아 있었다.
    /// </para>
    /// <para>
    /// <b>확인하지 않는 것</b>: 무한 지속 효과의 주기는 여기서 보지 않는다. 만료가 없으므로 끊을 경계도
    /// 없고, 그쪽은 <c>ALargeTickRunsEveryPeriodThatFitsInIt</c>와 <c>GameplayTickModeTests</c>가 본다.
    /// </para>
    /// </remarks>
    public sealed class GameplayEffectPeriodBoundaryTests
    {
        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<Object> _createdObjects = new();

        private AttributeDefinition _health;
        private AttributeSet _attributes;
        private GameplayEffectRunner _runner;

        [SetUp]
        public void SetUp()
        {
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_health);
            _runner = new GameplayEffectRunner(_attributes);
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
        public void AStepThatOvershootsTheExpiryDoesNotRunThePeriodsBeyondIt()
        {
            // 만료 뒤의 스텝 시간은 주기 실행 횟수에 포함하지 않는다.
            var definition = CreatePeriodic(duration: 3f, period: 1f);
            _runner.Apply(definition);

            _runner.Tick(10f);

            AssertExecutions(4, "적용 시 한 번과 살아 있던 3초 동안 세 번이다. 살지 않은 7초는 실행하지 않는다.");
            Assert.That(_runner.ActiveEffectCount, Is.Zero, "지속 시간이 다했으므로 걷혀 있어야 한다.");
        }

        [Test]
        public void ThePeriodThatLandsExactlyOnTheExpiryStillRuns()
        {
            var definition = CreatePeriodic(duration: 3f, period: 1f);
            _runner.Apply(definition);

            _runner.Tick(1f);
            _runner.Tick(1f);
            _runner.Tick(1f);

            AssertExecutions(4, "만료 시각과 겹치는 주기는 나간다. 그 시각까지는 살아 있었다.");
            Assert.That(_runner.ActiveEffectCount, Is.Zero);
        }

        [Test]
        public void TheCountIsTheSameHoweverTheStepIsSliced()
        {
            // 기기 성능이 결과를 바꾸지 않는다는 것이 이 검사의 요지다.
            foreach (var step in new[] { 0.25f, 0.5f, 1f, 1.5f, 3f, 10f })
            {
                SetUpFresh();
                var definition = CreatePeriodic(duration: 3f, period: 1f);
                _runner.Apply(definition);

                Advance(step, total: 12f);

                AssertExecutions(4, $"스텝 {step}초에서 횟수가 달라졌다.");
            }
        }

        [Test]
        public void APeriodLongerThanTheDurationRunsOnlyOnApplication()
        {
            var definition = CreatePeriodic(duration: 1f, period: 5f);
            _runner.Apply(definition);

            _runner.Tick(10f);

            AssertExecutions(1, "1초를 사는 동안 5초 주기는 한 번도 차지 않는다.");
        }

        [Test]
        public void ARefreshingStackKeepsUsingTheLeftoverOfTheStep()
        {
            // 지속 시간이 갱신되면 남은 스텝 시간에도 주기 실행을 이어간다.
            var definition = CreatePeriodic(
                duration: 1f,
                period: 1f,
                stacking: new GameplayEffectStackingSettings(
                    GameplayEffectStackingPolicy.AggregateByTarget,
                    expirationPolicy: GameplayEffectStackExpirationPolicy.RefreshDuration));
            _runner.Apply(definition);
            _runner.Apply(definition);

            // 🔴 층수를 먼저 못 박는다. 이것을 확인하지 않고 아래에서 층수를 곱하면,
            // 효과가 걷혀 층수가 0이 되었을 때 기대값도 0이 되어 검사가 헛되이 통과한다.
            var stacks = StackCount(definition);
            Assert.That(stacks, Is.EqualTo(2), "두 번 적용했으므로 두 층이어야 한다.");
            var afterApplications = ExecutionCount;

            _runner.Tick(3f);

            Assert.That(
                ExecutionCount - afterApplications,
                Is.EqualTo(3 * stacks),
                "지속이 갱신되면 3초 동안 세 번 실행하며, 한 번에 쌓인 층수만큼 실행된다.");
            Assert.That(_runner.ActiveEffectCount, Is.EqualTo(1), "갱신 정책은 스스로 걷히지 않는다.");
        }

        [Test]
        public void AStepFarLargerThanTheDurationExpiresItExactlyOnce()
        {
            var definition = CreatePeriodic(duration: 2f, period: 1f);
            _runner.Apply(definition);

            _runner.Tick(5f);

            AssertExecutions(3, "적용 시 한 번과 살아 있던 2초 동안 두 번이다.");
            Assert.That(_runner.ActiveEffectCount, Is.Zero, "스텝이 지속을 넘겨도 만료는 한 번 일어난다.");
        }

        /// <summary>지금까지 실행된 횟수이며, 체력이 얼마나 깎였는지로 센다.</summary>
        private int ExecutionCount => Mathf.RoundToInt((100f - _attributes.GetBaseValue(_health)) / 10f);

        /// <summary>실행 횟수를 확인한다.</summary>
        /// <param name="expected">기대하는 실행 횟수이다.</param>
        /// <param name="message">어긋났을 때 남길 말이다.</param>
        private void AssertExecutions(int expected, string message)
        {
            Assert.That(ExecutionCount, Is.EqualTo(expected), message);
        }

        /// <summary>정해진 스텝으로 전체 시간만큼 나아간다.</summary>
        /// <param name="step">한 번에 흘릴 시간(초)이다.</param>
        /// <param name="total">전부 흘릴 시간(초)이다.</param>
        private void Advance(float step, float total)
        {
            var elapsed = 0f;
            while (elapsed < total)
            {
                _runner.Tick(step);
                elapsed += step;
            }
        }

        /// <summary>그 정의로 쌓인 층수를 센다.</summary>
        /// <param name="definition">셀 효과 정의이다.</param>
        /// <returns>쌓인 층수이며 없으면 0이다.</returns>
        private int StackCount(GameplayEffectDefinition definition)
        {
            foreach (var effect in _runner.ActiveEffects)
            {
                if (effect.Definition == definition)
                {
                    return effect.StackCount;
                }
            }

            return 0;
        }

        /// <summary>한 검사 안에서 여러 스텝 크기를 견주기 위해 판을 새로 세운다.</summary>
        private void SetUpFresh()
        {
            _attributes?.Dispose();
            _health = Track(AttributeDefinition.CreateRuntime("Health", 100f));
            _attributes = new AttributeSet();
            _attributes.AddAttribute(_health);
            _runner = new GameplayEffectRunner(_attributes);
        }

        /// <summary>체력을 10씩 깎는 주기 효과를 만든다.</summary>
        /// <param name="duration">지속 시간(초)이다.</param>
        /// <param name="period">주기(초)이다.</param>
        /// <param name="stacking">쌓임 설정이며 기본은 쌓지 않는 것이다.</param>
        /// <returns>만든 효과 정의이다.</returns>
        private GameplayEffectDefinition CreatePeriodic(
            float duration,
            float period,
            GameplayEffectStackingSettings stacking = default)
        {
            return Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                new[] { GameplayEffectModifier.CreateRuntime(_health, AttributeModifierOperation.Add, -10f) },
                duration,
                period,
                stacking: stacking));
        }

        /// <summary>정리 목록에 등록한다.</summary>
        /// <typeparam name="T">등록할 개체의 타입이다.</typeparam>
        /// <param name="createdObject">등록할 개체이다.</param>
        /// <returns>등록한 개체 그대로이다.</returns>
        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
