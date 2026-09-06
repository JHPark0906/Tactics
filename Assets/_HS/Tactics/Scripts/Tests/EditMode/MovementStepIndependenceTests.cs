using System.Collections.Generic;
using HS.Tactics.Foundation.Geometry;
using HS.Tactics.Foundation.Simulation;
using NUnit.Framework;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 속도 변화와 경로 추종을 함께 돌렸을 때 스텝을 잘게 쪼개도 같은 시각마다 같은 자리를 지나는지 검증한다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 기기의 성능에 따라 게임의 결과가 달라지면 안 된다는 규칙의 이동 층 실물이다. 이동 층에서 그것은
    /// 「같은 초기 상태에서 굵은 스텝 N번과 잔 스텝 2N번이 같은 시각마다 같은 자리에 있다」로 나타난다.
    /// 여기서는 <c>PlanarCharacterMover.Tick</c>이 스텝마다 하는 계산 — 남은 거리, <see cref="SpeedRamp.Advance"/>,
    /// <see cref="PlanarPathFollower.Advance"/> — 을 그대로 늘어놓고 돌린다. 경로 계획과 분리된 순수 이동 계산이므로 씬 없이 검증한다. 실제 이동기가 이 계산을 그대로 쓰는지는 게임 쪽 무대 검사가 본다.
    /// </para>
    /// <para>
    /// <b>델타는 이진 정확한 값(0.25, 0.125)이고 수치도 그렇게 골랐다.</b> 0.02 같은 델타는 누적 오차로 한 스텝이
    /// 흔들려 재려는 것이 안 재진다. 최고 속도 4, 가속도 2, 길이 12면 가속 2초·최고 속도 1초·감속 2초로 나뉘고
    /// 전환점이 두 스텝 계획 모두의 경계에 놓인다. 사다리꼴 적분은 가속도가 일정한 구간에서 정확하므로 그때
    /// 두 계획은 비트까지 같다.
    /// </para>
    /// <para>
    /// <b>전환점이 스텝 중간에 오면 굵은 스텝은 그 스텝 안의 전환을 보지 못해 잔 스텝과 어긋난다.</b> 그것은 고정 스텝의
    /// 성질이지 프레임률 의존이 아니다 — 스텝 크기는 기기와 무관하게 <c>fixedDeltaTime</c> 하나이기 때문이다.
    /// 꺾임점은 다르다. 경로 추종은 한 스텝 안에서 꺾임점을 지나도 정확히 처리하므로, 꺾임점을 일부러 굵은 스텝의
    /// 한가운데 두어 그 성질까지 함께 고정한다.
    /// </para>
    /// </remarks>
    public sealed class MovementStepIndependenceTests
    {
        private const float MaxSpeed = 4f;
        private const float Acceleration = 2f;
        private const float CoarseStep = 0.25f;
        private const float FineStep = 0.125f;

        /// <summary>가속 2초 + 최고 속도 1초 + 감속 2초이며, 그 사이 12미터를 간다.</summary>
        private const int CoarseSteps = 20;

        [Test]
        public void HalvingTheStepLeavesEveryCoarseBoundaryUnchangedOnAStraightPath()
        {
            var corners = new List<PlanarPosition> { new(0f, 0f), new(0f, 12f) };

            var coarse = Simulate(corners, CoarseStep, CoarseSteps);
            var fine = Simulate(corners, FineStep, CoarseSteps * 2);

            AssertTheTripHappened(coarse, corners[1]);
            AssertSameAtEveryCoarseBoundary(coarse, fine);
        }

        [Test]
        public void ACornerInTheMiddleOfACoarseStepDoesNotSplitTheSchedules()
        {
            // 최고 속도 4에서 굵은 스텝 하나는 1미터다. 꺾임점을 4.5미터에 두면 t=2.0 의 4미터와 t=2.25 의 5미터 사이,
            // 굵은 스텝의 한가운데에 놓인다.
            var corners = new List<PlanarPosition> { new(0f, 0f), new(4.5f, 0f), new(4.5f, 7.5f) };

            var coarse = Simulate(corners, CoarseStep, CoarseSteps);
            var fine = Simulate(corners, FineStep, CoarseSteps * 2);

            AssertTheTripHappened(coarse, corners[2]);
            AssertSamePosition(fine[17].Position, corners[1], "무대 확인: 잔 스텝은 t=2.125 에 꺾임점 위에 선다.");
            Assert.That(coarse[8].Position.X, Is.LessThan(corners[1].X), "무대 확인: 굵은 스텝은 t=2.0 에 아직 꺾임점 앞이다.");
            Assert.That(coarse[9].Position.Z, Is.GreaterThan(0f), "무대 확인: 굵은 스텝은 t=2.25 에 이미 꺾임점을 지났다.");
            AssertSameAtEveryCoarseBoundary(coarse, fine);
        }

        /// <summary>
        /// 이동기가 스텝마다 하는 계산을 그대로 돌려 스텝 끝의 자리와 속도를 순서대로 모은다.
        /// 첫 항목은 출발 상태다.
        /// </summary>
        private static List<MovementSample> Simulate(IReadOnlyList<PlanarPosition> corners, float step, int stepCount)
        {
            var samples = new List<MovementSample>(stepCount + 1);
            var position = corners[0];
            var cornerIndex = 1;
            var speed = 0f;
            samples.Add(new MovementSample(position, speed));

            for (var index = 0; index < stepCount; index++)
            {
                var remaining = PlanarPathFollower.RemainingDistance(position, corners, cornerIndex);
                var speedStep = SpeedRamp.Advance(speed, MaxSpeed, Acceleration, remaining, step, brakeOnArrival: true);
                speed = speedStep.Speed;

                var advance = PlanarPathFollower.Advance(position, corners, cornerIndex, speedStep.Distance);
                position = advance.Position;
                cornerIndex = advance.CornerIndex;
                samples.Add(new MovementSample(position, speed));
            }

            return samples;
        }

        /// <summary>
        /// 여정이 실제로 일어났는지 확인한다. 아무것도 안 움직인 두 계획은 언제나 같으므로, 그 초록을 걸러 낸다.
        /// </summary>
        private static void AssertTheTripHappened(IReadOnlyList<MovementSample> coarse, PlanarPosition destination)
        {
            Assert.That(coarse[8].Speed, Is.EqualTo(MaxSpeed), "무대 확인: t=2.0 에 최고 속도에 닿는다.");
            Assert.That(coarse[12].Speed, Is.EqualTo(MaxSpeed), "무대 확인: t=3.0 까지 최고 속도로 간다.");
            Assert.That(coarse[13].Speed, Is.LessThan(MaxSpeed), "무대 확인: t=3.0 을 지나면 감속이 시작된다.");
            AssertSamePosition(coarse[CoarseSteps].Position, destination, "무대 확인: t=5.0 에 목적지에 선다.");
            Assert.That(coarse[CoarseSteps].Speed, Is.EqualTo(0f), "무대 확인: 목적지에서 속도는 0이다.");
        }

        /// <summary>굵은 스텝 하나가 끝나는 시각마다 잔 스텝 둘이 끝난 자리·속도와 비트까지 같은지 본다.</summary>
        private static void AssertSameAtEveryCoarseBoundary(
            IReadOnlyList<MovementSample> coarse,
            IReadOnlyList<MovementSample> fine)
        {
            for (var index = 0; index <= CoarseSteps; index++)
            {
                var time = index * CoarseStep;
                AssertSamePosition(fine[index * 2].Position, coarse[index].Position, $"t={time} 의 자리가 스텝 크기에 따라 다르다.");
                Assert.That(fine[index * 2].Speed, Is.EqualTo(coarse[index].Speed), $"t={time} 의 속도가 스텝 크기에 따라 다르다.");
            }
        }

        private static void AssertSamePosition(PlanarPosition actual, PlanarPosition expected, string message)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X), message);
            Assert.That(actual.Z, Is.EqualTo(expected.Z), message);
        }

        private readonly struct MovementSample
        {
            public MovementSample(PlanarPosition position, float speed)
            {
                Position = position;
                Speed = speed;
            }

            public PlanarPosition Position { get; }

            public float Speed { get; }
        }
    }
}
