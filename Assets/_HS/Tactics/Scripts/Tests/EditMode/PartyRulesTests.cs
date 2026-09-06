using System.Collections.Generic;
using HS.Tactics.Placement;
using HS.Tactics.Units;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>파티 규칙이 한 곳에서 나오고 배치가 그것을 따르는지 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// <b>이 검사가 막는 것은 상수가 두 벌이 되는 일이다.</b> 배치 상한과 경험치 분배가 같은 인원수를
    /// 봐야 하는데 각자 숫자를 들고 있으면, 한쪽만 고쳐진 채로 지나가도 <b>컴파일도 되고 나머지 검사도
    /// 통과한다.</b> 어긋난 것은 화면에서 "경험치가 이상하게 들어온다"로만 보인다.
    /// </para>
    /// <para>
    /// 그래서 수를 그대로 적어 두고 견주는 검사를 함께 둔다. <see cref="PartyRules"/>를 읽어 비교하기만 하면
    /// 양쪽이 함께 바뀌었을 때 <b>검사도 같이 바뀌어 버려</b> 아무것도 붙들지 못한다.
    /// </para>
    /// </remarks>
    public sealed class PartyRulesTests
    {
        /// <summary>검사가 만든 것들이며 정리 대상이다.</summary>
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
        public void ThePartyIsFiveHeroesOnANineCellGrid()
        {
            // 파티 상한의 고정 수치를 검증해 참조하는 양쪽이 함께 잘못 바뀌는 경우도 구별한다.
            Assert.That(PartyRules.MaxPartySize, Is.EqualTo(5), "데려갈 수 있는 영웅 수가 다섯이 아니다.");
            Assert.That(PartyRules.GridColumns, Is.EqualTo(3), "배치 격자의 열이 셋이 아니다.");
            Assert.That(PartyRules.GridRows, Is.EqualTo(3), "배치 격자의 행이 셋이 아니다.");
            Assert.That(PartyRules.GridCellCount, Is.EqualTo(9), "배치 격자의 칸이 아홉이 아니다.");
        }

        [Test]
        public void TheGridHoldsMoreCellsThanHeroes()
        {
            // 자리가 남는 것이 의도다. 어디에 세우는지가 선택이 되려면 칸이 인원보다 많아야 한다.
            Assert.That(
                PartyRules.GridCellCount,
                Is.GreaterThan(PartyRules.MaxPartySize),
                "격자 칸이 파티 인원보다 많지 않다. 자리를 고르는 선택이 사라진다.");
        }

        [Test]
        public void ThePlacementCapacityComesFromThePartyRules()
        {
            var plan = new UnitPlacementPlan(CreateArea());

            Assert.That(
                plan.Capacity,
                Is.EqualTo(PartyRules.MaxPartySize),
                "기본 배치 상한이 파티 규칙과 다르다. 두 수가 갈리면 경험치를 나누는 인원수도 갈린다.");
            Assert.That(
                UnitPlacementController.DefaultPlacementCapacity,
                Is.EqualTo(PartyRules.MaxPartySize),
                "컨트롤러의 기본 상한이 파티 규칙과 다르다.");
        }

        [Test]
        public void TheCapacityNeverRisesAboveThePartyRules()
        {
            var plan = new UnitPlacementPlan(CreateArea(), PartyRules.MaxPartySize + 4);

            Assert.That(
                plan.Capacity,
                Is.EqualTo(PartyRules.MaxPartySize),
                "규칙보다 큰 상한을 넘겼는데 그대로 받아들였다. 씬에 저장된 값이 규칙을 이길 수 있다.");

            plan.SetCapacity(PartyRules.GridCellCount);
            Assert.That(
                plan.Capacity,
                Is.EqualTo(PartyRules.MaxPartySize),
                "격자 칸 수만큼 상한을 올렸는데 그대로 받아들였다. 칸이 아홉이어도 데려가는 것은 다섯이다.");
        }

        [Test]
        public void ThePlacementIsRefusedOnceThePartyIsFull()
        {
            // 칸은 아홉이고 인원은 다섯이다. 서로 다른 칸에 채워야 상한이 시험된다.
            var plan = new UnitPlacementPlan(CreateArea());
            for (var cellIndex = 0; cellIndex < PartyRules.MaxPartySize; cellIndex++)
            {
                Assert.That(
                    plan.TryPlaceAtCell(CreateUnitDefinition(), cellIndex, out _),
                    Is.EqualTo(PlacementResult.Success),
                    $"{cellIndex + 1}번째 배치가 상한 안인데 거절됐다.");
            }

            Assert.That(
                plan.TryPlaceAtCell(CreateUnitDefinition(), PartyRules.MaxPartySize, out _),
                Is.EqualTo(PlacementResult.CapacityReached),
                "빈 칸이 남아 있어도 파티 인원을 넘겨서는 배치되지 않아야 한다.");
            Assert.That(plan.Count, Is.EqualTo(PartyRules.MaxPartySize));
        }

        [Test]
        public void AStageMayAllowFewerHeroesThanTheRuleAllows()
        {
            // 규칙은 상한이지 목표가 아니다. 스테이지가 더 적게 잡는 것은 그대로 지켜져야 한다.
            var plan = new UnitPlacementPlan(CreateArea(), 2);

            Assert.That(plan.Capacity, Is.EqualTo(2), "스테이지가 좁게 잡은 상한이 규칙 값으로 밀려 올라갔다.");
        }

        /// <summary>배치를 받아 줄 넉넉한 구역을 만든다.</summary>
        /// <returns>만든 배치 구역이다.</returns>
        private static PlacementArea CreateArea()
        {
            return new PlacementArea(Vector3.zero, new Vector3(20f, 10f, 20f));
        }

        /// <summary>검사에서 쓸 유닛 정의를 만든다.</summary>
        /// <returns>만든 유닛 정의이다.</returns>
        private UnitDefinition CreateUnitDefinition()
        {
            var definition = UnitDefinition.CreateRuntime("PartyTester", 100);
            _createdObjects.Add(definition);
            return definition;
        }
    }
}
