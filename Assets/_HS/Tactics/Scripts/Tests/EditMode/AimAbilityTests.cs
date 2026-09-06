using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Tactics.Character.Movement;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>조준이 시간을 쓰고, 그 시간이 유닛의 각속도로 정해지는 것을 고정한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>이것이 각속도라는 값이 뜻을 갖는 자리다.</b> 회전이 한 번에 끝나면 각속도가 빠른 유닛과 느린 유닛이
    /// 똑같이 굴고, 그러면 그 값을 유닛 데이터에 둘 이유가 없다. 등 뒤에서 온 적은 이미 사거리 안이라
    /// 접근이 곧바로 끝나고 조준만 남으므로, 그 시간이 그대로 대응 시간이 된다.
    /// </para>
    /// <para>
    /// <b>스텝 수를 셀 때 한 스텝의 어긋남은 재지 않는다.</b> 어빌리티와 방향 구성요소는 각자 고정 스텝에서
    /// 도는 별개의 자리이고 <b>둘 중 어느 쪽이 먼저 도는지는 규칙이 아니다.</b> 그 순서는 조준이 끝나는 시각을
    /// 한 스텝 옮길 뿐 비율을 바꾸지 않으므로, 여기서 고정하는 것은 비율이다.
    /// </para>
    /// </remarks>
    public sealed class AimAbilityTests
    {
        /// <summary>조준 어빌리티의 식별 태그 이름이다.</summary>
        private const string AimTagName = UnitAbilityTags.Aim;

        /// <summary>검사에서 쓰는 허용 각(도)이며, 스텝 수 계산이 딱 떨어지도록 작게 잡는다.</summary>
        private const float Tolerance = 0.1f;

        /// <summary>검사에서 쓰는 고정 스텝의 길이(초)이다.</summary>
        private const float Step = 0.1f;

        private readonly List<Object> _created = new();
        private readonly List<AttributeSet> _attributeSets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var attributeSet in _attributeSets)
            {
                attributeSet.Dispose();
            }

            _attributeSets.Clear();

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AimingTakesTwiceAsLongWhenTheUnitTurnsHalfAsFast()
        {
            // 정면(+Z)에서 90도 떨어진 자리다. 초당 90도면 스텝마다 9도씩 돌아 열 스텝, 180도면 다섯 스텝이다.
            var toTheSide = new Vector3(5f, 0f, 0f);

            var slowSteps = StepsUntilAimFinishes(turnSpeed: 90f, targetPosition: toTheSide);
            var fastSteps = StepsUntilAimFinishes(turnSpeed: 180f, targetPosition: toTheSide);

            Assert.That(slowSteps, Is.EqualTo(10).Within(1), "90도를 초당 90도로 도는 데 1초가 걸린다.");
            Assert.That(
                slowSteps,
                Is.EqualTo(fastSteps * 2).Within(1),
                "조준 시간은 각속도에 반비례해야 한다. 각속도가 빠른 유닛이 더 빨리 대처한다는 것이 그 뜻이다.");
        }

        [Test]
        public void AnEnemyBehindTakesLongerToAimAtThanOneInFront()
        {
            var behindSteps = StepsUntilAimFinishes(turnSpeed: 90f, targetPosition: new Vector3(0f, 0f, -5f));
            var frontSteps = StepsUntilAimFinishes(turnSpeed: 90f, targetPosition: new Vector3(0f, 0f, 5f));

            Assert.That(frontSteps, Is.EqualTo(0), "이미 마주 보고 있으면 조준에 시간이 들지 않는다.");
            Assert.That(
                behindSteps,
                Is.GreaterThan(frontSteps),
                "정면의 적으로는 각속도가 드러나지 않는다. 등 뒤에서 온 적이 이 설계가 겨누는 경우다.");
        }

        [Test]
        public void AUnitAlreadyFacingTheTargetFinishesTheAimAtOnce()
        {
            var harness = CreateHarness(turnSpeed: 90f, targetPosition: new Vector3(0f, 0f, 5f));

            var result = harness.System.TryActivate(GameplayTag.Parse(AimTagName));

            Assert.That(result, Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(harness.Ability.IsActive, Is.False, "활성화 직후의 첫 판정에서 끝난다.");
            Assert.That(
                harness.Ability.LastEndReason,
                Is.EqualTo(GameplayAbilityEndReason.Completed),
                "트리가 이것을 성공으로 읽어야 곧바로 사격으로 넘어간다.");
        }

        [Test]
        public void CancellingTheAimReleasesTheLookTarget()
        {
            var harness = CreateHarness(turnSpeed: 90f, targetPosition: new Vector3(5f, 0f, 0f));
            harness.System.TryActivate(GameplayTag.Parse(AimTagName));
            Assert.That(harness.Facing.HasLookTarget, Is.True, "도는 동안에는 바라볼 점이 걸려 있다.");

            harness.System.CancelAbility(GameplayTag.Parse(AimTagName));

            Assert.That(
                harness.Facing.HasLookTarget,
                Is.False,
                "가로채였는데 점이 남으면 다음 가지가 걷는 동안에도 그 점을 보느라 정면이 어긋난다.");
        }

        [Test]
        public void TheAimEndsAndReleasesTheLookTargetWhenTheTargetIsGone()
        {
            var harness = CreateHarness(turnSpeed: 90f, targetPosition: new Vector3(5f, 0f, 0f));
            harness.System.TryActivate(GameplayTag.Parse(AimTagName));

            harness.TargetSource.CurrentTarget = null;
            harness.System.Tick(Step);

            Assert.That(harness.Ability.IsActive, Is.False);
            Assert.That(harness.Facing.HasLookTarget, Is.False);
        }

        [Test]
        public void WithoutSomethingToTurnTheAimDoesNotStart()
        {
            var harness = CreateHarness(turnSpeed: 90f, targetPosition: new Vector3(5f, 0f, 0f), withFacing: false);

            Assert.That(
                harness.System.TryActivate(GameplayTag.Parse(AimTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "돌 수단이 없으면 조준을 시작하지 않는다. 시작해 두고 영영 못 끝내면 사격이 막힌다.");
        }

        [Test]
        public void WithoutATargetTheAimDoesNotStart()
        {
            var harness = CreateHarness(turnSpeed: 90f, targetPosition: new Vector3(5f, 0f, 0f));
            harness.TargetSource.CurrentTarget = null;

            Assert.That(
                harness.System.TryActivate(GameplayTag.Parse(AimTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected));
        }

        /// <summary>
        /// 조준이 끝날 때까지 걸린 고정 스텝 수를 센다. 활성화 직후 바로 끝났으면 0이다.
        /// </summary>
        /// <remarks>
        /// 방향 구성요소를 먼저 돌리고 어빌리티가 그 결과를 본다. 실제 실행에서 두 <c>FixedUpdate</c>의 순서는
        /// 정해져 있지 않으므로, 이 순서는 검사가 세는 방식일 뿐이며 검사는 그 차이를 재지 않는다.
        /// </remarks>
        /// <param name="turnSpeed">유닛의 각속도(초당 도)이다.</param>
        /// <param name="targetPosition">대상이 선 자리이다.</param>
        /// <returns>조준이 끝날 때까지 흐른 스텝 수이다.</returns>
        private int StepsUntilAimFinishes(float turnSpeed, Vector3 targetPosition)
        {
            var harness = CreateHarness(turnSpeed, targetPosition);
            Assert.That(
                harness.System.TryActivate(GameplayTag.Parse(AimTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Success),
                "검사가 기대는 준비 단계가 실패했다. 조준을 시작하지 못했다.");

            var steps = 0;
            while (harness.Ability.IsActive)
            {
                Assert.That(steps, Is.LessThan(1000), "조준이 끝나지 않는다. 허용 각이나 각속도를 확인해야 한다.");
                harness.Facing.Tick(Step);
                harness.System.Tick(Step);
                steps++;
            }

            return steps;
        }

        /// <summary>조준 어빌리티 하나만 부여한 유닛과 대상을 세운다.</summary>
        /// <param name="turnSpeed">유닛의 각속도(초당 도)이다.</param>
        /// <param name="targetPosition">대상이 선 자리이다.</param>
        /// <param name="withFacing">바라보는 방향을 도맡는 구성요소를 붙일지 여부이다.</param>
        /// <returns>세운 무대이다.</returns>
        private AimHarness CreateHarness(float turnSpeed, Vector3 targetPosition, bool withFacing = true)
        {
            var unitObject = CreateObject("Aimer");
            var targetSource = unitObject.AddComponent<FakeAimTargetSource>();

            CharacterFacing facing = null;
            if (withFacing)
            {
                facing = unitObject.AddComponent<CharacterFacing>();
                facing.SetTurnSpeed(turnSpeed);
            }

            var targetObject = CreateObject("Target");
            targetObject.transform.position = targetPosition;
            targetSource.CurrentTarget = targetObject.transform;

            var attributes = new AttributeSet();
            _attributeSets.Add(attributes);
            var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes), unitObject);
            system.GrantAbility(Track(AimAbilityDefinition.CreateRuntime(AimTagName, Tolerance)));
            system.TryGetAbility(GameplayTag.Parse(AimTagName), out var granted);

            return new AimHarness(system, (AimAbility)granted, facing, targetSource);
        }

        private GameObject CreateObject(string objectName)
        {
            var created = new GameObject(objectName);
            _created.Add(created);
            return created;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>조준 검사가 쓰는 무대이며 유닛과 협력자를 함께 든다.</summary>
        private sealed class AimHarness
        {
            public AimHarness(
                GameplayAbilitySystem system,
                AimAbility ability,
                CharacterFacing facing,
                FakeAimTargetSource targetSource)
            {
                System = system;
                Ability = ability;
                Facing = facing;
                TargetSource = targetSource;
            }

            public GameplayAbilitySystem System { get; }

            public AimAbility Ability { get; }

            public CharacterFacing Facing { get; }

            public FakeAimTargetSource TargetSource { get; }
        }

        /// <summary>대상을 검사가 직접 정하는 공급자이다.</summary>
        private sealed class FakeAimTargetSource : MonoBehaviour, ICombatTargetSource
        {
            public Transform CurrentTarget { get; set; }
        }
    }
}
