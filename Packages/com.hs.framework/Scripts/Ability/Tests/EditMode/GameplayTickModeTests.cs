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
    /// <summary>
    /// 시간을 흘리는 자리를 고를 수 있고, 어느 자리를 골라도 쿨다운·지속·주기가 같은 초를 세는 것을 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 프레임률 독립을 증명하는 검사의 형태는 하나뿐이다 — <b>같은 초기 상태에서 스텝 수나 델타를 달리해도 결과가 같다.</b>
    /// 여기서는 같은 효과를 두 실행기에 적용하고 한쪽은 굵은 스텝으로, 다른 쪽은 잔 스텝으로 같은 초를 흘려 견준다.
    /// </para>
    /// <para>
    /// 델타는 이진수로 정확한 값(0.25·0.125)만 쓴다. 0.02 같은 값은 더할수록 오차가 쌓여 만료가 한 스텝 앞뒤로
    /// 흔들리는데, 그것은 시계의 결함이 아니라 부동소수점의 성질이라 검사가 재려는 것이 아니다.
    /// </para>
    /// <para>
    /// 컴포넌트의 <c>Update</c>·<c>FixedUpdate</c> 분기 자체는 편집기 검사에서 관측할 수 없다 — 플레이 중이 아니면
    /// 흐르는 시간이 0이라 어느 쪽을 불러도 아무 일도 없다. 그래서 여기서는 기본값과 전파만 보고, 분기는 코드와 문서로 대조한다.
    /// </para>
    /// </remarks>
    public sealed class GameplayTickModeTests
    {
        /// <summary>굵은 스텝(초)이며 이진수로 정확하다.</summary>
        private const float CoarseStep = 0.25f;

        /// <summary>잔 스텝(초)이며 이진수로 정확하다.</summary>
        private const float FineStep = 0.125f;

        /// <summary>테스트가 만든 에셋이며 정리 대상이다.</summary>
        private readonly List<UnityEngine.Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
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
        public void ADurationEndsAfterTheSameSecondsWhicheverStepSizeDrivesIt()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                duration: 3f,
                grantedTags: new[] { "State.Buffed" }));
            using var coarse = new Rig();
            using var fine = new Rig();
            var coarseEffect = coarse.Runner.Apply(definition);
            var fineEffect = fine.Runner.Apply(definition);

            Advance(coarse.Runner, CoarseStep, 11);
            Advance(fine.Runner, FineStep, 23);
            Assert.That(coarseEffect.IsActive, Is.True, "2.75초에는 아직 살아 있다.");
            Assert.That(fineEffect.IsActive, Is.True, "2.875초에는 아직 살아 있다.");

            coarse.Runner.Tick(CoarseStep);
            fine.Runner.Tick(FineStep);

            Assert.That(coarseEffect.IsActive, Is.False, "3초가 되는 스텝에 걷힌다.");
            Assert.That(fineEffect.IsActive, Is.False, "스텝이 잘아도 같은 3초에 걷힌다.");
        }

        [Test]
        public void APeriodFiresTheSameNumberOfTimesOverTheSameSeconds()
        {
            var definition = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Infinite,
                period: 0.5f));
            using var coarse = new Rig();
            using var fine = new Rig();
            var coarseExecutions = CountExecutions(coarse.Runner);
            var fineExecutions = CountExecutions(fine.Runner);
            coarse.Runner.Apply(definition);
            fine.Runner.Apply(definition);

            Advance(coarse.Runner, CoarseStep, 12);
            Advance(fine.Runner, FineStep, 24);

            Assert.That(coarseExecutions.Count, Is.EqualTo(fineExecutions.Count), "3초 동안의 주기 실행 횟수는 스텝 크기와 무관하다.");
            Assert.That(coarseExecutions.Count, Is.EqualTo(7), "적용하는 순간 한 번, 그 뒤 0.5초마다 여섯 번이다.");
        }

        [Test]
        public void AnAbilityCooldownEndsAfterTheSameSecondsWhicheverStepSizeDrivesIt()
        {
            var cooldown = Track(GameplayEffectDefinition.CreateRuntime(
                GameplayEffectDurationPolicy.Duration,
                duration: 1f,
                grantedTags: new[] { "Cooldown.Test" }));
            using var coarse = new Rig();
            using var fine = new Rig();
            var coarseDefinition = GrantAndActivate(coarse.System, cooldown);
            var fineDefinition = GrantAndActivate(fine.System, cooldown);
            Assert.That(coarse.System.IsOnCooldown(coarseDefinition), Is.True);
            Assert.That(fine.System.IsOnCooldown(fineDefinition), Is.True);

            Advance(coarse.Runner, CoarseStep, 3);
            Advance(fine.Runner, FineStep, 7);
            Assert.That(coarse.System.IsOnCooldown(coarseDefinition), Is.True, "0.75초에는 아직 쿨다운이다.");
            Assert.That(fine.System.IsOnCooldown(fineDefinition), Is.True, "0.875초에는 아직 쿨다운이다.");

            coarse.Runner.Tick(CoarseStep);
            fine.Runner.Tick(FineStep);

            Assert.That(coarse.System.IsOnCooldown(coarseDefinition), Is.False, "1초가 되는 스텝에 쿨다운이 끝난다.");
            Assert.That(fine.System.IsOnCooldown(fineDefinition), Is.False, "스텝이 잘아도 같은 1초에 끝난다.");
        }

        [Test]
        public void TheDefaultTickModeIsFixedUpdateOnBothComponents()
        {
            var host = Track(new GameObject("Host"));
            var abilityComponent = host.AddComponent<GameplayAbilitySystemComponent>();
            var effectComponent = host.GetComponent<GameplayEffectComponent>();

            Assert.That(abilityComponent.TickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate), "기본은 프레임률이 결과를 바꾸지 않는 쪽이다.");
            Assert.That(effectComponent.TickMode, Is.EqualTo(GameplayTickMode.OnFixedUpdate));
        }

        [Test]
        public void ConfiguringTheAbilityComponentCarriesTheTickModeToTheEffectComponent()
        {
            var host = Track(new GameObject("Host"));
            var abilityComponent = host.AddComponent<GameplayAbilitySystemComponent>();
            var effectComponent = host.GetComponent<GameplayEffectComponent>();

            abilityComponent.ConfigureTickMode(GameplayTickMode.OnUpdate);

            Assert.That(abilityComponent.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate));
            Assert.That(effectComponent.TickMode, Is.EqualTo(GameplayTickMode.OnUpdate), "어빌리티와 효과는 같은 시계를 본다.");
        }

        /// <summary>실행기에 같은 크기의 스텝을 지정한 횟수만큼 흘린다.</summary>
        private static void Advance(GameplayEffectRunner runner, float step, int count)
        {
            for (var index = 0; index < count; index++)
            {
                runner.Tick(step);
            }
        }

        /// <summary>실행기의 주기 실행 알림을 모은다. 돌려준 목록의 길이가 실행 횟수다.</summary>
        private static List<GameplayEffectChange> CountExecutions(GameplayEffectRunner runner)
        {
            var executions = new List<GameplayEffectChange>();
            runner.Changed.Subscribe(change =>
            {
                if (change.ChangeKind == GameplayEffectChangeKind.Executed)
                {
                    executions.Add(change);
                }
            });
            return executions;
        }

        /// <summary>쿨다운 효과를 가진 어빌리티를 부여하고 한 번 켠다.</summary>
        private RunningAbilityDefinition GrantAndActivate(GameplayAbilitySystem system, GameplayEffectDefinition cooldown)
        {
            var definition = Track(RunningAbilityDefinition.CreateRuntime("Ability.Test", cooldown: cooldown));
            Assert.That(system.GrantAbility(definition), Is.Not.Null, "어빌리티 부여에 실패했다.");
            Assert.That(system.TryActivate(definition.AbilityTag), Is.EqualTo(GameplayAbilityActivationResult.Success));
            return definition;
        }

        private T Track<T>(T createdObject) where T : UnityEngine.Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>어트리뷰트 집합·효과 실행기·어빌리티 시스템 한 벌이다. 두 벌을 세워 서로 다른 스텝으로 견준다.</summary>
        private sealed class Rig : IDisposable
        {
            public Rig()
            {
                Attributes = new AttributeSet();
                Runner = new GameplayEffectRunner(Attributes);
                System = new GameplayAbilitySystem(Runner);
            }

            public AttributeSet Attributes { get; }

            public GameplayEffectRunner Runner { get; }

            public GameplayAbilitySystem System { get; }

            public void Dispose()
            {
                System.Dispose();
                Attributes.Dispose();
            }
        }
    }
}
