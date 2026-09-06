using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Attributes;
using HS.Framework.Ability.Effects;
using HS.Framework.Ability.Tags;
using HS.Framework.Character;
using HS.Tactics.Combat;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 접근이 사거리 안에서 끝나고, 종료할 때 자신이 시작한 이동을 정리하는지 검증한다.
    /// 방향 구성요소 없이도 접근할 수 있으며, 자리 도달 판정이 없으면 대상 위치를 목적지로 쓴다.
    /// 슬롯 분산 계산은 ApproachSlotResolverTests가 별도로 검증한다.
    /// </summary>
    public sealed class ApproachAbilityTests
    {
        /// <summary>접근 어빌리티의 식별 태그 이름이다.</summary>
        private const string ApproachTagName = UnitAbilityTags.Approach;

        /// <summary>검사 유닛의 사거리(미터)이다.</summary>
        private const float AttackRange = 10f;

        /// <summary>사거리에 곱해 멈출 거리를 정하는 비율이며, 멈출 거리는 9미터가 된다.</summary>
        private const float RangeRatio = 0.9f;

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
        public void ATargetFartherThanTheEngageDistanceIsWalkedToward()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));

            harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));

            Assert.That(harness.Ability.IsActive, Is.True, "사거리 밖이면 걸어야 한다.");
            Assert.That(harness.Mover.LastDestination, Is.Not.Null);
            Assert.That(harness.Mover.StopCount, Is.Zero);
        }

        [Test]
        public void ATargetAlreadyWithinTheEngageDistanceFinishesTheApproachAtOnce()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 5f));

            var result = harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));

            Assert.That(result, Is.EqualTo(GameplayAbilityActivationResult.Success));
            Assert.That(harness.Ability.IsActive, Is.False, "이미 사거리 안이면 걸을 것이 없다.");
            Assert.That(
                harness.Ability.LastEndReason,
                Is.EqualTo(GameplayAbilityEndReason.Completed),
                "등 뒤에서 온 적이 이 경우다. 접근이 곧바로 성공해야 남는 것이 조준뿐이 된다.");
            Assert.That(harness.Mover.LastDestination, Is.Null, "걷지 않았으면 이동 요청도 없다.");
        }

        [Test]
        public void TheApproachEndsWhenTheTargetComesWithinTheEngageDistance()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));
            harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));
            Assert.That(harness.Ability.IsActive, Is.True);

            harness.Target.position = new Vector3(0f, 0f, 8f);
            harness.System.Tick(0.1f);

            Assert.That(harness.Ability.IsActive, Is.False);
            Assert.That(harness.Mover.StopCount, Is.EqualTo(1), "걷던 것을 멈추지 않으면 유닛이 사거리 안을 지나쳐 간다.");
        }

        [Test]
        public void TheEngageDistanceIsTheAttackRangeTimesTheRatio()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));

            Assert.That(
                harness.Ability.EngageDistance,
                Is.EqualTo(AttackRange * RangeRatio).Within(0.001f),
                "사거리 끝에서 멈추면 대상이 한 걸음만 물러나도 다시 걷게 된다.");
        }

        [Test]
        public void CancellingTheApproachStopsTheWalk()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));
            harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));

            harness.System.CancelAbility(GameplayTag.Parse(ApproachTagName));

            Assert.That(
                harness.Mover.StopCount,
                Is.EqualTo(1),
                "사망이나 트리의 끊김으로 취소돼도 걷던 것은 멈춰야 한다.");
        }

        [Test]
        public void AnApproachThatNeverWalkedDoesNotStopSomeoneElsesWalk()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 5f));
            harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));

            Assert.That(
                harness.Mover.StopCount,
                Is.Zero,
                "이동 수단은 유닛에 하나뿐이라, 몰지 않던 자리가 멈추면 다른 자리가 몰던 이동이 끊긴다.");
        }

        [Test]
        public void TheApproachEndsAndStopsWhenTheTargetIsGone()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));
            harness.System.TryActivate(GameplayTag.Parse(ApproachTagName));

            harness.TargetSource.CurrentTarget = null;
            harness.System.Tick(0.1f);

            Assert.That(harness.Ability.IsActive, Is.False);
            Assert.That(harness.Mover.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void WithoutATargetTheApproachDoesNotStart()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f));
            harness.TargetSource.CurrentTarget = null;

            Assert.That(
                harness.System.TryActivate(GameplayTag.Parse(ApproachTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected));
        }

        [Test]
        public void WithoutAKnownAttackRangeTheApproachDoesNotStart()
        {
            var harness = CreateHarness(new Vector3(0f, 0f, 20f), withUnitDefinition: false);

            Assert.That(
                harness.System.TryActivate(GameplayTag.Parse(ApproachTagName)),
                Is.EqualTo(GameplayAbilityActivationResult.Rejected),
                "어디까지 다가갈지 모르면 걸을 곳도 모른다.");
        }

        /// <summary>접근 어빌리티 하나만 부여한 유닛과 대상을 세운다.</summary>
        /// <param name="targetPosition">대상이 선 자리이다.</param>
        /// <param name="withUnitDefinition">사거리를 알려 줄 유닛 정의를 붙일지 여부이다.</param>
        /// <returns>세운 무대이다.</returns>
        private ApproachHarness CreateHarness(Vector3 targetPosition, bool withUnitDefinition = true)
        {
            var unitObject = CreateObject("Approacher");
            var targetSource = unitObject.AddComponent<FakeApproachTargetSource>();
            var mover = unitObject.AddComponent<RecordingMover>();

            if (withUnitDefinition)
            {
                var unit = unitObject.AddComponent<TacticalUnit>();
                unit.SetDefinition(Track(UnitDefinition.CreateRuntime("소총병", 100, attackRange: AttackRange)));
                unit.InitializeUnit();
            }

            var targetObject = CreateObject("Target");
            targetObject.transform.position = targetPosition;
            targetSource.CurrentTarget = targetObject.transform;

            var attributes = new AttributeSet();
            _attributeSets.Add(attributes);
            var system = new GameplayAbilitySystem(new GameplayEffectRunner(attributes), unitObject);
            system.GrantAbility(Track(ApproachAbilityDefinition.CreateRuntime(ApproachTagName, RangeRatio)));
            system.TryGetAbility(GameplayTag.Parse(ApproachTagName), out var granted);

            return new ApproachHarness(system, (ApproachAbility)granted, mover, targetSource, targetObject.transform);
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

        /// <summary>접근 검사가 쓰는 무대이며 유닛과 협력자를 함께 든다.</summary>
        private sealed class ApproachHarness
        {
            public ApproachHarness(
                GameplayAbilitySystem system,
                ApproachAbility ability,
                RecordingMover mover,
                FakeApproachTargetSource targetSource,
                Transform target)
            {
                System = system;
                Ability = ability;
                Mover = mover;
                TargetSource = targetSource;
                Target = target;
            }

            public GameplayAbilitySystem System { get; }

            public ApproachAbility Ability { get; }

            public RecordingMover Mover { get; }

            public FakeApproachTargetSource TargetSource { get; }

            public Transform Target { get; }
        }

        /// <summary>이동 요청과 정지를 기록하는 이동 구성요소이며 실제로 움직이지는 않는다.</summary>
        private sealed class RecordingMover : MonoBehaviour, ICharacterMover
        {
            public Vector3? LastDestination { get; private set; }

            public int StopCount { get; private set; }

            public bool HasReachedDestination => false;

            public bool MoveTo(Vector3 destination)
            {
                LastDestination = destination;
                return true;
            }

            public void Stop() => StopCount++;
        }

        /// <summary>대상을 검사가 직접 정하는 공급자이다.</summary>
        private sealed class FakeApproachTargetSource : MonoBehaviour, ICombatTargetSource
        {
            public Transform CurrentTarget { get; set; }
        }
    }
}
