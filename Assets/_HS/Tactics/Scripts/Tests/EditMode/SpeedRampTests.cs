using HS.Tactics.Foundation.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace HS.Tactics.Tests.EditMode
{
    /// <summary>
    /// 가속·감속에 따른 속도와 이동 거리를 검증한다.
    /// 최고 속도로만 이동한 경우와 비교한 시간 손실은 가속 구간 길이의 절반이며, 감속도 대칭으로 계산한다.
    /// 실제 주행 결과를 공식과 비교해 시간 손실의 상한이 과대 계산되지 않는지 확인한다.
    /// </summary>
    public sealed class SpeedRampTests
    {
        private const float Tolerance = 0.0001f;
        private const float MaxSpeed = 3.5f;
        private const float Acceleration = 8f;
        private const float StepDuration = 1f / 30f;
        private const float FarAway = 1000f;

        [Test]
        public void ReachingTopSpeedTakesSpeedOverAcceleration()
        {
            var speed = 0f;
            var elapsed = 0f;

            while (speed < MaxSpeed - Tolerance && elapsed < 5f)
            {
                speed = SpeedRamp.Advance(speed, MaxSpeed, Acceleration, FarAway, StepDuration).Speed;
                elapsed += StepDuration;
            }

            Assert.That(
                elapsed,
                Is.EqualTo(MaxSpeed / Acceleration).Within(StepDuration),
                "최고 속도에 오르는 데 걸리는 시간은 속도/가속도이다.");
        }

        [Test]
        public void TheTimeLostIsHalfTheRampNotTheWholeRamp()
        {
            // 공식이 아니라 실제 주행(가속만 하고 목적지 앞에서 감속하지 않는 경우)으로 확인한다.
            // 최고 속도에 오르는 데 걸리는 속도/가속도초를 그대로 "잃는 시간"으로 잡으면 값이 두 배가 된다.
            // 그 구간에서도 유닛은 움직이므로, 같은 거리를 실제로 달린 시간과 최고 속도로만 달렸을 때의
            // 시간 차이는 그 절반이어야 한다.
            const float distance = 20f;
            var rampDuration = MaxSpeed / Acceleration;

            var elapsed = RunTrip(distance, brakeOnArrival: false) * StepDuration;
            var lost = elapsed - distance / MaxSpeed;

            Assert.That(lost, Is.EqualTo(rampDuration * 0.5f).Within(StepDuration * 4f));
            Assert.That(
                lost,
                Is.Not.EqualTo(rampDuration).Within(StepDuration * 4f),
                "구간의 길이를 그대로 잃는 시간으로 치면 실제 주행 시간과 어긋난다.");
        }

        [Test]
        public void BrakingDoublesTheTimeLost()
        {
            // 공식이 아니라 실제 주행 둘을 비교해 확인한다 — 같은 거리를 감속 없이 달렸을 때와
            // 목적지 앞에서 감속하며 달렸을 때, 실제로 걸린 시간이 최고 속도로만 달렸을 때보다 각각
            // 얼마나 더 걸렸는지(잃은 시간)를 재서 견준다.
            const float distance = 20f;
            var constantSpeedTime = distance / MaxSpeed;

            var elapsedWithoutBraking = RunTrip(distance, brakeOnArrival: false) * StepDuration;
            var elapsedWithBraking = RunTrip(distance) * StepDuration;

            var lostWithoutBraking = elapsedWithoutBraking - constantSpeedTime;
            var lostWithBraking = elapsedWithBraking - constantSpeedTime;

            Assert.That(
                lostWithBraking,
                Is.EqualTo(lostWithoutBraking * 2f).Within(StepDuration * 4f),
                "가속만 할 때보다 목적지 앞에서 감속까지 하면 실제로 잃는 시간이 두 배가 되어야 한다.");
            Assert.That(
                lostWithBraking,
                Is.EqualTo(MaxSpeed / Acceleration).Within(StepDuration * 4f),
                "가속과 감속을 합하면 실제로 잃는 시간은 속도/가속도만큼이어야 한다.");
        }

        [Test]
        public void AWholeTripTakesTheConstantSpeedTimePlusTheLostTime()
        {
            // 이 검사가 위의 숫자를 실제 주행으로 확인한다. 공식만 맞고 계산이 다르면 여기서 걸린다.
            const float distance = 20f;

            var steps = RunTrip(distance);
            var elapsed = steps * StepDuration;
            var expected = distance / MaxSpeed + SpeedRamp.ResolveLostTime(MaxSpeed, Acceleration);

            // 상한을 스텝 넷으로 둔다. 최고 속도에 닿는 스텝과 감속을 시작하는 스텝에서 각각 한 스텝 안쪽의
            // 어긋남이 생긴다. 틀린 값(속도/가속도의 두 배)과는 13스텝이나 떨어져 있으므로 이 폭으로도 구별된다.
            Assert.That(elapsed, Is.EqualTo(expected).Within(StepDuration * 4f));
        }

        [Test]
        public void AShortTripLosesLessThanTheMaximum()
        {
            // 최고 속도에 닿지 못할 만큼 짧은 이동이다. 잃는 시간은 최댓값보다 작아야 한다.
            // 그래서 최댓값을 허용 범위로 쓰는 것이 안전하다.
            var reachTopSpeedDistance = MaxSpeed * MaxSpeed / Acceleration;
            var shortDistance = reachTopSpeedDistance * 0.25f;

            var elapsed = RunTrip(shortDistance) * StepDuration;
            var lost = elapsed - shortDistance / MaxSpeed;

            Assert.That(lost, Is.GreaterThan(0f), "짧아도 가속에 시간은 든다.");
            Assert.That(
                lost,
                Is.LessThan(SpeedRamp.ResolveLostTime(MaxSpeed, Acceleration) + StepDuration),
                "최댓값을 넘으면 허용 범위가 뚫린다.");
        }

        [Test]
        public void TheStepDistanceUsesTheAverageOfBothEnds()
        {
            // 한쪽 값만 쓰면 가속 중에는 매 스텝 조금씩 적게 가고 그 치우침이 쌓인다.
            var step = SpeedRamp.Advance(0f, MaxSpeed, Acceleration, FarAway, StepDuration);
            var endSpeed = Acceleration * StepDuration;

            Assert.That(step.Speed, Is.EqualTo(endSpeed).Within(Tolerance));
            Assert.That(step.Distance, Is.EqualTo(endSpeed * 0.5f * StepDuration).Within(Tolerance));
        }

        [Test]
        public void SlowingDownStartsAtTheBrakingDistance()
        {
            var brakingDistance = SpeedRamp.ResolveBrakingDistance(MaxSpeed, Acceleration);

            var justBefore = SpeedRamp.Advance(
                MaxSpeed, MaxSpeed, Acceleration, brakingDistance * 1.5f, StepDuration);
            var atTheEdge = SpeedRamp.Advance(
                MaxSpeed, MaxSpeed, Acceleration, brakingDistance * 0.9f, StepDuration);

            Assert.That(justBefore.Speed, Is.EqualTo(MaxSpeed).Within(Tolerance), "아직 줄일 때가 아니다.");
            Assert.That(atTheEdge.Speed, Is.LessThan(MaxSpeed), "제때 줄이지 않으면 목적지를 지나친다.");
        }

        [Test]
        public void TheBrakingDistanceFollowsTheSquareOfTheSpeed()
        {
            var slow = SpeedRamp.ResolveBrakingDistance(2f, Acceleration);
            var fast = SpeedRamp.ResolveBrakingDistance(4f, Acceleration);

            Assert.That(fast, Is.EqualTo(slow * 4f).Within(Tolerance), "속도가 두 배면 멈출 거리는 네 배이다.");
        }

        [Test]
        public void BrakingCanBeTurnedOff()
        {
            var step = SpeedRamp.Advance(
                MaxSpeed, MaxSpeed, Acceleration, 0.01f, StepDuration, brakeOnArrival: false);

            Assert.That(step.Speed, Is.EqualTo(MaxSpeed).Within(Tolerance));
        }

        [Test]
        public void AStepNeverGoesPastTheDestination()
        {
            // 지나쳐 놓고 되돌아오면 목적지 언저리에서 떤다.
            var step = SpeedRamp.Advance(MaxSpeed, MaxSpeed, Acceleration, 0.01f, StepDuration);

            Assert.That(step.Distance, Is.LessThanOrEqualTo(0.01f));
        }

        [Test]
        public void WithoutAccelerationTheUnitIsInstantlyAtFullSpeed()
        {
            // 가속을 모형화하지 않을 때의 모습이다. 최고 속도만으로 적분하는 것과 같다.
            var step = SpeedRamp.Advance(0f, MaxSpeed, 0f, FarAway, StepDuration);

            Assert.That(step.Speed, Is.EqualTo(MaxSpeed).Within(Tolerance));
            Assert.That(step.Distance, Is.EqualTo(MaxSpeed * StepDuration).Within(Tolerance));
            Assert.That(
                SpeedRamp.ResolveLostTime(MaxSpeed, 0f),
                Is.Zero,
                "가속이 없으면 잃는 시간도 없다.");
        }

        [Test]
        public void AStepOfNoTimeChangesNothing()
        {
            var step = SpeedRamp.Advance(2f, MaxSpeed, Acceleration, FarAway, 0f);

            Assert.That(step.Speed, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(step.Distance, Is.Zero);
        }

        [Test]
        public void ANegativeSpeedIsTreatedAsStandingStill()
        {
            var step = SpeedRamp.Advance(-5f, MaxSpeed, Acceleration, FarAway, StepDuration);

            Assert.That(step.Speed, Is.EqualTo(Acceleration * StepDuration).Within(Tolerance));
            Assert.That(step.Distance, Is.GreaterThanOrEqualTo(0f), "뒤로 가면 안 된다.");
        }

        [Test]
        public void ArrivingLeavesTheUnitStopped()
        {
            var speed = MaxSpeed;
            var remaining = SpeedRamp.ResolveBrakingDistance(MaxSpeed, Acceleration);

            while (remaining > 0.001f)
            {
                var step = SpeedRamp.Advance(speed, MaxSpeed, Acceleration, remaining, StepDuration);
                speed = step.Speed;
                remaining -= step.Distance;
            }

            Assert.That(
                speed,
                Is.LessThan(MaxSpeed * 0.2f),
                "도착 순간에도 최고 속도이면 급정지로 보인다.");
        }

        /// <summary>정지에서 출발해 목적지에 닿을 때까지(또는 지나칠 자리에 이를 때까지) 걸린 스텝 수를 센다.</summary>
        /// <param name="distance">갈 거리(미터)이다.</param>
        /// <param name="brakeOnArrival">목적지 앞에서 감속할지 여부이다. 끄면 최고 속도를 유지한 채 도착한다.</param>
        /// <returns>걸린 스텝 수이다.</returns>
        private static int RunTrip(float distance, bool brakeOnArrival = true)
        {
            var speed = 0f;
            var remaining = distance;
            var steps = 0;

            while (remaining > 0.001f && steps < 100000)
            {
                var step = SpeedRamp.Advance(speed, MaxSpeed, Acceleration, remaining, StepDuration, brakeOnArrival);
                speed = step.Speed;
                remaining -= step.Distance;
                steps++;
            }

            return steps;
        }
    }
}
