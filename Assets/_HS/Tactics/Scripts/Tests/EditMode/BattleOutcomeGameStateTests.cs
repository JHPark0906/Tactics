using System.Collections.Generic;
using HS.Framework.Gameplay;
using HS.Framework.Gameplay.Health;
using HS.Framework.Gameplay.Teams;
using HS.Framework.Tests.Support;
using HS.Tactics.Flow;
using HS.Tactics.Placement;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 승패 판정기가 배치 컨트롤러를 직접 참조하지 않고 <see cref="PlacementCompletedEvent"/> 하나로
    /// 「전투가 시작됐다」는 자기 상태를 갖는지, 그리고 그 상태가 사망 반영에 실제로 쓰이는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>사망·전멸 판정 규칙 자체는 이 파일이 재는 것이 아니다.</b> 그 규칙은
    /// <see cref="BattleOutcomeTests"/>가 이미 지키고 있고, 그 파일은 배치 완료 신호를 곧바로 보내
    /// <see cref="BattleOutcomeService.HasBattleBegun"/> 문을 열어 둔 채로 검증한다. 이 파일은 그 문
    /// 자체 — 신호가 오기 전과 후로 같은 사망 이벤트가 다르게 처리되는지 — 만 잰다.
    /// </para>
    /// </remarks>
    public sealed class BattleOutcomeGameStateTests
    {
        private static readonly TeamId PlayerTeam = new(1);
        private static readonly TeamId EnemyTeam = new(2);

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
        public void BattleOutcomeServiceIsAGameState()
        {
            var service = CreateObject("Outcome").AddComponent<BattleOutcomeService>();

            Assert.That(service, Is.InstanceOf<GameState>());
        }

        [Test]
        public void HasBattleBegunStartsFalse()
        {
            var (service, _, _) = CreateWiredService();

            Assert.That(service.HasBattleBegun, Is.False);
        }

        [Test]
        public void PlacementCompletedTurnsHasBattleBegunOn()
        {
            var (service, _, placementChannel) = CreateWiredService();

            placementChannel.Publish(default);

            Assert.That(service.HasBattleBegun, Is.True);
        }

        [Test]
        public void DeathsBeforePlacementCompletesAreIgnored()
        {
            var (service, deathChannel, _) = CreateWiredService();
            var outcomePublisher = LastOutcomePublisher;
            var enemy = CreateObject("Enemy");
            service.RegisterUnit(CreateObject("Ally"), PlayerTeam);
            service.RegisterUnit(enemy, EnemyTeam);

            deathChannel.Publish(new DeathEvent(enemy, null));

            Assert.That(
                service.Outcome,
                Is.EqualTo(BattleOutcome.Undecided),
                "배치가 끝나기 전의 사망은 반영되지 않아야 한다 — 등록 수만으로는 실제 개시를 대신할 수 없다.");
            Assert.That(outcomePublisher.Published, Is.Empty);
        }

        [Test]
        public void DeathsAfterPlacementCompletesAreDecided()
        {
            var (service, deathChannel, placementChannel) = CreateWiredService();
            var outcomePublisher = LastOutcomePublisher;
            var enemy = CreateObject("Enemy");
            service.RegisterUnit(CreateObject("Ally"), PlayerTeam);
            service.RegisterUnit(enemy, EnemyTeam);
            placementChannel.Publish(default);

            deathChannel.Publish(new DeathEvent(enemy, null));

            Assert.That(service.Outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(outcomePublisher.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void ADeathThatArrivedBeforeCompletionIsNotRetroactivelyCounted()
        {
            // 배치 완료가 늦게 와도 "지나간" 사망까지 뒤늦게 반영하지는 않는다 — 신호는 그 순간부터 앞으로만 연다.
            var (service, deathChannel, placementChannel) = CreateWiredService();
            var outcomePublisher = LastOutcomePublisher;
            var enemy = CreateObject("Enemy");
            service.RegisterUnit(CreateObject("Ally"), PlayerTeam);
            service.RegisterUnit(enemy, EnemyTeam);
            deathChannel.Publish(new DeathEvent(enemy, null));

            placementChannel.Publish(default);

            Assert.That(
                service.Outcome,
                Is.EqualTo(BattleOutcome.Undecided),
                "이미 등록에서 빠진 사망을 신호가 늦게 왔다고 다시 세면 결과가 발행 시점에 따라 흔들린다.");
            Assert.That(outcomePublisher.Published, Is.Empty);
        }

        /// <summary>마지막으로 만든 서비스에 연결된 결과 발행자이다.</summary>
        private TestPublisher<BattleOutcomeDecidedEvent> LastOutcomePublisher { get; set; }

        /// <summary>사망·배치 완료 통로를 모두 연결한 승패 판정 서비스를 만든다.</summary>
        /// <returns>서비스와 두 통로이다.</returns>
        private (BattleOutcomeService Service, TestMessageChannel<DeathEvent> DeathChannel,
            TestMessageChannel<PlacementCompletedEvent> PlacementChannel) CreateWiredService()
        {
            var service = CreateObject("Outcome").AddComponent<BattleOutcomeService>();
            var deathChannel = new TestMessageChannel<DeathEvent>();
            var placementChannel = new TestMessageChannel<PlacementCompletedEvent>();
            var outcomePublisher = new TestPublisher<BattleOutcomeDecidedEvent>();
            LastOutcomePublisher = outcomePublisher;
            service.InjectMessagePipeDependencies(
                deathChannel, outcomePublisher, DefaultTeamRelationPolicy.Instance, placementChannel);
            return (service, deathChannel, placementChannel);
        }

        /// <summary>정리 목록에 등록된 빈 GameObject를 만든다.</summary>
        /// <param name="objectName">만들 오브젝트 이름이다.</param>
        /// <returns>만든 오브젝트이다.</returns>
        private GameObject CreateObject(string objectName)
        {
            var createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
