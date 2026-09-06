using System.Collections.Generic;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using MessagePipe;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>승패 판정기의 전멸 감지 규칙과 결과 발행 연결을 검증한다.</summary>
    public sealed class BattleOutcomeTests
    {
        /// <summary>테스트에서 플레이어 진영으로 사용하는 식별자이다.</summary>
        private static readonly TeamId PlayerTeam = new(1);

        /// <summary>테스트에서 적 진영으로 사용하는 식별자이다.</summary>
        private static readonly TeamId EnemyTeam = new(2);

        /// <summary>테스트가 만든 오브젝트이며 정리 대상이다.</summary>
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
        public void OneSidedRegistrationDoesNotDecideOutcome()
        {
            var tracker = CreateTracker();
            var enemy = CreateUnitObject("Enemy");
            tracker.Register(enemy, EnemyTeam);

            tracker.NotifyDeath(enemy);

            Assert.That(tracker.HasBattleStarted, Is.False);
            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Undecided));
        }

        [Test]
        public void HostileWipeOutDecidesVictory()
        {
            var tracker = CreateTracker();
            tracker.Register(CreateUnitObject("Ally"), PlayerTeam);
            var enemy = CreateUnitObject("Enemy");
            tracker.Register(enemy, EnemyTeam);

            Assert.That(tracker.HasBattleStarted, Is.True);
            Assert.That(tracker.NotifyDeath(enemy), Is.True);

            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(tracker.FriendlyAliveCount, Is.EqualTo(1));
            Assert.That(tracker.HostileAliveCount, Is.EqualTo(0));
        }

        [Test]
        public void FriendlyWipeOutDecidesDefeat()
        {
            var tracker = CreateTracker();
            var ally = CreateUnitObject("Ally");
            tracker.Register(ally, PlayerTeam);
            tracker.Register(CreateUnitObject("Enemy"), EnemyTeam);

            tracker.NotifyDeath(ally);

            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Defeat));
        }

        [Test]
        public void OutcomeIsNotOverturnedAfterDecision()
        {
            var tracker = CreateTracker();
            var ally = CreateUnitObject("Ally");
            var enemy = CreateUnitObject("Enemy");
            tracker.Register(ally, PlayerTeam);
            tracker.Register(enemy, EnemyTeam);

            tracker.NotifyDeath(enemy);
            tracker.NotifyDeath(ally);

            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void MutualEliminationIsTreatedAsVictory()
        {
            var tracker = CreateTracker();
            var ally = CreateUnitObject("Ally");
            var enemy = CreateUnitObject("Enemy");
            tracker.Register(ally, PlayerTeam);
            tracker.Register(enemy, EnemyTeam);

            // 회수는 판정을 일으키지 않으므로, 양측이 동시에 비는 순간을 사망 통지 하나로 만들 수 있다.
            tracker.Unregister(ally);
            tracker.NotifyDeath(enemy);

            Assert.That(tracker.FriendlyAliveCount, Is.EqualTo(0));
            Assert.That(tracker.HostileAliveCount, Is.EqualTo(0));
            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void NeutralUnitsAreNotTracked()
        {
            var tracker = CreateTracker();
            var neutral = CreateUnitObject("Neutral");

            Assert.That(tracker.Register(neutral, TeamId.None), Is.False);
            Assert.That(tracker.TrackedUnitCount, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateRegistrationIsIgnored()
        {
            var tracker = CreateTracker();
            var ally = CreateUnitObject("Ally");

            Assert.That(tracker.Register(ally, PlayerTeam), Is.True);
            Assert.That(tracker.Register(ally, PlayerTeam), Is.False);
            Assert.That(tracker.FriendlyAliveCount, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterDoesNotDecideOutcome()
        {
            var tracker = CreateTracker();
            var ally = CreateUnitObject("Ally");
            tracker.Register(ally, PlayerTeam);
            tracker.Register(CreateUnitObject("Enemy"), EnemyTeam);

            Assert.That(tracker.Unregister(ally), Is.True);

            Assert.That(tracker.FriendlyAliveCount, Is.EqualTo(0));
            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Undecided));
        }

        [Test]
        public void UntrackedDeathIsIgnored()
        {
            var tracker = CreateTracker();
            tracker.Register(CreateUnitObject("Ally"), PlayerTeam);
            tracker.Register(CreateUnitObject("Enemy"), EnemyTeam);

            Assert.That(tracker.NotifyDeath(CreateUnitObject("Barrel")), Is.False);
            Assert.That(tracker.Outcome, Is.EqualTo(BattleOutcome.Undecided));
        }

        [Test]
        public void ServicePublishesOutcomeOnceWhenHostilesAreWipedOut()
        {
            var deathSubscriber = new TestSubscriber<DeathEvent>();
            var outcomePublisher = new TestPublisher<BattleOutcomeDecidedEvent>();
            var service = CreateService(deathSubscriber, outcomePublisher);
            var enemy = CreateUnitObject("Enemy");
            service.RegisterUnit(CreateUnitObject("Ally"), PlayerTeam);
            service.RegisterUnit(enemy, EnemyTeam);
            deathSubscriber.Publish(new DeathEvent(enemy, null));

            Assert.That(outcomePublisher.Published, Has.Count.EqualTo(1));
            Assert.That(outcomePublisher.Published[0].Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(outcomePublisher.Published[0].HostileAliveCount, Is.EqualTo(0));
            Assert.That(service.Outcome, Is.EqualTo(BattleOutcome.Victory));
        }

        [Test]
        public void ServiceIgnoresDeathsOfUnregisteredObjects()
        {
            var deathSubscriber = new TestSubscriber<DeathEvent>();
            var outcomePublisher = new TestPublisher<BattleOutcomeDecidedEvent>();
            var service = CreateService(deathSubscriber, outcomePublisher);
            service.RegisterUnit(CreateUnitObject("Ally"), PlayerTeam);
            service.RegisterUnit(CreateUnitObject("Enemy"), EnemyTeam);
            deathSubscriber.Publish(new DeathEvent(CreateUnitObject("Barrel"), null));

            Assert.That(outcomePublisher.Published, Is.Empty);
            Assert.That(service.Outcome, Is.EqualTo(BattleOutcome.Undecided));
        }

        /// <summary>기본 진영 관계 규칙을 쓰는 판정기를 만든다.</summary>
        private static BattleOutcomeTracker CreateTracker()
        {
            return new BattleOutcomeTracker(PlayerTeam);
        }

        /// <summary>
        /// MessagePipe 구독자와 발행자를 연결한 승패 판정 서비스를 만든다.
        /// </summary>
        /// <remarks>
        /// 배치 완료 신호도 곧바로 보낸다. 이 파일이 재는 것은 사망·전멸 판정 자체이지 배치 완료 여부가 아니므로,
        /// <see cref="BattleOutcomeService.HasBattleBegun"/> 문을 열어 사망·전멸 규칙을 검증한다.
        /// 그 문 자체를 재는 검사는 <c>BattleOutcomeGameStateTests</c>에 따로 있다.
        /// </remarks>
        private BattleOutcomeService CreateService(
            ISubscriber<DeathEvent> deathSubscriber,
            IPublisher<BattleOutcomeDecidedEvent> outcomePublisher)
        {
            var serviceObject = CreateUnitObject("BattleOutcomeService");
            var service = serviceObject.AddComponent<BattleOutcomeService>();
            var placementChannel = new TestMessageChannel<PlacementCompletedEvent>();
            service.InjectMessagePipeDependencies(
                deathSubscriber,
                outcomePublisher,
                DefaultTeamRelationPolicy.Instance,
                placementChannel);
            placementChannel.Publish(default);
            return service;
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        private GameObject CreateUnitObject(string objectName)
        {
            var unitObject = new GameObject(objectName);
            _createdObjects.Add(unitObject);
            return unitObject;
        }

    }
}
