using System.Collections.Generic;
using HS.Framework.Tests.Support;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 배치 컨트롤러가 전투 개시를 선언할 때 <see cref="PlacementCompletedEvent"/>를 발행하는지,
    /// 그리고 발행하지 못했을 때는 발행하지 않는지 고정한다.
    /// </summary>
    /// <remarks>
    /// <b>이 컨트롤러는 받는 쪽을 모른다.</b> 발행자를 주입받아 부를 뿐이며, 구독자가 무엇을 하는지는
    /// 이 파일이 신경 쓰지 않는다 — 받는 쪽의 반응은 <see cref="BattleOutcomeGameStateTests"/>가 잰다.
    /// </remarks>
    public sealed class PlacementCompletedPublishingTests
    {
        private readonly List<Object> _createdObjects = new();

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
        public void StartingTheBattlePublishesExactlyOnce()
        {
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);
            var channel = new TestMessageChannel<PlacementCompletedEvent>();
            controller.InjectPlacementCompletedPublisher(channel);

            Assert.That(controller.TryStartBattle(), Is.True);

            Assert.That(channel.Published, Has.Count.EqualTo(1));
        }

        [Test]
        public void AFailedStartPublishesNothing()
        {
            // 배치한 유닛이 하나도 없으면 시작 자체가 거부된다 — 단계가 바뀌지 않았으니 알릴 것도 없다.
            var controller = CreatePlacementController();
            var channel = new TestMessageChannel<PlacementCompletedEvent>();
            controller.InjectPlacementCompletedPublisher(channel);

            Assert.That(controller.TryStartBattle(), Is.False);

            Assert.That(channel.Published, Is.Empty);
        }

        [Test]
        public void StartingTheBattleWithoutAPublisherStillSucceeds()
        {
            // 받는 쪽(GameState 구현)이 없는 무대에서도 배치와 전투 개시 자체는 이 컨트롤러의 책임이므로 그대로 된다.
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);

            Assert.That(controller.TryStartBattle(), Is.True);
        }

        [Test]
        public void CallingTryStartBattleAgainDoesNotPublishASecondTime()
        {
            var controller = CreatePlacementController();
            PlaceOneEntry(controller);
            var channel = new TestMessageChannel<PlacementCompletedEvent>();
            controller.InjectPlacementCompletedPublisher(channel);
            controller.TryStartBattle();

            Assert.That(
                controller.TryStartBattle(),
                Is.False,
                "이미 전투 단계이므로 다시 걸어도 성공하지 않아야 한다.");
            Assert.That(channel.Published, Has.Count.EqualTo(1), "실패한 시도가 신호를 한 번 더 내면 구독하는 쪽 상태가 어긋난다.");
        }

        /// <summary>배치 단계를 열고 넉넉한 구역을 지정한 배치 컨트롤러를 만든다.</summary>
        /// <returns>만든 배치 컨트롤러이다.</returns>
        private UnitPlacementController CreatePlacementController()
        {
            var controllerObject = new GameObject("UnitPlacementController");
            _createdObjects.Add(controllerObject);

            var controller = controllerObject.AddComponent<UnitPlacementController>();
            controller.BeginPlacement();
            controller.Plan.SetArea(new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f)));
            return controller;
        }

        /// <summary>계획에 배치 항목 하나를 직접 넣어 "유닛을 하나 놓은 상태"를 만든다.</summary>
        /// <param name="controller">항목을 넣을 배치 컨트롤러이다.</param>
        private void PlaceOneEntry(UnitPlacementController controller)
        {
            var definition = UnitDefinition.CreateRuntime("PlacementTester", 100);
            _createdObjects.Add(definition);
            var result = controller.Plan.TryPlace(definition, Vector3.zero, out _);
            Assert.That(
                result,
                Is.EqualTo(PlacementResult.Success),
                "검사가 기대는 준비 단계가 실패했다. 배치 항목을 하나도 넣지 못했다.");
        }
    }
}
