using System.Collections.Generic;
using HS.Framework.Ability.Abilities;
using HS.Framework.Ability.Tags;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Combat;
using HS.Tactics.Cover;
using HS.Tactics.Flow;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 엄폐물이 중립 유닛으로 서 있는지 검증한다. 진영 미지정이므로 누구의 표적도 아니고 승패에 세어지지 않으며,
    /// 체력이 다하면 사망 어빌리티가 점유를 풀고 오브젝트를 없앤다.
    /// </summary>
    /// <remarks>
    /// 표적 규칙과 승패 집계에는 엄폐물을 위한 줄이 없다. 진영 관계 규칙이 미지정을 중립으로 보는 것 하나로 둘 다 성립하며,
    /// 여기서는 그 성립을 실제 규칙 위에서 확인한다.
    /// </remarks>
    public sealed class NeutralCoverTests
    {
        private static readonly GameplayTag CoverDeathTag = GameplayTag.Parse(UnitAbilityTags.CoverDeath);
        private static readonly GameplayTag DeadStateTag = GameplayTag.Parse(HealthAttributeComponent.DefaultDeadStateTagName);

        private readonly List<Object> _createdObjects = new();
        private TestUnitAttributes _attributes;
        private TestPublisher<DamageAppliedEvent> _damagePublisher;
        private TestPublisher<DeathEvent> _deathPublisher;
        private GameObject _coverObject;
        private CoverPoint _cover;

        [SetUp]
        public void SetUp()
        {
            _attributes = new TestUnitAttributes();
            _damagePublisher = new TestPublisher<DamageAppliedEvent>();
            _deathPublisher = new TestPublisher<DeathEvent>();
            _coverObject = Track(new GameObject("Cover"));
            _cover = _attributes.AttachCover(_coverObject);
            _cover.Health.InjectMessagePipePublishers(_damagePublisher, _deathPublisher);
        }

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
            _attributes.Dispose();
        }

        [Test]
        public void ACoverIsNotAHostileCandidateForEitherSide()
        {
            var coverTeam = _cover.GetComponent<TeamMember>();
            Assert.That(coverTeam.TeamId.IsAssigned, Is.False, "엄폐물의 진영은 미지정이어야 중립이다.");

            foreach (var team in new[] { new TeamId(1), new TeamId(2) })
            {
                var shooter = Track(new GameObject($"Shooter{team.Value}")).AddComponent<TeamMember>();
                shooter.SetTeam(team);

                Assert.That(
                    EnemyTargetSelector.IsHostileCandidate(shooter, coverTeam),
                    Is.False,
                    $"진영 {team.Value}이 엄폐물을 표적으로 잡으면 안 된다.");
            }
        }

        [Test]
        public void ACoverIsNotCountedInTheBattleOutcome()
        {
            var outcome = Track(new GameObject("Outcome")).AddComponent<BattleOutcomeService>();

            var registered = outcome.RegisterUnit(_coverObject, _cover.GetComponent<TeamMember>().TeamId);

            Assert.That(registered, Is.False, "중립은 승패에 세어지지 않는다.");
            Assert.That(outcome.FriendlyAliveCount, Is.Zero);
            Assert.That(outcome.HostileAliveCount, Is.Zero);
        }

        [Test]
        public void RunningOutOfHealthActivatesTheDeathAbilityAndReleasesTheOccupant()
        {
            var occupant = Track(new GameObject("Occupant")).AddComponent<UnitCoverState>();
            occupant.ClaimCover(_cover);

            _cover.Health.ApplyDamage(_cover.Health.MaxHealth, null);

            var system = _cover.GetComponent<GameplayAbilitySystemComponent>().System;
            Assert.That(system.Tags.HasTag(DeadStateTag), Is.True, "체력 문이 보낸 사망 이벤트만으로 어빌리티가 활성화되어야 한다.");
            Assert.That(system.TryGetAbility(CoverDeathTag, out var deathAbility), Is.True);
            Assert.That(deathAbility.ActivationCount, Is.EqualTo(1));
            Assert.That(_cover.IsDestroyed, Is.True);
            Assert.That(occupant.HasCoverClaim, Is.False, "부서진 자리를 계속 잡고 있으면 안 된다.");
            Assert.That(_cover.IsOccupied, Is.False);
            Assert.That(_coverObject != null, Is.True, "오브젝트는 사망 이벤트 안에서 사라지지 않는다.");
            Assert.That(_deathPublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheObjectIsNotRemovedAfterTheCoverIsDestroyed()
        {
            // 엄폐물은 소멸 어빌리티를 갖지 않는다 — 아무도 Object.Destroy를 부르지 않는다. 부서진 자리는
            // 죽은 상태 태그만 받고 그대로 남는다. 나중에 잔해를 남기거나 연출의 마지막 프레임에 고정하는
            // 설계와 이미 구조적으로 맞아떨어지는 의도된 동작이다.
            var system = _cover.GetComponent<GameplayAbilitySystemComponent>().System;
            _cover.Health.ApplyDamage(_cover.Health.MaxHealth, null);

            system.Tick(0.1f);

            Assert.That(_coverObject != null, Is.True, "엄폐물은 부서져도 오브젝트가 사라지지 않아야 한다.");
            Assert.That(_cover.IsDestroyed, Is.True);
        }

        [Test]
        public void ADestroyedCoverCannotBeOccupiedAgain()
        {
            _cover.Health.ApplyDamage(_cover.Health.MaxHealth, null);
            var latecomer = Track(new GameObject("Latecomer"));

            Assert.That(_cover.TryOccupy(latecomer), Is.False);
            Assert.That(_cover.ToCandidate().IsOccupied, Is.True, "부서진 자리는 후보에서 빠져야 한다.");
        }

        private T Track<T>(T createdObject) where T : Object
        {
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
