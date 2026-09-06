using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Combat;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>적 주위에 자리를 나누는 계산을 검증한다.</summary>
    /// <remarks>
    /// <para>
    /// 여기서 막으려는 실패는 분명하다. <b>한 명이 죽었을 때 남은 유닛이 자리를 바꿔 우르르 움직이는 것</b>이다.
    /// 사용자 눈에는 명백한 버그이고, "몇 명 중 몇 번째"로 자리를 정하면 반드시 일어난다.
    /// 그래서 자리가 <b>다른 유닛의 수에 전혀 의존하지 않는지</b>를 직접 겨냥한다.
    /// </para>
    /// <para>
    /// 대체 자리를 고르는 차례도 고정되어 있어야 한다. 갈 수 없는 자리가 나왔을 때 아무 자리나 고르면
    /// 같은 상황에서 실행할 때마다 다른 곳으로 간다.
    /// </para>
    /// </remarks>
    public sealed class ApproachSlotResolverTests
    {
        private const float Tolerance = 0.001f;

        private static readonly PlanarPosition Target = new(10f, 5f);
        private static readonly PlanarPosition Reference = new(0f, -1f);

        [Test]
        public void EachUnitKeepsItsSlotNoMatterHowManyOthersExist()
        {
            // 같은 유닛 번호는 언제나 같은 자리로 떨어져야 한다.
            // 이 계산에는 다른 유닛의 수가 들어갈 자리 자체가 없다는 것을 확인한다.
            var first = Resolve(slotSeed: 3);
            var again = Resolve(slotSeed: 3);

            Assert.That(PlanarPosition.Distance(first, again), Is.LessThan(Tolerance));
        }

        [Test]
        public void RemovingOtherUnitsDoesNotMoveTheSurvivors()
        {
            // 넷이 서 있다가 둘이 죽는 상황이다. 남은 둘의 자리가 그대로여야 한다.
            var beforeDeaths = new List<PlanarPosition>();
            foreach (var seed in new[] { 1, 2, 3, 4 })
            {
                beforeDeaths.Add(Resolve(seed));
            }

            var survivorOne = Resolve(2);
            var survivorTwo = Resolve(4);

            Assert.That(
                PlanarPosition.Distance(survivorOne, beforeDeaths[1]),
                Is.LessThan(Tolerance),
                "한 명이 죽었다고 남은 유닛이 자리를 옮기면 전원이 우르르 이동한다.");
            Assert.That(PlanarPosition.Distance(survivorTwo, beforeDeaths[3]), Is.LessThan(Tolerance));
        }

        [Test]
        public void DifferentUnitsGetDifferentSlots()
        {
            var positions = new List<PlanarPosition>();
            for (var seed = 0; seed < 8; seed++)
            {
                positions.Add(Resolve(seed));
            }

            for (var left = 0; left < positions.Count; left++)
            {
                for (var right = left + 1; right < positions.Count; right++)
                {
                    Assert.That(
                        PlanarPosition.Distance(positions[left], positions[right]),
                        Is.GreaterThan(Tolerance),
                        $"{left}번과 {right}번이 같은 자리에 선다.");
                }
            }
        }

        [Test]
        public void SlotsWrapAroundWhenThereAreMoreUnitsThanSlots()
        {
            Assert.That(ApproachSlotResolver.ResolveSlotIndex(8, 8), Is.Zero);
            Assert.That(ApproachSlotResolver.ResolveSlotIndex(9, 8), Is.EqualTo(1));
            Assert.That(
                ApproachSlotResolver.ResolveSlotIndex(-1, 8),
                Is.EqualTo(7),
                "음수 번호도 언제나 같은 자리로 떨어져야 한다.");
        }

        [Test]
        public void EverySlotIsWithinTheRequestedRadius()
        {
            for (var seed = 0; seed < 16; seed++)
            {
                var position = Resolve(seed);

                Assert.That(
                    PlanarPosition.Distance(position, Target),
                    Is.EqualTo(4f).Within(Tolerance),
                    "모든 자리가 적에게서 같은 거리에 있어야 둘러싼 모양이 된다.");
            }
        }

        [Test]
        public void SlotsSpreadOnTheSideTheUnitsComeFrom()
        {
            // 기준 방향은 적에서 접근하는 쪽을 향한다. 자리가 그 반대편에 생기면 유닛이 적을 돌아가야 한다.
            for (var seed = 0; seed < 8; seed++)
            {
                var offset = Resolve(seed) - Target;

                Assert.That(
                    PlanarPosition.Dot(offset.Normalized, Reference.Normalized),
                    Is.GreaterThan(0f),
                    $"{seed}번 자리가 적의 반대편에 생겼다.");
            }
        }

        [Test]
        public void FallbackSlotsFollowAFixedOrder()
        {
            var first = Resolve(3, attempt: 0);
            var second = Resolve(3, attempt: 1);
            var secondAgain = Resolve(3, attempt: 1);

            Assert.That(PlanarPosition.Distance(first, second), Is.GreaterThan(Tolerance), "대체 자리는 달라야 한다.");
            Assert.That(
                PlanarPosition.Distance(second, secondAgain),
                Is.LessThan(Tolerance),
                "대체 차례가 흔들리면 같은 상황에서 다른 곳으로 간다.");
            Assert.That(
                ApproachSlotResolver.ResolveSlotIndex(3, 8, 1),
                Is.EqualTo(4),
                "대체는 다음 자리로 한 칸씩 넘어간다.");
        }

        [Test]
        public void TryingEverySlotComesBackToTheOriginalOne()
        {
            var original = ApproachSlotResolver.ResolveSlotIndex(3, 8);

            Assert.That(
                ApproachSlotResolver.ResolveSlotIndex(3, 8, 8),
                Is.EqualTo(original),
                "자리를 한 바퀴 다 돌면 처음으로 돌아온다. 그때는 더 시도할 자리가 없다는 뜻이다.");
        }

        [Test]
        public void SpreadIsDisabledWhenThereIsOnlyOneSlot()
        {
            var position = ApproachSlotResolver.ResolveSlotPosition(Target, Reference, 4f, 3, 1);

            Assert.That(position, Is.EqualTo(Target), "자리를 나누지 않으면 적의 자리를 그대로 쓴다.");
        }

        [Test]
        public void SpreadIsDisabledWhenTheRadiusIsZero()
        {
            var position = ApproachSlotResolver.ResolveSlotPosition(Target, Reference, 0f, 3, 8);

            Assert.That(position, Is.EqualTo(Target));
        }

        [Test]
        public void AReferenceWithoutDirectionFallsBackToAFixedAxis()
        {
            var position = ApproachSlotResolver.ResolveSlotPosition(
                Target, PlanarPosition.Zero, 4f, 0, 8);

            Assert.That(
                PlanarPosition.Distance(position, Target),
                Is.EqualTo(4f).Within(Tolerance),
                "방향을 정할 수 없어도 자리는 정해져야 한다.");
        }

        [Test]
        public void HeightIsNeverPartOfTheCalculation()
        {
            // 계산이 평면 타입만 다루므로 높이가 끼어들 자리가 없다는 것을 형태로 확인한다.
            var highTarget = PlanarPosition.FromWorld(new Vector3(10f, 999f, 5f));

            var position = ApproachSlotResolver.ResolveSlotPosition(highTarget, Reference, 4f, 3, 8);

            Assert.That(PlanarPosition.Distance(position, Target), Is.EqualTo(4f).Within(Tolerance));
        }

        /// <summary>기본 설정으로 자리를 구한다.</summary>
        /// <param name="slotSeed">유닛의 고정된 번호이다.</param>
        /// <param name="attempt">몇 번째 대체 자리인지이다.</param>
        /// <returns>구한 자리의 평면 좌표이다.</returns>
        private static PlanarPosition Resolve(int slotSeed, int attempt = 0)
        {
            return ApproachSlotResolver.ResolveSlotPosition(
                Target, Reference, 4f, slotSeed, 8, 180f, attempt);
        }
    }
}
