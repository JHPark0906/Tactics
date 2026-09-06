using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.AI.BehaviourTree;
using HS.Framework.Character;
using HS.Framework.Gameplay.Health;
using HS.Tactics.Character.Movement;
using HS.Tactics.Cover;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 유닛·엄폐물이 죽은 상태 태그를 얻는 순간 각 컴포넌트가 스스로 반응하는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>사망 어빌리티와 각 컴포넌트의 반응은 죽은 상태 태그로 연결된다.</b> <c>UnitCoverState</c>, <c>PlanarCharacterMover</c>,
    /// <see cref="BehaviourTreeDeathStop"/>, <c>CoverPoint</c>가 각자 자기 오브젝트의 어빌리티 시스템에서 죽은
    /// 상태 태그의 변화를 직접 구독해 스스로 반응한다. 그래서 여기서는 사망 어빌리티를 거치지 않고 태그를 직접
    /// 부여해 각 컴포넌트의 반응만 따로 검증한다.
    /// </para>
    /// <para>
    /// 에디터는 플레이 모드가 아닐 때 OnEnable을 호출하지 않는다. 구독이 걸리는 자리이므로
    /// <see cref="MonoBehaviourLifecycle.InvokeOnEnable"/>로 직접 구동한다.
    /// </para>
    /// </remarks>
    public sealed class UnitDeathReactionsTests
    {
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private readonly List<GameObject> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
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
        public void UnitCoverStateReleasesItsClaimWhenTheOwnerDies()
        {
            var (unitObject, abilitySystem) = CreateAbilitySystemOwner();
            var coverState = unitObject.AddComponent<UnitCoverState>();
            MonoBehaviourLifecycle.InvokeOnEnable(coverState);
            var coverObject = Track(new GameObject("Cover"));
            coverObject.transform.position = new Vector3(10f, 0f, 0f);
            var coverPoint = coverObject.AddComponent<CoverPoint>();
            coverState.ClaimCover(coverPoint);
            Assert.That(coverState.HasCoverClaim, Is.True);

            abilitySystem.System.Tags.AddTag(DeadStateTag);

            Assert.That(coverState.HasCoverClaim, Is.False, "죽으면 곧바로 엄폐 예약을 놓아야 한다.");
            Assert.That(coverPoint.IsOccupied, Is.False);
        }

        [Test]
        public void PlanarCharacterMoverStopsWhenTheOwnerDies()
        {
            var (unitObject, abilitySystem) = CreateAbilitySystemOwner();
            var character = unitObject.AddComponent<TestCharacter>();
            var mover = unitObject.AddComponent<PlanarCharacterMover>();
            MonoBehaviourLifecycle.InvokeAwake(character);
            MonoBehaviourLifecycle.InvokeOnEnable(mover);
            mover.SetPathPlanner((from, to, corners) =>
            {
                corners.Add(from);
                corners.Add(to);
                return true;
            });
            mover.MoveTo(new Vector3(10f, 0f, 0f));
            Assert.That(mover.HasReachedDestination, Is.False, "무대 확인: 목적지로 가는 중이어야 한다.");

            abilitySystem.System.Tags.AddTag(DeadStateTag);

            Assert.That(mover.HasReachedDestination, Is.True, "죽으면 곧바로 이동을 멈춰야 한다.");
        }

        [Test]
        public void BehaviourTreeDeathStopDisablesTheRunnerWhenTheOwnerDies()
        {
            var (unitObject, abilitySystem) = CreateAbilitySystemOwner();
            var runner = unitObject.AddComponent<BehaviourTreeRunner>();
            var deathStop = unitObject.AddComponent<BehaviourTreeDeathStop>();
            MonoBehaviourLifecycle.InvokeOnEnable(deathStop);
            Assert.That(runner.enabled, Is.True);

            abilitySystem.System.Tags.AddTag(DeadStateTag);

            Assert.That(runner.enabled, Is.False, "죽으면 곧바로 행동 트리를 꺼야 한다.");
        }

        [Test]
        public void CoverPointMarksItselfDestroyedWhenItDies()
        {
            var (coverObject, abilitySystem) = CreateAbilitySystemOwner();
            var coverPoint = coverObject.AddComponent<CoverPoint>();
            MonoBehaviourLifecycle.InvokeOnEnable(coverPoint);
            var occupantObject = Track(new GameObject("Occupant"));
            var occupantCover = occupantObject.AddComponent<UnitCoverState>();
            occupantCover.ClaimCover(coverPoint);
            Assert.That(coverPoint.IsOccupied, Is.True);

            abilitySystem.System.Tags.AddTag(DeadStateTag);

            Assert.That(coverPoint.IsDestroyed, Is.True, "죽으면 곧바로 파괴된 것으로 표시해야 한다.");
            Assert.That(coverPoint.IsOccupied, Is.False, "점유하고 있던 유닛의 예약도 함께 풀려야 한다.");
        }

        /// <summary>어빌리티 시스템을 갖춘 빈 오브젝트를 만든다.</summary>
        private (GameObject gameObject, GameplayAbilitySystemComponent abilitySystem) CreateAbilitySystemOwner()
        {
            var owner = Track(new GameObject("Owner"));
            var abilitySystem = owner.AddComponent<GameplayAbilitySystemComponent>();
            abilitySystem.ConfigureManualControl();
            return (owner, abilitySystem);
        }

        private GameObject Track(GameObject createdObject)
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        /// <summary>이동기 초기화를 구동하기 위한 최소 캐릭터 오케스트레이터이다.</summary>
        private sealed class TestCharacter : CharacterBase
        {
        }
    }
}
